using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DIC.ELibrary.Core.Abstractions;
using DIC.ELibrary.Core.Domain;
using DIC.ELibrary.UI.Mvvm;
using DIC.ELibrary.UI.Services;

namespace DIC.ELibrary.UI.ViewModels;

public partial class AuditViewModel : ViewModelBase
{
    private readonly IAuditService _audit;
    private readonly IReportService _reports;
    private readonly IDialogService _dialogs;

    public AuditViewModel(IAuditService audit, IReportService reports, IDialogService dialogs)
    {
        _audit = audit;
        _reports = reports;
        _dialogs = dialogs;
        Title = "Audit log";
    }

    [ObservableProperty]
    private int page = 1;

    [ObservableProperty]
    private int totalPages = 1;

    [ObservableProperty]
    private int totalCount;

    public ObservableCollection<AuditLog> Results { get; } = new();

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

    private async Task LoadAsync()
    {
        var result = await _audit.QueryAsync(Page, 50);
        Results.Clear();
        foreach (var entry in result.Items)
        {
            Results.Add(entry);
        }
        TotalCount = result.TotalCount;
        TotalPages = Math.Max(1, result.TotalPages);
    }

    [RelayCommand]
    private async Task ExportCsvAsync()
    {
        var file = _dialogs.SaveFile("CSV files (*.csv)|*.csv", "Export audit log", "audit-log.csv");
        if (file is null)
        {
            return;
        }

        try
        {
            var csv = await _reports.BuildAuditReportCsvAsync();
            File.WriteAllText(file, csv);
            _dialogs.ShowInfo($"Audit log saved to:{Environment.NewLine}{file}", "Export complete");
        }
        catch (Exception ex)
        {
            _dialogs.ShowError(ex.Message);
        }
    }
}
