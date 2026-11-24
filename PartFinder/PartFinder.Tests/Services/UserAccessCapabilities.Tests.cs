using Microsoft.VisualStudio.TestTools.UnitTesting;
using PartFinder.Services;

namespace PartFinder.Tests.Services;

[TestClass]
public sealed class UserAccessCapabilitiesTests
{
    private static IEnumerable<System.Reflection.PropertyInfo> BoolProperties() =>
        typeof(UserAccessCapabilities)
            .GetProperties()
            .Where(p => p.PropertyType == typeof(bool));

    [TestMethod]
    public void Default_GrantsOnlyInventoryViewing()
    {
        var caps = new UserAccessCapabilities();

        foreach (var prop in BoolProperties())
        {
            var expected = prop.Name == nameof(UserAccessCapabilities.CanViewInventory);
            Assert.AreEqual(expected, (bool)prop.GetValue(caps)!, prop.Name);
        }

        Assert.AreEqual(0, caps.AllowedTemplateIds.Count);
    }

    [TestMethod]
    public void FullAdmin_GrantsEveryCapability()
    {
        var caps = UserAccessCapabilities.FullAdmin;

        foreach (var prop in BoolProperties())
        {
            Assert.IsTrue((bool)prop.GetValue(caps)!, prop.Name);
        }

        Assert.AreEqual(0, caps.AllowedTemplateIds.Count);
    }

    [TestMethod]
    public void FullAdmin_IsSharedSingleton()
    {
        Assert.AreSame(UserAccessCapabilities.FullAdmin, UserAccessCapabilities.FullAdmin);
    }

    [TestMethod]
    public void RestrictedEmployee_KeepsAllowedTemplateIds()
    {
        var caps = new UserAccessCapabilities
        {
            CanAccessParts = true,
            PartsAllTemplates = false,
            AllowedTemplateIds = ["t1", "t2"],
        };

        CollectionAssert.AreEqual(new[] { "t1", "t2" }, caps.AllowedTemplateIds.ToArray());
        Assert.IsFalse(caps.CanManageInventory);
    }
}
