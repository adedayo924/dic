using System.Windows;
using System.Windows.Controls;
using DIC.ELibrary.UI.ViewModels;

namespace DIC.ELibrary.UI.Views;

public partial class LoginWindow : Window
{
    public LoginWindow(LoginViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = viewModel;
        Loaded += (_, _) => UsernameBox.Focus();
    }

    public LoginViewModel ViewModel { get; }

    private void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        ViewModel.Password = ((PasswordBox)sender).Password;
    }

    private async void OnSignIn(object sender, RoutedEventArgs e)
    {
        BusyText.Visibility = Visibility.Visible;
        var success = await ViewModel.LoginAsync();
        BusyText.Visibility = Visibility.Collapsed;
        if (success)
        {
            DialogResult = true;
        }
    }
}
