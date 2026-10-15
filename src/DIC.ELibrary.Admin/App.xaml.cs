using System.Windows;
using DIC.ELibrary.Core;
using DIC.ELibrary.Core.Abstractions;
using DIC.ELibrary.UI.Composition;
using DIC.ELibrary.UI.Mvvm;
using DIC.ELibrary.UI.ViewModels;
using DIC.ELibrary.UI.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DIC.ELibrary.Admin;

public partial class App : Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        ExceptionHandling.Attach(this, AppInfo.AdminName);

        try
        {
            _host = ELibraryComposition.BuildHost(e.Args, ConfigureShell);
            await _host.StartAsync();
            await ELibraryComposition.InitializeDatabaseAsync(_host);
            await _host.Services.GetRequiredService<IAuthService>().EnsureSeedAdminAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"The application failed to start.\n\n{ex.Message}", AppInfo.AdminName,
                MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
            return;
        }

        var login = _host.Services.GetRequiredService<LoginWindow>();
        if (login.ShowDialog() == true)
        {
            var shell = _host.Services.GetRequiredService<ShellWindow>();
            MainWindow = shell;
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            shell.Show();
        }
        else
        {
            Shutdown();
        }
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

    private static void ConfigureShell(AppShellOptions shell, IServiceCollection services)
    {
        shell.ProductName = AppInfo.AdminName;
        shell.Subtitle = "Administration & content management";
        shell.HeaderBrushKey = "IndigoDarkBrush";
        shell.AccentBrushKey = "RedBrush";
        shell.RequireStaffRole = true;
        shell.StartupViewModel = typeof(AdminDashboardViewModel);

        shell.NavigationItems.Add(new NavigationItem("Dashboard", "🏛", typeof(AdminDashboardViewModel)));
        shell.NavigationItems.Add(new NavigationItem("Books", "📚", typeof(BooksViewModel)));
        shell.NavigationItems.Add(new NavigationItem("Categories", "🏷", typeof(CategoriesViewModel)));
        shell.NavigationItems.Add(new NavigationItem("Ingest", "⤵", typeof(IngestViewModel)));
        shell.NavigationItems.Add(new NavigationItem("Packages", "📦", typeof(PackagesViewModel)));
        shell.NavigationItems.Add(new NavigationItem("Users", "👥", typeof(UsersViewModel)));
        shell.NavigationItems.Add(new NavigationItem("Loans", "🕒", typeof(LoansViewModel)));
        shell.NavigationItems.Add(new NavigationItem("Reports", "📊", typeof(ReportsViewModel)));
        shell.NavigationItems.Add(new NavigationItem("Audit", "🔍", typeof(AuditViewModel)));
        shell.NavigationItems.Add(new NavigationItem("Settings", "⚙", typeof(AdminSettingsViewModel)));
    }
}
