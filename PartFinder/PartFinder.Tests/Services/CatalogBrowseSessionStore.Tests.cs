using Microsoft.VisualStudio.TestTools.UnitTesting;
using PartFinder.Models;
using PartFinder.Services;

namespace PartFinder.Tests.Services;

[TestClass]
public sealed class CatalogBrowseSessionStoreTests
{
    private static PartTemplateDefinition T(string id, string name) => new() { Id = id, Name = name, Fields = [] };

    private static MasterDataSheetCacheEntry Entry(string templateId, MasterDataSurfaceMode mode) => new()
    {
        TemplateId = templateId,
        SurfaceMode = mode,
        Fields = [],
        Rows = [],
    };

    [TestMethod]
    public void GetTemplateBrowseState_IsStablePerModeAndTemplate()
    {
        var store = new CatalogBrowseSessionStore();

        var a = store.GetTemplateBrowseState(MasterDataSurfaceMode.ReadOnlyCatalog, "t1");
        a.PageIndex = 3;

        Assert.AreSame(a, store.GetTemplateBrowseState(MasterDataSurfaceMode.ReadOnlyCatalog, "t1"));
        Assert.AreEqual("t1", a.TemplateId);
        Assert.AreNotSame(a, store.GetTemplateBrowseState(MasterDataSurfaceMode.LinkedBrowse, "t1"));
        Assert.AreNotSame(a, store.GetTemplateDataBrowseState("t1"));
        Assert.AreSame(store.GetTemplateDataBrowseState("t1"), store.GetTemplateDataBrowseState("t1"));
    }

    [TestMethod]
    public void SheetCache_SetGetAndInvalidate()
    {
        var store = new CatalogBrowseSessionStore();
        var entry = Entry("t1", MasterDataSurfaceMode.LinkedBrowse);

        Assert.IsFalse(store.TryGetSheetCache(MasterDataSurfaceMode.LinkedBrowse, "t1", out _));
        store.SetSheetCache(MasterDataSurfaceMode.LinkedBrowse, "t1", entry);

        Assert.IsTrue(store.TryGetSheetCache(MasterDataSurfaceMode.LinkedBrowse, "t1", out var cached));
        Assert.AreSame(entry, cached);
        Assert.IsFalse(store.TryGetSheetCache(MasterDataSurfaceMode.ReadOnlyCatalog, "t1", out _));

        store.InvalidateSheetCache(MasterDataSurfaceMode.LinkedBrowse, "t1");
        Assert.IsFalse(store.TryGetSheetCache(MasterDataSurfaceMode.LinkedBrowse, "t1", out _));
    }

    [TestMethod]
    public void GetGridStateFor_LinkedBrowseUsesLinkedGrid()
    {
        var store = new CatalogBrowseSessionStore();

        Assert.AreSame(store.LinkedTemplatesGrid, store.GetGridStateFor(MasterDataSurfaceMode.LinkedBrowse));
        Assert.AreSame(store.MasterDataGrid, store.GetGridStateFor(MasterDataSurfaceMode.ReadOnlyCatalog));
        Assert.AreSame(store.MasterDataGrid, store.GetGridStateFor(MasterDataSurfaceMode.TemplateEdit));
    }

    [TestMethod]
    public void Reset_ClearsGridsCachesAndBrowseState()
    {
        var store = new CatalogBrowseSessionStore();
        store.MasterDataGrid.TemplateId = "t1";
        store.MasterDataGrid.PageIndex = 4;
        store.MasterDataGrid.ScrollVerticalOffset = 120;
        store.LinkedTemplatesGrid.ScrollHorizontalOffset = 30;
        store.LinkedTemplatesBrowseTemplateId = "t2";
        var browse = store.GetTemplateBrowseState(MasterDataSurfaceMode.ReadOnlyCatalog, "t1");
        store.SetSheetCache(MasterDataSurfaceMode.ReadOnlyCatalog, "t1", Entry("t1", MasterDataSurfaceMode.ReadOnlyCatalog));

        store.Reset();

        Assert.IsNull(store.MasterDataGrid.TemplateId);
        Assert.AreEqual(0, store.MasterDataGrid.PageIndex);
        Assert.AreEqual(0, store.MasterDataGrid.ScrollVerticalOffset);
        Assert.AreEqual(0, store.LinkedTemplatesGrid.ScrollHorizontalOffset);
        Assert.IsNull(store.LinkedTemplatesBrowseTemplateId);
        Assert.IsFalse(store.TryGetSheetCache(MasterDataSurfaceMode.ReadOnlyCatalog, "t1", out _));
        Assert.AreNotSame(browse, store.GetTemplateBrowseState(MasterDataSurfaceMode.ReadOnlyCatalog, "t1"));
    }

    [TestMethod]
    public void OrderForCatalogSidebar_PutsPartsCatalogFirstThenSortsByName()
    {
        var ordered = CatalogBrowseSessionStore.OrderForCatalogSidebar(
            [T("1", "zeta"), T("2", "Alpha"), T("3", "partfinder.parts"), T("4", "beta")]);

        CollectionAssert.AreEqual(new[] { "3", "2", "4", "1" }, ordered.Select(t => t.Id).ToArray());
    }

    [TestMethod]
    public void OrderForCatalogSidebar_EmptyInputIsReturnedAsIs()
    {
        var empty = new List<PartTemplateDefinition>();

        Assert.AreSame(empty, CatalogBrowseSessionStore.OrderForCatalogSidebar(empty));
    }

    [TestMethod]
    public void ResolveInitialTemplate_PrefersSavedThenPartsCatalogThenFirst()
    {
        var withParts = new[] { T("a", "Alpha"), T("p", "PartFinder.Parts"), T("z", "Zeta") };
        var withoutParts = new[] { T("a", "Alpha"), T("z", "Zeta") };

        Assert.AreEqual("z", CatalogBrowseSessionStore.ResolveInitialTemplate(withParts, "z")!.Id);
        Assert.AreEqual("p", CatalogBrowseSessionStore.ResolveInitialTemplate(withParts, "missing")!.Id);
        Assert.AreEqual("p", CatalogBrowseSessionStore.ResolveInitialTemplate(withParts, null)!.Id);
        Assert.AreEqual("a", CatalogBrowseSessionStore.ResolveInitialTemplate(withoutParts, " ")!.Id);
        Assert.IsNull(CatalogBrowseSessionStore.ResolveInitialTemplate([], "a"));
    }
}
