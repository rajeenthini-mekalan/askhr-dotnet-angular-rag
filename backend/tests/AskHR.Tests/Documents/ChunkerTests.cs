using AskHR.Application.Documents;

namespace AskHR.Tests.Documents;

public class ChunkerTests
{
    /// <summary>"w0 w1 w2 ..." — numbered words with no sentence ends, so cuts fall exactly on the word limit.</summary>
    private static string Words(int count, int from = 0) =>
        string.Join(' ', Enumerable.Range(from, count).Select(i => $"w{i}"));

    private static string[] WordsOf(string chunk) => chunk.Split(' ');

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \r\n\t  ")]
    public void Empty_text_gives_no_chunks(string? text) =>
        Assert.Empty(Chunker.Split(text));

    [Fact]
    public void Short_text_is_one_chunk_with_whitespace_normalised()
    {
        var chunks = Chunker.Split("  Annual leave\r\n\r\nis 18   days.\t");
        Assert.Equal(["Annual leave is 18 days."], chunks);
    }

    [Fact]
    public void Text_of_exactly_one_chunk_is_not_split() =>
        Assert.Single(Chunker.Split(Words(500)));

    [Fact]
    public void Long_text_is_cut_at_the_word_limit_with_the_requested_overlap()
    {
        var chunks = Chunker.Split(Words(1200), chunkWords: 500, overlapWords: 50);

        // starts: 0, 450, 900 → [0,500) [450,950) [900,1200)
        Assert.Equal(3, chunks.Count);
        Assert.Equal(500, WordsOf(chunks[0]).Length);
        Assert.Equal(500, WordsOf(chunks[1]).Length);
        Assert.Equal(300, WordsOf(chunks[2]).Length);
        Assert.StartsWith("w450 ", chunks[1]);
        Assert.StartsWith("w900 ", chunks[2]);

        // The last 50 words of each chunk are the first 50 of the next one.
        for (int i = 1; i < chunks.Count; i++)
            Assert.Equal(WordsOf(chunks[i - 1])[^50..], WordsOf(chunks[i])[..50]);
    }

    [Fact]
    public void Every_word_appears_and_in_order()
    {
        var chunks = Chunker.Split(Words(2345), chunkWords: 300, overlapWords: 30);

        // Rebuild the text by dropping each chunk's overlap with the previous one.
        var rebuilt = WordsOf(chunks[0]).ToList();
        foreach (var chunk in chunks.Skip(1))
            rebuilt.AddRange(WordsOf(chunk).Skip(30));

        Assert.Equal(WordsOf(Words(2345)), rebuilt);
    }

    [Fact]
    public void A_short_tail_is_merged_into_the_last_chunk_instead_of_becoming_its_own()
    {
        // 520 words: a second chunk would be 50 overlap words + only 20 new ones.
        var chunks = Chunker.Split(Words(520), chunkWords: 500, overlapWords: 50);
        Assert.Single(chunks);
        Assert.Equal(520, WordsOf(chunks[0]).Length);
    }

    [Fact]
    public void A_chunk_ends_at_a_nearby_sentence_end()
    {
        // A sentence end at word 470 (inside the last 20% of a 500-word chunk).
        var words = Enumerable.Range(0, 1000).Select(i => i == 469 ? "policy." : $"w{i}").ToArray();

        var chunks = Chunker.Split(string.Join(' ', words), chunkWords: 500, overlapWords: 50);

        Assert.EndsWith("policy.", chunks[0]);
        Assert.Equal(470, WordsOf(chunks[0]).Length);
        Assert.StartsWith("w420 ", chunks[1]);   // next chunk starts 50 words before the cut
    }

    [Fact]
    public void A_sentence_end_too_far_back_is_ignored()
    {
        var words = Enumerable.Range(0, 1000).Select(i => i == 100 ? "early." : $"w{i}").ToArray();
        var chunks = Chunker.Split(string.Join(' ', words), chunkWords: 500, overlapWords: 50);
        Assert.Equal(500, WordsOf(chunks[0]).Length);
    }

    [Theory]
    [InlineData(5, 0)]      // chunk too small
    [InlineData(100, -1)]   // negative overlap
    [InlineData(100, 51)]   // overlap more than half the chunk
    public void Invalid_sizes_throw(int chunkWords, int overlapWords) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Chunker.Split("text", chunkWords, overlapWords));
}
