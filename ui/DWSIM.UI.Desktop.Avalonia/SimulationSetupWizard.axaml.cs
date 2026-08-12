using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using DWSIM.Interfaces;
using DWSIM.Interfaces.Enums;
using DWSIM.Thermodynamics.PropertyPackages;
using DWSIM.UI.Shared.Avalonia;

namespace DWSIM.UI.Desktop.Avalonia;

/// <summary>
/// The setup wizard, with the steps the Windows wizard walks through: the compounds, the
/// reactions between them, the property packages, the system of units, how the flowsheet
/// behaves and whether undo and redo are kept. Everything it changes is applied to the
/// simulation as it is changed, so closing the wizard at any step leaves the work done.
/// </summary>
public partial class SimulationSetupWizard : Window
{

    private sealed class CompoundRow : INotifyPropertyChanged
    {
        private bool _added;

        public CompoundRow(ICompoundConstantProperties compound, bool added)
        {
            Compound = compound;
            _added = added;
        }

        public ICompoundConstantProperties Compound { get; }

        public Action<CompoundRow, bool>? AddedChanged;

        public bool Added
        {
            get { return _added; }
            set
            {
                if (_added == value) return;
                _added = value;
                Raise(nameof(Added));
                AddedChanged?.Invoke(this, value);
            }
        }

        public string Name => Compound.Name;
        public string CAS => Compound.CAS_Number ?? "";
        public string Formula => Compound.Formula ?? "";
        public string Database => Compound.CurrentDB ?? "";

        public event PropertyChangedEventHandler? PropertyChanged;
        private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    private sealed class PropPackRow
    {
        public PropPackRow(PropertyPackage package) { Package = package; }

        public PropertyPackage Package { get; }

        public string Name => Package.Tag;
        public string Type => Package.ComponentName;
    }

    private static readonly string[] NumberFormats =
        { "F", "G", "G2", "G4", "G6", "G8", "G10", "N", "N2", "N4", "N6", "R", "E", "E1", "E2", "E3", "E4", "E6" };

    private static readonly (string Title, string Description)[] Steps =
    {
        ("Introduction", "What this wizard sets up."),
        ("Compounds", "Pick the compounds the simulation works with. Use the search box to find them by name, CAS number, formula or database."),
        ("Reactions", "Set up the reactions between the compounds you added. A simulation without reactors does not need any."),
        ("Property Packages", "Add the thermodynamic models the simulation calculates with. The first one on the list is the default for new objects."),
        ("System of Units", "Choose the units the simulation is shown in. You can create your own set from any of the built-in ones."),
        ("Behavior", "How the flowsheet reacts while you work on it."),
        ("Undo/Redo", "Whether every change is recorded so it can be undone."),
        ("Details", "The name of the simulation and how numbers are written.")
    };

    private readonly IFlowsheet _flowsheet;

    private readonly List<Control> _pages = new();
    private readonly List<TextBlock> _stepLabels = new();

    private readonly ObservableCollection<CompoundRow> _compoundRows = new();
    private readonly List<CompoundRow> _allCompoundRows = new();
    private DataGrid? _compoundGrid;
    private readonly ObservableCollection<PropPackRow> _ppRows = new();

    private TextBlock _compoundCount = null!;
    private ComboBox _availablePP = null!;
    private ComboBox _unitSystems = null!;
    private Grid _unitsGrid = null!;
    private TextBox _simulationName = null!;

    private int _current;
    private bool _loading = true;

    // Parameterless ctor required by Avalonia's XAML compiler (designer-only).
    public SimulationSetupWizard() : this(null!) { }

