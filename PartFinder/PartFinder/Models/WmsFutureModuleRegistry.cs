namespace PartFinder.Models;

/// <summary>Placeholder metadata for future warehouse worksheets (no business logic).</summary>
public static class WmsFutureModuleRegistry
{
    public static IReadOnlyList<WmsFutureModuleDescriptor> Modules { get; } =
    [
        new("Order lines", "Phase 03B", "Worksheet shell for sales/purchase order lines."),
        new("Employee tasks", "Phase 03B", "Task assignment grid tied to WMS operator grants."),
        new("Picking confirmation", "Phase 03B", "Scan-to-confirm picks against reservations."),
        new("Package contents", "Phase 04", "Verify carton contents before seal."),
        new("Label printing", "Phase 04", "Pouch and carton labels (no fake print jobs)."),
        new("Dispatch confirmation", "Phase 05", "Post dispatch ledger entries."),
        new("Procurement alerts", "Phase 06", "Low-stock signals; read-only until APIs exist."),
    ];
}

public sealed record WmsFutureModuleDescriptor(string Title, string Phase, string Description);