using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DIC.ELibrary.Core.Abstractions;
using DIC.ELibrary.Core.Domain;
using DIC.ELibrary.Core.Models;
using DIC.ELibrary.UI.Mvvm;
using DIC.ELibrary.UI.Services;

namespace DIC.ELibrary.UI.ViewModels;

public sealed record LoanStatusFilter(string Label, LoanStatus? Status);

public partial class LoansViewModel : ViewModelBase
{
    private readonly ILoanService _loans;
    private readonly IDialogService _dialogs;

    public LoansViewModel(ILoanService loans, IDialogService dialogs)
    {
        _loans = loans;
        _dialogs = dialogs;
        Title = "Loans";
        Filters = new ObservableCollection<LoanStatusFilter>
        {
            new("All loans", null),
            new("Active", LoanStatus.Active),
            new("Overdue", LoanStatus.Overdue),
            new("Returned", LoanStatus.Returned)
        };
        SelectedFilter = Filters[0];
    }

    [ObservableProperty]
    private LoanStatusFilter selectedFilter;

    [ObservableProperty]
    private int page = 1;

    [ObservableProperty]
    private int totalPages = 1;

    [ObservableProperty]
    private int totalCount;

    [ObservableProperty]
    private int overdueRefreshed;

    public ObservableCollection<LoanStatusFilter> Filters { get; }

    public ObservableCollection<LoanSummary> Results { get; } = new();

    public override Task OnNavigatedToAsync(object? parameter) => RunAsync(LoadAsync);

    [RelayCommand]
    private Task RefreshAsync() => RunAsync(LoadAsync);

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
    private async Task RefreshOverdueAsync()
    {
        OverdueRefreshed = await _loans.RefreshOverdueAsync();
        await LoadAsync();
    }

    [RelayCommand]
    private async Task ReturnAsync(LoanSummary? loan)
    {
        if (loan is null)
        {
            return;
        }
        if (!_dialogs.Confirm($"Mark '{loan.BookTitle}' as returned?", "Return book"))
        {
            return;
        }
        var result = await _loans.ReturnAsync(loan.Id);
        if (result.Succeeded)
        {
            await LoadAsync();
        }
        else
        {
            _dialogs.ShowError(result.Error ?? "Unable to return the book.");
        }
    }

    private async Task LoadAsync()
    {
        var result = await _loans.QueryAsync(SelectedFilter.Status, Page, 25);
        Results.Clear();
        foreach (var loan in result.Items)
        {
            Results.Add(loan);
        }
        TotalCount = result.TotalCount;
        TotalPages = Math.Max(1, result.TotalPages);
    }
}
