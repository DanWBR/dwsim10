using System;
using System.Collections.Generic;
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
using DWSIM.Thermodynamics.Utilities.Biomass;
using DWSIM.UI.Shared.Avalonia;
using Newtonsoft.Json;

namespace DWSIM.UI.Desktop.Avalonia;

/// <summary>
/// Creates a pseudo-compound representing a biomass entity from an elemental formula.
/// Avalonia counterpart of the WinForms BiomassCompoundCreator; both drive the same
/// <see cref="BiomassCompoundBuilder"/>.
/// </summary>
public sealed class BiomassCompoundCreatorWindow : Window
{
    private readonly IFlowsheet? _flowsheet;

    private readonly TextBox _tbName = new() { Text = "Biomass_Custom" };
    private readonly TextBox _tbFormula = new() { Text = "C100H180O50N20S0.5" };
    private readonly ComboBox _cbType = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly TextBox _tbCmols = new() { Text = "100" };

    private readonly TextBlock _lblMW = new() { Text = "-", FontFamily = new FontFamily("Consolas,Courier New,monospace") };
    private readonly TextBlock _lblGamma = new() { Text = "-", FontFamily = new FontFamily("Consolas,Courier New,monospace") };
    private readonly TextBlock _lblPerCmol = new() { Text = "-", FontFamily = new FontFamily("Consolas,Courier New,monospace") };
    private readonly TextBlock _lblDHf = new() { Text = "-", FontFamily = new FontFamily("Consolas,Courier New,monospace") };

    private readonly TextBox _tbMuMax = new() { Text = "0.3" };
    private readonly TextBox _tbKs = new() { Text = "0.5" };
    private readonly TextBox _tbYXS = new() { Text = "0.5" };
    private readonly TextBox _tbYPS = new() { Text = "0.0" };
    private readonly TextBox _tbMs = new() { Text = "0.02" };
    private readonly TextBox _tbKd = new() { Text = "0.005" };
    private readonly TextBox _tbComments = new() { Text = "User-created biomass compound." };

