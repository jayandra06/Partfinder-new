using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using PartFinder.Services;
using System;

namespace PartFinder.ViewModels;

public partial class SessionUIWrapper : ObservableObject
{
    public AdminAuthApiClient.LoginSessionDto Session { get; }
    private readonly string _currentSessionId;

    public SessionUIWrapper(AdminAuthApiClient.LoginSessionDto session, string currentSessionId)
    {
        Session = session;
        _currentSessionId = currentSessionId;
    }

    public bool IsCurrent => Session.Id == _currentSessionId;
    public Visibility CurrentBadgeVisibility => IsCurrent ? Visibility.Visible : Visibility.Collapsed;
    public Visibility NonCurrentBadgeVisibility => IsCurrent ? Visibility.Collapsed : Visibility.Visible;
    public Visibility RevokeButtonVisibility => IsCurrent ? Visibility.Collapsed : Visibility.Visible;

    public string DisplayTime => Session.LoginTime.ToLocalTime().ToString("MMM dd, yyyy HH:mm");
    public string DisplayLastActive => Session.UpdatedAt.ToLocalTime().ToString("MMM dd, yyyy HH:mm");
}
