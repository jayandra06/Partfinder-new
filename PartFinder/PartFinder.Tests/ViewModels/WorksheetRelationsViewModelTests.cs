using Microsoft.VisualStudio.TestTools.UnitTesting;
using PartFinder.Core;

namespace PartFinder.Tests.ViewModels;

[TestClass]
public sealed class WorksheetRelationUiTextTests
{
    [TestMethod]
    public void GetMatchColumnHint_WhenNoColumnSelected_DescribesRequirement()
    {
        var hint = WorksheetRelationUiText.GetMatchColumnHint(Array.Empty<string>());

        StringAssert.Contains(hint, "one or more");
    }

    [TestMethod]
    public void GetMatchColumnHint_WhenColumnSelected_IncludesColumnName()
    {
        var hint = WorksheetRelationUiText.GetMatchColumnHint("Part Number");

        StringAssert.Contains(hint, "Part Number");
    }

    [TestMethod]
    public void GetSelectedMatchColumn_ReturnsFirstChecked()
    {
        var columns = new (string Name, bool IsChecked)[]
        {
            ("Part Number", true),
            ("SKU", false),
        };

        var selected = WorksheetRelationUiText.GetSelectedMatchColumn(columns);

        Assert.AreEqual("Part Number", selected);
    }
}
