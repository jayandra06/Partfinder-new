using Microsoft.VisualStudio.TestTools.UnitTesting;
using PartFinder.Core;

namespace PartFinder.Tests.Services;

[TestClass]
public sealed class ExplorerTemplateNamesTests
{
    [TestMethod]
    [DataRow("Explorer", true)]
    [DataRow("explorer", true)]
    [DataRow("Master Data", true)]
    [DataRow("master data", true)]
    [DataRow("Parts", false)]
    [DataRow("", false)]
    [DataRow(null, false)]
    public void IsExplorerTemplateName_MatchesExpected(string? name, bool expected)
    {
        var actual = ExplorerTemplateNames.IsExplorerTemplateName(name);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void ExplorerTemplateConstants_AreStable()
    {
        Assert.AreEqual("master-data", ExplorerTemplateNames.TemplateId);
        Assert.AreEqual("Explorer", ExplorerTemplateNames.DisplayName);
        Assert.AreEqual("Master Data", ExplorerTemplateNames.LegacyDisplayName);
    }
}
