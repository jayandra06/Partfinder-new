using Microsoft.VisualStudio.TestTools.UnitTesting;
using MongoDB.Driver;
using PartFinder.Services;

namespace PartFinder.Tests.Services;

[TestClass]
public sealed class MongoConnectionStringUtilTests
{
    [TestMethod]
    public void Normalize_Null_ReturnsEmpty()
    {
        Assert.AreEqual(string.Empty, MongoConnectionStringUtil.Normalize(null));
    }

    [TestMethod]
    public void Normalize_Whitespace_IsReturnedAsIs()
    {
        Assert.AreEqual("   ", MongoConnectionStringUtil.Normalize("   "));
    }

    [TestMethod]
    [DataRow("postgres://localhost/db")]
    [DataRow("localhost:27017")]
    [DataRow("http://cluster0.mongodb.net")]
    public void Normalize_NonMongoScheme_Throws(string uri)
    {
        Assert.ThrowsException<ArgumentException>(() => MongoConnectionStringUtil.Normalize(uri));
    }

    [TestMethod]
    public void Normalize_OverlongUri_Throws()
    {
        var uri = "mongodb://localhost/" + new string('a', 2100);

        Assert.ThrowsException<ArgumentException>(() => MongoConnectionStringUtil.Normalize(uri));
    }

    [TestMethod]
    public void Normalize_Srv_IsTrimmedButOtherwiseUnchanged()
    {
        Assert.AreEqual(
            "mongodb+srv://u:p@cluster0.abc.mongodb.net/db",
            MongoConnectionStringUtil.Normalize("  mongodb+srv://u:p@cluster0.abc.mongodb.net/db  "));
    }

    [TestMethod]
    public void Normalize_AtlasStandardUriWithoutQuery_AddsTls()
    {
        Assert.AreEqual(
            "mongodb://u:p@shard-00.abc.mongodb.net:27017/db?tls=true",
            MongoConnectionStringUtil.Normalize("mongodb://u:p@shard-00.abc.mongodb.net:27017/db"));
    }

    [TestMethod]
    public void Normalize_AtlasStandardUriWithQuery_AppendsTlsParameter()
    {
        Assert.AreEqual(
            "mongodb://shard-00.abc.mongodb.net:27017/db?replicaSet=rs0&tls=true",
            MongoConnectionStringUtil.Normalize("mongodb://shard-00.abc.mongodb.net:27017/db?replicaSet=rs0"));
    }

    [TestMethod]
    [DataRow("mongodb://h.abc.mongodb.net/db?tls=true")]
    [DataRow("mongodb://h.abc.mongodb.net/db?ssl=true")]
    [DataRow("mongodb://h.abc.mongodb.net/db?TLS=1")]
    public void Normalize_AtlasUriAlreadyUsingTls_IsUnchanged(string uri)
    {
        Assert.AreEqual(uri, MongoConnectionStringUtil.Normalize(uri));
    }

    [TestMethod]
    public void Normalize_NonAtlasHost_IsUnchanged()
    {
        Assert.AreEqual("mongodb://localhost:27017/db", MongoConnectionStringUtil.Normalize("mongodb://localhost:27017/db"));
    }

    [TestMethod]
    [DataRow("  shop  ", "acme", "shop")]
    [DataRow("admin", "acme", "acme")]
    [DataRow("ADMIN", " acme ", "acme")]
    [DataRow(null, "acme", "acme")]
    [DataRow(null, "admin", "partfinder")]
    [DataRow(null, null, "partfinder")]
    [DataRow("", "", "partfinder")]
    public void ResolveDatabaseName_NeverReturnsAdmin(string? db, string? org, string expected)
    {
        Assert.AreEqual(expected, MongoConnectionStringUtil.ResolveDatabaseName(db, org));
    }

    [TestMethod]
    [DataRow("mongodb://localhost:27017", "org1", "mongodb://localhost:27017/org1")]
    [DataRow("mongodb://localhost:27017/", "org1", "mongodb://localhost:27017/org1")]
    [DataRow("mongodb://h/?retryWrites=true", "org1", "mongodb://h/org1?retryWrites=true")]
    [DataRow("mongodb://h/admin?x=1", "org1", "mongodb://h/org1?x=1")]
    [DataRow("mongodb://h/existing?x=1", "org1", "mongodb://h/existing?x=1")]
    [DataRow("mongodb://h", "my org", "mongodb://h/my%20org")]
    [DataRow("localhost", "db", "localhost/db")]
    public void EnsureDatabase_InsertsDatabaseOnlyWhenMissingOrAdmin(string uri, string db, string expected)
    {
        Assert.AreEqual(expected, MongoConnectionStringUtil.EnsureDatabase(uri, db));
    }

    [TestMethod]
    public void EnsureDatabase_BlankDefault_ReturnsUriUnchanged()
    {
        Assert.AreEqual("mongodb://h", MongoConnectionStringUtil.EnsureDatabase("mongodb://h", " "));
    }

    [TestMethod]
    public void Prepare_AddsOrgDatabaseWhenUriHasNoPath()
    {
        Assert.AreEqual(
            "mongodb://localhost:27017/acme?retryWrites=true",
            MongoConnectionStringUtil.Prepare("mongodb://localhost:27017/?retryWrites=true", "acme"));
    }

    [TestMethod]
    public void Prepare_KeepsExplicitDatabase()
    {
        Assert.AreEqual(
            "mongodb://localhost:27017/inventory",
            MongoConnectionStringUtil.Prepare("mongodb://localhost:27017/inventory", "acme"));
    }

    [TestMethod]
    public void Prepare_AtlasUri_GetsTlsAndDatabase()
    {
        Assert.AreEqual(
            "mongodb://h.abc.mongodb.net:27017/acme?tls=true",
            MongoConnectionStringUtil.Prepare("mongodb://h.abc.mongodb.net:27017/", "acme"));
    }

    [TestMethod]
    public void Prepare_Blank_ReturnsEmpty()
    {
        Assert.AreEqual(string.Empty, MongoConnectionStringUtil.Prepare(null));
    }

    [TestMethod]
    public void GetTenantDatabase_AdminPath_UsesOrgCode()
    {
        var client = new MongoClient("mongodb://localhost:27017");

        var db = MongoConnectionStringUtil.GetTenantDatabase(client, "mongodb://localhost:27017/admin", "acme");

        Assert.AreEqual("acme", db.DatabaseNamespace.DatabaseName);
    }

    [TestMethod]
    public void GetTenantDatabase_NoPathNoOrg_UsesDefault()
    {
        var client = new MongoClient("mongodb://localhost:27017");

        var db = MongoConnectionStringUtil.GetTenantDatabase(client, "mongodb://localhost:27017");

        Assert.AreEqual("partfinder", db.DatabaseNamespace.DatabaseName);
    }

    [TestMethod]
    [DataRow("mongodb://localhost:27017", "test")]
    [DataRow("mongodb://localhost:27017/admin", "test")]
    [DataRow("mongodb://localhost:27017/tenant1", "tenant1")]
    public void GetUserDirectoryDatabase_FollowsUriPathAndFallsBackToTest(string uri, string expected)
    {
        var client = new MongoClient("mongodb://localhost:27017");

        var db = MongoConnectionStringUtil.GetUserDirectoryDatabase(client, uri);

        Assert.AreEqual(expected, db.DatabaseNamespace.DatabaseName);
    }
}
