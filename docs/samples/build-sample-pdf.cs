#:package PdfPig@0.1.16
#:property PublishAot=false
// Renders the invented sample policy (Markdown-ish: "# title", "## heading", paragraphs) to a simple A4 PDF.
// .NET 10 file-based app, run from the repo root:
//   dotnet run docs/samples/build-sample-pdf.cs -- docs/samples/lumora-leave-and-remote-work-policy.md docs/samples/lumora-leave-and-remote-work-policy.pdf
// Standard-14 fonts only support WinAnsi characters, so keep the source text to plain ASCII.

using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

string input = args.Length > 0 ? args[0] : "docs/samples/lumora-leave-and-remote-work-policy.md";
string output = args.Length > 1 ? args[1] : Path.ChangeExtension(input, ".pdf");

const double Margin = 60, BodySize = 10.5, HeadingSize = 13, TitleSize = 17;
var builder = new PdfDocumentBuilder();
var regular = builder.AddStandard14Font(Standard14Font.Helvetica);
var bold = builder.AddStandard14Font(Standard14Font.HelveticaBold);

PdfPageBuilder page = null!;
double y = 0, width = 0;
int pageNumber = 0;
NewPage();

foreach (string raw in File.ReadAllLines(input))
{
    string line = raw.Trim();
    if (line.Length == 0) { y -= BodySize * 0.6; continue; }

    if (line.StartsWith("# ")) Write(line[2..], bold, TitleSize, spaceAfter: 8);
    else if (line.StartsWith("## ")) { y -= 4; Write(line[3..], bold, HeadingSize, spaceAfter: 3); }
    else Write(line, regular, BodySize, spaceAfter: 0);
}

File.WriteAllBytes(output, builder.Build());
Console.WriteLine($"Wrote {output} ({pageNumber} pages)");

void NewPage()
{
    page = builder.AddPage(PageSize.A4);
    pageNumber++;
    width = page.PageSize.Width - 2 * Margin;
    y = page.PageSize.Height - Margin;
    // A footer on every page, like real policies have: it shows up in the extracted text too.
    page.AddText($"Lumora Digital (fictional) - Leave and Remote Work Policy - page {pageNumber}", 8, new PdfPoint(Margin, 30), regular);
}

void Write(string text, PdfDocumentBuilder.AddedFont font, double size, double spaceAfter)
{
    // Greedy word wrap using the font's real glyph widths.
    var line = "";
    foreach (string word in text.Split(' '))
    {
        string candidate = line.Length == 0 ? word : line + " " + word;
        if (line.Length > 0 && Measure(candidate, font, size) > width)
        {
            Emit(line, font, size);
            line = word;
        }
        else line = candidate;
    }
    if (line.Length > 0) Emit(line, font, size);
    y -= spaceAfter;
}

void Emit(string text, PdfDocumentBuilder.AddedFont font, double size)
{
    if (y - size < Margin) NewPage();
    y -= size * 1.35;
    page.AddText(text, size, new PdfPoint(Margin, y), font);
}

double Measure(string text, PdfDocumentBuilder.AddedFont font, double size)
{
    IReadOnlyList<Letter> letters = page.MeasureText(text, size, new PdfPoint(0, 0), font);
    return letters.Count == 0 ? 0 : letters[^1].EndBaseLine.X - letters[0].StartBaseLine.X;
}
