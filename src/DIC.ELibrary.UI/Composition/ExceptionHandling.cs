using System.Windows;
using Serilog;

namespace DIC.ELibrary.UI.Composition;

/// <summary>Routes otherwise-unhandled exceptions to the log and surfaces them instead of crashing silently.</summary>
public static class ExceptionHandling
{
    public static void Attach(Application app, string productName)
    {
        app.DispatcherUnhandledException += (_, e) =>
        {
            Log.Error(e.Exception, "Unhandled exception on the UI thread.");
            ShowError(productName, e.Exception);
            e.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            {
                Log.Error(ex, "Unhandled exception on a background thread.");
            }
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Log.Error(e.Exception, "Unobserved task exception.");
            e.SetObserved();
        };
    }

    private static void ShowError(string productName, Exception exception)
    {
        try
        {
            MessageBox.Show(
                $"An unexpected error occurred.\n\n{exception.Message}",
                productName,
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        catch
        {
            // Never let error reporting take down the process.
        }
    }
}
