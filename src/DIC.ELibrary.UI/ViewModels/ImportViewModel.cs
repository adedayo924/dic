using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DIC.ELibrary.Core.Abstractions;
using DIC.ELibrary.Core.Models;
using DIC.ELibrary.UI.Mvvm;
using DIC.ELibrary.UI.Services;

namespace DIC.ELibrary.UI.ViewModels;

public partial class ImportViewModel : ViewModelBase
{
    private readonly IPackageService _packages;
    private readonly IDialogService _dialogs;

    public ImportViewModel(IPackageService packages, IDialogService dialogs)
    {
        _packages = packages;
        _dialogs = dialogs;
        Title = "Import Book";
    }

    [ObservableProperty]
    private string? packagePath;

    [ObservableProperty]
    private PackageManifest? manifest;

    [ObservableProperty]
    private bool isVerified;

    [ObservableProperty]
    private string? statusMessage;

    public string FileName => string.IsNullOrWhiteSpace(PackagePath) ? string.Empty : Path.GetFileName(PackagePath);

    public override Task OnNavigatedToAsync(object? parameter)
    {
        Reset();
        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task BrowseAsync()
    {
        var file = _dialogs.OpenFile("DIC package (*.dicpkg)|*.dicpkg|All files (*.*)|*.*", "Select a package to import");
        if (file is null)
        {
            return;
        }
        PackagePath = file;
        OnPropertyChanged(nameof(FileName));
        await InspectAsync();
    }

    private async Task InspectAsync()
    {
        if (string.IsNullOrWhiteSpace(PackagePath))
        {
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _packages.InspectAsync(PackagePath);
            if (!result.Succeeded || result.Value is null)
            {
                Manifest = null;
                IsVerified = false;
                StatusMessage = result.Error ?? "Unable to read package.";
                return;
            }

            var inspection = result.Value;
            IsVerified = inspection.IsValid && inspection.SignatureVerified;
            Manifest = inspection.Manifest;
            StatusMessage = inspection.IsValid
                ? inspection.SignatureVerified ? "Signature verified. Ready to install." : "Warning: signature could not be verified."
                : inspection.Error ?? "Package is not valid.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task InstallAsync()
    {
        if (string.IsNullOrWhiteSpace(PackagePath))
        {
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _packages.InstallAsync(PackagePath);
            if (result.Succeeded)
            {
                _dialogs.ShowInfo("The book was imported into the library.", "Import complete");
                Reset();
            }
            else
            {
                _dialogs.ShowError(result.Error ?? "Import failed.");
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void Reset()
    {
        PackagePath = null;
        Manifest = null;
        IsVerified = false;
        StatusMessage = null;
        OnPropertyChanged(nameof(FileName));
    }
}
