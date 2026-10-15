using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DIC.ELibrary.Core.Abstractions;
using DIC.ELibrary.Core.Domain;
using DIC.ELibrary.Core.Models;
using DIC.ELibrary.UI.Mvvm;
using DIC.ELibrary.UI.Services;

namespace DIC.ELibrary.UI.ViewModels;

public partial class UsersViewModel : ViewModelBase
{
    private readonly IUserService _users;
    private readonly IModalService _modals;
    private readonly IDialogService _dialogs;
    private readonly ICurrentUser _currentUser;

    public UsersViewModel(
        IUserService users,
        IModalService modals,
        IDialogService dialogs,
        ICurrentUser currentUser)
    {
        _users = users;
        _modals = modals;
        _dialogs = dialogs;
        _currentUser = currentUser;
        Title = "Users";
    }

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private int page = 1;

    [ObservableProperty]
    private int totalPages = 1;

    [ObservableProperty]
    private int totalCount;

    public ObservableCollection<UserSummary> Results { get; } = new();

    /// <summary>Bulk CSV import is an administrator-only privilege.</summary>
    public bool CanManageBulk => _currentUser.IsInRole(UserRole.Administrator);

    public override Task OnNavigatedToAsync(object? parameter) => RunAsync(LoadAsync);

    [RelayCommand]
    private Task SearchAsync()
    {
        Page = 1;
        return RunAsync(LoadAsync);
    }

    [RelayCommand]
    private Task NextAsync()
    {
        if (Page >= TotalPages)
        {
            return Task.CompletedTask;
        }
        Page++;
        return RunAsync(LoadAsync);
    }

    [RelayCommand]
    private Task PreviousAsync()
    {
        if (Page <= 1)
        {
            return Task.CompletedTask;
        }
        Page--;
        return RunAsync(LoadAsync);
    }

    [RelayCommand]
    private async Task NewAsync()
    {
        if (await _modals.EditUserAsync(null))
        {
            await LoadAsync();
        }
    }

    [RelayCommand]
    private async Task ImportCsvAsync()
    {
        var file = _dialogs.OpenFile("CSV files (*.csv)|*.csv|All files (*.*)|*.*", "Import users from CSV");
        if (file is null)
        {
            return;
        }

        try
        {
            await using var stream = File.OpenRead(file);
            var result = await _users.ImportAsync(stream);
            if (!result.Succeeded || result.Value is null)
            {
                _dialogs.ShowError(result.Error ?? "Import failed.");
                return;
            }

            var summary = result.Value;
            var message = $"Created {summary.Created} of {summary.Total} row(s).";
            if (summary.Failed > 0)
            {
                const int maxShown = 15;
                var details = string.Join(Environment.NewLine, summary.Errors.Take(maxShown));
                if (summary.Errors.Count > maxShown)
                {
                    details += Environment.NewLine + $"…and {summary.Errors.Count - maxShown} more.";
                }
                message += Environment.NewLine + Environment.NewLine + "Skipped rows:" + Environment.NewLine + details;
            }

            _dialogs.ShowInfo(message, "Bulk user import");
            await LoadAsync();
        }
        catch (Exception ex)
        {
            _dialogs.ShowError(ex.Message);
        }
    }

    [RelayCommand]
    private void DownloadTemplate()
    {
        var file = _dialogs.SaveFile("CSV files (*.csv)|*.csv", "Save user import template", "user-import-template.csv");
        if (file is null)
        {
            return;
        }

        try
        {
            File.WriteAllText(file, _users.GetImportTemplate());
            _dialogs.ShowInfo($"Template saved to:{Environment.NewLine}{file}", "CSV template");
        }
        catch (Exception ex)
        {
            _dialogs.ShowError(ex.Message);
        }
    }

    [RelayCommand]
    private async Task EditAsync(UserSummary? user)
    {
        if (user is null)
        {
            return;
        }
        if (await _modals.EditUserAsync(user.Id))
        {
            await LoadAsync();
        }
    }

    [RelayCommand]
    private async Task ToggleActiveAsync(UserSummary? user)
    {
        if (user is null)
        {
            return;
        }
        if (user.Id == _currentUser.UserId)
        {
            _dialogs.ShowError("You cannot change the activation status of your own account while signed in.");
            return;
        }
        var result = await _users.SetActiveAsync(user.Id, !user.IsActive);
        if (result.Succeeded)
        {
            await LoadAsync();
        }
        else
        {
            _dialogs.ShowError(result.Error ?? "Unable to change activation.");
        }
    }

    [RelayCommand]
    private async Task DeleteAsync(UserSummary? user)
    {
        if (user is null)
        {
            return;
        }
        if (user.Id == _currentUser.UserId)
        {
            _dialogs.ShowError("You cannot delete your own account while signed in.");
            return;
        }
        if (!_dialogs.Confirm($"Delete user '{user.Username}'?", "Delete user"))
        {
            return;
        }
        var result = await _users.DeleteAsync(user.Id);
        if (result.Succeeded)
        {
            await LoadAsync();
        }
        else
        {
            _dialogs.ShowError(result.Error ?? "Unable to delete the user.");
        }
    }

    private async Task LoadAsync()
    {
        var result = await _users.QueryAsync(SearchText, Page, 25);
        Results.Clear();
        foreach (var user in result.Items)
        {
            Results.Add(user);
        }
        TotalCount = result.TotalCount;
        TotalPages = Math.Max(1, result.TotalPages);
    }
}
