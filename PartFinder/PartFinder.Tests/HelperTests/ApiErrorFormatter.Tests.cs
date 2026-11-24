using Microsoft.VisualStudio.TestTools.UnitTesting;
using PartFinder.Helpers;

namespace PartFinder.Tests.Helpers;

[TestClass]
public sealed class ApiErrorFormatterTests
{
    private const string SessionExpired = "Session expired. Sign out and sign in again.";

    [TestMethod]
    [DataRow((string?)null)]
    [DataRow("")]
    [DataRow("   ")]
    public void Blank_ReturnsGenericFailure(string? raw)
    {
        Assert.AreEqual("Request failed.", ApiErrorFormatter.ToUserMessage(raw));
    }

    [TestMethod]
    public void JsonObjectMessage_IsExtractedAndLocalised()
    {
        Assert.AreEqual(
            "Organisation not found",
            ApiErrorFormatter.ToUserMessage("{\"statusCode\":404,\"message\":\"Organization not found\"}"));
    }

    [TestMethod]
    public void JsonArrayBody_FallsBackToLocalisedRawText()
    {
        // TryGetProperty on a JSON array root throws and is caught, so array bodies are
        // shown as raw (localised) text rather than their first "message" entry.
        const string raw = "[{\"message\":\"License expired\"}]";

        Assert.AreEqual("[{\"message\":\"Licence expired\"}]", ApiErrorFormatter.ToUserMessage(raw));
    }

    [TestMethod]
    [DataRow("{\"message\":\"Unauthorized\"}")]
    [DataRow("{\"message\":\"Unauthorised access\"}")]
    [DataRow("401 Unauthorized")]
    public void UnauthorizedResponses_MapToSessionExpired(string raw)
    {
        Assert.AreEqual(SessionExpired, ApiErrorFormatter.ToUserMessage(raw));
    }

    [TestMethod]
    [DataRow("Cannot GET /api/wms/zones")]
    [DataRow("Cannot POST /api/wms/warehouses")]
    [DataRow("Not Found: /api/wms/locations")]
    public void MissingWmsRoutes_ExplainBackendDeployment(string raw)
    {
        StringAssert.StartsWith(
            ApiErrorFormatter.ToUserMessage(raw),
            "The license API does not include Warehouse Management routes yet.");
    }

    [TestMethod]
    public void OtherWmsErrors_AreNotMistakenForMissingRoutes()
    {
        Assert.AreEqual(
            "Zone code already exists on /api/wms/zones",
            ApiErrorFormatter.ToUserMessage("Zone code already exists on /api/wms/zones"));
    }

    [TestMethod]
    public void LongPlainText_IsTruncatedTo120CharsWithEllipsis()
    {
        var raw = new string('x', 200);

        var message = ApiErrorFormatter.ToUserMessage(raw);

        Assert.AreEqual(121, message.Length);
        Assert.IsTrue(message.EndsWith('…'));
    }

    [TestMethod]
    public void MalformedJson_FallsBackToRawText()
    {
        Assert.AreEqual("{oops", ApiErrorFormatter.ToUserMessage("{oops"));
    }

    [TestMethod]
    public void NonStringJsonMessage_FallsBackToRawText()
    {
        const string raw = "{\"message\":[\"a\",\"b\"]}";

        Assert.AreEqual(raw, ApiErrorFormatter.ToUserMessage(raw));
    }

    [TestMethod]
    public void PlainText_IsTrimmedAndLocalised()
    {
        Assert.AreEqual("Favourite removed", ApiErrorFormatter.ToUserMessage("  Favorite removed  "));
    }
}
