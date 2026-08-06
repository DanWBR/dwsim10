using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using DWSIM.ExtensionMethods;
using DWSIM.Interfaces;
using DWSIM.Interfaces.Enums.GraphicObjects;
using DWSIM.Thermodynamics.BaseClasses;
using DWSIM.Thermodynamics.Streams;
using DWSIM.UI.Shared.Avalonia;

namespace DWSIM.UI.Desktop.Avalonia;

/// <summary>
/// Characterizes a petroleum assay from a distillation curve. Avalonia counterpart of
/// DWSIM.UI.Desktop.Editors.DistCurvePCharacterization; both drive the shared
/// DistCurveCharacterizer.
/// </summary>
public sealed class DistillationCurveWindow : Window
{
    private readonly IFlowsheet _flowsheet;
    private readonly DWSIM.UI.Desktop.Editors.DistCurveCharacterizer _c = new();

    private ComboBox _curveType = null!, _curveBasis = null!, _decSep1 = null!, _decSep2 = null!, _pseudoMode = null!;
    private CheckBox _chkMW = null!, _chkSG = null!, _chkV100 = null!, _chkV210 = null!;
    private TextBox _curveData = null!, _pseudoData = null!;

    private readonly TextBlock _status = new() { FontSize = 11, Opacity = 0.85, TextWrapping = TextWrapping.Wrap };
    private Button _btnRun = null!;

    public DistillationCurveWindow(IFlowsheet flowsheet)
    {
        _flowsheet = flowsheet;
        _c.Flowsheet = flowsheet;
        _c.OnError = (m) => flowsheet.ShowMessage(m, IFlowsheet.MessageType.GeneralError);
        _c.assayname = "MyAssay" + new Random().Next(1, 100).ToString("000");

        Title = "Distillation Curve Characterization";
        Width = 640;
        Height = 760;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        IconHelper.ApplyWindowIcon(this);
        Content = BuildContent();
    }

