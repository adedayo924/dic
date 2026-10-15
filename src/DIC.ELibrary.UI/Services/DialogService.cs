using Microsoft.Win32;
using System.Windows;

namespace DIC.ELibrary.UI.Services;

public interface IDialogService
{
    void ShowInfo(string message, string? title = null);
    void ShowError(string message, string? title = null);
    bool Confirm(string message, string? title = null);
    string? OpenFile(string filter, string? title = null);
    string? SaveFile(string filter, string? title = null, string? defaultFileName = null);
    string? PickFolder(string? title = null);
}

public sealed class DialogService : IDialogService
{
    public void ShowInfo(string message, string? title = null)
        => MessageBox.Show(message, title ?? "DIC E-Library", MessageBoxButton.OK, MessageBoxImage.Information);

    public void ShowError(string message, string? title = null)
        => MessageBox.Show(message, title ?? "Error", MessageBoxButton.OK, MessageBoxImage.Error);

    public bool Confirm(string message, string? title = null)
        => MessageBox.Show(message, title ?? "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    public string? OpenFile(string filter, string? title = null)
    {
        var dialog = new OpenFileDialog { Filter = filter, Title = title ?? "Open file", CheckFileExists = true };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? SaveFile(string filter, string? title = null, string? defaultFileName = null)
    {
        var dialog = new SaveFileDialog { Filter = filter, Title = title ?? "Save file", FileName = defaultFileName, OverwritePrompt = true };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PickFolder(string? title = null)
    {
        var dialog = new OpenFolderDialog { Title = title ?? "Select folder" };
        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }
}
