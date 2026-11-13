namespace AskHR.Application.Documents;

/// <summary>
/// Splits a document's text into overlapping passages ("chunks") for retrieval. A pure function: no I/O, no state,
/// the same input always gives the same chunks, so it is cheap to unit test and safe to re-run.
///
/// Why these numbers:
/// <list type="bullet">
///   <item>~500 words ≈ 650–700 tokens: big enough to hold a whole policy rule with its conditions, small enough
///   that one chunk is about one topic (a chunk mixing three topics embeds to a vague "average" vector), and well
///   inside the embedding model's context window (nomic-embed-text in Ollama: 2,048 tokens by default; anything
///   longer is silently truncated before it is embedded).</item>
///   <item>~50 words of overlap (10%): a rule that straddles a boundary still appears whole in at least one chunk,
///   so a question about it can still match.</item>
///   <item>Cut at a sentence end when one is close: a chunk that stops mid-sentence loses meaning at both ends.</item>
/// </list>
/// </summary>
public static class Chunker
{
    public const int DefaultChunkWords = 500;
    public const int DefaultOverlapWords = 50;

    /// <summary>A sentence end is searched for in the last 20% of a chunk; further back would make chunks too uneven.</summary>
    private const double SentenceSearchWindow = 0.2;

    private static readonly char[] SentenceEnds = ['.', '!', '?'];
    private static readonly char[] ClosingMarks = ['"', '\'', ')', ']', '”', '’'];

    /// <param name="text">Plain text; whitespace (including line breaks) is normalised to single spaces.</param>
    /// <param name="chunkWords">Target words per chunk. A chunk can end a little earlier (sentence end) or,
    /// for the last chunk, a little later (a short tail is merged instead of becoming its own chunk).</param>
    /// <param name="overlapWords">Words repeated from the end of one chunk at the start of the next.</param>
    public static IReadOnlyList<string> Split(string? text, int chunkWords = DefaultChunkWords, int overlapWords = DefaultOverlapWords)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(chunkWords, 10);
        ArgumentOutOfRangeException.ThrowIfNegative(overlapWords);
        if (overlapWords * 2 > chunkWords)
            throw new ArgumentOutOfRangeException(nameof(overlapWords), "Overlap must be at most half the chunk size.");

        if (string.IsNullOrWhiteSpace(text))
            return [];

        // Splitting on null = any whitespace (spaces, tabs, \r\n). Words keep their punctuation.
        string[] words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length <= chunkWords)
            return [string.Join(' ', words)];

        int minTail = Math.Max(1, chunkWords / 10);   // a last piece shorter than this joins the previous chunk
        var chunks = new List<string>();
        int start = 0;

        while (true)
        {
            int end = Math.Min(start + chunkWords, words.Length);   // exclusive

            if (words.Length - end < minTail)
            {
                end = words.Length;   // merge the short tail instead of emitting a near-duplicate chunk
            }
            else
            {
                end = SnapToSentenceEnd(words, start, end, chunkWords);
            }

            chunks.Add(string.Join(' ', words, start, end - start));

            if (end >= words.Length)
                return chunks;

            start = end - overlapWords;   // always moves forward: end ≥ start + 80% of chunkWords > overlap
        }
    }

    /// <summary>Moves <paramref name="end"/> back to just after the nearest sentence end in the last 20% of the chunk, if any.</summary>
    private static int SnapToSentenceEnd(string[] words, int start, int end, int chunkWords)
    {
        int earliest = end - (int)(chunkWords * SentenceSearchWindow);
        for (int i = end - 1; i >= Math.Max(earliest, start + 1); i--)
        {
            if (EndsSentence(words[i]))
                return i + 1;
        }
        return end;   // no sentence end nearby (a long list or table): cut at the word limit
    }

    private static bool EndsSentence(string word)
    {
        ReadOnlySpan<char> w = word.AsSpan().TrimEnd(ClosingMarks);
        return w.Length > 1 && SentenceEnds.Contains(w[^1]);
    }
}
