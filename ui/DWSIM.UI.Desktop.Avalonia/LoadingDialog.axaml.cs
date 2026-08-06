using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;

namespace DWSIM.UI.Desktop.Avalonia;

/// <summary>
/// Modeless progress dialog for long-running engine operations (load, save, solve, regress).
/// Pattern: show, run the work on a background task, call SetProgress/SetMessage from any
/// thread, call Finish() to close. Avalonia equivalent of the Eto LoadingData dialog.
/// </summary>
public partial class LoadingDialog : Window
{
    public LoadingDialog()
    {
        InitializeComponent();
        IconHelper.ApplyWindowIcon(this);
    }

    public LoadingDialog(string title, string message) : this()
    {
        Title = title;
        LblTitle.Text = title;
        LblMessage.Text = message;
    }

    /// <summary>Sets the secondary message line (thread-safe).</summary>
    public void SetMessage(string msg)
    {
        Dispatcher.UIThread.Post(() => LblMessage.Text = msg);
    }

    /// <summary>Switches the bar from indeterminate to a 0-100 percent display (thread-safe).</summary>
    public void SetProgress(double percent)
    {
        Dispatcher.UIThread.Post(() =>
        {
            Bar.IsIndeterminate = false;
            Bar.Value = Math.Max(0, Math.Min(100, percent));
            LblPercent.Text = $"{Bar.Value:F0} %";
        });
    }

    /// <summary>Closes the dialog from any thread.</summary>
    public void Finish()
    {
        Dispatcher.UIThread.Post(Close);
    }

    /// <summary>
    /// Convenience: shows the dialog as modeless, runs <paramref name="work"/> on a background
    /// thread, then closes the dialog when the task finishes (success or failure).
    /// Returns whatever the task produced. Exceptions propagate to the caller.
    /// </summary>
    public static async Task<T> RunAsync<T>(Window owner, string title, string message,
        Func<LoadingDialog, Task<T>> work)
    {
        var dlg = new LoadingDialog(title, message);
        dlg.Show(owner);
        try
        {
            return await work(dlg).ConfigureAwait(true);
        }
        finally
        {
            dlg.Finish();
        }
    }

    public static async Task RunAsync(Window owner, string title, string message,
        Func<LoadingDialog, Task> work)
    {
        var dlg = new LoadingDialog(title, message);
        dlg.Show(owner);
        try
        {
            await work(dlg).ConfigureAwait(true);
        }
        finally
        {
            dlg.Finish();
        }
    }
}
