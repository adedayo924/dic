using CommunityToolkit.Mvvm.ComponentModel;

namespace DIC.ELibrary.UI.Mvvm;

/// <summary>Base class for all screen view models.</summary>
public abstract partial class ViewModelBase : ObservableObject
{
    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string title = string.Empty;

    public virtual Task InitializeAsync() => Task.CompletedTask;

    /// <summary>Called each time the view model becomes the active screen, with an optional argument.</summary>
    public virtual Task OnNavigatedToAsync(object? parameter) => Task.CompletedTask;

    protected async Task RunAsync(Func<Task> work)
    {
        if (IsBusy)
        {
            return;
        }
        IsBusy = true;
        try
        {
            await work();
        }
        finally
        {
            IsBusy = false;
        }
    }
}
