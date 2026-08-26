using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using DWSIM.Automation.DynamicRunner;
using DWSIM.Automation.DynamicRunner.Setup;
using DWSIM.Interfaces;

namespace DWSIM.UI.Desktop.Avalonia;

/// <summary>
/// Walks a steady-state flowsheet through everything it needs before it can be integrated:
/// holdup, hydraulics, the boundaries of the pressure-flow network, the control loops that keep
/// it from running dry, and an integrator to run it.
/// </summary>
/// <remarks>
/// The wizard decides nothing on its own. Each step lists what the engine found wrong, with the
/// value it would use to fix it; the user ticks what to apply and can edit any of the numbers
/// first. Nothing is written to the flowsheet until "Apply" is clicked on that step, and applying
/// twice changes nothing the second time.
/// </remarks>
public partial class DynamicsWizard : Window
{

    /// <summary>One issue, as the grid shows it: a tick box, an editable value, and the text.</summary>
    private sealed class IssueRow : INotifyPropertyChanged
    {
        private bool _selected;
        private string _value;

        public IssueRow(DynamicsIssue issue)
        {
            Issue = issue;
            _selected = issue.CanAutoFix;
            _value = Format(issue.SuggestedValue);
        }

        public DynamicsIssue Issue { get; }

        public bool Selected
        {
            get => _selected;
            set { if (_selected == value) return; _selected = value; Raise(nameof(Selected)); }
        }

        /// <summary>The suggested value as text, so the user can overwrite it before applying.</summary>
        public string Value
        {
            get => _value;
            set { if (_value == value) return; _value = value; Raise(nameof(Value)); }
        }

        public bool CanApply => Issue.CanAutoFix;

        /// <summary>The severity as a glyph. The word itself goes in the tooltip.</summary>
        public string SeverityIcon
        {
            get
            {
                switch (Issue.Severity)
                {
                    case DynamicsIssueSeverity.Blocker: return "⛔";  // no entry
                    case DynamicsIssueSeverity.Warning: return "⚠";  // warning sign
                    default: return "ℹ";                             // information
                }
            }
        }

        /// <summary>What the glyph stands for, for the tooltip and for screen readers.</summary>
        public string SeverityName
        {
            get
            {
                switch (Issue.Severity)
                {
                    case DynamicsIssueSeverity.Blocker: return "Error: the run cannot proceed until this is settled";
                    case DynamicsIssueSeverity.Warning: return "Warning: the run will proceed, but the result may mislead";
                    default: return "Information";
                }
            }
        }

        public string ObjectTag => string.IsNullOrEmpty(Issue.ObjectTag) ? "(flowsheet)" : Issue.ObjectTag;
        public string Message => Issue.Message;
        public string Fix => Issue.Fix;
        public string ValueLabel => Issue.ValueLabel ?? "";

        /// <summary>
        /// The edited text back in the type the issue expects. Anything unparseable falls back to
        /// the suggestion rather than writing nonsense into the flowsheet.
        /// </summary>
        public object ResolvedValue()
        {
            var suggestion = Issue.SuggestedValue;
            if (suggestion == null) return null;

            try
            {
                if (suggestion is bool) return bool.Parse(_value);
                if (suggestion is Enum) return Enum.Parse(suggestion.GetType(), _value, ignoreCase: true);
                if (suggestion is double || suggestion is int || suggestion is float)
                    return Convert.ToDouble(_value, CultureInfo.CurrentCulture);
            }
            catch { }

            return suggestion;
        }

