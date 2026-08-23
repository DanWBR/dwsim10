using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using DWSIM.Interfaces;
using DWSIM.Interfaces.Enums.GraphicObjects;
using DWSIM.Thermodynamics.BaseClasses;
using DWSIM.Thermodynamics.Streams;
using DWSIM.Thermodynamics.Utilities.PetroleumCharacterization;
using DWSIM.UI.Shared.Avalonia;
using cv = DWSIM.SharedClasses.SystemsOfUnits.Converter;

namespace DWSIM.UI.Desktop.Avalonia;

/// <summary>
/// Creates several petroleum pseudocompounds at once from a table of partial data. Avalonia
/// counterpart of the WinForms FormBulkAddPseudos; both drive the engine's PseudoEstimator,
/// which fills in whatever the user left blank.
/// </summary>
public sealed class BulkPseudocompoundsWindow : Window
{
    /// <summary>
    /// One table row. Values are strings so a blank cell stays blank: the estimator treats
    /// blanks as "estimate this one".
    /// </summary>
    private sealed class PseudoRow : INotifyPropertyChanged
    {
        private string _name = "", _mw = "", _nbp = "", _sg = "", _tc = "", _pc = "", _af = "";

        public string Name { get => _name; set { _name = value; Raise(nameof(Name)); } }
        public string MW { get => _mw; set { _mw = value; Raise(nameof(MW)); } }
        public string NBP { get => _nbp; set { _nbp = value; Raise(nameof(NBP)); } }
        public string SG { get => _sg; set { _sg = value; Raise(nameof(SG)); } }
        public string TC { get => _tc; set { _tc = value; Raise(nameof(TC)); } }
        public string PC { get => _pc; set { _pc = value; Raise(nameof(PC)); } }
        public string AF { get => _af; set { _af = value; Raise(nameof(AF)); } }

        public string XP { get; set; } = "";
        public string XN { get; set; } = "";
        public string XA { get; set; } = "";

        public string Sulfur { get; set; } = "";
        public string Nitrogen { get; set; } = "";
        public string MercaptanS { get; set; } = "";
        public string Nickel { get; set; } = "";
        public string Vanadium { get; set; } = "";
        public string Iron { get; set; } = "";
        public string Sodium { get; set; } = "";
        public string CCR { get; set; } = "";
        public string Asphaltenes { get; set; } = "";
        public string TAN { get; set; } = "";

        public event PropertyChangedEventHandler? PropertyChanged;
        private void Raise(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }

    private readonly IFlowsheet _flowsheet;
    private readonly IUnitsOfMeasure _su;

    private readonly ObservableCollection<PseudoRow> _rows = new();
    private readonly DataGrid _grid = new() { CanUserSortColumns = false, AutoGenerateColumns = false };
    private readonly TextBlock _status = new() { FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(11), Opacity = 0.85, TextWrapping = TextWrapping.Wrap };

    private readonly List<ConstantProperties> _compounds = new();

    private string _mwMethod = "Riazi (1986)";
    private string _tcMethod = "Riazi-Daubert (1985)";
    private string _pcMethod = "Riazi-Daubert (1985)";
    private string _afMethod = "Lee-Kesler (1976)";

    private Button _btnAdd = null!, _btnExportXml = null!, _btnExportJson = null!;

    public BulkPseudocompoundsWindow(IFlowsheet flowsheet)
    {
        _flowsheet = flowsheet;
        _su = flowsheet.FlowsheetOptions.SelectedUnitSystem;

        Title = "Bulk Add Pseudocompounds";
        Width = 1100;
        Height = 680;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        IconHelper.ApplyWindowIcon(this);

        Content = BuildContent();

        for (int i = 0; i < 10; i++) _rows.Add(new PseudoRow());
    }

