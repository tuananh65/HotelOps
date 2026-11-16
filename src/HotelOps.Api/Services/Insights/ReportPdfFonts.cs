using PdfSharp.Drawing;
using PdfSharp.Fonts;

namespace HotelOps.Api.Services.Insights;

internal static class ReportPdfFonts
{
    private const string Family = "HotelOps Report Noto Sans";
    private static readonly Lazy<bool> Ready = new(() =>
    {
        if (GlobalFontSettings.FontResolver is not null)
            throw new InvalidOperationException("Report PDF font resolver must be initialized before other PDF font users.");
        GlobalFontSettings.FontResolver = new Resolver();
        return true;
    });

    internal static XFont Create(double size)
    {
        _ = Ready.Value;
        return new XFont(Family, size, XFontStyleEx.Regular, new XPdfFontOptions(PdfSharp.Pdf.PdfFontEncoding.Unicode));
    }

    private sealed class Resolver : IFontResolver
    {
        private static readonly Lazy<byte[]> Bytes = new(() =>
        {
            using var stream = typeof(ReportPdfFonts).Assembly.GetManifestResourceStream("HotelOps.Api.Resources.Fonts.NotoSans-Regular.ttf")
                ?? throw new InvalidOperationException("Embedded report font is missing.");
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            return buffer.ToArray();
        });
        public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic) =>
            familyName == Family && !isBold && !isItalic ? new FontResolverInfo(Family) : null;
        public byte[]? GetFont(string faceName) => faceName == Family ? Bytes.Value : null;
    }
}