        private static string Format(object value)
        {
            if (value == null) return "";
            if (value is double d) return d.ToString("G6", CultureInfo.CurrentCulture);
            return value.ToString();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    private static readonly (string Title, string Description)[] Steps =
    {
        ("Introduction", "What this wizard sets up, and what it found in the flowsheet as it stands."),
        ("Holdup", "Vessels, tanks and reactors need a volume before they can accumulate anything. Without one the unit passes material straight through and the process shows no lag."),
        ("Hydraulics", "In dynamic mode the pressures decide the flow. A valve needs a flow coefficient and a working opening before it can resolve one."),
        ("Boundary Specs", "The edges of the pressure-flow network. A feed holds its flow and a product holds its pressure; the network resolves everything in between."),
        ("Control", "Loops that keep the process where you want it. A vessel with no level control fills or empties until the run fails."),
        ("Integrator", "How far to step, how long to run, and which variables to record. A run with no monitored variables finishes normally and leaves no results behind."),
        ("Summary", "What changed, what is still outstanding, and a short run to check that the flowsheet integrates.")
    };

    private static readonly DynamicsIssueCategory?[] PageCategories =
    {
        null,
        DynamicsIssueCategory.Holdup,
        DynamicsIssueCategory.Hydraulics,
        DynamicsIssueCategory.BoundarySpecs,
        DynamicsIssueCategory.Control,
        DynamicsIssueCategory.Integrator,
        null
    };

    private readonly IFlowsheet _flowsheet;
    private readonly DynamicsSetupOptions _options = new();

    private readonly List<Control> _pages = new();
    private readonly List<TextBlock> _stepLabels = new();

    private readonly Dictionary<DynamicsIssueCategory, ObservableCollection<IssueRow>> _rows = new();
    private readonly Dictionary<DynamicsIssueCategory, TextBlock> _emptyNotes = new();

    private readonly List<string> _applied = new();

    private IReadOnlyList<DynamicsIssue> _issues = Array.Empty<DynamicsIssue>();

    private TextBlock _scanSummary = null!;
    private TextBlock _unsupported = null!;
    private TextBlock _summaryText = null!;
    private TextBlock _runStatus = null!;
    private Button _btnRun = null!;
    private ProgressBar _runProgress = null!;

    private int _current;
    private bool _running;

    /// <summary>
    /// Raised after the wizard writes to the flowsheet. The Dynamics Manager lists are built once
    /// from the manager's dictionaries, so a schedule or integrator created here does not show up
    /// there until something asks it to repopulate.
    /// </summary>
    public Action OnApplied;

    // Parameterless ctor required by Avalonia's XAML compiler (designer-only).
    public DynamicsWizard() : this(null!) { }

    public DynamicsWizard(IFlowsheet flowsheet)
    {
        _flowsheet = flowsheet;

        InitializeComponent();
        IconHelper.ApplyWindowIcon(this);

        if (_flowsheet == null) return;

        BuildSteps();

        _pages.Add(BuildIntroduction());
        _pages.Add(BuildIssuePage(DynamicsIssueCategory.Holdup,
            "Give every holdup unit a volume. The suggestion is the flow it already carries, held for the residence time below."));
        _pages.Add(BuildIssuePage(DynamicsIssueCategory.Hydraulics,
            "Size the valves and put them in a mode that resolves flow from pressure."));
        _pages.Add(BuildIssuePage(DynamicsIssueCategory.BoundarySpecs,
            "Feeds hold their flow, products hold their pressure. Interior streams are left to the network."));
        _pages.Add(BuildIssuePage(DynamicsIssueCategory.Control,
            "Loops the wizard would add, and problems with the loops already there."));
        _pages.Add(BuildIssuePage(DynamicsIssueCategory.Integrator,
            "The integrator and schedule that will run this flowsheet, and what they record."));
        _pages.Add(BuildSummary());

        BtnBack.Click += (_, _) => Show(_current - 1);
        BtnNext.Click += (_, _) => Show(_current + 1);
        BtnFinish.Click += (_, _) => Close();
        BtnCancel.Click += (_, _) => Close();

        Rescan();
        Show(0);

        Opened += (_, _) => Show(0);
    }

    // -------------------------------------------------------------------------
    // Steps and navigation
    // -------------------------------------------------------------------------

    private void BuildSteps()
    {
        for (int i = 0; i < Steps.Length; i++)
        {
            var label = new TextBlock
            {
                Text = (i + 1) + ". " + Steps[i].Title,
                FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(12),
                TextWrapping = TextWrapping.Wrap
            };

            var button = new Button
            {
                Content = label,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(2, 3),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left
            };

            var step = i;
            button.Click += (_, _) => Show(step);

            _stepLabels.Add(label);
            StepList.Children.Add(button);
        }
    }

    private void Show(int page)
    {
        if (page < 0 || page >= _pages.Count) return;

        _current = page;

        PageHost.Content = _pages[page];

        LblHeaderTitle.Text = "Step " + (page + 1) + " of " + _pages.Count + " - " + Steps[page].Title;
        LblHeaderDesc.Text = Steps[page].Description;

        var blockers = _issues.Count(i => i.Severity == DynamicsIssueSeverity.Blocker);
        LblFooter.Text = blockers > 0
            ? blockers + " blocker" + (blockers == 1 ? "" : "s") + " left; the run will fail until they are settled."
            : "Nothing is written until you click Apply on a step.";

        BtnBack.IsEnabled = page > 0;
        BtnNext.IsEnabled = page < _pages.Count - 1;
        BtnFinish.IsVisible = page == _pages.Count - 1;

        for (int i = 0; i < _stepLabels.Count; i++)
        {
            _stepLabels[i].FontWeight = i == page ? FontWeight.Bold : FontWeight.Normal;
            _stepLabels[i].Opacity = i == page ? 1.0 : 0.7;
        }

        if (page == _pages.Count - 1) RefreshSummary();
    }

    private static Border Group(string title, Control content)
    {
        var stack = new StackPanel { Spacing = 4 };

        stack.Children.Add(new TextBlock
        {
            Text = title,
            FontWeight = FontWeight.SemiBold,
            Margin = new Thickness(0, 0, 0, 4)
        });

        stack.Children.Add(content);

        return new Border
        {
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.FromArgb(60, 128, 128, 128)),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(10),
            Margin = new Thickness(0, 0, 0, 8),
            Child = stack
        };
    }

