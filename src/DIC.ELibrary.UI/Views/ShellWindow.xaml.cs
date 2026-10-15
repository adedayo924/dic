using System.Windows;
using DIC.ELibrary.UI.ViewModels;

namespace DIC.ELibrary.UI.Views;

public partial class ShellWindow : Window
{
    public ShellWindow(ShellViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = viewModel;
        viewModel.SignOutRequested += OnSignOutRequested;
        Loaded += async (_, _) => await viewModel.InitializeAsync();
    }

    public ShellViewModel ViewModel { get; }

    /// <summary>True when the window closed because the user signed out (vs. closing the application).</summary>
    public bool SignedOut { get; private set; }

    private void OnSignOutRequested(object? sender, EventArgs e)
    {
        SignedOut = true;
        Close();
    }
}
