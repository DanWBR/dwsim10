using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using DWSIM.Interfaces;
using DWSIM.UI.Shared.Avalonia;
using Assay = DWSIM.SharedClasses.Utilities.PetroleumCharacterization.Assay.Assay;
using cv = DWSIM.SharedClasses.SystemsOfUnits.Converter;

namespace DWSIM.UI.Desktop.Avalonia;

/// <summary>
/// Manages the petroleum assays stored with the simulation. Avalonia counterpart of the
/// WinForms FormAssayManager: bulk and curve assay data, contaminant totals, and import
/// and export of individual assays.
/// </summary>
public sealed class AssayManagerWindow : Window
{
    private sealed class AssayRow : System.ComponentModel.INotifyPropertyChanged
    {
        private string _name = "";

        public string ID { get; init; } = "";
        public string Kind { get; init; } = "";

        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Name)));
            }
        }

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }

    /// <summary>One distillation curve point, in display units.</summary>
    private sealed class CurveRow
    {
        public double Fraction { get; set; }
        public double Temperature { get; set; }
        public double MolarWeight { get; set; }
        public double SpecificGravity { get; set; }
        public double Viscosity1 { get; set; }
        public double Viscosity2 { get; set; }
    }

    private readonly IFlowsheet _flowsheet;
    private readonly IUnitsOfMeasure _su;
    private readonly string _nf;

    private readonly ObservableCollection<AssayRow> _rows = new();
    private readonly ObservableCollection<CurveRow> _curve = new();
    private readonly DataGrid _gridAssays = new() { CanUserSortColumns = false, IsReadOnly = true, AutoGenerateColumns = false };
    private readonly DataGrid _gridCurve = new() { CanUserSortColumns = false, AutoGenerateColumns = false };

    private readonly StackPanel _detail = new() { Spacing = 2 };
    private readonly TextBlock _status = new() { FontSize = 11, Opacity = 0.85, TextWrapping = TextWrapping.Wrap };

    private Assay? _current;
    private bool _loading;

    private Dictionary<string, Assay> Assays
    {
        get
        {
            var opts = (global::DWSIM.SharedClasses.DWSIM.Flowsheet.FlowsheetVariables)_flowsheet.FlowsheetOptions;
            if (opts.PetroleumAssays == null) opts.PetroleumAssays = new Dictionary<string, Assay>();
            return opts.PetroleumAssays;
        }
    }

    public AssayManagerWindow(IFlowsheet flowsheet)
    {
        _flowsheet = flowsheet;
        _su = flowsheet.FlowsheetOptions.SelectedUnitSystem;
        _nf = flowsheet.FlowsheetOptions.NumberFormat;

        Title = "Petroleum Assay Manager";
        Width = 1000;
        Height = 640;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        IconHelper.ApplyWindowIcon(this);

        Content = BuildContent();
        Populate();
    }

    private Control BuildContent()
    {
        _gridAssays.Columns.Add(new DataGridTextColumn
        { Header = "Name", Binding = new global::Avalonia.Data.Binding("Name"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        _gridAssays.Columns.Add(new DataGridTextColumn
        { Header = "Type", Binding = new global::Avalonia.Data.Binding("Kind"), Width = new DataGridLength(70) });
        _gridAssays.ItemsSource = _rows;
        _gridAssays.SelectionChanged += (_, _) => OnAssaySelected();

        void AddCurveColumn(string header, string path)
        {
            _gridCurve.Columns.Add(new DataGridTextColumn
            {
                Header = header,
                Binding = new global::Avalonia.Data.Binding(path) { Mode = global::Avalonia.Data.BindingMode.TwoWay },
                Width = new DataGridLength(1, DataGridLengthUnitType.Star)
            });
        }

        AddCurveColumn("% Dist.", "Fraction");
        AddCurveColumn("NBP (" + _su.temperature + ")", "Temperature");
        AddCurveColumn("MW (" + _su.molecularWeight + ")", "MolarWeight");
        AddCurveColumn("SG", "SpecificGravity");
        AddCurveColumn("Visc 1 (" + _su.cinematic_viscosity + ")", "Viscosity1");
        AddCurveColumn("Visc 2 (" + _su.cinematic_viscosity + ")", "Viscosity2");
        _gridCurve.ItemsSource = _curve;
        _gridCurve.CellEditEnded += (_, _) => WriteCurveBack();

        var btnNewBulk = MakeToolButton("New Bulk Assay", async () => await NewAssay(true));
        var btnNewCurve = MakeToolButton("New Curve Assay", async () => await NewAssay(false));
        var btnClone = MakeToolButton("Clone", () => CloneSelected());
        var btnDelete = MakeToolButton("Delete", () => DeleteSelected());
        var btnImport = MakeToolButton("Import...", async () => await ImportAsync());
        var btnExport = MakeToolButton("Export...", async () => await ExportAsync());

        var tools = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
        foreach (var b in new[] { btnNewBulk, btnNewCurve, btnClone, btnDelete, btnImport, btnExport })
            tools.Children.Add(b);

        var left = new DockPanel { Width = 300, Margin = new Thickness(8) };
        DockPanel.SetDock(tools, global::Avalonia.Controls.Dock.Top);
        left.Children.Add(tools);
        left.Children.Add(_gridAssays);

        var rightTop = new ScrollViewer { Content = _detail, Padding = new Thickness(8) };
        var curveHeader = new TextBlock { Text = "Distillation Curve", FontWeight = FontWeight.SemiBold, Margin = new Thickness(8, 4, 8, 4) };

        var right = new Grid { RowDefinitions = new RowDefinitions("*,Auto,2*") };
        Grid.SetRow(rightTop, 0);
        Grid.SetRow(curveHeader, 1);
        Grid.SetRow(_gridCurve, 2);
        right.Children.Add(rightTop);
        right.Children.Add(curveHeader);
        right.Children.Add(_gridCurve);

        var body = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        Grid.SetColumn(left, 0);
        Grid.SetColumn(right, 1);
        body.Children.Add(left);
        body.Children.Add(right);

        var btnClose = new Button { Content = "Close", Width = 90, IsCancel = true };
        btnClose.Classes.Add("dialog");
        btnClose.Click += (_, _) => Close();

        var bottom = new DockPanel { Margin = new Thickness(8) };
        DockPanel.SetDock(btnClose, global::Avalonia.Controls.Dock.Right);
        bottom.Children.Add(btnClose);
        bottom.Children.Add(_status);

        var root = new DockPanel();
        DockPanel.SetDock(bottom, global::Avalonia.Controls.Dock.Bottom);
        root.Children.Add(bottom);
        root.Children.Add(body);
        return root;
    }

    private static Button MakeToolButton(string caption, Action action)
    {
        var b = new Button { Content = caption, Margin = new Thickness(0, 0, 4, 4) };
        b.Classes.Add("panel");
        b.Click += (_, _) => action();
        return b;
    }

    private static Button MakeToolButton(string caption, Func<Task> action)
    {
        var b = new Button { Content = caption, Margin = new Thickness(0, 0, 4, 4) };
        b.Classes.Add("panel");
        b.Click += async (_, _) => await action();
        return b;
    }

    // -------------------------------------------------------------------------

    private void Populate()
    {
        _rows.Clear();
        foreach (var kvp in Assays)
        {
            _rows.Add(new AssayRow
            {
                ID = kvp.Key,
                Name = kvp.Value.Name,
                Kind = kvp.Value.IsBulk ? "Bulk" : "Curves"
            });
        }

        if (_rows.Count > 0)
            _gridAssays.SelectedIndex = 0;
        else
        {
            _current = null;
            _detail.Children.Clear();
            _curve.Clear();
            _status.Text = "This simulation has no petroleum assays yet.";
        }
    }

    private void OnAssaySelected()
    {
        if (_gridAssays.SelectedItem is not AssayRow row || !Assays.ContainsKey(row.ID))
        {
            _current = null;
            return;
        }

        _current = Assays[row.ID];
        BuildDetail(row);
        LoadCurve();
        _status.Text = $"Assay '{_current.Name}' selected.";
    }

    private void BuildDetail(AssayRow row)
    {
        var a = _current!;

        _loading = true;
        _detail.Children.Clear();

        var p = new AvaloniaEditorPanel();

        p.CreateAndAddLabelRow("Assay Information");
        p.CreateAndAddStringEditorRow("Name", a.Name, (tb, e) =>
        {
            if (_loading) return;
            a.Name = tb.Text ?? "";
            row.Name = a.Name;
        });
        p.CreateAndAddTwoLabelsRow("Type", a.IsBulk ? "Bulk properties" : "Distillation curves");

        if (a.IsBulk)
        {
            p.CreateAndAddLabelRow("Bulk Properties");
            AddConverted(p, "Molar Weight", _su.molecularWeight, a.MW, v => a.MW = v);
            AddPlain(p, "Specific Gravity @ 60 F", a.SG60, v => a.SG60 = v);
            AddConverted(p, "Average NBP", _su.temperature, a.NBPAVG, v => a.NBPAVG = v);
            AddConverted(p, "Viscosity Temperature 1", _su.temperature, a.T1, v => a.T1 = v);
            AddConverted(p, "Viscosity Temperature 2", _su.temperature, a.T2, v => a.T2 = v);
            AddConverted(p, "Kinematic Viscosity 1", _su.cinematic_viscosity, a.V1, v => a.V1 = v);
            AddConverted(p, "Kinematic Viscosity 2", _su.cinematic_viscosity, a.V2, v => a.V2 = v);
        }
        else
        {
            p.CreateAndAddLabelRow("Curve Setup");
            var methods = new List<string> { "TBP, ASTM D2892", "ASTM D86", "ASTM D1160", "ASTM D2887" };
            p.CreateAndAddDropDownRow("Distillation Method", methods, Math.Min(Math.Max(a.NBPType, 0), 3),
                (dd, e) => { if (!_loading && dd.SelectedIndex >= 0) a.NBPType = dd.SelectedIndex; });

            var bases = new List<string> { "Liquid Volume", "Molar", "Mass" };
            var bidx = Math.Max(0, bases.IndexOf(a.CurveBasis ?? ""));
            p.CreateAndAddDropDownRow("Curve Basis", bases, bidx,
                (dd, e) => { if (!_loading && dd.SelectedIndex >= 0) a.CurveBasis = bases[dd.SelectedIndex]; });

            AddPlain(p, "Bulk Molar Weight", a.MW, v => a.MW = v);
            AddPlain(p, "Bulk API Gravity", a.API, v => a.API = v);
            AddPlain(p, "Watson K (API)", a.K_API, v => a.K_API = v);
            p.CreateAndAddCheckBoxRow("Has Molar Weight Curve", a.HasMWCurve,
                (cb, e) => { if (!_loading) a.HasMWCurve = cb.IsChecked.GetValueOrDefault(); });
            p.CreateAndAddCheckBoxRow("Has Specific Gravity Curve", a.HasSGCurve,
                (cb, e) => { if (!_loading) a.HasSGCurve = cb.IsChecked.GetValueOrDefault(); });
            p.CreateAndAddCheckBoxRow("Has Viscosity Curves", a.HasViscCurves,
                (cb, e) => { if (!_loading) a.HasViscCurves = cb.IsChecked.GetValueOrDefault(); });
        }

        p.CreateAndAddLabelRow("Contaminants");
        AddPlain(p, "Sulfur (wt %)", a.BulkSulfurWtPct, v => a.BulkSulfurWtPct = v);
        AddPlain(p, "Nitrogen (wt %)", a.BulkNitrogenWtPct, v => a.BulkNitrogenWtPct = v);
        AddPlain(p, "Mercaptan Sulfur (wt %)", a.BulkMercaptanSulfurWtPct, v => a.BulkMercaptanSulfurWtPct = v);
        AddPlain(p, "Nickel (ppm wt)", a.BulkNiPpm, v => a.BulkNiPpm = v);
        AddPlain(p, "Vanadium (ppm wt)", a.BulkVPpm, v => a.BulkVPpm = v);
        AddPlain(p, "Iron (ppm wt)", a.BulkFePpm, v => a.BulkFePpm = v);
        AddPlain(p, "Sodium (ppm wt)", a.BulkNaPpm, v => a.BulkNaPpm = v);
        AddPlain(p, "CCR (wt %)", a.BulkCCRWtPct, v => a.BulkCCRWtPct = v);
        AddPlain(p, "Asphaltenes (wt %)", a.BulkAsphaltenesWtPct, v => a.BulkAsphaltenesWtPct = v);
        AddPlain(p, "TAN (mgKOH/g)", a.BulkTAN, v => a.BulkTAN = v);
        AddPlain(p, "BS&W (vol %)", a.BSWVolPct, v => a.BSWVolPct = v);
        AddPlain(p, "Salt (PTB)", a.SaltPTB, v => a.SaltPTB = v);

        _detail.Children.Add(p);
        _loading = false;
    }

    private void AddPlain(AvaloniaEditorPanel p, string label, double value, Action<double> setter)
    {
        p.CreateAndAddTextBoxRow(_nf, label, value, (tb, e) =>
        {
            if (_loading) return;
            if (UtilityHelpers.TryVal(tb.Text, out var v)) setter(v);
        });
    }

    private void AddConverted(AvaloniaEditorPanel p, string label, string units, double siValue, Action<double> siSetter)
    {
        p.CreateAndAddTextBoxRow(_nf, label + " (" + units + ")", cv.ConvertFromSI(units, siValue), (tb, e) =>
        {
            if (_loading) return;
            if (UtilityHelpers.TryVal(tb.Text, out var v)) siSetter(cv.ConvertToSI(units, v));
        });
    }

    // -------------------------------------------------------------------------

    private void LoadCurve()
    {
        _curve.Clear();
        var a = _current;
        if (a == null || a.IsBulk || a.PX == null) return;

        for (int i = 0; i < a.PX.Count; i++)
        {
            _curve.Add(new CurveRow
            {
                Fraction = Convert.ToDouble(a.PX[i]) * 100,
                Temperature = cv.ConvertFromSI(_su.temperature, At(a.PY_NBP, i)),
                MolarWeight = At(a.PY_MW, i),
                SpecificGravity = At(a.PY_SG, i),
                Viscosity1 = cv.ConvertFromSI(_su.cinematic_viscosity, At(a.PY_V1, i)),
                Viscosity2 = cv.ConvertFromSI(_su.cinematic_viscosity, At(a.PY_V2, i))
            });
        }
    }

    private static double At(System.Collections.ArrayList? list, int i)
        => list != null && i < list.Count && list[i] != null ? Convert.ToDouble(list[i]) : 0.0;

    /// <summary>Writes the edited grid back to the assay, converting to SI.</summary>
    private void WriteCurveBack()
    {
        var a = _current;
        if (a == null || a.IsBulk) return;

        a.PX = new System.Collections.ArrayList(_curve.Select(r => (object)(r.Fraction / 100)).ToList());
        a.PY_NBP = new System.Collections.ArrayList(_curve.Select(r => (object)cv.ConvertToSI(_su.temperature, r.Temperature)).ToList());
        a.PY_MW = new System.Collections.ArrayList(_curve.Select(r => (object)r.MolarWeight).ToList());
        a.PY_SG = new System.Collections.ArrayList(_curve.Select(r => (object)r.SpecificGravity).ToList());
        a.PY_V1 = new System.Collections.ArrayList(_curve.Select(r => (object)cv.ConvertToSI(_su.cinematic_viscosity, r.Viscosity1)).ToList());
        a.PY_V2 = new System.Collections.ArrayList(_curve.Select(r => (object)cv.ConvertToSI(_su.cinematic_viscosity, r.Viscosity2)).ToList());

        _status.Text = $"Curve updated: {_curve.Count} point(s).";
    }

    // -------------------------------------------------------------------------

    private Task NewAssay(bool bulk)
    {
        var a = bulk
            ? new Assay(200.0, 0.85, 600.0, 311.0, 372.0, 0.0, 0.0)
            : new Assay(12.0, 0.0, 30.0, 310.928, 372.039, 0, "",
                new System.Collections.ArrayList(new object[] { 0.1, 0.3, 0.5, 0.7, 0.9 }),
                new System.Collections.ArrayList(new object[] { 373.15, 423.15, 473.15, 523.15, 573.15 }),
                new System.Collections.ArrayList(), new System.Collections.ArrayList(),
                new System.Collections.ArrayList(), new System.Collections.ArrayList());

        a.Name = (bulk ? "Bulk Assay " : "Curve Assay ") + (Assays.Count + 1);

        var id = Guid.NewGuid().ToString();
        Assays.Add(id, a);
        _rows.Add(new AssayRow { ID = id, Name = a.Name, Kind = bulk ? "Bulk" : "Curves" });
        _gridAssays.SelectedIndex = _rows.Count - 1;
        return Task.CompletedTask;
    }

    private void CloneSelected()
    {
        if (_gridAssays.SelectedItem is not AssayRow row || !Assays.ContainsKey(row.ID)) return;

        var clone = (Assay)Assays[row.ID].Clone();
        clone.Name = row.Name + " (copy)";
        var id = Guid.NewGuid().ToString();
        Assays.Add(id, clone);
        _rows.Add(new AssayRow { ID = id, Name = clone.Name, Kind = clone.IsBulk ? "Bulk" : "Curves" });
        _gridAssays.SelectedIndex = _rows.Count - 1;
        _status.Text = $"Assay cloned as '{clone.Name}'.";
    }

    private void DeleteSelected()
    {
        if (_gridAssays.SelectedItem is not AssayRow row) return;

        Assays.Remove(row.ID);
        _rows.Remove(row);
        _status.Text = $"Assay '{row.Name}' removed.";
        if (_rows.Count > 0) _gridAssays.SelectedIndex = 0;
        else { _current = null; _detail.Children.Clear(); _curve.Clear(); }
    }

    // -------------------------------------------------------------------------

    /// <summary>
    /// Assays are exchanged as XML through the engine's ICustomXMLSerialization support. The
    /// WinForms tool writes the legacy binary format, which .NET 8 cannot deserialize.
    /// </summary>
    private async Task ImportAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import Assay",
            AllowMultiple = false,
            FileTypeFilter = new[] { new FilePickerFileType("Assay File") { Patterns = new[] { "*.dwasf", "*.xml" } } }
        });

        var path = files?.FirstOrDefault()?.Path?.LocalPath;
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            var text = System.IO.File.ReadAllText(path);
            if (!text.TrimStart().StartsWith("<"))
            {
                _status.Text = "This is a legacy binary assay file. Open it in the Windows interface and export it again from this manager to get a portable file.";
                return;
            }

            var doc = XDocument.Parse(text);
            var a = new Assay();
            a.LoadData(doc.Root!.Elements().ToList());

            if (string.IsNullOrEmpty(a.Name)) a.Name = System.IO.Path.GetFileNameWithoutExtension(path);

            var id = Guid.NewGuid().ToString();
            Assays.Add(id, a);
            _rows.Add(new AssayRow { ID = id, Name = a.Name, Kind = a.IsBulk ? "Bulk" : "Curves" });
            _gridAssays.SelectedIndex = _rows.Count - 1;
            _status.Text = $"Assay '{a.Name}' imported.";
        }
        catch (Exception ex)
        {
            _status.Text = "Import failed: " + ex.Message;
        }
    }

    private async Task ExportAsync()
    {
        if (_current == null) { _status.Text = "Select an assay first."; return; }

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export Assay",
            SuggestedFileName = _current.Name + ".dwasf",
            DefaultExtension = "dwasf",
            FileTypeChoices = new[] { new FilePickerFileType("Assay File") { Patterns = new[] { "*.dwasf" } } }
        });

        var path = file?.Path?.LocalPath;
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            var doc = new XDocument(new XElement("Assay", _current.SaveData().ToArray()));
            doc.Save(path);
            _status.Text = "Assay exported to " + path + ".";
        }
        catch (Exception ex)
        {
            _status.Text = "Export failed: " + ex.Message;
        }
    }
}