    private Control BuildContent()
    {
        var su = _flowsheet.FlowsheetOptions.SelectedUnitSystem;
        var nf = _flowsheet.FlowsheetOptions.NumberFormat;

        var p = new AvaloniaEditorPanel();

        p.CreateAndAddLabelRow("Assay Information");
        p.CreateAndAddStringEditorRow("Assay Name", _c.assayname, (tb, e) => _c.assayname = tb.Text ?? "MyAssay");
        p.CreateAndAddDescriptionRow("Enter the name of the assay. It will be used to identify the Material Stream on the flowsheet and the associated compounds as well.");

        p.CreateAndAddLabelRow("Property Methods");
        p.CreateAndAddDescriptionRow("Select the methods to calculate compound properties.");

        var mwList = new List<string> { "Winn (1956)", "Riazi (1986)", "Lee-Kesler (1974)" };
        p.CreateAndAddDropDownRow("Molecular Weight", mwList, 0,
            (dd, e) => { if (dd.SelectedIndex >= 0) _c.MWcorr = mwList[dd.SelectedIndex]; });

        var tcList = new List<string> { "Riazi-Daubert (1985)", "Riazi (2005)", "Lee-Kesler (1976)", "Farah (2006)" };
        p.CreateAndAddDropDownRow("Critical Temperature", tcList, 0,
            (dd, e) => { if (dd.SelectedIndex >= 0) _c.Tccorr = tcList[dd.SelectedIndex]; });

        var pcList = new List<string> { "Riazi-Daubert (1985)", "Lee-Kesler (1976)", "Farah (2006)" };
        p.CreateAndAddDropDownRow("Critical Pressure", pcList, 0,
            (dd, e) => { if (dd.SelectedIndex >= 0) _c.Pccorr = pcList[dd.SelectedIndex]; });

        var afList = new List<string> { "Lee-Kesler (1976)", "Korsten (2000)" };
        p.CreateAndAddDropDownRow("Acentric Factor", afList, 0,
            (dd, e) => { if (dd.SelectedIndex >= 0) _c.AFcorr = afList[dd.SelectedIndex]; });

        p.CreateAndAddCheckBoxRow("Adjust Acentric Factors to match Normal Boiling Temperatures", _c.adjustAf,
            (cb, e) => _c.adjustAf = cb.IsChecked.GetValueOrDefault());
        p.CreateAndAddCheckBoxRow("Adjust Rackett Parameters to match Specific Gravities", _c.adjustZR,
            (cb, e) => _c.adjustZR = cb.IsChecked.GetValueOrDefault());

        p.CreateAndAddLabelRow("Setup Curves");

        _curveType = p.CreateAndAddDropDownRow("Boiling Point Curve Type",
            new List<string> { "TBP, ASTM D2892", "ASTM D86", "ASTM D1160", "ASTM D2887" }, 0, null);

        _chkMW = p.CreateAndAddCheckBoxRow("Molar Weight", false, null);
        _chkSG = p.CreateAndAddCheckBoxRow("Specific Gravity", false, null);
        _chkV100 = p.CreateAndAddCheckBoxRow("Kinematic Viscosity @ 100 F", false, null);
        _chkV210 = p.CreateAndAddCheckBoxRow("Kinematic Viscosity @ 210 F", false, null);

        _curveBasis = p.CreateAndAddDropDownRow("Curve Basis",
            new List<string> { "Liquid Volume (%)", "Molar (%)", "Mass (%)" }, 0, null);

        p.CreateAndAddLabelRow("Bulk Sample Data");
        p.CreateAndAddTextBoxRow(nf, "Specific Gravity", _c.sgb,
            (tb, e) => { if (UtilityHelpers.TryVal(tb.Text, out var v)) _c.sgb = v; });
        p.CreateAndAddDescriptionRow("Leave it unchanged if not available.");
        p.CreateAndAddTextBoxRow(nf, "Molar Weight", _c.mwb,
            (tb, e) => { if (UtilityHelpers.TryVal(tb.Text, out var v)) _c.mwb = v; });
        p.CreateAndAddDescriptionRow("Leave it unchanged if not available.");

        p.CreateAndAddLabelRow("Contaminants & PNA Analysis");
        p.CreateAndAddDescriptionRow("Optional bulk contaminant and PNA composition data attached to the generated assay.");
        p.CreateAndAddTextBoxRow(nf, "Total Sulfur (wt %)", _c.bulkSulfur, (tb, e) => { if (UtilityHelpers.TryVal(tb.Text, out var v)) _c.bulkSulfur = v; });
        p.CreateAndAddTextBoxRow(nf, "Total Nitrogen (wt %)", _c.bulkNitrogen, (tb, e) => { if (UtilityHelpers.TryVal(tb.Text, out var v)) _c.bulkNitrogen = v; });
        p.CreateAndAddTextBoxRow(nf, "Nickel (wppm)", _c.bulkNickel, (tb, e) => { if (UtilityHelpers.TryVal(tb.Text, out var v)) _c.bulkNickel = v; });
        p.CreateAndAddTextBoxRow(nf, "Vanadium (wppm)", _c.bulkVanadium, (tb, e) => { if (UtilityHelpers.TryVal(tb.Text, out var v)) _c.bulkVanadium = v; });
        p.CreateAndAddTextBoxRow(nf, "Asphaltenes (wt %)", _c.bulkAsphaltenes, (tb, e) => { if (UtilityHelpers.TryVal(tb.Text, out var v)) _c.bulkAsphaltenes = v; });
        p.CreateAndAddTextBoxRow(nf, "Water / BSW (vol %)", _c.bulkWater, (tb, e) => { if (UtilityHelpers.TryVal(tb.Text, out var v)) _c.bulkWater = v; });
        p.CreateAndAddTextBoxRow(nf, "Paraffins (wt %)", _c.pnaParaffins, (tb, e) => { if (UtilityHelpers.TryVal(tb.Text, out var v)) _c.pnaParaffins = v; });
        p.CreateAndAddTextBoxRow(nf, "Naphthenes (wt %)", _c.pnaNaphthenes, (tb, e) => { if (UtilityHelpers.TryVal(tb.Text, out var v)) _c.pnaNaphthenes = v; });
        p.CreateAndAddTextBoxRow(nf, "Aromatics (wt %)", _c.pnaAromatics, (tb, e) => { if (UtilityHelpers.TryVal(tb.Text, out var v)) _c.pnaAromatics = v; });

        p.CreateAndAddLabelRow("Curve Data");
        p.CreateAndAddDescriptionRow("Enter curve data in the field below, separating the column values with spaces. Values should be input without thousands separator. First column is the curve basis data, following columns should contain the data according to the curve selection above.");
        p.CreateAndAddDescriptionRow("Current Temperature units: " + su.temperature);
        p.CreateAndAddDescriptionRow("Current Kinematic Viscosity units: " + su.cinematic_viscosity);

        _decSep1 = p.CreateAndAddDropDownRow("Decimal Separator", new List<string> { "Dot (.)", "Comma (,)" }, 0, null);
        _curveData = p.CreateAndAddMultilineMonoSpaceTextBoxRow("", 200, false, null);

        p.CreateAndAddLabelRow("Pseudo Compounds");
        p.CreateAndAddDescriptionRow("Select the method to be used for generation of pseudo compounds (petroleum fractions).");

        _pseudoMode = p.CreateAndAddDropDownRow("Pseudo Cut Type",
            new List<string> { "Defined Number", "Defined Cut Temperatures" }, 0, null);

        p.CreateAndAddDescriptionRow("Enter the number of pseudo compounds in the field below if you selected the first option in the selector above, or the cut temperatures in the current temperature units, separated by spaces and without thousands separator. Do not include the maximum and minimum temperatures on the list.");

        _decSep2 = p.CreateAndAddDropDownRow("Decimal Separator", new List<string> { "Dot (.)", "Comma (,)" }, 0, null);
        _pseudoData = p.CreateAndAddFullTextBoxRow("10", null);

        _btnRun = new Button
        {
            Content = "Characterize Assay and Create Compounds",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(8)
        };
        _btnRun.Classes.Add("action");
        _btnRun.Click += async (_, _) => await CharacterizeAsync();

        var bottom = new StackPanel { Margin = new Thickness(8, 0, 8, 8), Spacing = 4 };
        bottom.Children.Add(_status);

        var dock = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(_btnRun, global::Avalonia.Controls.Dock.Bottom);
        DockPanel.SetDock(bottom, global::Avalonia.Controls.Dock.Bottom);
        dock.Children.Add(bottom);
        dock.Children.Add(_btnRun);
        dock.Children.Add(new ScrollViewer { Content = p, Padding = new Thickness(8) });
        return dock;
    }

