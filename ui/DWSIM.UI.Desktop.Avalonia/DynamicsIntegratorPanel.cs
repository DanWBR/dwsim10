using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using DWSIM.Automation.DynamicRunner;
using DWSIM.Interfaces;

namespace DWSIM.UI.Desktop.Avalonia;

/// <summary>
/// Avalonia port of DynamicsIntegratorControl (Eto).
/// Row 1: Schedule dropdown + View Results button.
/// Row 2: Play / Real-Time / Stop buttons + status label + progress bar.
/// Placed in the bottom dock alongside the Log panel.
/// </summary>
public sealed class DynamicsIntegratorPanel : StackPanel
{
    private readonly ComboBox _cbSchedule;
    private readonly Button _btnPlay;
    private readonly Button _btnRT;
    private readonly Button _btnStop;
    private readonly Button _btnViewResults;
    private readonly TextBlock _lbStatus;
    private readonly ProgressBar _pbProgress;

    private IFlowsheet? _flowsheet;

    /// <summary>Set to true by the Stop button; checked by the integrator loop.</summary>
    public bool Abort { get; set; }

    public DynamicsIntegratorPanel()
    {
        Orientation = global::Avalonia.Layout.Orientation.Vertical;
        Spacing = 4;
        Margin = new Thickness(4);

        // --- Row 1: Schedule + View Results ---
        _cbSchedule = new ComboBox
        {
            Width = DWSIM.UI.Shared.Avalonia.UiScale.Size(300),
            FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(11),
            VerticalAlignment = VerticalAlignment.Center
        };

        _btnViewResults = new Button
        {
            Content = "View Results",
            FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(11),
            Padding = new Thickness(8, 3),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0)
        };
        _btnViewResults.Classes.Add("panel");

