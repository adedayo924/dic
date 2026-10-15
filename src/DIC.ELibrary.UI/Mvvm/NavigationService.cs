using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;

namespace DIC.ELibrary.UI.Mvvm;

public interface INavigationService : INotifyPropertyChanged
{
    ViewModelBase? Current { get; }
    bool CanGoBack { get; }
    event EventHandler? CurrentChanged;

    Task NavigateToAsync(Type viewModelType, object? parameter = null);
    Task NavigateToAsync<TViewModel>(object? parameter = null) where TViewModel : ViewModelBase;
    Task GoBackAsync();

    /// <summary>Clears the current view and back stack (e.g. when starting a new session).</summary>
    void Reset();
}

public sealed class NavigationService : ObservableObject, INavigationService
{
    private readonly IServiceProvider _services;
    private readonly Stack<ViewModelBase> _backStack = new();
    private ViewModelBase? _current;

    public NavigationService(IServiceProvider services) => _services = services;

    public ViewModelBase? Current
    {
        get => _current;
        private set
        {
            if (SetProperty(ref _current, value))
            {
                OnPropertyChanged(nameof(CanGoBack));
                CurrentChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public bool CanGoBack => _backStack.Count > 0;

    public event EventHandler? CurrentChanged;

    public async Task NavigateToAsync(Type viewModelType, object? parameter = null)
    {
        if (viewModelType is null)
        {
            return;
        }
        if (!typeof(ViewModelBase).IsAssignableFrom(viewModelType))
        {
            throw new ArgumentException($"{viewModelType.Name} is not a view model.", nameof(viewModelType));
        }

        var viewModel = (ViewModelBase)_services.GetRequiredService(viewModelType);
        if (_current is not null && viewModelType != _current.GetType())
        {
            _backStack.Push(_current);
        }

        Current = viewModel;
        try
        {
            await viewModel.OnNavigatedToAsync(parameter);
        }
        catch
        {
            // Surface via view-model state; navigation should not crash the app.
        }
    }

    public Task NavigateToAsync<TViewModel>(object? parameter = null) where TViewModel : ViewModelBase
        => NavigateToAsync(typeof(TViewModel), parameter);

    public Task GoBackAsync()
    {
        if (_backStack.Count == 0)
        {
            return Task.CompletedTask;
        }
        Current = _backStack.Pop();
        return Task.CompletedTask;
    }

    public void Reset()
    {
        _backStack.Clear();
        Current = null;
    }
}
