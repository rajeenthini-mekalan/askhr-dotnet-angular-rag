using System.Text;
using System.Text.RegularExpressions;
using AskHR.Application.Abstractions;
using AskHR.Application.Documents;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using UglyToad.PdfPig.Exceptions;

namespace AskHR.Infrastructure.Documents;

/// <summary>
/// PDF text extraction with PdfPig (Apache 2.0, pure .NET, no native dependencies) and .txt decoding.
///
/// How PDF text extraction works: a PDF page is not text with layout, it is drawing instructions —
/// "use font F1 at 11pt, move to (72, 700), show glyphs 0x2A 0x11...". The extractor maps each glyph back to a
/// character (through the font's ToUnicode table), then rebuilds words and lines from the glyph positions.
///
/// Where it fails (and what the HR Admin sees):
/// <list type="bullet">
///   <item>Scanned PDFs: the page is one image, there are no glyphs → no text. Needs OCR (out of scope) → Failed.</item>
///   <item>Password-protected PDFs → Failed with a clear message.</item>
///   <item>Fonts without a ToUnicode map: glyphs come back as wrong or empty characters ("mojibake"). Not detectable
///   reliably; the chunks will read as garbage.</item>
///   <item>Layout: multi-column pages, tables, headers/footers on every page. ContentOrderTextExtractor follows the
///   order the PDF drew things in, which is usually reading order for Word-generated PDFs but not always.</item>
///   <item>Words broken by a hyphen at a line end ("employ-\nees") are re-joined below.</item>
/// </list>
/// </summary>
public sealed partial class DocumentTextExtractor : IDocumentTextExtractor
{
    public string ExtractText(byte[] content, string contentType) => contentType switch
    {
        UploadRules.Pdf => ExtractPdf(content),
        UploadRules.Text => DecodeText(content),
        _ => throw new DocumentIngestionException($"Unsupported content type '{contentType}'."),
    };

    private static string ExtractPdf(byte[] content)
    {
        try
        {
            using PdfDocument pdf = PdfDocument.Open(content);
            var text = new StringBuilder();
            foreach (var page in pdf.GetPages())
            {
                text.Append(ContentOrderTextExtractor.GetText(page));
                text.Append("\n\n");   // page break = paragraph break
            }

            string result = HyphenatedLineBreak().Replace(text.ToString(), "$1$2").Trim();
            if (result.Length == 0)
                throw new DocumentIngestionException(
                    $"No text found in the PDF ({pdf.NumberOfPages} page(s)). It is probably scanned images; OCR is not supported.");
            return result;
        }
        catch (PdfDocumentEncryptedException ex)
        {
            throw new DocumentIngestionException("The PDF is password-protected. Upload a copy without a password.", ex);
        }
        catch (Exception ex) when (ex is not DocumentIngestionException)
        {
            // PdfDocumentFormatException and friends: a damaged or non-standard file.
            throw new DocumentIngestionException("The PDF could not be read; it may be damaged.", ex);
        }
    }

    private static string DecodeText(byte[] content)
    {
        // Honour a byte-order mark (UTF-8/UTF-16) if there is one; otherwise expect UTF-8 and fall back to Latin-1
        // (an old Windows "ANSI" file), which can decode any byte.
        try
        {
            using var reader = new StreamReader(new MemoryStream(content),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true),
                detectEncodingFromByteOrderMarks: true);
            return reader.ReadToEnd();
        }
        catch (DecoderFallbackException)
        {
            return Encoding.Latin1.GetString(content);
        }
    }

    // A lowercase letter, a hyphen, a line break, a lowercase letter: "employ-\nees" → "employees".
    // (A real hyphenated word that happens to break there, "part-\ntime", is joined too: an accepted trade-off.)
    [GeneratedRegex(@"(\p{Ll})-\r?\n(\p{Ll})")]
    private static partial Regex HyphenatedLineBreak();
}
