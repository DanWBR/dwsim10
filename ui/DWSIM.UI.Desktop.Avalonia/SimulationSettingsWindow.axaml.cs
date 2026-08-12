using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using DWSIM.Interfaces;
using DWSIM.Interfaces.Enums;
using DWSIM.Thermodynamics.PropertyPackages;
using DWSIM.UI.Shared.Avalonia;
using Compound = DWSIM.Thermodynamics.BaseClasses.Compound;
using ConstantProperties = DWSIM.Thermodynamics.BaseClasses.ConstantProperties;
using MaterialStream = DWSIM.Thermodynamics.Streams.MaterialStream;
using Units = DWSIM.SharedClasses.SystemsOfUnits.Units;

namespace DWSIM.UI.Desktop.Avalonia;

/// <summary>
/// Simulation settings, laid out like the WinForms FormSimulSettings: Compounds, Thermodynamics,
/// Reactions, Mass and Energy Balances, System of Units, Behavior, Object Properties and Details.
/// As in the WinForms form, every control writes its setting through as soon as it changes; the
/// window has no OK/Apply, only Close.
/// </summary>
public partial class SimulationSettingsWindow : Window
{

    // =========================================================================
    // Rows
    // =========================================================================

    /// <summary>One compound of the database, added to the simulation or not.</summary>
    private sealed class CompoundRow : INotifyPropertyChanged
    {
        private bool _added;
        private string _tag;

        public CompoundRow(ICompoundConstantProperties compound, bool added)
        {
            Compound = compound;
            _added = added;
            _tag = compound.Tag ?? "";
        }

        public ICompoundConstantProperties Compound { get; }

        /// <summary>Raised when the user ticks or unticks the Added box.</summary>
        public Action<CompoundRow, bool>? AddedChanged;

        public bool Added
        {
            get => _added;
            set
            {
                if (_added == value) return;
                _added = value;
                Raise(nameof(Added));
                AddedChanged?.Invoke(this, value);
            }
        }

        public string Tag
        {
            get => _tag;
            set
            {
                _tag = value ?? "";
                Compound.Tag = _tag;
                Raise(nameof(Tag));
            }
        }

        public string Name => Compound.Name;
        public string CAS => Compound.CAS_Number ?? "";
        public string Formula => Compound.Formula ?? "";
        public string Database => Compound.CurrentDB ?? "";
        public bool CoolProp => Compound.IsCOOLPROPSupported;

