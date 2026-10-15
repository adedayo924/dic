using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using DIC.ELibrary.UI.ViewModels;

namespace DIC.ELibrary.UI.Views;

public partial class ReaderView : UserControl
{
    private readonly DispatcherTimer _resizeDebounce;

    public ReaderView()
    {
        InitializeComponent();
        _resizeDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _resizeDebounce.Tick += OnResizeDebounceTick;
        PageScrollViewer.SizeChanged += OnPageAreaResized;
        Unloaded += (_, _) => _resizeDebounce.Stop();
    }

    private void OnPageAreaResized(object sender, SizeChangedEventArgs e)
    {
        _resizeDebounce.Stop();
        _resizeDebounce.Start();
    }

    private void OnResizeDebounceTick(object? sender, EventArgs e)
    {
        _resizeDebounce.Stop();
        if (DataContext is ReaderViewModel viewModel)
        {
            var width = PageScrollViewer.ViewportWidth > 0
                ? PageScrollViewer.ViewportWidth
                : PageScrollViewer.ActualWidth;
            viewModel.SetViewportWidth(width);
        }
    }
}