    // -------------------------------------------------------------------------

    private async Task CharacterizeAsync()
    {
        var su = _flowsheet.FlowsheetOptions.SelectedUnitSystem;

        _c.tbpcurvetype = Math.Max(0, _curveType.SelectedIndex);
        _c.curvebasis = Math.Max(0, _curveBasis.SelectedIndex);
        _c.pseudomode = Math.Max(0, _pseudoMode.SelectedIndex);
        _c.hasmwc = _chkMW.IsChecked.GetValueOrDefault();
        _c.hassgc = _chkSG.IsChecked.GetValueOrDefault();
        _c.hasvisc100c = _chkV100.IsChecked.GetValueOrDefault();
        _c.hasvisc210c = _chkV210.IsChecked.GetValueOrDefault();
        _c.decsep = _decSep1.SelectedIndex == 1 ? "," : ".";
        var decsep2 = _decSep2.SelectedIndex == 1 ? "," : ".";

        try
        {
            _c.ParseCurveData((_curveData.Text ?? "").Split('\n'), su);
        }
        catch (Exception ex)
        {
            _status.Text = "Error parsing curve data: " + ex.Message;
            return;
        }

        if (_c.cb.Count < 2)
        {
            _status.Text = "Enter at least two curve points.";
            return;
        }

        try
        {
            if (_c.pseudomode == 0)
            {
                _c.pseudocuts = int.Parse((_pseudoData.Text ?? "").Trim());
            }
            else
            {
                _c.cuttemps = (_pseudoData.Text ?? "").Trim()
                    .Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(t => t.ToDoubleWithSeparator(decsep2)).ToList();
            }
        }
        catch (Exception ex)
        {
            _status.Text = "Error parsing pseudo compound cuts: " + ex.Message;
            return;
        }

        _btnRun.IsEnabled = false;
        _status.Text = "Generating compounds, please wait...";

        Dictionary<string, Compound> comps;
        try
        {
            comps = await Task.Run(() => _c.GenerateCompounds(su));
        }
        catch (Exception ex)
        {
            _status.Text = "Characterization failed: " + (ex.InnerException?.Message ?? ex.Message);
            _btnRun.IsEnabled = true;
            return;
        }

        try
        {
            if (!await ConfirmQualityCheckAsync(comps))
            {
                _status.Text = "Characterization discarded.";
                return;
            }

            AddToFlowsheet(comps);
            _flowsheet.UpdateInterface();
            _status.Text = $"Material stream '{_c.assayname}' added with {comps.Count} generated compound(s).";
            _flowsheet.ShowMessage(_status.Text, IFlowsheet.MessageType.Information);

            await OfferXmlExportAsync(comps);
        }
        catch (Exception ex)
        {
            _status.Text = "Failed to add the generated compounds: " + ex.Message;
        }
        finally
        {
            _btnRun.IsEnabled = true;
        }
    }

