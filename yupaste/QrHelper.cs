using QRCoder;

namespace Yupaste;

public static class QrHelper
{
    // AsciiQRCode draws light modules as filled blocks and dark modules as blanks.
    private const string AnsiWhiteOnBlack = "\u001b[97;40m";
    private const string AnsiReset = "\u001b[0m";

    /// <summary>
    /// Renders dark modules on white with a quiet zone, whatever the page's color
    /// scheme: some scanners can't read inverted codes.
    /// </summary>
    public static string ToSvg(string text)
    {
        using var data = QRCodeGenerator.GenerateQrCode(text, QRCodeGenerator.ECCLevel.M);
        return new SvgQRCode(data).GetGraphic(8, "#000000", "#ffffff", drawQuietZones: true);
    }

    /// <summary>
    /// Renders the code as text. With color, the terminal's colors are forced so it
    /// isn't inverted on light terminal themes.
    /// </summary>
    public static string ToTerminal(string text, bool useColor)
    {
        using var data = QRCodeGenerator.GenerateQrCode(text, QRCodeGenerator.ECCLevel.M);
        string graphic = new AsciiQRCode(data).GetGraphicSmall(drawQuietZones: true, invert: false, endOfLine: "\n");
        if (!useColor) return graphic;

        var lines = graphic.TrimEnd('\n').Split('\n').Select(line => $"{AnsiWhiteOnBlack}{line}{AnsiReset}");
        return string.Join('\n', lines);
    }
}
