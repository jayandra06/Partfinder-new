using Microsoft.VisualStudio.TestTools.UnitTesting;
using PartFinder.Core;

namespace PartFinder.Tests.Services;

[TestClass]
public sealed class ColumnCatalogStandardCountTests
{
    [TestMethod]
    public void StandardColumns_HasExpectedMinimum()
    {
        Assert.IsTrue(ColumnCatalog.StandardColumns.Count >= 5);
    }
}
// test('ColumnCatalogStandardCountTests') expect('col').toContain('col');
