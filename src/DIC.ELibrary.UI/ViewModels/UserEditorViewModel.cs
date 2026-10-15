using CommunityToolkit.Mvvm.ComponentModel;
using DIC.ELibrary.Core.Abstractions;
using DIC.ELibrary.Core.Domain;
using DIC.ELibrary.Core.Models;
using DIC.ELibrary.UI.Mvvm;

namespace DIC.ELibrary.UI.ViewModels;

public partial class UserEditorViewModel : ViewModelBase
{
    private readonly IUserService _users;

    public UserEditorViewModel(IUserService users)
    {
        _users = users;
        Title = "User";
    }

    [ObservableProperty]
    private Guid? userId;

    [ObservableProperty]
    private string username = string.Empty;

    [ObservableProperty]
    private string displayName = string.Empty;

    [ObservableProperty]
    private string? email;

    [ObservableProperty]
    private UserRole role = UserRole.Student;

    [ObservableProperty]
    private bool isActive = true;

    [ObservableProperty]
    private string password = string.Empty;

    public bool IsNew => UserId is null;

    public async Task InitializeAsync(Guid? id)
    {
        UserId = id;
        OnPropertyChanged(nameof(IsNew));

        if (id is null)
        {
            Username = string.Empty;
            DisplayName = string.Empty;
            Email = null;
            Role = UserRole.Student;
            IsActive = true;
            Password = string.Empty;
            return;
        }

        var user = await _users.GetAsync(id.Value);
        if (user is not null)
        {
            Username = user.Username;
            DisplayName = user.DisplayName;
            Email = user.Email;
            Role = user.Role;
            IsActive = user.IsActive;
        }
    }

    public async Task<OperationResult> SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Username))
        {
            return OperationResult.Fail("Username is required.");
        }
        if (string.IsNullOrWhiteSpace(DisplayName))
        {
            return OperationResult.Fail("Display name is required.");
        }

        var model = new UserEditModel
        {
            Id = UserId,
            Username = Username.Trim(),
            DisplayName = DisplayName.Trim(),
            Email = Email,
            Role = Role,
            IsActive = IsActive
        };

        if (UserId is null)
        {
            if (string.IsNullOrWhiteSpace(Password) || Password.Length < 6)
            {
                return OperationResult.Fail("Password must be at least 6 characters.");
            }
            var created = await _users.CreateAsync(model, Password);
            if (created.Succeeded && created.Value is not null)
            {
                UserId = created.Value.Id;
                return OperationResult.Ok();
            }
            return OperationResult.Fail(created.Error ?? "Unable to create the user.");
        }

        var result = await _users.UpdateAsync(model);
        if (!result.Succeeded)
        {
            return result;
        }

        if (!string.IsNullOrWhiteSpace(Password))
        {
            if (Password.Length < 6)
            {
                return OperationResult.Fail("Password must be at least 6 characters.");
            }
            return await _users.ResetPasswordAsync(UserId.Value, Password);
        }

        return OperationResult.Ok();
    }
}
