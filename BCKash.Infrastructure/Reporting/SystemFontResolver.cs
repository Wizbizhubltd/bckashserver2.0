using PdfSharp.Fonts;

namespace BCKash.Infrastructure.Reporting;

/// <summary>
/// PDFsharp's cross-platform build has no built-in "standard 14 fonts" fallback — it requires an
/// <see cref="IFontResolver"/> before creating any <c>XFont</c>, even for a plain sans-serif
/// face. Rather than embedding a font file in the repo, this resolves against whichever
/// TrueType sans-serif font the host OS already ships (macOS's bundled Arial, common Linux
/// DejaVu Sans paths, or Windows' Arial). A production deployment on an OS/container without
/// any of these installed would need a bundled font file instead — a limitation worth knowing
/// about, not a bug.
/// </summary>
public class SystemFontResolver : IFontResolver
{
    private static readonly (bool Bold, bool Italic, string[] Paths)[] Candidates =
    [
        (false, false,
        [
            "/System/Library/Fonts/Supplemental/Arial.ttf",
            "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
            "/usr/share/fonts/truetype/liberation/LiberationSans-Regular.ttf",
            "C:\\Windows\\Fonts\\arial.ttf",
        ]),
        (true, false,
        [
            "/System/Library/Fonts/Supplemental/Arial Bold.ttf",
            "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf",
            "/usr/share/fonts/truetype/liberation/LiberationSans-Bold.ttf",
            "C:\\Windows\\Fonts\\arialbd.ttf",
        ]),
    ];

    public byte[] GetFont(string faceName)
    {
        var isBold = faceName.EndsWith("#bold", StringComparison.OrdinalIgnoreCase);
        var candidate = Candidates.FirstOrDefault(c => c.Bold == isBold) is { Paths.Length: > 0 } match ? match : Candidates[0];
        var path = candidate.Paths.FirstOrDefault(File.Exists)
            ?? throw new InvalidOperationException(
                "No usable TrueType sans-serif font found on this host for PDF export — see SystemFontResolver's doc comment.");

        return File.ReadAllBytes(path);
    }

    public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic) =>
        new(isBold ? "SystemSans#bold" : "SystemSans");
}