    private static TextBlock Note(string text)
    {
        return new TextBlock
        {
            Text = text,
            FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(11),
            Opacity = 0.75,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 2)
        };
    }

    // -------------------------------------------------------------------------
    // Scanning
    // -------------------------------------------------------------------------

    /// <summary>
    /// Asks the engine what is missing, and refills every page from the answer. Called on entry
    /// and after each apply, so a fix that resolves several issues at once clears them all.
    /// </summary>
    private void Rescan()
    {
        try
        {
            _issues = DynamicsSetupPlan.Propose(_flowsheet, _options);
        }
        catch (Exception ex)
        {
            _issues = Array.Empty<DynamicsIssue>();
            _flowsheet.ShowMessage("Dynamics Wizard could not scan the flowsheet: " + ex.Message,
                IFlowsheet.MessageType.GeneralError);
        }

        foreach (var category in _rows.Keys.ToList())
        {
            var list = _rows[category];
            list.Clear();
            foreach (var issue in _issues.Where(i => i.Category == category))
            {
                list.Add(new IssueRow(issue));
            }

            if (_emptyNotes.TryGetValue(category, out var note))
            {
                note.IsVisible = list.Count == 0;
            }
        }

        RefreshScanSummary();
    }

    private void RefreshScanSummary()
    {
        if (_scanSummary == null) return;

        var blockers = _issues.Count(i => i.Severity == DynamicsIssueSeverity.Blocker);
        var warnings = _issues.Count(i => i.Severity == DynamicsIssueSeverity.Warning);

        _scanSummary.Text = _issues.Count == 0
            ? "Nothing to change: this flowsheet is ready to run dynamically."
            : blockers + " blocker" + (blockers == 1 ? "" : "s") + ", " +
              warnings + " warning" + (warnings == 1 ? "" : "s") +
              ". The steps on the left cover them in the order they matter.";

        var unsupported = _issues
            .Where(i => i.Code == "UNSUPPORTED_OBJECT")
            .Select(i => i.ObjectTag)
            .ToList();

        _unsupported.Text = unsupported.Count == 0
            ? "Every object in this flowsheet has a dynamic model."
            : "Solved at steady state on every step, because they have no dynamic model: " +
              string.Join(", ", unsupported) + ".";
    }

    // -------------------------------------------------------------------------
    // 1. Introduction
    // -------------------------------------------------------------------------

