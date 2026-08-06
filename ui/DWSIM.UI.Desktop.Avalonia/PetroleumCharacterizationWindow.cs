using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
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
/// Bulk C7+ petroleum characterization. Avalonia counterpart of
/// DWSIM.UI.Desktop.Editors.BulkC7PCharacterization: drives the engine's GenerateCompounds
/// routine, adds the generated pseudocompounds to the flowsheet and creates a material
/// stream holding the assay composition.
/// </summary>
public sealed class PetroleumCharacterizationWindow : Window
{
    private readonly IFlowsheet _flowsheet;

    private string _assayName = "OIL";
    private int _ncomps = 10;

    private string _mwCorr = "Winn (1956)";
    private string _tcCorr = "Riazi-Daubert (1985)";
    private string _pcCorr = "Riazi-Daubert (1985)";
    private string _afCorr = "Lee-Kesler (1976)";
    private bool _adjustAf = true, _adjustZR = true;

    private double? _mw, _sg, _nbp;
    private double _mw0 = 80.0, _sg0 = 0.70, _nbp0 = 333.0;
    private double _t1 = 38 + 273.15, _t2 = 98.9 + 273.15, _v1, _v2;

    private double _sulfur, _nitrogen, _nickel, _vanadium, _asphaltenes, _water;
    private double _pnaP, _pnaN, _pnaA;

    private readonly TextBlock _status = new() { FontSize = 11, Opacity = 0.85, TextWrapping = global::Avalonia.Media.TextWrapping.Wrap };
    private Button _btnRun = null!;

    public PetroleumCharacterizationWindow(IFlowsheet flowsheet)
    {
        _flowsheet = flowsheet;
        Title = "Petroleum C7+ Characterization";
        Width = 620;
        Height = 700;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        IconHelper.ApplyWindowIcon(this);
        Content = BuildContent();
    }