        var row1 = new StackPanel
        {
            Orientation = global::Avalonia.Layout.Orientation.Horizontal,
            Spacing = 6,
            Margin = new Thickness(0, 2)
        };
        row1.Children.Add(new TextBlock
        {
            Text = "Schedule:",
            FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(11),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 4, 0)
        });
        row1.Children.Add(_cbSchedule);
        row1.Children.Add(_btnViewResults);
        Children.Add(row1);

        // --- Row 2: Play / RT / Stop + Status + Progress ---
        _btnPlay = new Button
        {
            Content = "▶",  // play triangle
            FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(16),
            Width = DWSIM.UI.Shared.Avalonia.UiScale.Size(36),
            Height = DWSIM.UI.Shared.Avalonia.UiScale.Size(36),
            Padding = new Thickness(0),
            VerticalContentAlignment = VerticalAlignment.Center,
            HorizontalContentAlignment = HorizontalAlignment.Center
        };
        _btnPlay.Classes.Add("toolbar");
        ToolTip.SetTip(_btnPlay, "Run Integrator");

        _btnRT = new Button
        {
            Content = "⏱",  // stopwatch (real-time)
            FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(16),
            Width = DWSIM.UI.Shared.Avalonia.UiScale.Size(36),
            Height = DWSIM.UI.Shared.Avalonia.UiScale.Size(36),
            Padding = new Thickness(0),
            VerticalContentAlignment = VerticalAlignment.Center,
            HorizontalContentAlignment = HorizontalAlignment.Center
        };
        _btnRT.Classes.Add("toolbar");
        ToolTip.SetTip(_btnRT, "Run Real-Time");

        _btnStop = new Button
        {
            Content = "⏹",  // stop
            FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(16),
            Width = DWSIM.UI.Shared.Avalonia.UiScale.Size(36),
            Height = DWSIM.UI.Shared.Avalonia.UiScale.Size(36),
            Padding = new Thickness(0),
            Foreground = new SolidColorBrush(Colors.Red),
            VerticalContentAlignment = VerticalAlignment.Center,
            HorizontalContentAlignment = HorizontalAlignment.Center
        };
        _btnStop.Classes.Add("toolbar");
        ToolTip.SetTip(_btnStop, "Stop Integrator");

        _lbStatus = new TextBlock
        {
            Text = "00:00:00 / 00:30:00",
            FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(11),
            FontFamily = new FontFamily("Consolas,Courier New,monospace"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0)
        };

        _pbProgress = new ProgressBar
        {
            Minimum = 0,
            Maximum = 100,
            Value = 0,
            Width = DWSIM.UI.Shared.Avalonia.UiScale.Size(200),
            Height = DWSIM.UI.Shared.Avalonia.UiScale.Size(16),
            VerticalAlignment = VerticalAlignment.Center
        };

        var row2 = new StackPanel
        {
            Orientation = global::Avalonia.Layout.Orientation.Horizontal,
            Spacing = 4,
            Margin = new Thickness(0, 2)
        };
        row2.Children.Add(_btnPlay);
        row2.Children.Add(_btnRT);
        row2.Children.Add(_btnStop);
        row2.Children.Add(_lbStatus);
        row2.Children.Add(_pbProgress);
        Children.Add(row2);

        // Wire button events
        _btnStop.Click += (_, _) => Abort = true;

        _btnPlay.Click += async (_, _) => await RunAsync(realtime: false);

        _btnRT.Click += async (_, _) => await RunAsync(realtime: true);

        _btnViewResults.Click += (_, _) => OnViewResults?.Invoke();

        _cbSchedule.SelectionChanged += (_, _) =>
        {
            if (_flowsheet == null || _cbSchedule.SelectedIndex < 0) return;
            var schedules = _flowsheet.DynamicsManager.ScheduleList.Values.ToList();
            if (_cbSchedule.SelectedIndex < schedules.Count)
                _flowsheet.DynamicsManager.CurrentSchedule = schedules[_cbSchedule.SelectedIndex].ID;
        };
    }

    public void SetFlowsheet(IFlowsheet flowsheet)
    {
        _flowsheet = flowsheet;
        PopulateSchedules();
    }

    /// <summary>Refresh the schedule dropdown from the flowsheet's DynamicsManager.</summary>
    public void PopulateSchedules()
    {
        if (_flowsheet == null) return;

        var prevIndex = _cbSchedule.SelectedIndex;
        _cbSchedule.Items.Clear();
        foreach (var sch in _flowsheet.DynamicsManager.ScheduleList.Values)
        {
            string text = sch.Description;
            if (!string.IsNullOrEmpty(sch.CurrentIntegrator) &&
                _flowsheet.DynamicsManager.IntegratorList.ContainsKey(sch.CurrentIntegrator))
            {
                text += " (" + _flowsheet.DynamicsManager.IntegratorList[sch.CurrentIntegrator].Description + ")";
            }
            _cbSchedule.Items.Add(text);
        }
        if (_cbSchedule.Items.Count > 0)
            _cbSchedule.SelectedIndex = Math.Min(prevIndex < 0 ? 0 : prevIndex, _cbSchedule.Items.Count - 1);
    }

    /// <summary>Update progress bar and status label (called from integrator loop).</summary>
    public void UpdateProgress(int currentSeconds, TimeSpan duration)
    {
        Dispatcher.UIThread.Post(() =>
        {
            _pbProgress.Maximum = duration.TotalSeconds;
            _pbProgress.Value = currentSeconds;
            _lbStatus.Text = new TimeSpan(0, 0, currentSeconds).ToString(@"hh\:mm\:ss") + " / " + duration.ToString(@"hh\:mm\:ss");
        });
    }

    /// <summary>Reset controls after integration finishes.</summary>
    public void ResetControls()
    {
        Dispatcher.UIThread.Post(() =>
        {
            _btnPlay.IsEnabled = true;
            _btnRT.IsEnabled = true;
            _btnViewResults.IsEnabled = true;
            _pbProgress.Value = 0;
        });
    }

    /// <summary>Called after every integration step so the host can refresh the canvas.</summary>
    public Action? OnIntegratorStep { get; set; }

    /// <summary>
    /// Called by the View Results button. The host owns the spreadsheet, so it writes the
    /// monitored-variable history there.
    /// </summary>
    public Action? OnViewResults { get; set; }

    /// <summary>True while a run is in progress.</summary>
    public bool IsRunning { get; private set; }

    private volatile bool _refreshPending;

    /// <summary>
    /// Runs the current schedule on a background thread through
    /// <see cref="IntegratorRunner"/>, keeping the toolbar in sync.
    /// </summary>
    public async Task RunAsync(bool realtime)
    {
        if (_flowsheet == null || IsRunning) return;

        if (!_flowsheet.DynamicMode)
        {
            _flowsheet.ShowMessage("Dynamic Mode is inactive. Enable it from the Dynamics menu and try again.",
                IFlowsheet.MessageType.Warning);
            return;
        }

        var fs = _flowsheet;
        Abort = false;
        IsRunning = true;
        _btnPlay.IsEnabled = false;
        _btnRT.IsEnabled = false;
        _btnViewResults.IsEnabled = false;

        var options = new IntegratorRunOptions
        {
            RealTime = realtime,
            EnableHistorian = fs.DynamicsManager.EnableHistorian,
            AbortRequested = () => Abort,
            OnProgress = p => Dispatcher.UIThread.Post(() =>
            {
                // Real-time runs report an unbounded total; the bar tracks the step count instead.
                var total = double.IsInfinity(p.TotalSeconds) || p.TotalSeconds > int.MaxValue
                    ? p.CurrentSeconds
                    : p.TotalSeconds;
                _pbProgress.Maximum = Math.Max(1, total);
                _pbProgress.Value = Math.Min(p.CurrentSeconds, _pbProgress.Maximum);
                _lbStatus.Text = p.Status;
            }),
            // Throttled: the integrator can step far faster than the canvas can redraw, and an
            // unbounded queue of refresh posts starves the UI thread.
            OnStep = () =>
            {
                if (_refreshPending) return;
                _refreshPending = true;
                Dispatcher.UIThread.Post(() =>
                {
                    _refreshPending = false;
                    OnIntegratorStep?.Invoke();
                });
            }
        };

        List<Exception> exceptions;
        try
        {
            var result = await new IntegratorRunner(fs).RunAsync(options);
            exceptions = result.Exceptions.ToList();
        }
        catch (Exception ex)
        {
            exceptions = new List<Exception> { ex };
        }

        IsRunning = false;
        ResetControls();
        fs.UpdateOpenEditForms();
        OnIntegratorStep?.Invoke();

        if (exceptions.Count > 0)
        {
            foreach (var ex in exceptions.Take(5))
            {
                var baseex = ex;
                while (baseex.InnerException != null) baseex = baseex.InnerException;
                fs.ShowMessage("Integrator error: " + baseex.Message, IFlowsheet.MessageType.GeneralError);
            }
        }
        else
        {
            fs.ShowMessage(Abort ? "Integration aborted by the user." : "Integration finished.",
                IFlowsheet.MessageType.Information);
        }
    }
}
