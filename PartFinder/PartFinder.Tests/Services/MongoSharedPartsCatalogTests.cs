using Microsoft.VisualStudio.TestTools.UnitTesting;
using MongoDB.Bson;
using PartFinder.Services;

namespace PartFinder.Tests.Services;

[TestClass]
public sealed class MongoSharedPartsCatalogTests
{
    [TestMethod]
    public void IsCatalogTemplateName_MatchesPartfinderParts()
    {
        Assert.IsTrue(MongoSharedPartsCatalog.IsCatalogTemplateName("partfinder.parts"));
        Assert.IsFalse(MongoSharedPartsCatalog.IsCatalogTemplateName("Explorer"));
    }

    [TestMethod]
    public void ResolvePartFieldValue_MapsLegacyPartDocument()
    {
        var doc = new BsonDocument
        {
            { "_id", ObjectId.GenerateNewId() },
            { "brand", "DVZ" },
            { "partNumber", "500PC COALESCER" },
            { "org_code", "884470" },
        };

        Assert.AreEqual("DVZ", MongoSharedPartsCatalog.ResolvePartFieldValue(doc, "brand"));
        Assert.AreEqual("500PC COALESCER", MongoSharedPartsCatalog.ResolvePartFieldValue(doc, "partNumber"));
        Assert.IsFalse(string.IsNullOrWhiteSpace(MongoSharedPartsCatalog.ResolvePartFieldValue(doc, "_id")));
    }
}
