namespace PartFinder.Services;

/// <summary>
/// Lets the shell re-apply navigation parameters when the user is already on the target page.
/// </summary>
public interface INavigationParameterSink
{
    void ReceiveNavigationParameter(object? parameter);
}