    private Control BuildContent()
    {
        void AddCol(string header, string path, double width)
        {
            _grid.Columns.Add(new DataGridTextColumn
            {
                Header = header,
                Binding = new global::Avalonia.Data.Binding(path) { Mode = global::Avalonia.Data.BindingMode.TwoWay },
                Width = new DataGridLength(width)
            });
        }

        AddCol("Name", "Name", 140);
        AddCol("MW", "MW", 80);
        AddCol("NBP (" + _su.temperature + ")", "NBP", 100);
        AddCol("SG", "SG", 80);
        AddCol("Tc (" + _su.temperature + ")", "TC", 100);
        AddCol("Pc (" + _su.pressure + ")", "PC", 100);
        AddCol("AF", "AF", 80);
        AddCol("xP", "XP", 60);
        AddCol("xN", "XN", 60);
        AddCol("xA", "XA", 60);
        AddCol("S wt%", "Sulfur", 70);
        AddCol("N wt%", "Nitrogen", 70);
        AddCol("MercS wt%", "MercaptanS", 90);
        AddCol("Ni ppm", "Nickel", 70);
        AddCol("V ppm", "Vanadium", 70);
        AddCol("Fe ppm", "Iron", 70);
        AddCol("Na ppm", "Sodium", 70);
        AddCol("CCR wt%", "CCR", 80);
        AddCol("Asph wt%", "Asphaltenes", 85);
        AddCol("TAN", "TAN", 70);

        _grid.ItemsSource = _rows;

        var p = new AvaloniaEditorPanel { Width = 320 };

        p.CreateAndAddLabelRow("Estimation Methods");
        p.CreateAndAddDescriptionRow("Used for the columns left blank in the table. Enter at least one of MW, NBP or SG on each row.");

        var mwList = new List<string> { "Riazi (1986)", "Winn (1956)", "Lee-Kesler (1974)" };
        p.CreateAndAddDropDownRow("Molecular Weight", mwList, 0,
            (dd, e) => { if (dd.SelectedIndex >= 0) _mwMethod = mwList[dd.SelectedIndex]; });

        var tcList = new List<string> { "Riazi-Daubert (1985)", "Riazi (2005)", "Lee-Kesler (1976)", "Farah (2006)" };
        p.CreateAndAddDropDownRow("Critical Temperature", tcList, 0,
            (dd, e) => { if (dd.SelectedIndex >= 0) _tcMethod = tcList[dd.SelectedIndex]; });

        var pcList = new List<string> { "Riazi-Daubert (1985)", "Riazi (2005)", "Lee-Kesler (1976)", "Farah (2006)" };
        p.CreateAndAddDropDownRow("Critical Pressure", pcList, 0,
            (dd, e) => { if (dd.SelectedIndex >= 0) _pcMethod = pcList[dd.SelectedIndex]; });

        var afList = new List<string> { "Lee-Kesler (1976)", "Korsten (2000)" };
        p.CreateAndAddDropDownRow("Acentric Factor", afList, 0,
            (dd, e) => { if (dd.SelectedIndex >= 0) _afMethod = afList[dd.SelectedIndex]; });

        p.CreateAndAddLabelRow("Actions");
        p.CreateAndAddButtonRow("Add 10 Empty Rows", null, (_, _) =>
        {
            for (int i = 0; i < 10; i++) _rows.Add(new PseudoRow());
        });
        p.CreateAndAddButtonRow("Paste Table from Clipboard", null, async (_, _) => await PasteAsync());
        p.CreateAndAddButtonRow("Estimate Properties", null, (_, _) => Estimate());

        _btnAdd = p.CreateAndAddButtonRow("Add Compounds to Simulation", null, (_, _) => AddToFlowsheet());
        _btnExportXml = p.CreateAndAddButtonRow("Export to XML Database...", null, async (_, _) => await ExportXmlAsync());
        _btnExportJson = p.CreateAndAddButtonRow("Export to JSON Files...", null, async (_, _) => await ExportJsonAsync());

        _btnAdd.IsEnabled = false;
        _btnExportXml.IsEnabled = false;
        _btnExportJson.IsEnabled = false;

        p.CreateAndAddDescriptionRow("Blank cells are filled by the correlations above and written back to the table. Contaminant columns are optional and stored as compound extra properties.");

        var body = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        Grid.SetColumn(_grid, 0);
        var side = new ScrollViewer { Content = p, Padding = new Thickness(8) };
        Grid.SetColumn(side, 1);
        body.Children.Add(_grid);
        body.Children.Add(side);

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

    // -------------------------------------------------------------------------

    private static double? Parse(string s, int row, string caption)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        if (!double.TryParse(s.Trim(), NumberStyles.Any, CultureInfo.CurrentCulture, out var v) &&
            !double.TryParse(s.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out v))
        {
            throw new Exception($"Error in row {row + 1}: {caption} is not a valid number.");
        }
        return v;
    }

