using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DIC.ELibrary.Core.Abstractions;
using DIC.ELibrary.Core.Domain;
using DIC.ELibrary.UI.Mvvm;
using DIC.ELibrary.UI.Services;

namespace DIC.ELibrary.UI.ViewModels;

public partial class ShellViewModel : ViewModelBase
{
    private readonly INavigationService _navigation;
    private readonly IAuthService _auth;
    private readonly ICurrentUser _currentUser;
    private readonly IDialogService _dialogs;
    private readonly Type? _startupViewModel;

    public ShellViewModel(
        INavigationService navigation,
        IAuthService auth,
        ICurrentUser currentUser,
        IDialogService dialogs,
        AppShellOptions shell)
    {
        _navigation = navigation;
        _auth = auth;
        _currentUser = currentUser;
        _dialogs = dialogs;
        Shell = shell;
        Title = shell.ProductName;

        var isStaff = _currentUser.Session?.Role is UserRole.Librarian or UserRole.Administrator;
        var useStaffNav = isStaff && shell.StaffNavigationItems.Count > 0;
        var items = useStaffNav ? shell.StaffNavigationItems : shell.NavigationItems;
        _startupViewModel = useStaffNav ? shell.StaffStartupViewModel : shell.StartupViewModel;
        NavigationItems = new ObservableCollection<NavigationItem>(items);

        _navigation.Reset();
        _navigation.CurrentChanged += (_, _) =>
        {
            Current = _navigation.Current;
            OnPropertyChanged(nameof(CanGoBack));
        };
    }

    public AppShellOptions Shell { get; }

    public ObservableCollection<NavigationItem> NavigationItems { get; }

    [ObservableProperty]
    private NavigationItem? selectedItem;

    [ObservableProperty]
    private ViewModelBase? current;

    public bool CanGoBack => _navigation.CanGoBack;

    public string UserDisplay
    {
        get
        {
            var session = _currentUser.Session;
            if (session is null)
            {
                return "Guest";
            }
            return $"{session.DisplayName} · {session.Role}";
        }
    }

    public event EventHandler? SignOutRequested;

    public override Task InitializeAsync()
    {
        var startup = _startupViewModel is not null
            ? NavigationItems.FirstOrDefault(i => i.ViewModelType == _startupViewModel) ?? NavigationItems.FirstOrDefault()
            : NavigationItems.FirstOrDefault();

        if (startup is not null)
        {
            SelectedItem = startup;
        }

        return Task.CompletedTask;
    }

    partial void OnSelectedItemChanged(NavigationItem? value)
    {
        if (value is not null)
        {
            _ = _navigation.NavigateToAsync(value.ViewModelType);
        }
    }

    [RelayCommand]
    private Task GoBackAsync() => _navigation.GoBackAsync();

    [RelayCommand]
    private async Task SignOutAsync()
    {
        if (!_dialogs.Confirm("Sign out of the current account?", "Sign out"))
        {
            return;
        }
        await _auth.LogoutAsync();
        SignOutRequested?.Invoke(this, EventArgs.Empty);
    }
}
