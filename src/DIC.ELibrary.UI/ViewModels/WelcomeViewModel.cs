using CommunityToolkit.Mvvm.ComponentModel;
using DIC.ELibrary.Core.Domain;
using DIC.ELibrary.UI.Mvvm;

namespace DIC.ELibrary.UI.ViewModels;

/// <summary>Landing screen that lets a user pick the role they are signing in as.</summary>
public partial class WelcomeViewModel : ViewModelBase
{
    public WelcomeViewModel(AppShellOptions shell)
    {
        Shell = shell;
        Title = shell.ProductName;
    }

    public AppShellOptions Shell { get; }

    [ObservableProperty]
    private UserRole? selectedRole;

    public void Choose(UserRole role) => SelectedRole = role;
}
