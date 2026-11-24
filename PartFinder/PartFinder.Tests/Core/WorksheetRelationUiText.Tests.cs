using Microsoft.VisualStudio.TestTools.UnitTesting;
using PartFinder.Core;

namespace PartFinder.Tests.Core;

[TestClass]
public sealed class WorksheetRelationUiTextTests
{
    [TestMethod]
    public void GetSharedColumnNames_DedupesSkipsBlankAndSorts()
    {
        var shared = WorksheetRelationUiText.GetSharedColumnNames(
            ["Model", "brand", "Brand", " "],
            ["BRAND", "model", " "]);

        CollectionAssert.AreEqual(new[] { "brand", "Model" }, shared.ToArray());
    }

    [TestMethod]
    public void GetSharedColumnNames_NoOverlap_ReturnsEmpty()
    {
        var shared = WorksheetRelationUiText.GetSharedColumnNames(["Brand"], ["Vendor"]);

        Assert.AreEqual(0, shared.Count);
    }

    [TestMethod]
    public void GetMatchColumnHint_SingleColumn_QuotesIt()
    {
        Assert.AreEqual(
            "Matching when \"Part No\" equals a row in the lookup template.",
            WorksheetRelationUiText.GetMatchColumnHint(["Part No"]));
    }

    [TestMethod]
    public void GetMatchColumnHint_MultipleColumns_JoinsWithCommas()
    {
        Assert.AreEqual(
            "Matching when all of these equal the lookup row: Brand, Model.",
            WorksheetRelationUiText.GetMatchColumnHint(["Brand", "Model"]));
    }

    [TestMethod]
    [DataRow((string?)null)]
    [DataRow("")]
    public void GetMatchColumnHint_NullOrEmptyColumn_AsksForSelection(string? column)
    {
        Assert.AreEqual(
            "Choose one or more columns to match on.",
            WorksheetRelationUiText.GetMatchColumnHint(column));
    }

    [TestMethod]
    public void GetMatchColumnHint_SingleStringOverload_DelegatesToListVersion()
    {
        Assert.AreEqual(
            WorksheetRelationUiText.GetMatchColumnHint(["SKU"]),
            WorksheetRelationUiText.GetMatchColumnHint("SKU"));
    }

    [TestMethod]
    public void GetSelectedMatchColumn_ReturnsFirstChecked()
    {
        var selected = WorksheetRelationUiText.GetSelectedMatchColumn(
            [("Brand", false), ("Model", true), ("SKU", true)]);

        Assert.AreEqual("Model", selected);
    }

    [TestMethod]
    public void GetSelectedMatchColumn_NoneChecked_ReturnsNull()
    {
        Assert.IsNull(WorksheetRelationUiText.GetSelectedMatchColumn([("Brand", false)]));
    }

    [TestMethod]
    public void GetSharedColumnsSummary_PluralisesCorrectly()
    {
        Assert.AreEqual(
            "No columns share the same name in both templates.",
            WorksheetRelationUiText.GetSharedColumnsSummary(0));
        StringAssert.StartsWith(WorksheetRelationUiText.GetSharedColumnsSummary(1), "1 shared column —");
        StringAssert.StartsWith(WorksheetRelationUiText.GetSharedColumnsSummary(3), "3 shared columns —");
    }
}
