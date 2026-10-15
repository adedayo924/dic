using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DIC.ELibrary.Core.Abstractions;
using DIC.ELibrary.Core.Models;
using DIC.ELibrary.UI.Mvvm;

namespace DIC.ELibrary.UI.ViewModels;

public partial class AdminDashboardViewModel : ViewModelBase
{
    private readonly IStatsService _stats;
    private readonly ILoanService _loans;

    public AdminDashboardViewModel(IStatsService stats, ILoanService loans)
    {
        _stats = stats;
        _loans = loans;
        Title = "Dashboard";
    }

    [ObservableProperty]
    private LibraryStats? stats;

    [ObservableProperty]
    private int overdueCount;

    public ObservableCollection<CategoryCount> BooksByCategory { get; } = new();

    public override Task OnNavigatedToAsync(object? parameter) => RunAsync(async () =>
    {
        Stats = await _stats.GetStatsAsync();
        OverdueCount = Stats?.OverdueLoans ?? 0;

        BooksByCategory.Clear();
        if (Stats is not null)
        {
            foreach (var entry in Stats.BooksByCategory)
            {
                BooksByCategory.Add(entry);
            }
        }
    });

    [RelayCommand]
    private Task RefreshAsync() => OnNavigatedToAsync(null);
}