    private Control BuildIntroduction()
    {
        var stack = new StackPanel { Spacing = 6 };

        stack.Children.Add(new TextBlock
        {
            Text = "A steady-state solution tells you where the process settles, but not how it gets " +
                   "there. Integrating over time needs values the steady state never had to supply: how " +
                   "much each vessel holds, how the valves resolve flow from pressure, where the " +
                   "network is pinned down, and what keeps the levels in range.",
            TextWrapping = TextWrapping.Wrap
        });

        _scanSummary = new TextBlock { TextWrapping = TextWrapping.Wrap, FontWeight = FontWeight.SemiBold };
        _unsupported = new TextBlock { TextWrapping = TextWrapping.Wrap, Opacity = 0.85 };

        var scan = new StackPanel { Spacing = 6 };
        scan.Children.Add(_scanSummary);
        scan.Children.Add(_unsupported);

        var rescan = new Button { Content = "Scan again", Width = DWSIM.UI.Shared.Avalonia.UiScale.Size(130) };
        rescan.Classes.Add("action");
        rescan.Click += (_, _) => Rescan();
        scan.Children.Add(rescan);

        stack.Children.Add(Group("What the flowsheet looks like now", scan));

        var defaults = new StackPanel { Spacing = 6 };
        defaults.Children.Add(Note("These set the numbers the later steps suggest. Change them here and the " +
                                   "suggestions follow; you can still override any of them row by row."));

        defaults.Children.Add(LabelledBox("Target residence time (minutes)",
            (_options.TargetResidenceTimeSeconds / 60.0).ToString("G4", CultureInfo.CurrentCulture),
            text =>
            {
                if (double.TryParse(text, NumberStyles.Any, CultureInfo.CurrentCulture, out var minutes) && minutes > 0)
                {
                    _options.TargetResidenceTimeSeconds = minutes * 60.0;
                    Rescan();
                }
            }));

        defaults.Children.Add(LabelledBox("Design valve opening (%)",
            _options.DesignValveOpeningPct.ToString("G4", CultureInfo.CurrentCulture),
            text =>
            {
                if (double.TryParse(text, NumberStyles.Any, CultureInfo.CurrentCulture, out var pct) && pct > 0 && pct <= 100)
                {
                    _options.DesignValveOpeningPct = pct;
                    Rescan();
                }
            }));

        defaults.Children.Add(LabelledBox("Integration step (seconds)",
            _options.IntegrationStep.TotalSeconds.ToString("G4", CultureInfo.CurrentCulture),
            text =>
            {
                if (double.TryParse(text, NumberStyles.Any, CultureInfo.CurrentCulture, out var seconds) && seconds > 0)
                {
                    _options.IntegrationStep = TimeSpan.FromSeconds(seconds);
                    Rescan();
                }
            }));

        defaults.Children.Add(LabelledBox("Run duration (minutes)",
            _options.Duration.TotalMinutes.ToString("G4", CultureInfo.CurrentCulture),
            text =>
            {
                if (double.TryParse(text, NumberStyles.Any, CultureInfo.CurrentCulture, out var minutes) && minutes > 0)
                {
                    _options.Duration = TimeSpan.FromMinutes(minutes);
                    Rescan();
                }
            }));

        stack.Children.Add(Group("Defaults the suggestions are built from", defaults));

        stack.Children.Add(Note("The flowsheet should be solved at steady state before you start: the " +
                                "suggested volumes and flow coefficients are read off the converged " +
                                "operating point, and a dynamic run integrates forward from it."));

        return new ScrollViewer { Content = stack };
    }