    private void Estimate()
    {
        _compounds.Clear();

        var estimator = new PseudoEstimator
        {
            MWMethod = _mwMethod,
            TcMethod = _tcMethod,
            PcMethod = _pcMethod,
            AFMethod = _afMethod
        };

        try
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                var r = _rows[i];
                if (string.IsNullOrWhiteSpace(r.Name)) continue;

                var input = new PseudoCompoundInput { Name = r.Name.Trim() };

                input.MW = Parse(r.MW, i, "MW");
                input.SG = Parse(r.SG, i, "SG");
                input.AF = Parse(r.AF, i, "AF");
                input.xP = Parse(r.XP, i, "xP");
                input.xN = Parse(r.XN, i, "xN");
                input.xA = Parse(r.XA, i, "xA");

                var nbp = Parse(r.NBP, i, "NBP");
                var tc = Parse(r.TC, i, "Tc");
                var pc = Parse(r.PC, i, "Pc");
                input.NBP = nbp.HasValue ? cv.ConvertToSI(_su.temperature, nbp.Value) : (double?)null;
                input.Tc = tc.HasValue ? cv.ConvertToSI(_su.temperature, tc.Value) : (double?)null;
                input.Pc = pc.HasValue ? cv.ConvertToSI(_su.pressure, pc.Value) : (double?)null;

                input.Contaminants = new double?[]
                {
                    Parse(r.Sulfur, i, "S wt%"), Parse(r.Nitrogen, i, "N wt%"),
                    Parse(r.MercaptanS, i, "MercS wt%"), Parse(r.Nickel, i, "Ni ppm"),
                    Parse(r.Vanadium, i, "V ppm"), Parse(r.Iron, i, "Fe ppm"),
                    Parse(r.Sodium, i, "Na ppm"), Parse(r.CCR, i, "CCR wt%"),
                    Parse(r.Asphaltenes, i, "Asph wt%"), Parse(r.TAN, i, "TAN")
                };

                _compounds.Add(estimator.Estimate(input, i));

                // the estimator writes the filled-in values back
                r.MW = input.MW.GetValueOrDefault().ToString("G6", CultureInfo.CurrentCulture);
                r.NBP = cv.ConvertFromSI(_su.temperature, input.NBP.GetValueOrDefault()).ToString("G6", CultureInfo.CurrentCulture);
                r.SG = input.SG.GetValueOrDefault().ToString("G6", CultureInfo.CurrentCulture);
                r.TC = cv.ConvertFromSI(_su.temperature, input.Tc.GetValueOrDefault()).ToString("G6", CultureInfo.CurrentCulture);
                r.PC = cv.ConvertFromSI(_su.pressure, input.Pc.GetValueOrDefault()).ToString("G6", CultureInfo.CurrentCulture);
                r.AF = input.AF.GetValueOrDefault().ToString("G6", CultureInfo.CurrentCulture);
            }
        }
        catch (Exception ex)
        {
            _compounds.Clear();
            _status.Text = ex.Message;
            _btnAdd.IsEnabled = false;
            _btnExportXml.IsEnabled = false;
            _btnExportJson.IsEnabled = false;
            return;
        }

