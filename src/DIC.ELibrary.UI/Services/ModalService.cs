using System.Windows;
using DIC.ELibrary.UI.ViewModels;
using DIC.ELibrary.UI.Views;
using Microsoft.Extensions.DependencyInjection;

namespace DIC.ELibrary.UI.Services;

public interface IModalService
{
    Task<bool> EditBookAsync(Guid? bookId);
    Task<bool> EditUserAsync(Guid? userId);
    Task<bool> EditCategoryAsync(Guid? categoryId);
}

public sealed class ModalService : IModalService
{
    private readonly IServiceProvider _services;

    public ModalService(IServiceProvider services) => _services = services;

    public async Task<bool> EditBookAsync(Guid? bookId)
    {
        var window = _services.GetRequiredService<BookEditorWindow>();
        await window.ViewModel.InitializeAsync(bookId);
        window.Owner = Application.Current.MainWindow;
        return window.ShowDialog() == true;
    }

    public async Task<bool> EditUserAsync(Guid? userId)
    {
        var window = _services.GetRequiredService<UserEditorWindow>();
        await window.ViewModel.InitializeAsync(userId);
        window.Owner = Application.Current.MainWindow;
        return window.ShowDialog() == true;
    }

    public async Task<bool> EditCategoryAsync(Guid? categoryId)
    {
        var window = _services.GetRequiredService<CategoryEditorWindow>();
        await window.ViewModel.InitializeAsync(categoryId);
        window.Owner = Application.Current.MainWindow;
        return window.ShowDialog() == true;
    }
}
