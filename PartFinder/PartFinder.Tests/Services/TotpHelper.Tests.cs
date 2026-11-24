using System.Security.Cryptography;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PartFinder.Services;

namespace PartFinder.Tests.Services;

[TestClass]
public sealed class TotpHelperTests
{
    // RFC 6238 reference secret "12345678901234567890" (ASCII) in base32.
    private const string RfcSecretBase32 = "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ";
    private static readonly byte[] RfcSecret = Encoding.ASCII.GetBytes("12345678901234567890");

    /// <summary>Independent RFC 4226 HOTP reference implementation used as the oracle.</summary>
    private static string Hotp(byte[] key, long counter)
    {
        var msg = BitConverter.GetBytes(counter);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(msg);
        }

        using var hmac = new HMACSHA1(key);
        var hash = hmac.ComputeHash(msg);
        var offset = hash[^1] & 0x0f;
        var code = ((hash[offset] & 0x7f) << 24)
                   | (hash[offset + 1] << 16)
                   | (hash[offset + 2] << 8)
                   | hash[offset + 3];
        return (code % 1_000_000).ToString("D6");
    }

    private static long CurrentStep() => DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30;

    [TestMethod]
    public void ReferenceOracle_MatchesRfc4226Vectors()
    {
        Assert.AreEqual("755224", Hotp(RfcSecret, 0));
        Assert.AreEqual("287082", Hotp(RfcSecret, 1));
        Assert.AreEqual("520489", Hotp(RfcSecret, 9));
    }

    [TestMethod]
    public void Verify_AcceptsCodeForCurrentStep()
    {
        Assert.IsTrue(TotpHelper.Verify(Hotp(RfcSecret, CurrentStep()), RfcSecretBase32));
    }

    [TestMethod]
    public void Verify_AcceptsPreviousStepWithinDefaultDrift()
    {
        Assert.IsTrue(TotpHelper.Verify(Hotp(RfcSecret, CurrentStep() - 1), RfcSecretBase32));
    }

    [TestMethod]
    public void Verify_RejectsCodeFarOutsideDrift()
    {
        Assert.IsFalse(TotpHelper.Verify(Hotp(RfcSecret, CurrentStep() + 10), RfcSecretBase32));
    }

    [TestMethod]
    public void Verify_ZeroDrift_RejectsStaleCode()
    {
        Assert.IsFalse(TotpHelper.Verify(Hotp(RfcSecret, CurrentStep() - 3), RfcSecretBase32, allowedStepDrift: 0));
    }

    [TestMethod]
    public void Verify_SecretIsCaseAndSpaceInsensitive()
    {
        var spaced = "gezd gnbv gy3t qojq gezd gnbv gy3t qojq";

        Assert.IsTrue(TotpHelper.Verify(Hotp(RfcSecret, CurrentStep()), spaced));
    }

    [TestMethod]
    [DataRow("12345")]
    [DataRow("1234567")]
    [DataRow("12a456")]
    [DataRow("")]
    public void Verify_RejectsMalformedCodes(string code)
    {
        Assert.IsFalse(TotpHelper.Verify(code, RfcSecretBase32));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("NOT-BASE32!")]
    [DataRow("ABC1")]
    public void Verify_InvalidSecret_ReturnsFalseInsteadOfThrowing(string secret)
    {
        Assert.IsFalse(TotpHelper.Verify("123456", secret));
    }

    [TestMethod]
    public void GenerateRandomSecretBase32_Default_Is32Base32Chars()
    {
        var secret = TotpHelper.GenerateRandomSecretBase32();

        Assert.AreEqual(32, secret.Length);
        Assert.IsTrue(secret.All(c => "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567".Contains(c)));
    }

    [TestMethod]
    public void GenerateRandomSecretBase32_LengthFollowsByteCount()
    {
        Assert.AreEqual(16, TotpHelper.GenerateRandomSecretBase32(10).Length);
        Assert.AreEqual(2, TotpHelper.GenerateRandomSecretBase32(1).Length);
    }

    [TestMethod]
    public void GenerateRandomSecretBase32_IsRandom()
    {
        Assert.AreNotEqual(TotpHelper.GenerateRandomSecretBase32(), TotpHelper.GenerateRandomSecretBase32());
    }
}