    public SimulationSetupWizard(IFlowsheet flowsheet)
    {
        _flowsheet = flowsheet;

        InitializeComponent();
        IconHelper.ApplyWindowIcon(this);

        if (_flowsheet == null) return;

        BuildSteps();

        _pages.Add(BuildIntroduction());
        _pages.Add(BuildCompounds());
        _pages.Add(BuildReactions());
        _pages.Add(BuildPropertyPackages());
        _pages.Add(BuildUnits());
        _pages.Add(BuildBehavior());
        _pages.Add(BuildUndoRedo());
        _pages.Add(BuildDetails());

        BtnBack.Click += (_, _) => Show(_current - 1);
        BtnNext.Click += (_, _) => Show(_current + 1);
        BtnFinish.Click += (_, _) => Close();
        BtnCancel.Click += (_, _) => Close();

        _loading = false;

        Show(0);

        // whatever happens while the pages are built, the wizard starts at the beginning
        Opened += (_, _) => Show(0);
    }

    private IFlowsheetOptions Options => _flowsheet.FlowsheetOptions;

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
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap
            };

            // the steps are buttons so a step is only reached on a click, never on a stray press
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

        LblFooter.Text = page == _pages.Count - 1
            ? "Everything is already applied to the simulation. Click 'Finish' to start building your process."
            : "Everything you change here is applied right away. You can close the wizard at any time.";

        BtnBack.IsEnabled = page > 0;
        BtnNext.IsEnabled = page < _pages.Count - 1;
        BtnFinish.IsVisible = page == _pages.Count - 1;

        for (int i = 0; i < _stepLabels.Count; i++)
        {
            _stepLabels[i].FontWeight = i == page ? FontWeight.Bold : FontWeight.Normal;
            _stepLabels[i].Opacity = i == page ? 1.0 : 0.7;
        }

