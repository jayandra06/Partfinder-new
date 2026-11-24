using Microsoft.VisualStudio.TestTools.UnitTesting;
using PartFinder.Services;

namespace PartFinder.Tests;

[TestClass]
public sealed class LoginCredentialStoreTests
{
    [TestMethod]
    public void Save_and_load_round_trip_for_org_email_password()
    {
        var store = new LoginCredentialStore();
        const string org = "123456";
        const string email = "teammate@example.com";
        const string password = "SecretPass1";

        Assert.IsTrue(store.Save(email, password, org));
        Assert.IsTrue(store.TryGetPassword(org, email, out var loaded));
        Assert.AreEqual(password, loaded);

        var all = store.GetAll();
        Assert.IsTrue(all.Any(e => e.OrgCode == org && e.Email == email));

        store.Remove(org, email);
    }

    [TestMethod]
    public void Save_rejects_invalid_org_code()
    {
        var store = new LoginCredentialStore();
        Assert.IsFalse(store.Save("user@example.com", "SecretPass1", "12ab"));
    }
}