using System.Windows;
using DIC.ELibrary.Core;
using DIC.ELibrary.Core.Abstractions;
using DIC.ELibrary.UI.Composition;
using DIC.ELibrary.UI.Mvvm;
using DIC.ELibrary.UI.ViewModels;
using DIC.ELibrary.UI.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DIC.ELibrary.App;

public partial class App : Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        ExceptionHandling.Attach(this, AppInfo.Name);

        try
        {
            _host = ELibraryComposition.BuildHost(e.Args, ConfigureShell);
            await _host.StartAsync();
            await ELibraryComposition.InitializeDatabaseAsync(_host);
            await _host.Services.GetRequiredService<IAuthService>().EnsureSeedAdminAsync();
            await RunScheduledBackupAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"The application failed to start.\n\n{ex.Message}", AppInfo.Name,
                MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
            return;
        }

        while (true)
        {
            var welcome = _host.Services.GetRequiredService<WelcomeWindow>();
            if (welcome.ShowDialog() != true || welcome.ViewModel.SelectedRole is not { } role)
            {
                break;
            }

            var login = _host.Services.GetRequiredService<LoginWindow>();
            login.ViewModel.Configure(role);
            if (login.ShowDialog() != true)
            {
                continue;
            }

            var shell = _host.Services.GetRequiredService<ShellWindow>();
            MainWindow = shell;
            shell.ShowDialog();

            if (shell.SignedOut)
            {
                MainWindow = null;
                continue;
            }
            break;
        }

        Shutdown();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
            _host = null;
        }

        base.OnExit(e);
    }

    private async Task RunScheduledBackupAsync()
    {
        try
        {
            var scheduler = _host!.Services.GetRequiredService<IBackupScheduler>();
            await scheduler.RunIfDueAsync();
        }
        catch (Exception ex)
        {
            // Best effort: a failed scheduled backup must not prevent the app from starting.
            System.Diagnostics.Debug.WriteLine($"Scheduled backup failed: {ex.Message}");
        }
    }

    private static void ConfigureShell(AppShellOptions shell, IServiceCollection services)
    {
        shell.ProductName = AppInfo.Name;
        shell.Subtitle = "Digital library system";
        shell.HeaderBrushKey = "IndigoBrush";
        shell.AccentBrushKey = "RedBrush";

        // Student / reader experience
        shell.StartupViewModel = typeof(DashboardViewModel);
        shell.NavigationItems.Add(new NavigationItem("Dashboard", "🏠", typeof(DashboardViewModel)));
        shell.NavigationItems.Add(new NavigationItem("Catalog", "📚", typeof(CatalogViewModel)));
        shell.NavigationItems.Add(new NavigationItem("My Library", "⭐", typeof(MyLibraryViewModel)));
        shell.NavigationItems.Add(new NavigationItem("Import Book", "📥", typeof(ImportViewModel)));
        shell.NavigationItems.Add(new NavigationItem("Settings", "⚙", typeof(SettingsViewModel)));

        // Staff / administration experience
        shell.StaffStartupViewModel = typeof(AdminDashboardViewModel);
        shell.StaffNavigationItems.Add(new NavigationItem("Dashboard", "🏛", typeof(AdminDashboardViewModel)));
        shell.StaffNavigationItems.Add(new NavigationItem("Books", "📚", typeof(BooksViewModel)));
        shell.StaffNavigationItems.Add(new NavigationItem("Categories", "🏷", typeof(CategoriesViewModel)));
        shell.StaffNavigationItems.Add(new NavigationItem("Ingest", "⤵", typeof(IngestViewModel)));
        shell.StaffNavigationItems.Add(new NavigationItem("Packages", "📦", typeof(PackagesViewModel)));
        shell.StaffNavigationItems.Add(new NavigationItem("Users", "👥", typeof(UsersViewModel)));
        shell.StaffNavigationItems.Add(new NavigationItem("Loans", "🕒", typeof(LoansViewModel)));
        shell.StaffNavigationItems.Add(new NavigationItem("Reports", "📊", typeof(ReportsViewModel)));
        shell.StaffNavigationItems.Add(new NavigationItem("Audit", "🔍", typeof(AuditViewModel)));
        shell.StaffNavigationItems.Add(new NavigationItem("Settings", "⚙", typeof(AdminSettingsViewModel)));
    }
}