        /// <summary>Sets Added without running the add/remove side effects.</summary>
        public void SetAddedSilently(bool value)
        {
            if (_added == value) return;
            _added = value;
            Raise(nameof(Added));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>One property package of the simulation.</summary>
    private sealed class PropPackRow : INotifyPropertyChanged
    {
        private string _name;

        public PropPackRow(PropertyPackage pp)
        {
            Package = pp;
            _name = pp.Tag ?? pp.ComponentName;
        }

        public PropertyPackage Package { get; }

        public string Name
        {
            get => _name;
            set
            {
                _name = value ?? "";
                Package.Tag = _name;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
            }
        }

        public string Type => Package.ComponentName;

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    // =========================================================================
    // State
    // =========================================================================

    private readonly IFlowsheet _flowsheet;
    private bool _loading = true;

    private readonly ObservableCollection<CompoundRow> _compoundRows = new();
    private readonly List<CompoundRow> _allCompoundRows = new();
    private readonly ObservableCollection<PropPackRow> _ppRows = new();

    /// <summary>Display name to type name, for the VisibleProperties keys.</summary>
    private readonly Dictionary<string, string> _objectTypeNames = new();

    /// <summary>Display name to the full property list of that object type.</summary>
    private readonly Dictionary<string, string[]> _objectProperties = new();

    private static readonly string[] NumberFormats =
        { "F", "G", "G2", "G4", "G6", "G8", "G10", "N", "N2", "N4", "N6", "R", "E", "E1", "E2", "E3", "E4", "E6" };

    /// <summary>Unit sets that ship with DWSIM and cannot be renamed, edited or deleted.</summary>
    // Parameterless ctor required by Avalonia's XAML compiler (designer-only).
    public SimulationSettingsWindow() : this(null!) { }

    public SimulationSettingsWindow(IFlowsheet flowsheet)
    {
        _flowsheet = flowsheet!;
        InitializeComponent();
        IconHelper.ApplyWindowIcon(this);
        if (flowsheet == null) return;

        PopulateCompounds();
        PopulatePropertyPackages();
        PopulateReactions();
        PopulateBalances();
        PopulateUnits();
        PopulateBehavior();
        PopulateObjectProperties();
        PopulateDetails();

        _loading = false;

        WireEvents();
    }

    private IFlowsheetOptions Options => _flowsheet.FlowsheetOptions;

    // =========================================================================
    // Compounds
    // =========================================================================

    private void PopulateCompounds()
    {
        _allCompoundRows.Clear();

        var available = _flowsheet.AvailableCompounds?.Values.OrderBy(x => x.Name).ToList()
                        ?? new List<ICompoundConstantProperties>();

        foreach (var compound in available)
        {
            var row = new CompoundRow(compound, _flowsheet.SelectedCompounds.ContainsKey(compound.Name));
            row.AddedChanged = OnCompoundAddedChanged;
            _allCompoundRows.Add(row);
        }

        // added ones first, as the WinForms grid sorts itself on load
        _allCompoundRows.Sort((a, b) => a.Added == b.Added
            ? string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase)
            : b.Added.CompareTo(a.Added));

        GridCompounds.ItemsSource = _compoundRows;
        FilterCompounds("");
    }

    private void FilterCompounds(string query)
    {
        _compoundRows.Clear();

        IEnumerable<CompoundRow> source = _allCompoundRows;

        if (!string.IsNullOrWhiteSpace(query))
        {
            var q = query.Trim();
            source = _allCompoundRows.Where(x =>
                x.Name.IndexOf(q, StringComparison.CurrentCultureIgnoreCase) >= 0 ||
                x.CAS.IndexOf(q, StringComparison.CurrentCultureIgnoreCase) >= 0 ||
                x.Formula.IndexOf(q, StringComparison.CurrentCultureIgnoreCase) >= 0 ||
                x.Database.IndexOf(q, StringComparison.CurrentCultureIgnoreCase) >= 0);
        }

        foreach (var row in source) _compoundRows.Add(row);

        LblCompoundCount.Text = $"{_compoundRows.Count} of {_allCompoundRows.Count} compounds, " +
                                $"{_flowsheet.SelectedCompounds.Count} in the simulation";
    }

    private void OnCompoundAddedChanged(CompoundRow row, bool added)
    {
        if (_loading) return;

        try
        {
            if (added) AddCompoundToSimulation(row.Compound);
            else RemoveCompoundFromSimulation(row.Compound);

            _flowsheet.UpdateOpenEditForms();
            _flowsheet.UpdateInterface();

            LblCompoundCount.Text = $"{_compoundRows.Count} of {_allCompoundRows.Count} compounds, " +
                                    $"{_flowsheet.SelectedCompounds.Count} in the simulation";
            Status(added ? $"'{row.Name}' added to the simulation." : $"'{row.Name}' removed from the simulation.");
        }
        catch (Exception ex)
        {
            row.SetAddedSilently(!added);
            Status(ex.Message);
        }
    }

    /// <summary>Adds the compound to the simulation and to every phase of every material stream.</summary>
    private void AddCompoundToSimulation(ICompoundConstantProperties compound)
    {
        CompoundSelection.Add(_flowsheet, compound);
    }

    private void RemoveCompoundFromSimulation(ICompoundConstantProperties compound)
    {
        CompoundSelection.Remove(_flowsheet, compound);
    }

    private IEnumerable<MaterialStream> MaterialStreams()
        => _flowsheet.SimulationObjects.Values.OfType<MaterialStream>().ToList();

    private void ViewSelectedCompound()
    {
        if (GridCompounds.SelectedItem is not CompoundRow row)
        {
            Status("Select a compound first.");
            return;
        }
        new PureCompoundPropertiesWindow(_flowsheet, row.Name).Show(this);
    }

    /// <summary>Runs one of the import windows and picks up whatever it added.</summary>
    private async System.Threading.Tasks.Task ImportFromAsync(Window window)
    {
        await window.ShowDialog(this);
        PopulateCompounds();
        BuildKeyCompoundList(PanelKeyFeeds, Options.Metadata.KeyCompounds);
        BuildKeyCompoundList(PanelKeyReactants, Options.Metadata.KeyReactants);
        BuildKeyCompoundList(PanelKeyProducts, Options.Metadata.KeyProducts);
    }

    private async void ImportCompoundFromJson()
    {
        try
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Import Compound from JSON",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("JSON compound files") { Patterns = new[] { "*.json" } }
                }
            });

            var file = files.FirstOrDefault();
            if (file == null) return;

            var text = System.IO.File.ReadAllText(file.Path.LocalPath);
            var compound = Newtonsoft.Json.JsonConvert.DeserializeObject<ConstantProperties>(text);
            if (compound == null || string.IsNullOrEmpty(compound.Name))
            {
                Status("The file does not contain a valid compound.");
                return;
            }

            compound.CurrentDB = "User";

            if (_flowsheet.AvailableCompounds.ContainsKey(compound.Name))
                _flowsheet.AvailableCompounds[compound.Name] = compound;
            else
                _flowsheet.AvailableCompounds.Add(compound.Name, compound);

            if (_flowsheet.SelectedCompounds.ContainsKey(compound.Name))
                _flowsheet.SelectedCompounds[compound.Name] = compound;

