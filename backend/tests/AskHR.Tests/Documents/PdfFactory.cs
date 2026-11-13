using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace AskHR.Tests.Documents;

/// <summary>Builds small real PDFs in memory with PdfPig's writer, so tests need no binary fixtures.</summary>
internal static class PdfFactory
{
    /// <summary>One A4 page per argument; each string is one or more lines (split on \n).</summary>
    public static byte[] WithText(params string[] pages)
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        foreach (var text in pages)
        {
            var page = builder.AddPage(PageSize.A4);
            double y = 780;
            foreach (var line in text.Split('\n'))
            {
                page.AddText(line, 11, new PdfPoint(60, y), font);
                y -= 16;
            }
        }
        return builder.Build();
    }

    /// <summary>A valid PDF whose page has no text at all — what a scanned document looks like to an extractor.</summary>
    public static byte[] WithoutText()
    {
        var builder = new PdfDocumentBuilder();
        var page = builder.AddPage(PageSize.A4);
        page.DrawRectangle(new PdfPoint(60, 600), 200, 100);   // a drawing, no glyphs
        return builder.Build();
    }
}
