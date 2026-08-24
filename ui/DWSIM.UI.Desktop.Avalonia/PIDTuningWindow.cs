using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using DotNumerics.Optimization;
using DWSIM.Automation.DynamicRunner;
using DWSIM.Interfaces;
using DWSIM.UnitOperations.SpecialOps;

namespace DWSIM.UI.Desktop.Avalonia;

/// <summary>
/// PID Controller Tuning. Avalonia counterpart of
/// DWSIM.UI.Desktop.Editors.Dynamics.PIDTuningTool: minimizes the summed absolute cumulative
/// error of the selected controllers with a Nelder-Mead simplex over their Kp/Ki/Kd, running
/// the schedule through <see cref="IntegratorRunner"/> once per function evaluation.
/// </summary>
public sealed class PIDTuningWindow : Window
{
    private readonly IFlowsheet _flowsheet;

    private readonly ComboBox _cbSchedule = new() { Width = 260 };
    private readonly StackPanel _controllerList = new() { Spacing = 2 };
    private readonly NumericUpDown _nudIterations = new()
    {
        Minimum = 5, Maximum = 500, Value = 30, FormatString = "F0", Width = 120
    };
    private readonly TextBox _results = new()
    {
        IsReadOnly = true,
        AcceptsReturn = true,
        TextWrapping = TextWrapping.NoWrap,
        FontFamily = new FontFamily("Consolas,Courier New,monospace"),
        FontSize = 12,
        VerticalContentAlignment = VerticalAlignment.Top
    };

    private readonly Button _btnRun = new() { Content = "Begin Tuning", HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly Button _btnCancel = new() { Content = "Cancel", HorizontalAlignment = HorizontalAlignment.Stretch, IsEnabled = false };

    private readonly List<CheckBox> _checkBoxes = new();
    private readonly StringBuilder _log = new();

    private bool _abort;
    private bool _running;

    public PIDTuningWindow(IFlowsheet flowsheet)
    {
        _flowsheet = flowsheet;
        Title = "PID Controller Tuning";
        Width = 900;
        Height = 600;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        IconHelper.ApplyWindowIcon(this);

        _btnRun.Classes.Add("action");
        _btnCancel.Classes.Add("panel");

        Content = BuildContent();
        Populate();

        _btnRun.Click += async (_, _) => await RunAsync();
        _btnCancel.Click += (_, _) => { _abort = true; Append("Abort requested..."); };
    }

    private Control BuildContent()
    {
        var left = new StackPanel { Spacing = 6, Width = 320, Margin = new Thickness(8) };

        left.Children.Add(new TextBlock { Text = "Schedule", FontWeight = FontWeight.SemiBold });
        left.Children.Add(_cbSchedule);
        left.Children.Add(new TextBlock
        {
            Text = "The schedule must have a stored initial state; tuning restores it before every trial run.",
            FontSize = 11, Opacity = 0.8, TextWrapping = TextWrapping.Wrap
        });

        left.Children.Add(new TextBlock { Text = "Controllers", FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 8, 0, 0) });
        left.Children.Add(new TextBlock
        {
            Text = "Select the PID controllers to tune.", FontSize = 11, Opacity = 0.8
        });
        left.Children.Add(new Border
        {
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.FromArgb(60, 128, 128, 128)),
            Height = 200,
            Child = new ScrollViewer { Content = _controllerList, Padding = new Thickness(4) }
        });

        left.Children.Add(new TextBlock { Text = "Maximum Iterations:", Margin = new Thickness(0, 8, 0, 0) });
        left.Children.Add(_nudIterations);

        left.Children.Add(_btnRun);
        left.Children.Add(_btnCancel);