    /// <summary>
    /// Runs the engine quality check against the assay and shows its report, so the generated
    /// compounds can be discarded before they reach the flowsheet.
    /// </summary>
    private async Task<bool> ConfirmQualityCheckAsync(Dictionary<string, Compound> comps)
    {
        string report;
        try
        {
            var assay = _c.BuildAssay();
            var ms = new MaterialStream("", "");
            ms.SetFlowsheet(_flowsheet);
            ms.SetPropertyPackage(_flowsheet.PropertyPackages.Count > 0
                ? _flowsheet.PropertyPackages.Values.First()
                : new DWSIM.Thermodynamics.PropertyPackages.PengRobinsonPropertyPackage());
            foreach (var subst in comps.Values)
            {
                ms.Phases[0].Compounds.Add(subst.Name, subst);
                for (int i = 1; i <= 7; i++)
                {
                    ms.Phases[i].Compounds.Add(subst.Name,
                        new Compound(subst.Name, "") { ConstantProperties = subst.ConstantProperties });
                }
            }
            var qc = new DWSIM.Thermodynamics.QualityCheck(assay, ms);
            qc.DoQualityCheck();
            report = qc.GetQualityCheckReport();
        }
        catch (Exception ex)
        {
            report = "The quality check could not be completed: " + ex.Message;
        }

        var dlg = new Window
        {
            Title = "Assay Quality Check",
            Width = 720,
            Height = 560,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        IconHelper.ApplyWindowIcon(dlg);

        var text = new TextBox
        {
            Text = report,
            IsReadOnly = true,
            AcceptsReturn = true,
            FontFamily = new FontFamily("Consolas,Courier New,monospace"),
            FontSize = 11
        };

        var accept = new Button { Content = "Add Compounds", Width = 140, IsDefault = true };
        var cancel = new Button { Content = "Discard", Width = 100, IsCancel = true };
        accept.Classes.Add("dialog");
        cancel.Classes.Add("dialog");
        accept.Click += (_, _) => dlg.Close(true);
        cancel.Click += (_, _) => dlg.Close(false);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(8)
        };
        buttons.Children.Add(cancel);
        buttons.Children.Add(accept);

        var root = new DockPanel();
        DockPanel.SetDock(buttons, global::Avalonia.Controls.Dock.Bottom);
        root.Children.Add(buttons);
        root.Children.Add(new ScrollViewer { Content = text, Padding = new Thickness(8) });
        dlg.Content = root;

        return await dlg.ShowDialog<bool>(this);
    }

    private void AddToFlowsheet(Dictionary<string, Compound> comps)
    {
        foreach (var comp in comps.Values)
        {
            if (!_flowsheet.AvailableCompounds.ContainsKey(comp.Name))
                _flowsheet.AvailableCompounds.Add(comp.Name, comp.ConstantProperties);
            if (!_flowsheet.SelectedCompounds.ContainsKey(comp.Name))
                _flowsheet.SelectedCompounds.Add(comp.Name, _flowsheet.AvailableCompounds[comp.Name]);

            foreach (MaterialStream obj in _flowsheet.SimulationObjects.Values
                         .Where(x => x.GraphicObject != null && x.GraphicObject.ObjectType == ObjectType.MaterialStream))
            {
                foreach (var phase in obj.Phases.Values)
                {
                    if (phase.Compounds.ContainsKey(comp.Name)) continue;
                    phase.Compounds.Add(comp.Name, new Compound(comp.Name, ""));
                    phase.Compounds[comp.Name].ConstantProperties = _flowsheet.SelectedCompounds[comp.Name];
                }
            }
        }

        var ms = (MaterialStream)_flowsheet.AddObject(ObjectType.MaterialStream, 100, 100, _c.assayname);

        double wtotal = comps.Values
            .Select(x => x.MoleFraction.GetValueOrDefault() * x.ConstantProperties.Molar_Weight).Sum();

        foreach (var c in ms.Phases[0].Compounds.Values) { c.MassFraction = 0.0; c.MoleFraction = 0.0; }
        foreach (var c in comps.Values)
        {
            c.MassFraction = wtotal > 0
                ? c.MoleFraction.GetValueOrDefault() * c.ConstantProperties.Molar_Weight / wtotal
                : 0.0;
            ms.Phases[0].Compounds[c.Name].MassFraction = c.MassFraction.GetValueOrDefault();
            ms.Phases[0].Compounds[c.Name].MoleFraction = c.MoleFraction.GetValueOrDefault();
        }
    }

    private async Task OfferXmlExportAsync(Dictionary<string, Compound> comps)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export Generated Compounds to XML Database (Cancel to skip)",
            SuggestedFileName = _c.assayname + ".xml",
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
                DWSIM.Thermodynamics.Databases.UserDB.AddCompounds(
                    comps.Values.Select(x => x.ConstantProperties).ToArray(), stream, true);
            }
            _status.Text += " Compounds exported to " + path + ".";
        }
        catch (Exception ex)
        {
            _status.Text += " Export failed: " + ex.Message;
        }
    }
}
