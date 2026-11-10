namespace PartFinder.Models;

public enum MasterDataSurfaceMode
{
    /// <summary>Master Data nav: all templates, read-only, no hyperlinks.</summary>
    ReadOnlyCatalog = 0,

    /// <summary>Linked Templates: browse rows with worksheet hyperlinks and linked-data panel.</summary>
    LinkedBrowse = 1,

    /// <summary>Templates → Edit data: full spreadsheet editing for one template.</summary>
    TemplateEdit = 2,
}