        var right = new DockPanel { Margin = new Thickness(0, 8, 8, 8) };
        var header = new TextBlock { Text = "Tuning Log", FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 0, 0, 4) };
        DockPanel.SetDock(header, global::Avalonia.Controls.Dock.Top);
        right.Children.Add(header);
        right.Children.Add(_results);

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        Grid.SetColumn(left, 0);
        Grid.SetColumn(right, 1);
        grid.Children.Add(left);
        grid.Children.Add(right);
        return grid;
    }

    private void Populate()
    {
        foreach (var sch in _flowsheet.DynamicsManager.ScheduleList.Values)
            _cbSchedule.Items.Add(string.IsNullOrEmpty(sch.Description) ? sch.ID : sch.Description);
        if (_cbSchedule.Items.Count > 0) _cbSchedule.SelectedIndex = 0;

        foreach (var obj in _flowsheet.SimulationObjects.Values.OfType<PIDController>()
                     .OrderBy(x => x.GraphicObject?.Tag))
        {
            var cb = new CheckBox { Content = obj.GraphicObject?.Tag ?? obj.Name, Tag = obj.Name };
            _checkBoxes.Add(cb);
            _controllerList.Children.Add(cb);
        }
        if (_checkBoxes.Count == 0)
            _controllerList.Children.Add(new TextBlock { Text = "No PID controllers on this flowsheet.", FontSize = 11, Opacity = 0.8 });
    }

    // -------------------------------------------------------------------------

    private async Task RunAsync()
    {
        if (_running) return;

        if (!_flowsheet.DynamicMode)
        { Append("Error: Dynamic Mode is not activated. Activate it and try again."); return; }

        var schedules = _flowsheet.DynamicsManager.ScheduleList.Values.ToList();
        if (_cbSchedule.SelectedIndex < 0 || _cbSchedule.SelectedIndex >= schedules.Count)
        { Append("Select a schedule first."); return; }

        var schedule = schedules[_cbSchedule.SelectedIndex];
        _flowsheet.DynamicsManager.CurrentSchedule = schedule.ID;

        if (string.IsNullOrEmpty(schedule.InitialFlowsheetStateID) || schedule.UseCurrentStateAsInitial)
        { Append("The selected schedule must have a valid initial state to start from."); return; }

        var controllers = _checkBoxes
            .Where(cb => cb.IsChecked.GetValueOrDefault())
            .Select(cb => (PIDController)_flowsheet.SimulationObjects[(string)cb.Tag!])
            .ToList();

        if (controllers.Count == 0) { Append("Select at least one controller to tune."); return; }

        // Three decision variables per controller, bounded the same way the Classic tool does.
        var vars = new List<OptSimplexBoundVariable>();
        foreach (var c in controllers)
        {
            vars.Add(new OptSimplexBoundVariable(c.Kp, 0.0, c.Kp * 10));
            vars.Add(new OptSimplexBoundVariable(c.Ki, 0.0, 100.0));
            vars.Add(new OptSimplexBoundVariable(c.Kd, 0.0, 100.0));
        }

        _log.Clear();
        _running = true;
        _abort = false;
        _btnRun.IsEnabled = false;
        _btnCancel.IsEnabled = true;

        Append($"Tuning {controllers.Count} controller(s) on schedule '{schedule.Description}'.");
        Append($"Simplex, up to {(int)(_nudIterations.Value ?? 30)} function evaluations.");
        Append("");

        var maxIts = (int)(_nudIterations.Value ?? 30);
        double[] result;

        try
        {
            result = await Task.Run(() =>
            {
                var simplex = new Simplex { MaxFunEvaluations = maxIts };
                int counter = 1;

                return simplex.ComputeMin(x =>
                {
                    if (_abort) return 0.0;

                    AppendFromWorker($"Iteration #{counter}:");

                    IntegratorRunner.RestoreState(_flowsheet, schedule.InitialFlowsheetStateID);

                    var k = 0;
                    foreach (var controller in controllers)
                    {
                        controller.Kp = x[k];
                        controller.Ki = x[k + 1];
                        controller.Kd = x[k + 2];
                        AppendFromWorker($"  {controller.GraphicObject?.Tag}: Kp = {controller.Kp:G6}, Ki = {controller.Ki:G6}, Kd = {controller.Kd:G6}");
                        k += 3;
                    }

                    new IntegratorRunner(_flowsheet).Run(new IntegratorRunOptions
                    {
                        Schedule = schedule.ID,
                        RealTime = false,
                        // The state was just restored above; do not restore it again.
                        RestoreInitialState = false,
                        // Hot path: the objective runs the whole schedule once per evaluation, and a
                        // snapshot plus compression per step would dominate the cost. Nothing here
                        // needs the historian — event transitions are not being tuned.
                        EnableHistorian = false,
                        AbortRequested = () => _abort
                    });

                    var totalError = controllers.Sum(c => Math.Abs(c.CumulativeError));
                    AppendFromWorker($"  total error = {totalError:G8}");
                    counter += 1;
                    return totalError;
                }, vars.ToArray());
            });
        }
        catch (Exception ex)
        {
            var baseex = ex;
            while (baseex.InnerException != null) baseex = baseex.InnerException;
            Append("Tuning failed: " + baseex.Message);
            _running = false;
            _btnRun.IsEnabled = true;
            _btnCancel.IsEnabled = false;
            return;
        }

        Append("");
        Append(_abort ? "Tuning aborted by the user. Results:" : "Tuning finished successfully. Results:");

        var j = 0;
        foreach (var controller in controllers)
        {
            controller.Kp = result[j];
            controller.Ki = result[j + 1];
            controller.Kd = result[j + 2];
            Append($"  {controller.GraphicObject?.Tag}: Kp = {controller.Kp:G6}, Ki = {controller.Ki:G6}, Kd = {controller.Kd:G6}");
            j += 3;
        }

        _flowsheet.UpdateInterface();
        _running = false;
        _btnRun.IsEnabled = true;
        _btnCancel.IsEnabled = false;
    }

    // -------------------------------------------------------------------------

    private void Append(string line)
    {
        lock (_log) { _log.AppendLine(line); _results.Text = _log.ToString(); }
    }

    /// <summary>
    /// Appends to the log from the tuning thread. The posted action re-reads the buffer rather
    /// than carrying a snapshot: a snapshot queued by the worker would otherwise land after the
    /// final results were written and roll the text box back.
    /// </summary>
    private void AppendFromWorker(string line)
    {
        lock (_log) _log.AppendLine(line);
        Dispatcher.UIThread.Post(() => { lock (_log) _results.Text = _log.ToString(); });
    }
}
