using CommunityToolkit.Mvvm.ComponentModel;
using DIC.ELibrary.Core.Abstractions;
using DIC.ELibrary.Core.Domain;
using DIC.ELibrary.UI.Mvvm;

namespace DIC.ELibrary.UI.ViewModels;

public partial class LoginViewModel : ViewModelBase
{
    private readonly IAuthService _auth;

    public LoginViewModel(IAuthService auth, AppShellOptions shell)
    {
        _auth = auth;
        Shell = shell;
        Title = "Sign in";
    }

    public AppShellOptions Shell { get; }

    /// <summary>When set, the signed-in account must have exactly this role.</summary>
    public UserRole? RequiredRole { get; private set; }

    [ObservableProperty]
    private string roleSubtitle = "Enter your account credentials.";

    [ObservableProperty]
    private string username = string.Empty;

    [ObservableProperty]
    private string password = string.Empty;

    [ObservableProperty]
    private string? errorMessage;

    /// <summary>Prepares the sign-in form for the role the user selected on the welcome screen.</summary>
    public void Configure(UserRole role)
    {
        RequiredRole = role;
        RoleSubtitle = $"Sign in with your {role} account.";
        Username = string.Empty;
        Password = string.Empty;
        ErrorMessage = null;
    }

    public async Task<bool> LoginAsync()
    {
        ErrorMessage = null;

        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrEmpty(Password))
        {
            ErrorMessage = "Enter your username and password.";
            return false;
        }

        IsBusy = true;
        try
        {
            var result = await _auth.LoginAsync(Username.Trim(), Password);
            if (!result.Succeeded || result.Value is null)
            {
                ErrorMessage = result.Error ?? "Sign in failed.";
                return false;
            }

            if (RequiredRole is { } required && result.Value.Role != required)
            {
                await _auth.LogoutAsync();
                ErrorMessage = $"This account is registered as {result.Value.Role}, not as {required}. " +
                               "Go back and choose the matching role.";
                return false;
            }

            if (RequiredRole is null && Shell.RequireStaffRole &&
                result.Value.Role is not (UserRole.Librarian or UserRole.Administrator))
            {
                await _auth.LogoutAsync();
                ErrorMessage = "This account does not have administrator access.";
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