    private static Control LabelledBox(string label, string initial, Action<string> onChanged)
    {
        var panel = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 2) };

        var text = new TextBlock
        {
            Text = label,
            Width = DWSIM.UI.Shared.Avalonia.UiScale.Size(220),
            VerticalAlignment = VerticalAlignment.Center
        };
        DockPanel.SetDock(text, global::Avalonia.Controls.Dock.Left);
        panel.Children.Add(text);

        var box = new TextBox { Text = initial, Width = DWSIM.UI.Shared.Avalonia.UiScale.Size(120) };
        box.LostFocus += (_, _) => onChanged(box.Text ?? "");
        panel.Children.Add(box);

        return panel;
    }

    // -------------------------------------------------------------------------
    // 2-6. The issue pages
    // -------------------------------------------------------------------------

    /// <summary>
    /// One page of findings: a grid of what is wrong, what would be written, and a button to
    /// write the ticked ones.
    /// </summary>
    private Control BuildIssuePage(DynamicsIssueCategory category, string intro)
    {
        var rows = new ObservableCollection<IssueRow>();
        _rows[category] = rows;

        var host = new DockPanel { LastChildFill = true };

        var header = new StackPanel { Spacing = 4, Margin = new Thickness(0, 0, 0, 8) };
        header.Children.Add(new TextBlock { Text = intro, TextWrapping = TextWrapping.Wrap });

        var empty = new TextBlock
        {
            Text = "Nothing to change on this step.",
            FontWeight = FontWeight.SemiBold,
            Opacity = 0.8,
            Margin = new Thickness(0, 4)
        };
        _emptyNotes[category] = empty;
        header.Children.Add(empty);

        DockPanel.SetDock(header, global::Avalonia.Controls.Dock.Top);
        host.Children.Add(header);

        var footer = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 8, 0, 0)
        };

        var apply = new Button { Content = "Apply selected" };
        apply.Classes.Add("action");
        apply.Click += (_, _) => ApplySelected(category);
        footer.Children.Add(apply);

        var selectAll = new Button { Content = "Select all" };
        selectAll.Click += (_, _) => { foreach (var r in rows.Where(r => r.CanApply)) r.Selected = true; };
        footer.Children.Add(selectAll);

        var selectNone = new Button { Content = "Select none" };
        selectNone.Click += (_, _) => { foreach (var r in rows) r.Selected = false; };
        footer.Children.Add(selectNone);

        DockPanel.SetDock(footer, global::Avalonia.Controls.Dock.Bottom);
        host.Children.Add(footer);

        host.Children.Add(BuildIssueList(rows));

        return host;
    }

    // Column widths shared by the header and every row, so the two line up. The message takes
    // whatever is left over.
    private static ColumnDefinitions IssueColumns()
    {
        return new ColumnDefinitions("56,40,150,*,150,120");
    }

    /// <summary>
    /// The findings of one step, as a list rather than a DataGrid: these rows carry sentences, and
    /// a DataGrid row will not grow past one line to fit them.
    /// </summary>
    private static Control BuildIssueList(ObservableCollection<IssueRow> rows)
    {
        var host = new DockPanel { LastChildFill = true };

        var header = new Grid
        {
            ColumnDefinitions = IssueColumns(),
            Margin = new Thickness(0, 0, 0, 2)
        };

        AddHeaderCell(header, 0, "Apply");
        AddHeaderCell(header, 1, "");
        AddHeaderCell(header, 2, "Object");
        AddHeaderCell(header, 3, "What is wrong");
        AddHeaderCell(header, 4, "Setting");
        AddHeaderCell(header, 5, "Value");

        var headerBorder = new Border
        {
            BorderThickness = new Thickness(0, 0, 0, 1),
            BorderBrush = new SolidColorBrush(Color.FromArgb(90, 128, 128, 128)),
            Padding = new Thickness(0, 0, 0, 4),
            Child = header
        };
        DockPanel.SetDock(headerBorder, global::Avalonia.Controls.Dock.Top);
        host.Children.Add(headerBorder);

        var list = new ItemsControl
        {
            ItemsSource = rows,
            ItemTemplate = new FuncDataTemplate<IssueRow>((row, _) =>
            {
                if (row == null) return new TextBlock();

                var grid = new Grid { ColumnDefinitions = IssueColumns(), Margin = new Thickness(0, 6) };

                var check = new CheckBox
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Top,
                    IsEnabled = row.CanApply
                };
                check.Bind(CheckBox.IsCheckedProperty, new Binding("Selected") { Mode = BindingMode.TwoWay });
                Grid.SetColumn(check, 0);
                grid.Children.Add(check);

                var glyph = new TextBlock
                {
                    Text = row.SeverityIcon,
                    FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(14),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Top
                };
                ToolTip.SetTip(glyph, row.SeverityName);
                Grid.SetColumn(glyph, 1);
                grid.Children.Add(glyph);

                var tag = new TextBlock
                {
                    Text = row.ObjectTag,
                    TextWrapping = TextWrapping.Wrap,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(0, 0, 8, 0)
                };
                Grid.SetColumn(tag, 2);
                grid.Children.Add(tag);

                // The message and the fix, stacked: both are sentences, and hiding the fix behind a
                // selection meant the advice was never read.
                var text = new StackPanel { Spacing = 2, Margin = new Thickness(0, 0, 8, 0) };
                text.Children.Add(new TextBlock { Text = row.Message, TextWrapping = TextWrapping.Wrap });
                if (!string.IsNullOrEmpty(row.Fix))
                {
                    text.Children.Add(new TextBlock
                    {
                        Text = row.Fix,
                        TextWrapping = TextWrapping.Wrap,
                        FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(11),
                        Opacity = 0.75
                    });
                }
                Grid.SetColumn(text, 3);
                grid.Children.Add(text);

                var setting = new TextBlock
                {
                    Text = row.ValueLabel,
                    TextWrapping = TextWrapping.Wrap,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(0, 0, 8, 0)
                };
                Grid.SetColumn(setting, 4);
                grid.Children.Add(setting);

                if (row.CanApply)
                {
                    var box = new TextBox { VerticalAlignment = VerticalAlignment.Top };
                    box.Bind(TextBox.TextProperty, new Binding("Value") { Mode = BindingMode.TwoWay });
                    Grid.SetColumn(box, 5);
                    grid.Children.Add(box);
                }

                return new Border
                {
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(40, 128, 128, 128)),
                    Child = grid
                };
            }, supportsRecycling: false)
        };

        host.Children.Add(new ScrollViewer
        {
            Content = list,
            HorizontalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Auto
        });

        return host;
    }

    private static void AddHeaderCell(Grid grid, int column, string text)
    {
        var block = new TextBlock
        {
            Text = text,
            FontWeight = FontWeight.SemiBold,
            Opacity = 0.8,
            TextWrapping = TextWrapping.Wrap
        };
        Grid.SetColumn(block, column);
        grid.Children.Add(block);
    }

    /// <summary>Writes the ticked rows of one step, then rescans so the pages reflect the result.</summary>
    private void ApplySelected(DynamicsIssueCategory category)
    {
        if (!_rows.TryGetValue(category, out var rows)) return;

        var chosen = rows.Where(r => r.Selected && r.CanApply).ToList();
        if (chosen.Count == 0)
        {
            _flowsheet.ShowMessage("Nothing selected to apply on this step.", IFlowsheet.MessageType.Information);
            return;
        }

        var done = 0;
        var failed = new List<string>();

        foreach (var row in chosen)
        {
            try
            {
                if (DynamicsSetupPlan.Apply(row.Issue, row.ResolvedValue()))
                {
                    done++;
                    _applied.Add(row.ObjectTag + ": " + row.Issue.Code +
                                 (string.IsNullOrEmpty(row.ValueLabel) ? "" : " (" + row.ValueLabel + " = " + row.Value + ")"));
                }
            }
            catch (Exception ex)
            {
                failed.Add(row.ObjectTag + ": " + ex.Message);
            }
        }

        _flowsheet.UpdateInterface();
        OnApplied?.Invoke();

        foreach (var failure in failed.Take(5))
        {
            _flowsheet.ShowMessage("Dynamics Wizard could not apply " + failure, IFlowsheet.MessageType.GeneralError);
        }

        if (done > 0)
        {
            _flowsheet.ShowMessage("Dynamics Wizard applied " + done + " change" + (done == 1 ? "" : "s") + ".",
                IFlowsheet.MessageType.Information);
        }

        Rescan();
    }

    // -------------------------------------------------------------------------
    // 7. Summary and test run
    // -------------------------------------------------------------------------

    private Control BuildSummary()
    {
        var stack = new StackPanel { Spacing = 6 };

        _summaryText = new TextBlock { TextWrapping = TextWrapping.Wrap };
        stack.Children.Add(Group("What changed", _summaryText));

        var run = new StackPanel { Spacing = 6 };

        run.Children.Add(new TextBlock
        {
            Text = "This turns dynamic mode on, integrates for a few steps, and reports whatever the " +
                   "solver says.",
            TextWrapping = TextWrapping.Wrap
        });

        _btnRun = new Button { Content = "Run a short test", Width = DWSIM.UI.Shared.Avalonia.UiScale.Size(170) };
        _btnRun.Classes.Add("action");
        _btnRun.Click += async (_, _) => await RunTestAsync();
        run.Children.Add(_btnRun);

        _runProgress = new ProgressBar { Minimum = 0, Maximum = 1, Value = 0, Height = DWSIM.UI.Shared.Avalonia.UiScale.Size(6) };
        run.Children.Add(_runProgress);

        _runStatus = new TextBlock { TextWrapping = TextWrapping.Wrap, Opacity = 0.85 };
        run.Children.Add(_runStatus);

        stack.Children.Add(Group("Test run", run));

        stack.Children.Add(Note("The integrator controls and the Dynamics Manager, both on the Dynamics menu, " +
                                "handle the rest: events, schedules and the full run."));

        return new ScrollViewer { Content = stack };
    }

    private void RefreshSummary()
    {
        if (_summaryText == null) return;

        var outstanding = _issues.Count(i => i.Severity == DynamicsIssueSeverity.Blocker);

        var parts = new List<string>();

        parts.Add(_applied.Count == 0
            ? "Nothing has been applied yet."
            : _applied.Count + " change" + (_applied.Count == 1 ? "" : "s") + " applied:\n  " +
              string.Join("\n  ", _applied.Distinct().Take(40)));

        parts.Add(outstanding == 0
            ? "No blockers remain. The flowsheet is ready to integrate."
            : outstanding + " blocker" + (outstanding == 1 ? "" : "s") +
              " still outstanding; the run will fail until they are settled.");

        _summaryText.Text = string.Join("\n\n", parts);
    }

    /// <summary>
    /// Integrates a handful of steps, bounded in both steps and wall time, so a flowsheet that
    /// cannot move says so in seconds rather than hanging the wizard.
    /// </summary>
    private async Task RunTestAsync()
    {
        if (_running) return;

        var blockers = _issues.Where(i => i.Severity == DynamicsIssueSeverity.Blocker).ToList();
        if (blockers.Count > 0)
        {
            _runStatus.Text = "Settle the blockers first: " +
                              string.Join("; ", blockers.Take(3).Select(b =>
                                  (string.IsNullOrEmpty(b.ObjectTag) ? "" : b.ObjectTag + " - ") + b.Message));
            return;
        }

        _running = true;
        _btnRun.IsEnabled = false;
        _runStatus.Text = "Running...";

        var wasDynamic = _flowsheet.DynamicMode;
        _flowsheet.DynamicMode = true;

        var options = new IntegratorRunOptions
        {
            RealTime = false,
            EnableHistorian = _flowsheet.DynamicsManager.EnableHistorian,
            MaxSteps = 20,
            MaxWallTime = TimeSpan.FromSeconds(60),
            OnProgress = p => Dispatcher.UIThread.Post(() =>
            {
                var total = double.IsInfinity(p.TotalSeconds) || p.TotalSeconds > int.MaxValue
                    ? p.CurrentSeconds
                    : p.TotalSeconds;
                _runProgress.Maximum = Math.Max(1, total);
                _runProgress.Value = Math.Min(p.CurrentSeconds, _runProgress.Maximum);
            })
        };

        try
        {
            var result = await new IntegratorRunner(_flowsheet).RunAsync(options);

            if (result.Exceptions.Any())
            {
                var first = result.Exceptions.First();
                while (first.InnerException != null) first = first.InnerException;
                _runStatus.Text = "The run stopped after " + result.Steps + " step" +
                                  (result.Steps == 1 ? "" : "s") + ": " + first.Message;
            }
            else
            {
                _runStatus.Text = "Ran " + result.Steps + " step" + (result.Steps == 1 ? "" : "s") +
                                  " to t = " + result.FinalTimeSeconds.ToString("G4", CultureInfo.CurrentCulture) +
                                  " s without error. The flowsheet integrates.";
            }
        }
        catch (IntegratorBusyException)
        {
            _runStatus.Text = "Another integration is already running. Stop it from the integrator controls and try again.";
        }
        catch (Exception ex)
        {
            _runStatus.Text = "The run could not start: " + ex.Message;
        }
        finally
        {
            _flowsheet.DynamicMode = wasDynamic;
            _running = false;
            _btnRun.IsEnabled = true;
            _flowsheet.UpdateInterface();
        }
    }
}
