using DIC.ELibrary.UI.Mvvm;
using DIC.ELibrary.UI.Services;
using DIC.ELibrary.UI.ViewModels;
using DIC.ELibrary.UI.Views;
using Microsoft.Extensions.DependencyInjection;

namespace DIC.ELibrary.UI.DependencyInjection;

public static class ServiceCollectionExtensions
{
    /// <summary>Registers shared UI services, view models and windows.</summary>
    public static IServiceCollection AddELibraryUi(this IServiceCollection services)
    {
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<IModalService, ModalService>();
        services.AddSingleton<IThemeService, ThemeService>();

        services.AddTransient<LoginViewModel>();
        services.AddTransient<ShellViewModel>();
        services.AddTransient<WelcomeViewModel>();

        services.AddTransient<DashboardViewModel>();
        services.AddTransient<CatalogViewModel>();
        services.AddTransient<BookDetailViewModel>();
        services.AddTransient<ReaderViewModel>();
        services.AddTransient<MyLibraryViewModel>();
        services.AddTransient<ImportViewModel>();
        services.AddTransient<SettingsViewModel>();

        services.AddTransient<AdminDashboardViewModel>();
        services.AddTransient<BooksViewModel>();
        services.AddTransient<CategoriesViewModel>();
        services.AddTransient<IngestViewModel>();
        services.AddTransient<PackagesViewModel>();
        services.AddTransient<UsersViewModel>();
        services.AddTransient<LoansViewModel>();
        services.AddTransient<ReportsViewModel>();
        services.AddTransient<AuditViewModel>();
        services.AddTransient<AdminSettingsViewModel>();

        services.AddTransient<BookEditorViewModel>();
        services.AddTransient<UserEditorViewModel>();
        services.AddTransient<CategoryEditorViewModel>();

        services.AddTransient<LoginWindow>();
        services.AddTransient<ShellWindow>();
        services.AddTransient<WelcomeWindow>();
        services.AddTransient<BookEditorWindow>();
        services.AddTransient<UserEditorWindow>();
        services.AddTransient<CategoryEditorWindow>();

        return services;
    }
}