        var ok = _compounds.Count > 0;
        _btnAdd.IsEnabled = ok;
        _btnExportXml.IsEnabled = ok;
        _btnExportJson.IsEnabled = ok;
        _status.Text = ok
            ? $"{_compounds.Count} pseudocompound(s) estimated."
            : "Enter a name on at least one row.";
    }

    /// <summary>Reads a tab or space separated table from the clipboard into the grid.</summary>
    private async Task PasteAsync()
    {
        var clipboard = Clipboard;
        var text = clipboard == null ? null : await clipboard.GetTextAsync();
        if (string.IsNullOrWhiteSpace(text)) { _status.Text = "The clipboard has no text."; return; }

        var setters = new Action<PseudoRow, string>[]
        {
            (r, v) => r.Name = v, (r, v) => r.MW = v, (r, v) => r.NBP = v, (r, v) => r.SG = v,
            (r, v) => r.TC = v, (r, v) => r.PC = v, (r, v) => r.AF = v,
            (r, v) => r.XP = v, (r, v) => r.XN = v, (r, v) => r.XA = v,
            (r, v) => r.Sulfur = v, (r, v) => r.Nitrogen = v, (r, v) => r.MercaptanS = v,
            (r, v) => r.Nickel = v, (r, v) => r.Vanadium = v, (r, v) => r.Iron = v,
            (r, v) => r.Sodium = v, (r, v) => r.CCR = v, (r, v) => r.Asphaltenes = v, (r, v) => r.TAN = v
        };

        _rows.Clear();
        var count = 0;
        foreach (var line in text.Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var cells = line.Trim().Split(new[] { '\t', ';' }, StringSplitOptions.None);
            if (cells.Length == 1) cells = line.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            var r = new PseudoRow();
            for (int i = 0; i < Math.Min(cells.Length, setters.Length); i++)
                setters[i](r, cells[i].Trim());
            _rows.Add(r);
            count++;
        }

        for (int i = 0; i < 5; i++) _rows.Add(new PseudoRow());
        _status.Text = $"{count} row(s) pasted.";
    }

    // -------------------------------------------------------------------------

    private void AddToFlowsheet()
    {
        try
        {
            foreach (var cprops in _compounds)
            {
                if (!_flowsheet.AvailableCompounds.ContainsKey(cprops.Name))
                    _flowsheet.AvailableCompounds.Add(cprops.Name, cprops);
                if (!_flowsheet.SelectedCompounds.ContainsKey(cprops.Name))
                    _flowsheet.SelectedCompounds.Add(cprops.Name, _flowsheet.AvailableCompounds[cprops.Name]);

                foreach (MaterialStream obj in _flowsheet.SimulationObjects.Values
                             .Where(x => x.GraphicObject != null && x.GraphicObject.ObjectType == ObjectType.MaterialStream))
                {
                    foreach (var phase in obj.Phases.Values)
                    {
                        if (phase.Compounds.ContainsKey(cprops.Name)) continue;
                        phase.Compounds.Add(cprops.Name, new Compound(cprops.Name, ""));
                        phase.Compounds[cprops.Name].ConstantProperties = _flowsheet.SelectedCompounds[cprops.Name];
                    }
                }
            }

            _flowsheet.UpdateInterface();
            _status.Text = $"{_compounds.Count} compound(s) added to the simulation.";
            _flowsheet.ShowMessage(_status.Text, IFlowsheet.MessageType.Information);
        }
        catch (Exception ex)
        {
            _status.Text = "Failed to add the compounds: " + ex.Message;
        }
    }

    private async Task ExportXmlAsync()
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export Pseudocompounds to XML Database",
            SuggestedFileName = "pseudocompounds.xml",
            DefaultExtension = "xml",
            FileTypeChoices = new[] { new FilePickerFileType("XML Compound Database") { Patterns = new[] { "*.xml" } } }
        });

        var path = file?.Path?.LocalPath;
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            if (!File.Exists(path))
            {
                File.WriteAllText(path, "");
                DWSIM.Thermodynamics.Databases.UserDB.CreateNew(path, "compounds");
            }
            using (var stream = new FileStream(path, FileMode.OpenOrCreate))
            {
                DWSIM.Thermodynamics.Databases.UserDB.AddCompounds(_compounds.ToArray(), stream, true);
            }
            _status.Text = "Compounds exported to " + path + ".";
        }
        catch (Exception ex)
        {
            _status.Text = "Export failed: " + ex.Message;
        }
    }

    /// <summary>Writes one Compound Creator JSON file per pseudocompound into a chosen folder.</summary>
    private async Task ExportJsonAsync()
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select the folder for the JSON compound files",
            AllowMultiple = false
        });

        var dir = folders?.FirstOrDefault()?.Path?.LocalPath;
        if (string.IsNullOrEmpty(dir)) return;

        try
        {
            foreach (var comp in _compounds)
            {
                var name = string.Join("_", comp.Name.Split(Path.GetInvalidFileNameChars()));
                File.WriteAllText(Path.Combine(dir, name + ".json"),
                    Newtonsoft.Json.JsonConvert.SerializeObject(comp, Newtonsoft.Json.Formatting.Indented));
            }
            _status.Text = $"{_compounds.Count} JSON file(s) written to {dir}.";
        }
        catch (Exception ex)
        {
            _status.Text = "Export failed: " + ex.Message;
        }
    }
}
