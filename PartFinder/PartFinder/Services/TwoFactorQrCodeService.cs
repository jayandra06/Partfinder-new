using QRCoder;

namespace PartFinder.Services;

/// <summary>
/// Builds otpauth:// URIs and PNG QR codes for authenticator apps (Google/Microsoft Authenticator).
/// </summary>
public static class TwoFactorQrCodeService
{
    public static string BuildTotpSetupUri(string issuer, string accountLabel, string secretBase32)
    {
        var i = Uri.EscapeDataString(issuer.Trim());
        var a = Uri.EscapeDataString(accountLabel.Trim());
        var s = Uri.EscapeDataString(secretBase32.Trim().ToUpperInvariant());
        return $"otpauth://totp/{i}:{a}?secret={s}&issuer={i}";
    }

    public static byte[] RenderPng(string otpauthUri, int pixelsPerModule = 6)
    {
        using var gen = new QRCodeGenerator();
        var data = gen.CreateQrCode(otpauthUri, QRCodeGenerator.ECCLevel.Q);
        var png = new PngByteQRCode(data);
        return png.GetGraphic(pixelsPerModule);
    }
}