    private readonly TextBox _tbOutput = new();
    private readonly TextBlock _status = new()
    {
        FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(11), Opacity = 0.85, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0)
    };

    public BiomassCompoundCreatorWindow(IFlowsheet? flowsheet = null)
    {
        _flowsheet = flowsheet;

        Title = "Biomass Compound Creator";
        Width = 640;
        Height = 760;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        IconHelper.ApplyWindowIcon(this);

        _tbOutput.Text = DefaultOutputFile();
        Content = BuildContent();
        Compute();
    }

    // -------------------------------------------------------------------------

    private Control BuildContent()
    {
        foreach (var t in BiomassCompoundBuilder.BiomassTypes) _cbType.Items.Add(t);
        _cbType.SelectedIndex = 0;

        var p = new AvaloniaEditorPanel();

        p.CreateAndAddLabelRow("Identification");
        p.CreateAndAddLabelAndControlRow("Name", _tbName);
        p.CreateAndAddDescriptionRow(
            "Unique name for the pseudo-compound, also the default JSON file name. Avoid spaces and characters that are invalid in a file name.");

        p.CreateAndAddLabelAndControlRow("Elemental Formula (C,H,O,N,S)", _tbFormula);
        p.CreateAndAddDescriptionRow(
            "Overall elemental composition of one mol of biomass. Subscripts may be integers or decimals (C1H1.8O0.5N0.2S0.01). Sulfur is optional.");

        p.CreateAndAddLabelAndControlRow("Biomass Type", _cbType);
        p.CreateAndAddDescriptionRow(
            "Category tag stored in ExtraProperties. The Bioreactor editor uses it to pre-select sensible kinetic defaults.");

        p.CreateAndAddLabelAndControlRow("C-mols per mol", _tbCmols);
        p.CreateAndAddDescriptionRow(
            "Scaling factor for the formula: 1 means it is already written per C-mol (CH1.8O0.5N0.2); 10 or 100 allow integer coefficients.");

        p.CreateAndAddLabelRow("Derived Properties");
        p.CreateAndAddLabelAndControlRow("Molar Weight (g/mol)", _lblMW);
        p.CreateAndAddLabelAndControlRow("Degree of Reduction", _lblGamma);
        p.CreateAndAddDescriptionRow(
            "Per C-mol, referenced to CO2 / H2O / NH3 / SO4 (Roels-Heijnen): 4 + H/C - 2 O/C - 3 N/C + 6 S/C. " +
            "Carbohydrate is about 4.00, yeast and bacteria about 4.2, lipid-rich biomass above 5.");
        p.CreateAndAddLabelAndControlRow("Per C-mol Formula", _lblPerCmol);
        p.CreateAndAddLabelAndControlRow("Enthalpy of Formation (kJ/kg)", _lblDHf);

        var btnCompute = new Button { Content = "Compute", HorizontalAlignment = HorizontalAlignment.Stretch };
        btnCompute.Classes.Add("panel");
        btnCompute.Click += (_, _) => Compute();
        p.CreateAndAddControlRow(btnCompute);

        p.CreateAndAddLabelRow("Kinetic Defaults");
        p.CreateAndAddLabelAndControlRow("mu_max (1/h)", _tbMuMax);
        p.CreateAndAddDescriptionRow("Maximum specific growth rate. E. coli about 0.6-1.0, S. cerevisiae 0.3-0.5, CHO 0.03-0.06.");
        p.CreateAndAddLabelAndControlRow("Ks (g/L)", _tbKs);
        p.CreateAndAddDescriptionRow("Half-saturation substrate concentration for the Monod / Contois / Moser / Haldane models.");
        p.CreateAndAddLabelAndControlRow("Y_XS (g/g)", _tbYXS);
        p.CreateAndAddDescriptionRow("Biomass-on-substrate yield. Aerobic glucose fermentation is typically 0.40-0.55.");
        p.CreateAndAddLabelAndControlRow("Y_PS (g/g)", _tbYPS);
        p.CreateAndAddDescriptionRow("Product-on-substrate yield. Leave at zero when there is no primary extracellular product.");
        p.CreateAndAddLabelAndControlRow("Maintenance (g/g/h)", _tbMs);
        p.CreateAndAddDescriptionRow("Substrate consumed for non-growth activities per g cell per hour.");
        p.CreateAndAddLabelAndControlRow("Death Rate (1/h)", _tbKd);
        p.CreateAndAddDescriptionRow("First-order endogenous decay. The net specific rate is mu minus this value.");
        p.CreateAndAddLabelAndControlRow("Comments", _tbComments);

        p.CreateAndAddLabelRow("Output");
        p.CreateAndAddLabelAndControlRow("File", _tbOutput);
        p.CreateAndAddDescriptionRow(
            "Full path of the compound JSON. It defaults to the application's addcomps folder; DWSIM has to be restarted to pick up a new compound.");

        var btnBrowse = new Button { Content = "Browse...", HorizontalAlignment = HorizontalAlignment.Stretch };
        btnBrowse.Classes.Add("panel");
        btnBrowse.Click += async (_, _) => await BrowseAsync();
        p.CreateAndAddControlRow(btnBrowse);

        var btnSave = new Button
        {
            Content = "Save Biomass Compound JSON",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(8)
        };
        btnSave.Classes.Add("action");
        btnSave.Click += (_, _) => Save();

        var bottom = new StackPanel { Margin = new Thickness(8, 0, 8, 8) };
        bottom.Children.Add(_status);

        var dock = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(btnSave, global::Avalonia.Controls.Dock.Bottom);
        DockPanel.SetDock(bottom, global::Avalonia.Controls.Dock.Bottom);
        dock.Children.Add(bottom);
        dock.Children.Add(btnSave);
        dock.Children.Add(new ScrollViewer { Content = p, Padding = new Thickness(8) });
        return dock;
    }

    // -------------------------------------------------------------------------

    private void Compute()
    {
        var c = BiomassCompoundBuilder.ParseFormula(_tbFormula.Text ?? "");
        if (!c.IsValid)
        {
            _lblMW.Text = _lblGamma.Text = _lblPerCmol.Text = _lblDHf.Text = "-";
            _status.Text = "Invalid formula: no carbon found.";
            return;
        }

        _lblMW.Text = BiomassCompoundBuilder.MolarWeight(c).ToString("N2", CultureInfo.CurrentCulture);
        _lblGamma.Text = BiomassCompoundBuilder.DegreeOfReduction(c).ToString("N2", CultureInfo.CurrentCulture);
        _lblPerCmol.Text = BiomassCompoundBuilder.FormulaPerCmol(c);
        _lblDHf.Text = BiomassCompoundBuilder.EnthalpyOfFormation_kJkg(c).ToString("N1", CultureInfo.CurrentCulture);
        _status.Text = "";
    }

    private void Save()
    {
        try
        {
            var kinetics = new BiomassKineticDefaults
            {
                MuMax_h = Val(_tbMuMax, 0.3),
                Ks_gL = Val(_tbKs, 0.5),
                YXS = Val(_tbYXS, 0.5),
                YPS = Val(_tbYPS, 0.0),
                Maintenance = Val(_tbMs, 0.02),
                DeathRate_h = Val(_tbKd, 0.005)
            };

            var name = string.IsNullOrWhiteSpace(_tbName.Text) ? "Biomass_Custom" : _tbName.Text!.Trim();

            var compound = BiomassCompoundBuilder.BuildCompound(
                name,
                _tbFormula.Text ?? "",
                _cbType.SelectedItem as string ?? "Generic",
                Val(_tbCmols, 0.0),
                _tbComments.Text ?? "",
                kinetics);

            var path = ResolveOutputPath(name);
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

            File.WriteAllText(path, JsonConvert.SerializeObject(compound, Formatting.Indented));
            _tbOutput.Text = path;

            _status.Text = "Saved to " + path + ". Restart DWSIM to load the new compound.";
            _flowsheet?.ShowMessage("Biomass compound saved to " + path + ".",
                IFlowsheet.MessageType.Information);
        }
        catch (Exception ex)
        {
            _status.Text = "Could not save: " + ex.Message;
        }
    }

    /// <summary>Fills in a file name when the box holds a folder, and a .json extension when missing.</summary>
    private string ResolveOutputPath(string name)
    {
        var path = (_tbOutput.Text ?? "").Trim();
        if (string.IsNullOrEmpty(path)) path = Path.Combine(DefaultAddcompsPath(), name + ".json");
        else if (Directory.Exists(path)) path = Path.Combine(path, name + ".json");
        if (string.IsNullOrEmpty(Path.GetExtension(path))) path += ".json";
        return path;
    }

    private async Task BrowseAsync()
    {
        var name = string.IsNullOrWhiteSpace(_tbName.Text) ? "Biomass_Custom" : _tbName.Text!.Trim();
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Biomass Compound JSON",
            SuggestedFileName = name + ".json",
            DefaultExtension = "json",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("DWSIM Compound JSON") { Patterns = new[] { "*.json" } }
            }
        });

        var path = file?.Path?.LocalPath;
        if (!string.IsNullOrEmpty(path)) _tbOutput.Text = path;
    }

    private static string DefaultAddcompsPath()
    {
        var exeDir = AppContext.BaseDirectory;
        var p = Path.Combine(exeDir, "addcomps");
        return Directory.Exists(p) ? p : exeDir;
    }

    private string DefaultOutputFile()
    {
        var name = string.IsNullOrWhiteSpace(_tbName.Text) ? "Biomass_Custom" : _tbName.Text!.Trim();
        return Path.Combine(DefaultAddcompsPath(), name + ".json");
    }

    private static double Val(TextBox tb, double fallback)
        => UtilityHelpers.TryVal(tb.Text, out var v) ? v : fallback;
}
