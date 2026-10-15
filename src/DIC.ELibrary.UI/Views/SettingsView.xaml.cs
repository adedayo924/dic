using System.Windows;
using System.Windows.Controls;
using DIC.ELibrary.UI.ViewModels;

namespace DIC.ELibrary.UI.Views;

public partial class SettingsView : UserControl
{
    public SettingsView() => InitializeComponent();

    private void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel vm || sender is not PasswordBox box)
        {
            return;
        }

        switch (box.Tag as string)
        {
            case "current":
                vm.CurrentPassword = box.Password;
                break;
            case "new":
                vm.NewPassword = box.Password;
                break;
            case "confirm":
                vm.ConfirmPassword = box.Password;
                break;
        }
    }
}