            PopulateCompounds();
            Status($"'{compound.Name}' imported.");
        }
        catch (Exception ex)
        {
            Status("Could not import the compound: " + ex.Message);
        }
    }

    // =========================================================================
    // Thermodynamics
    // =========================================================================

    private void PopulatePropertyPackages()
    {
        CbAvailablePP.Items.Clear();
        CbAvailablePP.Items.Add("click to show the list...");
        foreach (var name in _flowsheet.GetAvailablePropertyPackages().OrderBy(x => x))
            CbAvailablePP.Items.Add(name);
        CbAvailablePP.SelectedIndex = 0;

        GridPP.ItemsSource = _ppRows;
        RefreshPropertyPackages();
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
            Status($"'{pp.Tag}' added.");
        }
        catch (Exception ex)
        {
            Status($"Could not add '{name}': {ex.Message}");
        }
    }

    private static PropPackRow? RowOf(object? sender)
        => (sender as Control)?.DataContext as PropPackRow;

    private sealed class PropMethodRow
    {
        public string Property { get; set; } = "";
        public string Model { get; set; } = "";
        public static PropMethodRow Section(string t) => new() { Property = t };
    }

    private void OnPPDetails(object? sender, RoutedEventArgs e)
    {
        if (RowOf(sender) is not { } row) return;
        var pp = row.Package;
        var mi = pp.PropertyMethodsInfo;

        var desc = new TextBlock
        {
            Text = string.IsNullOrEmpty(pp.ComponentDescription) ? pp.ComponentName : pp.ComponentDescription,
            TextWrapping = global::Avalonia.Media.TextWrapping.Wrap,
            Margin = new Thickness(12, 12, 12, 8)
        };

        // The model used for each property, by phase, as in the classic Property Package info.
        var rows = new List<PropMethodRow>
        {
            PropMethodRow.Section("Vapor Phase Properties"),
            new() { Property = "Fugacity", Model = mi.Vapor_Fugacity },
            new() { Property = "Enthalpy/Entropy/Cp", Model = mi.Vapor_Enthalpy_Entropy_CpCv },
            new() { Property = "Density", Model = mi.Vapor_Density },
            new() { Property = "Viscosity", Model = mi.Vapor_Viscosity },
            new() { Property = "Thermal Conductivity", Model = mi.Vapor_Thermal_Conductivity },
            PropMethodRow.Section("Liquid Phase Properties"),
            new() { Property = "Fugacity", Model = mi.Liquid_Fugacity },
            new() { Property = "Enthalpy/Entropy/Cp", Model = mi.Liquid_Enthalpy_Entropy_CpCv },
            new() { Property = "Density", Model = mi.Liquid_Density },
            new() { Property = "Viscosity", Model = mi.Liquid_Viscosity },
            new() { Property = "Thermal Conductivity", Model = mi.Liquid_ThermalConductivity },
            new() { Property = "Surface Tension", Model = mi.SurfaceTension },
            PropMethodRow.Section("Solid Phase Properties"),
            new() { Property = "Density", Model = mi.Solid_Density },
            new() { Property = "Enthalpy/Entropy/Cp", Model = mi.Solid_Enthalpy_Entropy_CpCv },
        };

        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            IsReadOnly = true,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            FontSize = 11,
            ItemsSource = rows
        };
        grid.Columns.Add(new DataGridTextColumn { Header = "Property", Binding = new global::Avalonia.Data.Binding("Property"), Width = new DataGridLength(230) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Model", Binding = new global::Avalonia.Data.Binding("Model"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });

        var close = new Button { Content = "Close", Width = 90, IsCancel = true, Margin = new Thickness(12) };
        close.Classes.Add("dialog");

        var root = new DockPanel();
        DockPanel.SetDock(desc, global::Avalonia.Controls.Dock.Top);
        DockPanel.SetDock(close, global::Avalonia.Controls.Dock.Bottom);
        root.Children.Add(desc);
        root.Children.Add(close);
        root.Children.Add(grid);

        var dlg = new Window
        {
            Title = pp.ComponentName,
            Width = 560,
            Height = 480,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = root
        };
        IconHelper.ApplyWindowIcon(dlg);
        close.Click += (_, _) => dlg.Close();
        dlg.ShowDialog(this);
    }

    private void OnPPConfigure(object? sender, RoutedEventArgs e)
    {
        if (RowOf(sender) is not { } row) return;
        var pp = row.Package;

        if (pp.ImplementsCrossPlatformEditor)
        {
            var panel = AvaloniaCommon.GetDefaultContainer();
            pp.PopulateCrossPlatformEditor(panel);

            var close = new Button { Content = "Close", Width = 90, IsCancel = true };
            close.Classes.Add("dialog");

            var bottom = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 8, 0, 0)
            };
            bottom.Children.Add(close);

            var root = new DockPanel { Margin = new Thickness(8) };
            DockPanel.SetDock(bottom, global::Avalonia.Controls.Dock.Bottom);
            root.Children.Add(bottom);
            root.Children.Add(new ScrollViewer { Content = panel });

            var dlg = new Window
            {
                Title = $"Edit '{pp.Tag}' ({pp.ComponentName})",
                Width = 620,
                Height = 460,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = root
            };
            IconHelper.ApplyWindowIcon(dlg);
            close.Click += (_, _) => dlg.Close();
            dlg.ShowDialog(this);
        }
        else
        {
            new PropertyPackageEditorWindow(_flowsheet, pp).ShowDialog(this);
        }
    }

    private void OnPPCopy(object? sender, RoutedEventArgs e)
    {
        if (RowOf(sender) is not { } row) return;
        try
        {
            _flowsheet.RegisterSnapshot(SnapshotType.PropertyPackages);
            var copy = row.Package.DeepClone();
            copy.UniqueID = "PP-" + Guid.NewGuid();
            copy.Tag = row.Package.ComponentName + " (" + (_flowsheet.PropertyPackages.Count + 1) + ")";
            copy.Flowsheet = _flowsheet;
            _flowsheet.PropertyPackages.Add(copy.UniqueID, copy);
            RefreshPropertyPackages();
            Status($"'{copy.Tag}' created.");
        }
        catch (Exception ex)
        {
            Status("Could not copy the property package: " + ex.Message);
        }
    }

    private void OnPPRemove(object? sender, RoutedEventArgs e)
    {
        if (RowOf(sender) is not { } row) return;
        _flowsheet.RegisterSnapshot(SnapshotType.PropertyPackages);
        _flowsheet.PropertyPackages.Remove(row.Package.UniqueID);
        RefreshPropertyPackages();
        Status($"'{row.Name}' removed.");
    }

    // =========================================================================
    // Reactions
    // =========================================================================

    private void PopulateReactions()
    {
        try
        {
            ReactionsHost.Content = ReactionManagerWindow.CreateEmbeddedContent(_flowsheet);
        }
        catch (Exception ex)
        {
            ReactionsHost.Content = new TextBlock
            {
                Text = "The reaction manager could not be loaded: " + ex.Message,
                Margin = new Thickness(12),
                TextWrapping = global::Avalonia.Media.TextWrapping.Wrap
            };
        }
    }

    // =========================================================================
    // Mass and Energy Balances
    // =========================================================================

    private void PopulateBalances()
    {
        CbMassBalCheck.SelectedIndex = (int)Options.MassBalanceCheck;
        CbEnergyBalCheck.SelectedIndex = (int)Options.EnergyBalanceCheck;
        TbMassBalTol.Text = Options.MassBalanceRelativeTolerance.ToString();
        TbEnergyBalTol.Text = Options.EnergyBalanceRelativeTolerance.ToString();
    }

    // =========================================================================
    // System of Units
    // =========================================================================

    private List<IUnitsOfMeasure> UnitSystems => _flowsheet.AvailableSystemsOfUnits;

    private static bool IsBuiltIn(IUnitsOfMeasure u) => UnitSystemEditor.IsBuiltIn(u);

    private void PopulateUnits()
    {
        RefreshUnitSystemList(Options.SelectedUnitSystem?.Name);
    }

    private void RefreshUnitSystemList(string? select)
    {
        var previous = _loading;
        _loading = true;

        CbUnitSystem.Items.Clear();
        foreach (var u in UnitSystems) CbUnitSystem.Items.Add(u.Name);

        var index = select == null ? 0 : UnitSystems.FindIndex(x => x.Name == select);
        CbUnitSystem.SelectedIndex = UnitSystems.Count == 0 ? -1 : Math.Max(0, index);

        _loading = previous;

        if (CbUnitSystem.SelectedIndex >= 0) LoadUnitSystem(UnitSystems[CbUnitSystem.SelectedIndex]);
    }

    /// <summary>Fills the units grid with one picker per measure, two pairs per row.</summary>
    private void LoadUnitSystem(IUnitsOfMeasure system)
    {
        var readOnly = IsBuiltIn(system);

        var previous = _loading;
        _loading = true;
        TbUnitSystemName.Text = system.Name;
        TbUnitSystemName.IsEnabled = !readOnly;
        _loading = previous;

        UnitSystemEditor.Fill(UnitsGrid, system, () =>
        {
            if (_loading) return;
            _flowsheet.UpdateOpenEditForms();
            _flowsheet.UpdateInterface();
        });
    }

    private string UniqueUnitSystemName(string baseName)
        => UnitSystemEditor.UniqueName(UnitSystems, baseName);

    private static void CopyUnits(IUnitsOfMeasure from, IUnitsOfMeasure to)
        => UnitSystemEditor.CopyUnits(from, to);

    private IUnitsOfMeasure? CurrentUnitSystem
        => CbUnitSystem.SelectedIndex >= 0 && CbUnitSystem.SelectedIndex < UnitSystems.Count
            ? UnitSystems[CbUnitSystem.SelectedIndex]
            : null;

    private async void SaveUnitSystem()
    {
        var system = CurrentUnitSystem;
        if (system == null) return;

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save System of Units",
            SuggestedFileName = system.Name + ".json",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("Unit system files") { Patterns = new[] { "*.json" } }
            }
        });
        if (file == null) return;

        try
        {
            var map = new Dictionary<string, string>();
            foreach (var prop in typeof(Units).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (prop.PropertyType != typeof(string) || !prop.CanRead) continue;
                map[prop.Name] = prop.GetValue(system) as string ?? "";
            }
            System.IO.File.WriteAllText(file.Path.LocalPath,
                Newtonsoft.Json.JsonConvert.SerializeObject(map, Newtonsoft.Json.Formatting.Indented));
            Status($"'{system.Name}' saved.");
        }
        catch (Exception ex)
        {
            Status("Could not save the unit system: " + ex.Message);
        }
    }

    private async void LoadUnitSystemFromFile()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Load System of Units",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Unit system files") { Patterns = new[] { "*.json" } }
            }
        });

        var file = files.FirstOrDefault();
        if (file == null) return;

        try
        {
            var text = System.IO.File.ReadAllText(file.Path.LocalPath);
            var map = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(text);
            if (map == null) { Status("The file does not contain a unit system."); return; }

            var system = new DWSIM.SharedClasses.SystemsOfUnits.SI();
            foreach (var pair in map)
            {
                var prop = typeof(Units).GetProperty(pair.Key, BindingFlags.Public | BindingFlags.Instance);
                if (prop != null && prop.PropertyType == typeof(string) && prop.CanWrite)
                    prop.SetValue(system, pair.Value);
            }
            system.Name = UniqueUnitSystemName(string.IsNullOrEmpty(system.Name)
                ? System.IO.Path.GetFileNameWithoutExtension(file.Path.LocalPath)
                : system.Name);

            UnitSystems.Add(system);
            RefreshUnitSystemList(system.Name);
            Status($"'{system.Name}' loaded.");
        }
        catch (Exception ex)
        {
            Status("Could not load the unit system: " + ex.Message);
        }
    }

    // =========================================================================
    // Behavior
    // =========================================================================

    private void PopulateBehavior()
    {
        ChkSkipEqCalcs.IsChecked = Options.SkipEquilibriumCalculationOnDefinedStreams;
        CbForcePhase.SelectedIndex = Options.ForceStreamPhase switch
        {
            ForcedPhase.Vapor => 1,
            ForcedPhase.Liquid => 2,
            ForcedPhase.Solid => 3,
            _ => 0
        };
        ChkForceObjectCalculation.IsChecked = Options.ForceObjectSolving;
        ChkRestoreUnitOpState.IsChecked = Options.RestoreUnitOperationStateAfterError;
        CbSpecCalcMode.SelectedIndex = (int)Options.SpecCalculationMode;
        CbInfoCarrierCalcMode.SelectedIndex = (int)Options.InformationCarrierCalculationMode;
        CbOrderCompoundsBy.SelectedIndex = (int)Options.CompoundOrderingMode;

        foreach (var f in NumberFormats)
        {
            CbNumberFormat.Items.Add(f);
            CbFractionFormat.Items.Add(f);
        }
        CbNumberFormat.SelectedIndex = Math.Max(0, Array.IndexOf(NumberFormats, Options.NumberFormat));
        CbFractionFormat.SelectedIndex = Math.Max(0, Array.IndexOf(NumberFormats, Options.FractionNumberFormat));

        ChkEnableUndoRedo.IsChecked = Options.EnabledUndoRedo;
        ChkIncludeMessagesInFile.IsChecked = Options.SaveFlowsheetMessagesInFile;
    }

    // =========================================================================
    // Object Properties
    // =========================================================================

    private void PopulateObjectProperties()
    {
        ChkShowFloatingTables.IsChecked = Options.DisplayFloatingPropertyTables;
        ChkShowAnchoredLists.IsChecked = Options.DisplayCornerPropertyList;
        ChkShowExtraPropertiesEditor.IsChecked = Options.DisplayUserDefinedPropertiesEditor;
        ChkFloatingTableCompoundAmounts.IsChecked = Options.DisplayFloatingTableCompoundAmounts;
        CbAmountBasis.SelectedIndex = (int)Options.DefaultFloatingTableCompoundAmountBasis;

        TbPropListFontName.Text = Options.DisplayCornerPropertyListFontName ?? "Consolas";
        NudPropListFontSize.Value = Options.DisplayCornerPropertyListFontSize;
        NudPropListPadding.Value = Options.DisplayCornerPropertyListPadding;

        var colorNames = typeof(SkiaSharp.SKColors)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(f => f.Name).OrderBy(n => n).ToList();
        foreach (var c in colorNames) CbPropListFontColor.Items.Add(c);
        CbPropListFontColor.SelectedIndex =
            Math.Max(0, colorNames.IndexOf(Options.DisplayCornerPropertyListFontColor ?? "Black"));

        ChkShowMSTemp.IsChecked = Options.DisplayMaterialStreamTemperatureValue;
        ChkShowMSPressure.IsChecked = Options.DisplayMaterialStreamPressureValue;
        ChkShowMSMassFlow.IsChecked = Options.DisplayMaterialStreamMassFlowValue;
        ChkShowMSMolarFlow.IsChecked = Options.DisplayMaterialStreamMolarFlowValue;
        ChkShowMSVolFlow.IsChecked = Options.DisplayMaterialStreamVolFlowValue;
        ChkShowMSEnergyFlow.IsChecked = Options.DisplayMaterialStreamEnergyFlowValue;
        ChkShowESPower.IsChecked = Options.DisplayEnergyStreamPowerValue;
        ChkShowDynProps.IsChecked = Options.DisplayDynamicPropertyValues;

        PopulateObjectTypes();
    }

    /// <summary>
    /// The object types of the engine and their properties, and the default visible property
    /// lists for whatever the flowsheet has no entry for yet.
    /// </summary>
    private void PopulateObjectTypes()
    {
        DWSIM.UI.Desktop.Editors.FlowsheetObjectTypes.EnsureDefaults(_flowsheet);

        foreach (var entry in DWSIM.UI.Desktop.Editors.FlowsheetObjectTypes.All(_flowsheet))
        {
            if (_objectProperties.ContainsKey(entry.DisplayName)) continue;
            _objectProperties.Add(entry.DisplayName, entry.Properties);
            _objectTypeNames.Add(entry.DisplayName, entry.TypeName);
            CbObjectType.Items.Add(entry.DisplayName);
        }

        if (CbObjectType.ItemCount > 0) CbObjectType.SelectedIndex = 0;
    }

    private void LoadPropertyList()
    {
        LbProperties.Items.Clear();

        if (CbObjectType.SelectedItem is not string display) return;
        if (!_objectProperties.TryGetValue(display, out var properties)) return;

        var typeName = _objectTypeNames[display];
        if (!Options.VisibleProperties.ContainsKey(typeName))
            Options.VisibleProperties.Add(typeName, new List<string>());
        var visible = Options.VisibleProperties[typeName];

        foreach (var property in properties)
        {
            var item = new CheckBox
            {
                Content = _flowsheet.GetTranslatedString(property),
                Tag = property,
                IsChecked = visible.Contains(property),
                FontSize = 11,
                MinHeight = 0
            };
            item.IsCheckedChanged += (_, _) => ToggleVisibleProperty(item);
            LbProperties.Items.Add(item);
        }
    }

    private void ToggleVisibleProperty(CheckBox item)
    {
        if (_loading) return;
        if (CbObjectType.SelectedItem is not string display) return;
        if (item.Tag is not string property) return;

        var visible = Options.VisibleProperties[_objectTypeNames[display]];

        if (item.IsChecked.GetValueOrDefault())
        {
            if (!visible.Contains(property)) visible.Add(property);
        }
        else
        {
            visible.Remove(property);
        }
    }

    private void SetAllProperties(bool visible)
    {
        foreach (var item in LbProperties.Items.OfType<CheckBox>())
            item.IsChecked = visible;
    }

    // =========================================================================
    // Details
    // =========================================================================

    private void PopulateDetails()
    {
        TbTitle.Text = Options.SimulationName ?? "";
        TbAuthor.Text = Options.SimulationAuthor ?? "";
        CbProcessType.SelectedIndex = (int)Options.Metadata.ProcessType;
        TbProcessDescription.Text = Options.Metadata.ProcessDescription ?? "";

        BuildKeyCompoundList(PanelKeyFeeds, Options.Metadata.KeyCompounds);
        BuildKeyCompoundList(PanelKeyReactants, Options.Metadata.KeyReactants);
        BuildKeyCompoundList(PanelKeyProducts, Options.Metadata.KeyProducts);
    }

    private void BuildKeyCompoundList(StackPanel host, List<string> target)
    {
        host.Children.Clear();

        foreach (var name in _flowsheet.SelectedCompounds.Keys.OrderBy(x => x))
        {
            var item = new CheckBox
            {
                Content = name,
                IsChecked = target.Contains(name),
                FontSize = 11,
                MinHeight = 0
            };
            item.IsCheckedChanged += (_, _) =>
            {
                if (_loading) return;
                if (item.IsChecked.GetValueOrDefault())
                {
                    if (!target.Contains(name)) target.Add(name);
                }
                else
                {
                    target.Remove(name);
                }
            };
            host.Children.Add(item);
        }
    }

    // =========================================================================
    // Events
    // =========================================================================

    private void WireEvents()
    {
        BtnClose.Click += (_, _) => Close();

        // ---- Compounds ----
        TbCompoundSearch.TextChanged += (_, _) => FilterCompounds(TbCompoundSearch.Text ?? "");
        BtnClearSearch.Click += (_, _) => TbCompoundSearch.Text = "";
        BtnViewCompound.Click += (_, _) => ViewSelectedCompound();
        GridCompounds.DoubleTapped += (_, _) =>
        {
            if (GridCompounds.SelectedItem is CompoundRow row) row.Added = !row.Added;
        };
        BtnImportJson.Click += (_, _) => ImportCompoundFromJson();
        BtnImportThermoData.Click += async (_, _) => await ImportFromAsync(new CompoundImportThermoDataWindow(_flowsheet));
        BtnImportOnline.Click += async (_, _) => await ImportFromAsync(new CompoundImportOnlineWindow(_flowsheet));
        BtnImportChEDL.Click += async (_, _) => await ImportFromAsync(new CompoundImportChEDLWindow(_flowsheet));

        BtnBulkC7.Click += (_, _) => new BulkPseudocompoundsWindow(_flowsheet).Show(this);
        BtnAssayManager.Click += (_, _) => new AssayManagerWindow(_flowsheet).Show(this);
        BtnDistCurves.Click += (_, _) => new DistillationCurveWindow(_flowsheet).Show(this);

        // ---- Thermodynamics ----
        CbAvailablePP.SelectionChanged += (_, _) =>
        {
            if (_loading || CbAvailablePP.SelectedIndex <= 0) return;
            if (CbAvailablePP.SelectedItem is string name) AddPropertyPackage(name);
            CbAvailablePP.SelectedIndex = 0;
        };


        // ---- Mass and Energy Balances ----
        CbMassBalCheck.SelectionChanged += (_, _) =>
        {
            if (!_loading) Options.MassBalanceCheck = (WarningType)CbMassBalCheck.SelectedIndex;
        };
        CbEnergyBalCheck.SelectionChanged += (_, _) =>
        {
            if (!_loading) Options.EnergyBalanceCheck = (WarningType)CbEnergyBalCheck.SelectedIndex;
        };
        TbMassBalTol.TextChanged += (_, _) =>
        {
            if (!_loading && UtilityHelpers.TryVal(TbMassBalTol.Text, out var v))
                Options.MassBalanceRelativeTolerance = v;
        };
        TbEnergyBalTol.TextChanged += (_, _) =>
        {
            if (!_loading && UtilityHelpers.TryVal(TbEnergyBalTol.Text, out var v))
                Options.EnergyBalanceRelativeTolerance = v;
        };

        // ---- System of Units ----
        CbUnitSystem.SelectionChanged += (_, _) =>
        {
            if (_loading) return;
            var system = CurrentUnitSystem;
            if (system == null) return;
            Options.SelectedUnitSystem = system;
            LoadUnitSystem(system);
            _flowsheet.UpdateOpenEditForms();
            _flowsheet.UpdateInterface();
            Status($"'{system.Name}' is now the active system of units.");
        };

        TbUnitSystemName.TextChanged += (_, _) =>
        {
            if (_loading) return;
            var system = CurrentUnitSystem;
            if (system == null || IsBuiltIn(system)) return;
            system.Name = TbUnitSystemName.Text ?? "";
            var index = CbUnitSystem.SelectedIndex;
            _loading = true;
            CbUnitSystem.Items[index] = system.Name;
            CbUnitSystem.SelectedIndex = index;
            _loading = false;
        };

        BtnUnitsNew.Click += (_, _) =>
        {
            var system = new DWSIM.SharedClasses.SystemsOfUnits.SI { Name = UniqueUnitSystemName("New Unit Set") };
            UnitSystems.Add(system);
            RefreshUnitSystemList(system.Name);
            Status("New unit set created.");
        };

        BtnUnitsClone.Click += (_, _) =>
        {
            var system = CurrentUnitSystem;
            if (system == null) return;
            var copy = new DWSIM.SharedClasses.SystemsOfUnits.SI { Name = UniqueUnitSystemName(system.Name) };
            CopyUnits(system, copy);
            UnitSystems.Add(copy);
            RefreshUnitSystemList(copy.Name);
            Status($"'{copy.Name}' created.");
        };

        BtnUnitsRemove.Click += (_, _) =>
        {
            var system = CurrentUnitSystem;
            if (system == null) return;
            if (IsBuiltIn(system)) { Status("Built-in unit sets cannot be removed."); return; }

            var wasActive = Options.SelectedUnitSystem == system;
            UnitSystems.Remove(system);
            if (wasActive)
                Options.SelectedUnitSystem = UnitSystems.FirstOrDefault(x => x.Name == "SI") ?? UnitSystems.FirstOrDefault();
            RefreshUnitSystemList(Options.SelectedUnitSystem?.Name);
            Status("Unit set removed.");
        };

        BtnUnitsLoad.Click += (_, _) => LoadUnitSystemFromFile();
        BtnUnitsSave.Click += (_, _) => SaveUnitSystem();

        // ---- Behavior ----
        ChkSkipEqCalcs.IsCheckedChanged += (_, _) =>
        {
            if (!_loading) Options.SkipEquilibriumCalculationOnDefinedStreams = ChkSkipEqCalcs.IsChecked.GetValueOrDefault();
        };
        CbForcePhase.SelectionChanged += (_, _) =>
        {
            if (_loading) return;
            Options.ForceStreamPhase = CbForcePhase.SelectedIndex switch
            {
                1 => ForcedPhase.Vapor,
                2 => ForcedPhase.Liquid,
                3 => ForcedPhase.Solid,
                _ => ForcedPhase.None
            };
        };
        ChkForceObjectCalculation.IsCheckedChanged += (_, _) =>
        {
            if (!_loading) Options.ForceObjectSolving = ChkForceObjectCalculation.IsChecked.GetValueOrDefault();
        };
        ChkRestoreUnitOpState.IsCheckedChanged += (_, _) =>
        {
            if (!_loading) Options.RestoreUnitOperationStateAfterError = ChkRestoreUnitOpState.IsChecked.GetValueOrDefault();
        };
        CbSpecCalcMode.SelectionChanged += (_, _) =>
        {
            if (!_loading) Options.SpecCalculationMode = (SpecCalcMode)CbSpecCalcMode.SelectedIndex;
        };
        CbInfoCarrierCalcMode.SelectionChanged += (_, _) =>
        {
            if (!_loading) Options.InformationCarrierCalculationMode = (SpecCalcMode)CbInfoCarrierCalcMode.SelectedIndex;
        };
        CbOrderCompoundsBy.SelectionChanged += (_, _) =>
        {
            if (_loading) return;
            Options.CompoundOrderingMode = (CompoundOrdering)CbOrderCompoundsBy.SelectedIndex;
            _flowsheet.UpdateOpenEditForms();
        };
        CbNumberFormat.SelectionChanged += (_, _) =>
        {
            if (_loading || CbNumberFormat.SelectedItem is not string f) return;
            Options.NumberFormat = f;
            _flowsheet.UpdateOpenEditForms();
            _flowsheet.UpdateInterface();
        };
        CbFractionFormat.SelectionChanged += (_, _) =>
        {
            if (_loading || CbFractionFormat.SelectedItem is not string f) return;
            Options.FractionNumberFormat = f;
            _flowsheet.UpdateOpenEditForms();
            _flowsheet.UpdateInterface();
        };
        LnkFormatHelp.PointerPressed += (_, _) => OpenUrl(
            "https://docs.microsoft.com/en-us/dotnet/standard/base-types/standard-numeric-format-strings");
        ChkEnableUndoRedo.IsCheckedChanged += (_, _) =>
        {
            if (!_loading) Options.EnabledUndoRedo = ChkEnableUndoRedo.IsChecked.GetValueOrDefault();
        };
        ChkIncludeMessagesInFile.IsCheckedChanged += (_, _) =>
        {
            if (!_loading) Options.SaveFlowsheetMessagesInFile = ChkIncludeMessagesInFile.IsChecked.GetValueOrDefault();
        };

        // ---- Object Properties ----
        ChkShowFloatingTables.IsCheckedChanged += (_, _) =>
        {
            if (_loading) return;
            Options.DisplayFloatingPropertyTables = ChkShowFloatingTables.IsChecked.GetValueOrDefault();
            ApplySurfaceOptions();
        };
        ChkShowAnchoredLists.IsCheckedChanged += (_, _) =>
        {
            if (_loading) return;
            Options.DisplayCornerPropertyList = ChkShowAnchoredLists.IsChecked.GetValueOrDefault();
            ApplySurfaceOptions();
        };
        ChkShowExtraPropertiesEditor.IsCheckedChanged += (_, _) =>
        {
            if (!_loading) Options.DisplayUserDefinedPropertiesEditor = ChkShowExtraPropertiesEditor.IsChecked.GetValueOrDefault();
        };
        ChkFloatingTableCompoundAmounts.IsCheckedChanged += (_, _) =>
        {
            if (!_loading) Options.DisplayFloatingTableCompoundAmounts = ChkFloatingTableCompoundAmounts.IsChecked.GetValueOrDefault();
        };
        CbAmountBasis.SelectionChanged += (_, _) =>
        {
            if (!_loading) Options.DefaultFloatingTableCompoundAmountBasis = (CompositionBasis)CbAmountBasis.SelectedIndex;
        };
        TbPropListFontName.TextChanged += (_, _) =>
        {
            if (!_loading) Options.DisplayCornerPropertyListFontName = TbPropListFontName.Text ?? "Consolas";
        };
        NudPropListFontSize.ValueChanged += (_, _) =>
        {
            if (!_loading) Options.DisplayCornerPropertyListFontSize = (int)(NudPropListFontSize.Value ?? 10);
        };
        NudPropListPadding.ValueChanged += (_, _) =>
        {
            if (!_loading) Options.DisplayCornerPropertyListPadding = (int)(NudPropListPadding.Value ?? 5);
        };
        CbPropListFontColor.SelectionChanged += (_, _) =>
        {
            if (!_loading && CbPropListFontColor.SelectedItem is string c)
                Options.DisplayCornerPropertyListFontColor = c;
        };

        CbObjectType.SelectionChanged += (_, _) => LoadPropertyList();
        BtnSelectAllProps.Click += (_, _) => SetAllProperties(true);
        BtnClearPropSelection.Click += (_, _) => SetAllProperties(false);

        ChkShowMSTemp.IsCheckedChanged += (_, _) =>
        {
            if (!_loading) Options.DisplayMaterialStreamTemperatureValue = ChkShowMSTemp.IsChecked.GetValueOrDefault();
        };
        ChkShowMSPressure.IsCheckedChanged += (_, _) =>
        {
            if (!_loading) Options.DisplayMaterialStreamPressureValue = ChkShowMSPressure.IsChecked.GetValueOrDefault();
        };
        ChkShowMSMassFlow.IsCheckedChanged += (_, _) =>
        {
            if (!_loading) Options.DisplayMaterialStreamMassFlowValue = ChkShowMSMassFlow.IsChecked.GetValueOrDefault();
        };
        ChkShowMSMolarFlow.IsCheckedChanged += (_, _) =>
        {
            if (!_loading) Options.DisplayMaterialStreamMolarFlowValue = ChkShowMSMolarFlow.IsChecked.GetValueOrDefault();
        };
        ChkShowMSVolFlow.IsCheckedChanged += (_, _) =>
        {
            if (!_loading) Options.DisplayMaterialStreamVolFlowValue = ChkShowMSVolFlow.IsChecked.GetValueOrDefault();
        };
        ChkShowMSEnergyFlow.IsCheckedChanged += (_, _) =>
        {
            if (!_loading) Options.DisplayMaterialStreamEnergyFlowValue = ChkShowMSEnergyFlow.IsChecked.GetValueOrDefault();
        };
        ChkShowESPower.IsCheckedChanged += (_, _) =>
        {
            if (!_loading) Options.DisplayEnergyStreamPowerValue = ChkShowESPower.IsChecked.GetValueOrDefault();
        };
        ChkShowDynProps.IsCheckedChanged += (_, _) =>
        {
            if (!_loading) Options.DisplayDynamicPropertyValues = ChkShowDynProps.IsChecked.GetValueOrDefault();
        };

        // ---- Details ----
        TbTitle.TextChanged += (_, _) =>
        {
            if (!_loading) Options.SimulationName = TbTitle.Text ?? "";
        };
        TbAuthor.TextChanged += (_, _) =>
        {
            if (!_loading) Options.SimulationAuthor = TbAuthor.Text ?? "";
        };
        CbProcessType.SelectionChanged += (_, _) =>
        {
            if (!_loading) Options.Metadata.ProcessType = (ProcessType)CbProcessType.SelectedIndex;
        };
        TbProcessDescription.TextChanged += (_, _) =>
        {
            if (!_loading) Options.Metadata.ProcessDescription = TbProcessDescription.Text ?? "";
        };

        // the property list needs the wiring in place before it fills itself
        LoadPropertyList();
    }

    private void ApplySurfaceOptions()
    {
        try
        {
            var surface = (DWSIM.Drawing.SkiaSharp.GraphicsSurface)_flowsheet.GetSurface();
            surface.DrawFloatingTable = Options.DisplayFloatingPropertyTables;
            surface.DrawPropertyList = Options.DisplayCornerPropertyList;
            _flowsheet.UpdateInterface();
        }
        catch (Exception) { }
    }

    private static void OpenUrl(string url)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception) { }
    }

    private void Status(string message)
    {
        StatusLabel.Text = message;
    }
}
