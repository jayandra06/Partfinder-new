using Microsoft.VisualStudio.TestTools.UnitTesting;
using PartFinder.Core;

namespace PartFinder.Tests.Core;

[TestClass]
public sealed class ExplorerTemplateNamesTests
{
    [TestMethod]
    public void Constants_AreStable()
    {
        Assert.AreEqual("master-data", ExplorerTemplateNames.TemplateId);
        Assert.AreEqual("Explorer", ExplorerTemplateNames.DisplayName);
        Assert.AreEqual("Master Data", ExplorerTemplateNames.LegacyDisplayName);
    }

    [TestMethod]
    [DataRow("Explorer")]
    [DataRow("explorer")]
    [DataRow("EXPLORER")]
    [DataRow("Master Data")]
    [DataRow("master data")]
    public void IsExplorerTemplateName_AcceptsCurrentAndLegacyNames(string name)
    {
        Assert.IsTrue(ExplorerTemplateNames.IsExplorerTemplateName(name));
    }

    [TestMethod]
    [DataRow((string?)null)]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("Explorer 2")]
    [DataRow("MasterData")]
    [DataRow("master-data")]
    public void IsExplorerTemplateName_RejectsOtherNames(string? name)
    {
        Assert.IsFalse(ExplorerTemplateNames.IsExplorerTemplateName(name));
    }
}