    private Control BuildContent()
    {
        var su = _flowsheet.FlowsheetOptions.SelectedUnitSystem;
        var nf = _flowsheet.FlowsheetOptions.NumberFormat;

        var p = new AvaloniaEditorPanel();

        p.CreateAndAddLabelRow("Assay Identification");
        p.CreateAndAddStringEditorRow("Assay Name", _assayName, (tb, e) => _assayName = tb.Text ?? "OIL");

        p.CreateAndAddLabelRow("Property Methods");
        p.CreateAndAddDescriptionRow("Select the methods used to calculate the compound properties.");

        var mwList = new List<string> { "Winn (1956)", "Riazi (1986)", "Lee-Kesler (1974)" };
        p.CreateAndAddDropDownRow("Molecular Weight", mwList, 0,
            (dd, e) => { if (dd.SelectedIndex >= 0) _mwCorr = mwList[dd.SelectedIndex]; });

        var tcList = new List<string> { "Riazi-Daubert (1985)", "Riazi (2005)", "Lee-Kesler (1976)", "Farah (2006)" };
        p.CreateAndAddDropDownRow("Critical Temperature", tcList, 0,
            (dd, e) => { if (dd.SelectedIndex >= 0) _tcCorr = tcList[dd.SelectedIndex]; });

        var pcList = new List<string> { "Riazi-Daubert (1985)", "Lee-Kesler (1976)", "Farah (2006)" };
        p.CreateAndAddDropDownRow("Critical Pressure", pcList, 0,
            (dd, e) => { if (dd.SelectedIndex >= 0) _pcCorr = pcList[dd.SelectedIndex]; });

        var afList = new List<string> { "Lee-Kesler (1976)", "Korsten (2000)" };
        p.CreateAndAddDropDownRow("Acentric Factor", afList, 0,
            (dd, e) => { if (dd.SelectedIndex >= 0) _afCorr = afList[dd.SelectedIndex]; });

        p.CreateAndAddCheckBoxRow("Adjust Acentric Factors to match Normal Boiling Temperatures", _adjustAf,
            (cb, e) => _adjustAf = cb.IsChecked.GetValueOrDefault());
        p.CreateAndAddCheckBoxRow("Adjust Rackett Parameters to match Specific Gravities", _adjustZR,
            (cb, e) => _adjustZR = cb.IsChecked.GetValueOrDefault());

        p.CreateAndAddLabelRow("Assay Properties");
        p.CreateAndAddDescriptionRow("Define at least one of the three properties below so a property distribution can be calculated. Leave a value at zero if it is not available.");
        p.CreateAndAddTextBoxRow(nf, "Molar Weight", 0.0,
            (tb, e) => { if (UtilityHelpers.TryVal(tb.Text, out var v)) _mw = v; });
        p.CreateAndAddTextBoxRow(nf, "Specific Gravity", 0.0,
            (tb, e) => { if (UtilityHelpers.TryVal(tb.Text, out var v)) _sg = v; });
        p.CreateAndAddTextBoxRow(nf, "Average NBP (" + su.temperature + ")", 0.0,
            (tb, e) => { if (UtilityHelpers.TryVal(tb.Text, out var v)) _nbp = cv.ConvertToSI(su.temperature, v); });

        p.CreateAndAddLabelRow("Initial Values for Property Distribution");
        p.CreateAndAddTextBoxRow(nf, "Molar Weight", _mw0,
            (tb, e) => { if (UtilityHelpers.TryVal(tb.Text, out var v)) _mw0 = v; });
        p.CreateAndAddDescriptionRow("Molar weight of the lightest compound in the assay.");
        p.CreateAndAddTextBoxRow(nf, "Specific Gravity", _sg0,
            (tb, e) => { if (UtilityHelpers.TryVal(tb.Text, out var v)) _sg0 = v; });
        p.CreateAndAddDescriptionRow("Specific gravity of the lightest compound in the assay.");
        p.CreateAndAddTextBoxRow(nf, "Normal Boiling Point (" + su.temperature + ")",
            cv.ConvertFromSI(su.temperature, _nbp0),
            (tb, e) => { if (UtilityHelpers.TryVal(tb.Text, out var v)) _nbp0 = cv.ConvertToSI(su.temperature, v); });
        p.CreateAndAddDescriptionRow("Normal boiling point of the lightest compound in the assay.");

        p.CreateAndAddLabelRow("Contaminants & PNA Analysis");
        p.CreateAndAddDescriptionRow("Optional bulk contaminant and PNA composition data attached to the generated assay. Used by downstream refinery models; it does not affect C7+ pseudocompound generation.");
        p.CreateAndAddTextBoxRow(nf, "Total Sulfur (wt %)", _sulfur, (tb, e) => { if (UtilityHelpers.TryVal(tb.Text, out var v)) _sulfur = v; });
        p.CreateAndAddTextBoxRow(nf, "Total Nitrogen (wt %)", _nitrogen, (tb, e) => { if (UtilityHelpers.TryVal(tb.Text, out var v)) _nitrogen = v; });
        p.CreateAndAddTextBoxRow(nf, "Nickel (wppm)", _nickel, (tb, e) => { if (UtilityHelpers.TryVal(tb.Text, out var v)) _nickel = v; });
        p.CreateAndAddTextBoxRow(nf, "Vanadium (wppm)", _vanadium, (tb, e) => { if (UtilityHelpers.TryVal(tb.Text, out var v)) _vanadium = v; });
        p.CreateAndAddTextBoxRow(nf, "Asphaltenes (wt %)", _asphaltenes, (tb, e) => { if (UtilityHelpers.TryVal(tb.Text, out var v)) _asphaltenes = v; });
        p.CreateAndAddTextBoxRow(nf, "Water / BSW (vol %)", _water, (tb, e) => { if (UtilityHelpers.TryVal(tb.Text, out var v)) _water = v; });
        p.CreateAndAddTextBoxRow(nf, "Paraffins (wt %)", _pnaP, (tb, e) => { if (UtilityHelpers.TryVal(tb.Text, out var v)) _pnaP = v; });
        p.CreateAndAddTextBoxRow(nf, "Naphthenes (wt %)", _pnaN, (tb, e) => { if (UtilityHelpers.TryVal(tb.Text, out var v)) _pnaN = v; });
        p.CreateAndAddTextBoxRow(nf, "Aromatics (wt %)", _pnaA, (tb, e) => { if (UtilityHelpers.TryVal(tb.Text, out var v)) _pnaA = v; });

        p.CreateAndAddLabelRow("Pseudo Compounds");
        p.CreateAndAddTextBoxRow("N0", "Number of Compounds", _ncomps,
            (tb, e) => { if (UtilityHelpers.TryVal(tb.Text, out var v) && v >= 1) _ncomps = (int)v; });
        p.CreateAndAddDescriptionRow("Number of pseudocompounds that together represent the assay. They are added to the simulation, and a material stream is created holding the distributed amounts.");

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
        if (!_mw.HasValue && !_sg.HasValue && !_nbp.HasValue)
        {
            _status.Text = "Define at least one assay property (molar weight, specific gravity or average NBP).";
            return;
        }

        _btnRun.IsEnabled = false;
        _status.Text = "Generating compounds, please wait...";

        Dictionary<string, ICompound> comps;
        try
        {
            comps = await Task.Run(() => new GenerateCompounds().GenerateCompounds(
                _assayName, _ncomps, _tcCorr, _pcCorr, _afCorr, _mwCorr,
                _adjustAf, _adjustZR, _mw, _sg, _nbp, _v1, _v2, _t1, _t2, _mw0, _sg0, _nbp0));
        }
        catch (Exception ex)
        {
            _status.Text = "Characterization failed: " + (ex.InnerException?.Message ?? ex.Message);
            _btnRun.IsEnabled = true;
            return;
        }

        try
        {
            if (_pnaP > 0 || _pnaN > 0 || _pnaA > 0)
            {
                foreach (var subst in comps.Values)
                {
                    subst.ConstantProperties.BO_PNA_P = _pnaP;
                    subst.ConstantProperties.BO_PNA_N = _pnaN;
                    subst.ConstantProperties.BO_PNA_A = _pnaA;
                }
            }

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

            var ms = (MaterialStream)_flowsheet.AddObject(ObjectType.MaterialStream, 100, 100, _assayName);

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

            _flowsheet.UpdateInterface();
            _status.Text = $"Material stream '{_assayName}' added with {comps.Count} generated compound(s).";
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

    /// <summary>Optional export of the generated pseudocompounds to a user compound database.</summary>
    private async Task OfferXmlExportAsync(Dictionary<string, ICompound> comps)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export Generated Compounds to XML Database (Cancel to skip)",
            SuggestedFileName = _assayName + ".xml",
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
