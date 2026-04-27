namespace PartFinder.Helpers;

/// <summary>
/// Pure static helper that computes depth-based visual parameters for the
/// stacked card carousel. All methods are free of UI dependencies and can be
/// unit-tested directly.
/// </summary>
public static class CarouselDepthCalculator
{
    /// <summary>
    /// Returns the opacity for a card at the given depth level.
    /// Formula: max(0, 1.0 - depth × 0.15)
    /// </summary>
    public static double GetOpacity(int depth) => Math.Max(0, 1.0 - depth * 0.15);

    /// <summary>
    /// Returns the scale factor for a card at the given depth level.
    /// Formula: max(0, 1.0 - depth × 0.05)
    /// </summary>
    public static double GetScale(int depth) => Math.Max(0, 1.0 - depth * 0.05);

    /// <summary>
    /// Returns the blur radius (in pixels) for a card at the given depth level.
    /// Formula: depth × 1.5
    /// </summary>
    public static double GetBlurRadius(int depth) => depth * 1.5;

    /// <summary>
    /// Returns the Y offset (in pixels) for a card at the given depth level.
    /// Formula: depth × 12.0
    /// </summary>
    public static double GetYOffset(int depth) => depth * 12.0;
}
