using System.Text;
using AskHR.Application.Abstractions;
using AskHR.Application.Documents;
using AskHR.Infrastructure.Documents;

namespace AskHR.Tests.Documents;

public class DocumentTextExtractorTests
{
    private readonly DocumentTextExtractor _extractor = new();

    [Fact]
    public void Pdf_text_is_extracted_from_every_page()
    {
        var pdf = PdfFactory.WithText("Annual leave is 18 days.", "Sick leave is 10 days.");

        string text = _extractor.ExtractText(pdf, UploadRules.Pdf);

        Assert.Contains("Annual leave is 18 days.", text);
        Assert.Contains("Sick leave is 10 days.", text);
        Assert.True(text.IndexOf("Annual", StringComparison.Ordinal) < text.IndexOf("Sick", StringComparison.Ordinal));
    }

    [Fact]
    public void A_word_hyphenated_across_lines_is_joined_again()
    {
        var pdf = PdfFactory.WithText("All employ-\nees get a laptop.");
        Assert.Contains("employees get a laptop.", _extractor.ExtractText(pdf, UploadRules.Pdf));
    }

    [Fact]
    public void Pdf_without_text_fails_with_a_scanned_document_message()
    {
        var ex = Assert.Throws<DocumentIngestionException>(() =>
            _extractor.ExtractText(PdfFactory.WithoutText(), UploadRules.Pdf));
        Assert.Contains("scanned", ex.Message);
    }

    [Fact]
    public void Damaged_pdf_fails_with_a_clear_message()
    {
        var ex = Assert.Throws<DocumentIngestionException>(() =>
            _extractor.ExtractText(Encoding.ASCII.GetBytes("%PDF-1.7 then nothing useful"), UploadRules.Pdf));
        Assert.Contains("could not be read", ex.Message);
    }

    [Fact]
    public void Utf8_text_with_a_byte_order_mark_is_decoded()
    {
        byte[] content = [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes("Café leave policy")];
        Assert.Equal("Café leave policy", _extractor.ExtractText(content, UploadRules.Text));
    }

    [Fact]
    public void Non_utf8_text_falls_back_to_latin1()
    {
        byte[] content = Encoding.Latin1.GetBytes("Café");   // 0xE9 alone is invalid UTF-8
        Assert.Equal("Café", _extractor.ExtractText(content, UploadRules.Text));
    }

    [Fact]
    public void The_invented_sample_policy_pdf_extracts_into_chunks()
    {
        // docs/samples sits next to backend/; walk up from the test output folder to find it.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "docs", "samples")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        var pdf = File.ReadAllBytes(Path.Combine(dir.FullName, "docs", "samples", "lumora-leave-and-remote-work-policy.pdf"));

        string text = _extractor.ExtractText(pdf, UploadRules.Pdf);
        var chunks = Chunker.Split(text);

        // The PDF's line breaks survive extraction ("may be carried\nover"); the chunker turns them into spaces.
        Assert.Contains("\n", text);
        Assert.Contains(chunks, c => c.Contains("Up to 5 days of unused annual leave may be carried over to the next calendar year."));
        // ContentOrderTextExtractor follows drawing order: the footer was drawn before the body, so it comes first.
        Assert.StartsWith("Lumora Digital (fictional) - Leave and Remote Work Policy - page 1", text);
        Assert.InRange(chunks.Count, 2, 5);
    }
}