        // the pages that read the simulation are filled when they are shown, so a compound
        // added on step 2 is already there when the property packages are picked on step 4
        if (page == 3) RefreshPropertyPackages();
        if (page == 4) LoadUnitSystem();
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
            FontSize = 11,
            Opacity = 0.75,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 2)
        };
    }

    // -------------------------------------------------------------------------
    // 1. Introduction
    // -------------------------------------------------------------------------

    private Control BuildIntroduction()
    {
        var stack = new StackPanel { Spacing = 10 };

        stack.Children.Add(new TextBlock
        {
            Text = "Welcome to the simulation setup wizard.",
            FontSize = 14,
            FontWeight = FontWeight.SemiBold
        });

        stack.Children.Add(new TextBlock
        {
            Text = "The next steps walk through what a new simulation needs: the compounds, the " +
                   "reactions between them, the thermodynamic models, the system of units, and " +
                   "how the flowsheet behaves while you work.",
            TextWrapping = TextWrapping.Wrap
        });

        stack.Children.Add(new TextBlock
        {
            Text = "Nothing here is exclusive to the wizard. Everything can be changed later in " +
                   "Simulation Settings, and every change you make is applied to the simulation " +
                   "as you make it, so you can close the wizard whenever you want.",
            TextWrapping = TextWrapping.Wrap
        });

        stack.Children.Add(new TextBlock
        {
            Text = "Click 'Next' to continue.",
            Margin = new Thickness(0, 8, 0, 0)
        });

        return stack;
    }

    // -------------------------------------------------------------------------
    // 2. Compounds
    // -------------------------------------------------------------------------

    private Control BuildCompounds()
    {
        var host = new DockPanel();

        var search = new TextBox { Watermark = "Filter by name, CAS number, formula or database" };
        search.TextChanged += (_, _) => FilterCompounds(search.Text ?? "");
        // Enter adds the best match (the selected, top-of-list compound)
        search.KeyDown += (_, e) =>
        {
            if (e.Key != global::Avalonia.Input.Key.Enter) return;
            var row = _compoundGrid?.SelectedItem as CompoundRow
                      ?? (_compoundRows.Count > 0 ? _compoundRows[0] : null);
            if (row != null) row.Added = true;
            e.Handled = true;
        };

        var searchRow = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
        var searchLabel = new TextBlock
        {
            Text = "Search",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        };
        DockPanel.SetDock(searchLabel, global::Avalonia.Controls.Dock.Left);
        searchRow.Children.Add(searchLabel);
        searchRow.Children.Add(search);

        DockPanel.SetDock(searchRow, global::Avalonia.Controls.Dock.Top);
        host.Children.Add(searchRow);

        _compoundCount = new TextBlock { FontSize = 11, Opacity = 0.75, Margin = new Thickness(0, 6, 0, 0) };
        DockPanel.SetDock(_compoundCount, global::Avalonia.Controls.Dock.Bottom);
        host.Children.Add(_compoundCount);

        var grid = new DataGrid
        {
            ItemsSource = _compoundRows,
            AutoGenerateColumns = false,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            CanUserSortColumns = true,
            SelectionMode = DataGridSelectionMode.Single
        };
        _compoundGrid = grid;

        // a checkbox column of the grid only reacts once the cell is in edit mode, which costs
        // two clicks and reads as broken; a checkbox in the cell itself takes the first one
        grid.Columns.Add(new DataGridTemplateColumn
        {
            Header = "Added",
            Width = new DataGridLength(70),
            CellTemplate = new FuncDataTemplate<CompoundRow>((row, _) =>
            {
                if (row == null) return new TextBlock();

                var check = new CheckBox { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                check.Bind(CheckBox.IsCheckedProperty, new Binding("Added") { Mode = BindingMode.TwoWay });
                return check;
            }, supportsRecycling: false)
        });
        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Name",
            Binding = new Binding("Name"),
            IsReadOnly = true,
            Width = new DataGridLength(2.2, DataGridLengthUnitType.Star)
        });
        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "CAS Number",
            Binding = new Binding("CAS"),
            IsReadOnly = true,
            Width = new DataGridLength(110)
        });
        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Formula",
            Binding = new Binding("Formula"),
            IsReadOnly = true,
            Width = new DataGridLength(1, DataGridLengthUnitType.Star)
        });
        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Database",
            Binding = new Binding("Database"),
            IsReadOnly = true,
            Width = new DataGridLength(100)
        });

        host.Children.Add(grid);

        PopulateCompounds();

        return host;
    }

    private void PopulateCompounds()
    {
        _allCompoundRows.Clear();

        if (_flowsheet.AvailableCompounds != null)
        {
            foreach (var compound in _flowsheet.AvailableCompounds.Values.OrderBy(x => x.Name))
            {
                var row = new CompoundRow(compound, _flowsheet.SelectedCompounds.ContainsKey(compound.Name));
                row.AddedChanged = OnCompoundAddedChanged;
                _allCompoundRows.Add(row);
            }
        }

        FilterCompounds("");
    }

    private void FilterCompounds(string query)
    {
        _compoundRows.Clear();

        IEnumerable<CompoundRow> source = _allCompoundRows;

        if (!string.IsNullOrWhiteSpace(query))
        {
            var q = query.Trim();
            // most-similar first, so the exact name lands at the top and gets selected below
            source = _allCompoundRows
                .Where(x => CompoundSearch.Matches(x.Name, x.CAS, x.Formula, x.Database, q))
                .OrderBy(x => CompoundSearch.Rank(x.Name, q))
                .ThenBy(x => x.Name.Length)
                .ThenBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase);
        }

        foreach (var row in source) _compoundRows.Add(row);

        // select the best match so the user can add it with Enter
        if (!string.IsNullOrWhiteSpace(query) && _compoundRows.Count > 0 && _compoundGrid != null)
        {
            _compoundGrid.SelectedItem = _compoundRows[0];
            _compoundGrid.ScrollIntoView(_compoundRows[0], null);
        }

        UpdateCompoundCount();
    }

    private void UpdateCompoundCount()
    {
        _compoundCount.Text = _compoundRows.Count + " compounds listed, " +
                              _flowsheet.SelectedCompounds.Count + " added to the simulation.";
    }

    private void OnCompoundAddedChanged(CompoundRow row, bool added)
    {
        if (_loading) return;

        try
        {
            if (added) CompoundSelection.Add(_flowsheet, row.Compound);
            else CompoundSelection.Remove(_flowsheet, row.Compound);

            _flowsheet.UpdateInterface();
        }
        catch (Exception ex)
        {
            _flowsheet.ShowMessage("Could not change the compound list: " + ex.Message,
                IFlowsheet.MessageType.GeneralError);
        }

        UpdateCompoundCount();
    }

    // -------------------------------------------------------------------------
    // 3. Reactions
    // -------------------------------------------------------------------------

    private Control BuildReactions()
    {
        try
        {
            return ReactionManagerWindow.CreateEmbeddedContent(_flowsheet);
        }
        catch (Exception ex)
        {
            return new TextBlock
            {
                Text = "The reaction manager could not be loaded: " + ex.Message,
                TextWrapping = TextWrapping.Wrap
            };
        }
    }

    // -------------------------------------------------------------------------
    // 4. Property packages
    // -------------------------------------------------------------------------

    private Control BuildPropertyPackages()
    {
        var host = new DockPanel();

        _availablePP = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
        _availablePP.Items.Add("click to show the list...");

        foreach (var name in _flowsheet.GetAvailablePropertyPackages().OrderBy(x => x))
            _availablePP.Items.Add(name);

        _availablePP.SelectedIndex = 0;
        _availablePP.SelectionChanged += (_, _) =>
        {
            if (_availablePP.SelectedIndex <= 0) return;
            if (_availablePP.SelectedItem is string name) AddPropertyPackage(name);
            _availablePP.SelectedIndex = 0;
        };

        var top = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
        var label = new TextBlock
        {
            Text = "Select a property package to add it:",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        };
        DockPanel.SetDock(label, global::Avalonia.Controls.Dock.Left);
        top.Children.Add(label);
        top.Children.Add(_availablePP);

        DockPanel.SetDock(top, global::Avalonia.Controls.Dock.Top);
        host.Children.Add(top);

        var grid = new DataGrid
        {
            ItemsSource = _ppRows,
            AutoGenerateColumns = false,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            CanUserSortColumns = false,
            IsReadOnly = true,
            SelectionMode = DataGridSelectionMode.Single,

            // the rows carry buttons, which need the room or their captions are clipped
            RowHeight = 38
        };

        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Name",
            Binding = new Binding("Name"),
            Width = new DataGridLength(2, DataGridLengthUnitType.Star)
        });
        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Type",
            Binding = new Binding("Type"),
            Width = new DataGridLength(2.4, DataGridLengthUnitType.Star)
        });
        grid.Columns.Add(ButtonColumn("⚙️ Configure", 120, ConfigurePropertyPackage));
        grid.Columns.Add(ButtonColumn("❌ Remove", 110, RemovePropertyPackage));

        host.Children.Add(Group("Added Property Packages", grid));

        RefreshPropertyPackages();

        return host;
    }

    private static DataGridTemplateColumn ButtonColumn(string caption, double width,
                                                       Action<PropPackRow> action)
    {
        return new DataGridTemplateColumn
        {
            Header = "",
            Width = new DataGridLength(width),
            CellTemplate = new FuncDataTemplate<PropPackRow>((row, _) =>
            {
                if (row == null) return new TextBlock();

                var button = new Button
                {
                    Content = caption,
                    FontSize = 11,
                    Margin = new Thickness(2),
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };
                button.Classes.Add("panel");
                button.Click += (_, _) => action(row);
                return button;
            }, supportsRecycling: false)
        };
    }

    private void RefreshPropertyPackages()
    {
        _ppRows.Clear();

        foreach (var pp in _flowsheet.PropertyPackages.Values.OfType<PropertyPackage>())
        {
            pp.Flowsheet = _flowsheet;
            _ppRows.Add(new PropPackRow(pp));
        }
    }

    private void AddPropertyPackage(string name)
    {
        try
        {
            _flowsheet.RegisterSnapshot(SnapshotType.PropertyPackages);
            var pp = (PropertyPackage)_flowsheet.CreateAndAddPropertyPackage(name);
            pp.Tag = pp.ComponentName + " (" + _flowsheet.PropertyPackages.Count + ")";
            RefreshPropertyPackages();
        }
        catch (Exception ex)
        {
            _flowsheet.ShowMessage("Could not add '" + name + "': " + ex.Message,
                IFlowsheet.MessageType.GeneralError);
        }
    }

    private void ConfigurePropertyPackage(PropPackRow row)
    {
        var pp = row.Package;

        if (pp.ImplementsCrossPlatformEditor)
        {
            var panel = AvaloniaCommon.GetDefaultContainer();
            pp.PopulateCrossPlatformEditor(panel);

            var close = new Button { Content = "Close", Width = 90, IsCancel = true };
            close.Classes.Add("dialog");

            var bottom = new StackPanel
            {
                Orientation = global::Avalonia.Layout.Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 8, 0, 0)
            };
            bottom.Children.Add(close);

            var root = new DockPanel { Margin = new Thickness(8) };
            DockPanel.SetDock(bottom, global::Avalonia.Controls.Dock.Bottom);
            root.Children.Add(bottom);
            root.Children.Add(new ScrollViewer { Content = panel });

            var window = new Window
            {
                Title = "Edit '" + pp.Tag + "' (" + pp.ComponentName + ")",
                Width = 620,
                Height = 460,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = root
            };
            IconHelper.ApplyWindowIcon(window);

            close.Click += (_, _) => window.Close();
            window.ShowDialog(this);
        }
        else
        {
            new PropertyPackageEditorWindow(_flowsheet, pp).ShowDialog(this);
        }
    }

    private void RemovePropertyPackage(PropPackRow row)
    {
        _flowsheet.RegisterSnapshot(SnapshotType.PropertyPackages);
        _flowsheet.PropertyPackages.Remove(row.Package.UniqueID);
        RefreshPropertyPackages();
    }

    // -------------------------------------------------------------------------
    // 5. System of units
    // -------------------------------------------------------------------------

    private Control BuildUnits()
    {
        var host = new DockPanel();

        _unitSystems = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
        _unitSystems.SelectionChanged += (_, _) =>
        {
            if (_loading) return;

            var system = SelectedUnitSystem();
            if (system == null) return;

            Options.SelectedUnitSystem = system;
            _flowsheet.UpdateInterface();

            UnitSystemEditor.Fill(_unitsGrid, system, () =>
            {
                _flowsheet.UpdateOpenEditForms();
                _flowsheet.UpdateInterface();
            });
        };

        var top = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
        var label = new TextBlock
        {
            Text = "System of Units",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        };
        DockPanel.SetDock(label, global::Avalonia.Controls.Dock.Left);
        top.Children.Add(label);
        top.Children.Add(_unitSystems);

        DockPanel.SetDock(top, global::Avalonia.Controls.Dock.Top);
        host.Children.Add(top);

        var buttons = new StackPanel
        {
            Orientation = global::Avalonia.Layout.Orientation.Horizontal,
            Spacing = 6,
            Margin = new Thickness(0, 8, 0, 0)
        };

        var create = new Button { Content = "Create New...", FontSize = 11 };
        create.Classes.Add("panel");
        create.Click += (_, _) => CreateUnitSystem(clone: false);

        var clone = new Button { Content = "Clone Selected", FontSize = 11 };
        clone.Classes.Add("panel");
        clone.Click += (_, _) => CreateUnitSystem(clone: true);

        buttons.Children.Add(create);
        buttons.Children.Add(clone);

        DockPanel.SetDock(buttons, global::Avalonia.Controls.Dock.Bottom);
        host.Children.Add(buttons);

        var note = Note("The built-in sets cannot be edited. Clone one to change the units of any measure.");
        DockPanel.SetDock(note, global::Avalonia.Controls.Dock.Top);
        host.Children.Add(note);

        _unitsGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,*") };

        host.Children.Add(new ScrollViewer { Content = _unitsGrid });

        return host;
    }

    private IUnitsOfMeasure? SelectedUnitSystem()
    {
        var systems = _flowsheet.AvailableSystemsOfUnits;

        return _unitSystems.SelectedIndex >= 0 && _unitSystems.SelectedIndex < systems.Count
            ? systems[_unitSystems.SelectedIndex]
            : null;
    }

    private void LoadUnitSystem()
    {
        var systems = _flowsheet.AvailableSystemsOfUnits;

        var previous = _loading;
        _loading = true;

        _unitSystems.ItemsSource = systems.Select(x => x.Name).ToList();

        var current = Options.SelectedUnitSystem?.Name ?? "SI";
        _unitSystems.SelectedIndex = Math.Max(0, systems.FindIndex(x => x.Name == current));

        _loading = previous;

        var system = SelectedUnitSystem();
        if (system == null) return;

        UnitSystemEditor.Fill(_unitsGrid, system, () =>
        {
            _flowsheet.UpdateOpenEditForms();
            _flowsheet.UpdateInterface();
        });
    }

    private void CreateUnitSystem(bool clone)
    {
        var systems = _flowsheet.AvailableSystemsOfUnits;
        var source = SelectedUnitSystem();

        var created = new DWSIM.SharedClasses.SystemsOfUnits.SI();

        if (clone && source != null) UnitSystemEditor.CopyUnits(source, created);

        created.Name = UnitSystemEditor.UniqueName(systems,
            clone && source != null ? source.Name + " (copy)" : "New System");

        systems.Add(created);
        Options.SelectedUnitSystem = created;

        LoadUnitSystem();
    }

    // -------------------------------------------------------------------------
    // 6. Behavior
    // -------------------------------------------------------------------------

    private Control BuildBehavior()
    {
        var stack = new StackPanel { Spacing = 6 };

        var editors = new StackPanel { Spacing = 4 };

        var doubleClick = new CheckBox
        {
            Content = "Open the object editor on a double click",
            IsChecked = !DWSIM.GlobalSettings.Settings.EditOnSelect
        };
        doubleClick.IsCheckedChanged += (_, _) =>
        {
            if (_loading) return;
            DWSIM.GlobalSettings.Settings.EditOnSelect = !doubleClick.IsChecked.GetValueOrDefault();
        };

        editors.Children.Add(doubleClick);
        editors.Children.Add(Note(
            "Objects are edited with a single click by default. Turn this on to open the editor " +
            "only when the object is double-clicked."));

        stack.Children.Add(Group("Object Editing Behavior", editors));

        var solving = new StackPanel { Spacing = 4 };

        var smart = new CheckBox
        {
            Content = "Calculate an object only when one of its inputs changed",
            IsChecked = !Options.ForceObjectSolving
        };
        smart.IsCheckedChanged += (_, _) =>
        {
            if (_loading) return;
            Options.ForceObjectSolving = !smart.IsChecked.GetValueOrDefault();
        };

        solving.Children.Add(smart);
        solving.Children.Add(Note(
            "DWSIM can skip the calculation of a flowsheet object when none of its input " +
            "parameters, including the inlet streams, has changed since the last run."));

        stack.Children.Add(Group("Smart Object Solving", solving));

        var flash = new StackPanel { Spacing = 4 };

        var failSafe = new CheckBox
        {
            Content = "Try a simple VLE calculation when the selected method fails",
            IsChecked = FailSafeEnabled()
        };
        failSafe.IsCheckedChanged += (_, _) =>
        {
            if (_loading) return;
            SetFailSafe(failSafe.IsChecked.GetValueOrDefault());
        };

        flash.Children.Add(failSafe);
        flash.Children.Add(Note(
            "If the selected equilibrium calculation method fails, DWSIM can try again with a " +
            "simple vapor-liquid procedure. This is applied to every property package of the " +
            "simulation."));

        stack.Children.Add(Group("Fail-Safe Flash Calculations", flash));

        return new ScrollViewer { Content = stack };
    }

    /// <summary>
    /// Whether the property packages fall back to the simple procedure. Mode 1 is the fall-back,
    /// mode 3 is off, which is how the Windows wizard writes it.
    /// </summary>
    private bool FailSafeEnabled()
    {
        var first = _flowsheet.PropertyPackages.Values.OfType<PropertyPackage>().FirstOrDefault();
        if (first == null) return false;

        try { return Convert.ToInt32(first.FlashSettings[FlashSetting.FailSafeCalculationMode]) == 1; }
        catch (Exception) { return false; }
    }

    private void SetFailSafe(bool enabled)
    {
        foreach (var pp in _flowsheet.PropertyPackages.Values.OfType<PropertyPackage>())
        {
            try { pp.FlashSettings[FlashSetting.FailSafeCalculationMode] = enabled ? "1" : "3"; }
            catch (Exception) { }
        }
    }

    // -------------------------------------------------------------------------
    // 7. Undo/Redo
    // -------------------------------------------------------------------------

    private Control BuildUndoRedo()
    {
        var stack = new StackPanel { Spacing = 6 };

        var content = new StackPanel { Spacing = 4 };

        var enabled = new CheckBox
        {
            Content = "Record every change so it can be undone",
            IsChecked = Options.EnabledUndoRedo
        };
        enabled.IsCheckedChanged += (_, _) =>
        {
            if (_loading) return;
            Options.EnabledUndoRedo = enabled.IsChecked.GetValueOrDefault();
        };

        content.Children.Add(enabled);
        content.Children.Add(Note(
            "Undo and redo are turned off by default because recording every change costs memory " +
            "and makes large simulations slower to edit."));

        stack.Children.Add(Group("Undo/Redo Operations", content));

        return stack;
    }

    // -------------------------------------------------------------------------
    // 8. Details
    // -------------------------------------------------------------------------

    private Control BuildDetails()
    {
        var stack = new StackPanel { Spacing = 6 };

        var general = new StackPanel { Spacing = 4 };

        _simulationName = new TextBox { Text = Options.SimulationName ?? "" };
        _simulationName.TextChanged += (_, _) =>
        {
            if (_loading) return;
            Options.SimulationName = _simulationName.Text ?? "";
        };

        general.Children.Add(_simulationName);
        general.Children.Add(Note(
            "The name identifies the simulation on reports and is suggested as the file name " +
            "when it is saved."));

        stack.Children.Add(Group("Simulation Name", general));

        var formats = new StackPanel { Spacing = 4 };

        var numbers = new ComboBox
        {
            ItemsSource = NumberFormats,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            SelectedIndex = Math.Max(0, Array.IndexOf(NumberFormats, Options.NumberFormat))
        };
        numbers.SelectionChanged += (_, _) =>
        {
            if (_loading) return;
            if (numbers.SelectedItem is string format) Options.NumberFormat = format;
        };

        formats.Children.Add(new TextBlock { Text = "General numbers", FontSize = 11 });
        formats.Children.Add(numbers);

        var fractions = new ComboBox
        {
            ItemsSource = NumberFormats,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            SelectedIndex = Math.Max(0, Array.IndexOf(NumberFormats, Options.FractionNumberFormat))
        };
        fractions.SelectionChanged += (_, _) =>
        {
            if (_loading) return;
            if (fractions.SelectedItem is string format) Options.FractionNumberFormat = format;
        };

        formats.Children.Add(new TextBlock
        {
            Text = "Compound amounts",
            FontSize = 11,
            Margin = new Thickness(0, 6, 0, 0)
        });
        formats.Children.Add(fractions);

        stack.Children.Add(Group("Number Formats", formats));

        return new ScrollViewer { Content = stack };
    }

}
