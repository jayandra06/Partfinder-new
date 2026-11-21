using Microsoft.VisualStudio.TestTools.UnitTesting;
using PartFinder.Core;

namespace PartFinder.Tests.Services;

[TestClass]
public sealed class ExplorerTemplateNamesLegacyTests
{
    [TestMethod]
    public void IsExplorerTemplateName_AcceptsLegacyMasterData()
    {
        Assert.IsTrue(ExplorerTemplateNames.IsExplorerTemplateName("Master Data"));
    }
}
// test('ExplorerTemplateNamesLegacyTests') expect('legacy').toContain('leg');
