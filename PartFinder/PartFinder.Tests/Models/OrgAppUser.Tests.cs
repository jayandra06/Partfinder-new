using Microsoft.VisualStudio.TestTools.UnitTesting;
using PartFinder.Models;

namespace PartFinder.Tests.Models;

[TestClass]
public sealed class OrgAppUserSummaryTests
{
    private static OrgAppUserSummary User(
        string name = "alice",
        string role = "Employee",
        bool all = false,
        string[]? ids = null,
        string status = "Pending",
        bool orgAdmin = false,
        DateTime invited = default) => new()
    {
        Id = "u1",
        Name = name,
        Email = "a@example.test",
        Role = role,
        PartsAllTemplates = all,
        AllowedTemplateIds = ids ?? [],
        Status = status,
        IsOrgAdminDirectoryEntry = orgAdmin,
        InvitedAtUtc = invited,
    };

    [TestMethod]
    [DataRow("alice", "A")]
    [DataRow("  bob", "B")]
    [DataRow("", "?")]
    [DataRow("   ", "?")]
    public void Initials_UsesFirstNonBlankLetter(string name, string expected)
    {
        Assert.AreEqual(expected, User(name: name).Initials);
    }

    [TestMethod]
    [DataRow("Admin")]
    [DataRow("admin")]
    public void PartsScopeDisplay_AdminHasFullAccess(string role)
    {
        Assert.AreEqual("Full access", User(role: role, all: false).PartsScopeDisplay);
    }

    [TestMethod]
    public void PartsScopeDisplay_EmployeeWithAllTemplates()
    {
        Assert.AreEqual("Parts (all part templates)", User(all: true).PartsScopeDisplay);
    }

    [TestMethod]
    public void PartsScopeDisplay_EmployeeWithSomeTemplates_ShowsCount()
    {
        Assert.AreEqual("Parts (2 template(s))", User(ids: ["t1", "t2"]).PartsScopeDisplay);
    }

    [TestMethod]
    public void Status_DefaultsToPendingAndInactive()
    {
        var user = User();

        Assert.AreEqual("Pending", user.Status);
        Assert.IsFalse(user.IsActive);
    }

    [TestMethod]
    [DataRow("Active")]
    [DataRow("ACTIVE")]
    public void IsActive_IsCaseInsensitive(string status)
    {
        Assert.IsTrue(User(status: status).IsActive);
    }

    [TestMethod]
    public void OrgAdminDirectoryEntries_AreNotEditable()
    {
        Assert.IsFalse(User(orgAdmin: true).CanEditInUserManagement);
        Assert.IsTrue(User(orgAdmin: false).CanEditInUserManagement);
    }

    [TestMethod]
    public void InvitedAtDisplay_DefaultDateShowsDash()
    {
        Assert.AreEqual("—", User().InvitedAtDisplay);
    }

    [TestMethod]
    public void InvitedAtDisplay_FormatsLocalDate()
    {
        var invited = new DateTime(2026, 3, 14, 12, 0, 0, DateTimeKind.Utc);

        Assert.AreEqual(invited.ToLocalTime().ToString("dd MMM yyyy"), User(invited: invited).InvitedAtDisplay);
    }

    [TestMethod]
    public void PermissionDtos_DefaultToNoAccess()
    {
        var t = new TemplatePermissionsDto();
        var m = new MasterDataPermissionsDto();

        Assert.IsFalse(t.Add || t.View || t.Edit || t.Delete);
        Assert.IsFalse(m.Copy || m.View || m.Edit || m.Add || m.Delete);
    }
}
