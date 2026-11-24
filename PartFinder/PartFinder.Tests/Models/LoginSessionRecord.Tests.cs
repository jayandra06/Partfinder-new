using Microsoft.VisualStudio.TestTools.UnitTesting;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using PartFinder.Models;

namespace PartFinder.Tests.Models;

[TestClass]
public sealed class LoginSessionRecordTests
{
    [TestMethod]
    public void NewRecord_IsActiveWithDesktopUserAgent()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var record = new LoginSessionRecord();

        Assert.IsTrue(record.IsActive);
        Assert.AreEqual("PartFinder-WinUI/1.0", record.UserAgent);
        Assert.IsTrue(record.LoginTime >= before && record.LoginTime <= DateTime.UtcNow);
        Assert.IsTrue(record.LastActivityTime >= before);
    }

    [TestMethod]
    public void Serialises_UsingCamelCaseElementNames()
    {
        var record = new LoginSessionRecord { UserId = "u1", SessionId = "s1", DeviceName = "PC-1" };

        var doc = record.ToBsonDocument();

        Assert.AreEqual("u1", doc["userId"].AsString);
        Assert.AreEqual("s1", doc["sessionId"].AsString);
        Assert.AreEqual("PC-1", doc["deviceName"].AsString);
        Assert.IsTrue(doc["isActive"].AsBoolean);
        Assert.IsTrue(doc.Contains("_id"));
    }

    [TestMethod]
    public void Deserialises_IgnoringUnknownElements()
    {
        var doc = new BsonDocument
        {
            { "_id", ObjectId.GenerateNewId() },
            { "userId", "u2" },
            { "isActive", false },
            { "somethingNew", 123 },
        };

        var record = BsonSerializer.Deserialize<LoginSessionRecord>(doc);

        Assert.AreEqual("u2", record.UserId);
        Assert.IsFalse(record.IsActive);
    }
}
