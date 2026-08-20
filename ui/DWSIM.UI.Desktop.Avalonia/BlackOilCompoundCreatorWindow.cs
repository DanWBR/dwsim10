using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using DWSIM.Interfaces;
using DWSIM.Thermodynamics.Utilities.BlackOil;
using DWSIM.UI.Shared.Avalonia;
using Newtonsoft.Json;

namespace DWSIM.UI.Desktop.Avalonia;

/// <summary>
/// Creates a BLACK-OIL pseudo-compound from black-oil parameters (oil API/SG, gas SG, GOR, BSW and
/// optional oil viscosity). Avalonia counterpart of the WinForms BlackOilCompoundCreator; both drive
/// the same <see cref="BlackOilCompoundBuilder"/>.
/// </summary>
public sealed class BlackOilCompoundCreatorWindow : Window
{
    private readonly IFlowsheet? _flowsheet;

    private readonly TextBox _tbName = new() { Text = "BlackOil_Custom" };
    private readonly TextBox _tbAPI = new() { Text = "32" };
    private readonly TextBox _tbGasSG = new() { Text = "0.75" };
    private readonly TextBox _tbGOR = new() { Text = "90" };
    private readonly TextBox _tbBSW = new() { Text = "0" };

    private readonly TextBlock _lblSG = new() { Text = "-", FontFamily = new FontFamily("Consolas,Courier New,monospace") };
    private readonly TextBlock _lblMW = new() { Text = "-", FontFamily = new FontFamily("Consolas,Courier New,monospace") };
    private readonly TextBlock _lblNBP = new() { Text = "-", FontFamily = new FontFamily("Consolas,Courier New,monospace") };

    private readonly TextBox _tbVisc1 = new() { Text = "0" };
    private readonly TextBox _tbViscT1 = new() { Text = "20" };
    private readonly TextBox _tbVisc2 = new() { Text = "0" };
    private readonly TextBox _tbViscT2 = new() { Text = "50" };
    private readonly TextBox _tbComments = new() { Text = "User-created black-oil compound." };

    private readonly TextBox _tbOutput = new();
    private readonly TextBlock _status = new()
    {
        FontSize = 11, Opacity = 0.85, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0)
    };

    public BlackOilCompoundCreatorWindow(IFlowsheet? flowsheet = null)
    {
        _flowsheet = flowsheet;

        Title = "Black Oil Compound Creator";
        Width = 640;
        Height = 720;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        IconHelper.ApplyWindowIcon(this);

        _tbOutput.Text = DefaultOutputFile();
        Content = BuildContent();
        Compute();
    }

    // -------------------------------------------------------------------------

    private Control BuildContent()
    {
        var p = new AvaloniaEditorPanel();

        p.CreateAndAddLabelRow("Identification");
        p.CreateAndAddLabelAndControlRow("Name", _tbName);
        p.CreateAndAddDescriptionRow(
            "Unique name for the pseudo-compound, also the default JSON file name. Avoid spaces and characters that are invalid in a file name.");

        p.CreateAndAddLabelRow("Black Oil Properties");
        p.CreateAndAddLabelAndControlRow("Oil Gravity (°API)", _tbAPI);
        p.CreateAndAddDescriptionRow(
            "Stock-tank oil gravity in degrees API. Oil specific gravity = 141.5 / (API + 131.5). Light > 31.1, medium 22.3-31.1, heavy < 22.3.");
        p.CreateAndAddLabelAndControlRow("Gas Specific Gravity (air=1)", _tbGasSG);
        p.CreateAndAddDescriptionRow("Produced-gas specific gravity relative to air. Typical associated gas 0.65-0.85.");
        p.CreateAndAddLabelAndControlRow("GOR (m³/m³ STD)", _tbGOR);
        p.CreateAndAddDescriptionRow("Solution / producing gas-oil ratio at standard conditions, m3 gas per m3 stock-tank oil.");
        p.CreateAndAddLabelAndControlRow("BSW / Water Cut (%)", _tbBSW);
        p.CreateAndAddDescriptionRow("Basic sediment & water: water volume fraction of the produced liquid, 0-100 %. Blended as IAPWS water by the PP.");

        p.CreateAndAddLabelRow("Derived Properties");
        p.CreateAndAddLabelAndControlRow("Oil Specific Gravity", _lblSG);
        p.CreateAndAddLabelAndControlRow("Molar Weight (g/mol)", _lblMW);
        p.CreateAndAddLabelAndControlRow("Normal Boiling Point (K)", _lblNBP);

        var btnCompute = new Button { Content = "Compute", HorizontalAlignment = HorizontalAlignment.Stretch };
        btnCompute.Classes.Add("panel");
        btnCompute.Click += (_, _) => Compute();
        p.CreateAndAddControlRow(btnCompute);

        p.CreateAndAddLabelRow("Oil Viscosity (optional)");
        p.CreateAndAddLabelAndControlRow("Viscosity 1 (cSt)", _tbVisc1);
        p.CreateAndAddDescriptionRow("Optional measured oil kinematic viscosity (centistokes) at temperature 1. Leave 0 to use the Beggs-Robinson correlation.");
        p.CreateAndAddLabelAndControlRow("Temperature 1 (°C)", _tbViscT1);
        p.CreateAndAddLabelAndControlRow("Viscosity 2 (cSt)", _tbVisc2);
        p.CreateAndAddDescriptionRow("Optional second viscosity point for Twu interpolation. Requires both points non-zero at different temperatures.");
        p.CreateAndAddLabelAndControlRow("Temperature 2 (°C)", _tbViscT2);
        p.CreateAndAddLabelAndControlRow("Comments", _tbComments);

        p.CreateAndAddLabelRow("Output");
        p.CreateAndAddLabelAndControlRow("File", _tbOutput);
        p.CreateAndAddDescriptionRow(
            "Full path of the compound JSON. Defaults to the application's addcomps folder; DWSIM has to be restarted to pick up a new compound.");

        var btnBrowse = new Button { Content = "Browse...", HorizontalAlignment = HorizontalAlignment.Stretch };
        btnBrowse.Classes.Add("panel");
        btnBrowse.Click += async (_, _) => await BrowseAsync();
        p.CreateAndAddControlRow(btnBrowse);

        var btnSave = new Button
        {
            Content = "Save Black Oil Compound JSON",
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

    private double OilSG() => BlackOilCompoundBuilder.SGOFromAPI(Val(_tbAPI, 32.0));

    private void Compute()
    {
        try
        {
            var sgo = OilSG();
            var bsw = Val(_tbBSW, 0.0);
            _lblSG.Text = sgo.ToString("N4", CultureInfo.CurrentCulture);
            _lblMW.Text = BlackOilCompoundBuilder.MolarWeight(sgo).ToString("N1", CultureInfo.CurrentCulture);
            _lblNBP.Text = BlackOilCompoundBuilder.NormalBoilingPoint(sgo, bsw).ToString("N1", CultureInfo.CurrentCulture);
            _status.Text = "";
        }
        catch (Exception ex)
        {
            _lblSG.Text = _lblMW.Text = _lblNBP.Text = "-";
            _status.Text = "Error: " + ex.Message;
        }
    }

    private void Save()
    {
        try
        {
            var name = string.IsNullOrWhiteSpace(_tbName.Text) ? "BlackOil_Custom" : _tbName.Text!.Trim();

            var sgo = OilSG();
            var sgg = Val(_tbGasSG, 0.75);
            var gor = Val(_tbGOR, 0.0);
            var bsw = Val(_tbBSW, 0.0);
            // cSt -> m2/s (kinematic), degC -> K
            var v1 = Val(_tbVisc1, 0.0) * 1e-6;
            var v2 = Val(_tbVisc2, 0.0) * 1e-6;
            var t1 = Val(_tbViscT1, 20.0) + 273.15;
            var t2 = Val(_tbViscT2, 50.0) + 273.15;

            var compound = BlackOilCompoundBuilder.BuildCompound(name, sgo, sgg, gor, bsw, v1, t1, v2, t2, _tbComments.Text ?? "");

            var path = ResolveOutputPath(name);
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

            File.WriteAllText(path, JsonConvert.SerializeObject(compound, Formatting.Indented));
            _tbOutput.Text = path;

            _status.Text = "Saved to " + path + ". Restart DWSIM and select the Black Oil property package to use it.";
            _flowsheet?.ShowMessage("Black-oil compound saved to " + path + ".", IFlowsheet.MessageType.Information);
        }
        catch (Exception ex)
        {
            _status.Text = "Could not save: " + ex.Message;
        }
    }

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
        var name = string.IsNullOrWhiteSpace(_tbName.Text) ? "BlackOil_Custom" : _tbName.Text!.Trim();
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Black Oil Compound JSON",
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
        var name = string.IsNullOrWhiteSpace(_tbName.Text) ? "BlackOil_Custom" : _tbName.Text!.Trim();
        return Path.Combine(DefaultAddcompsPath(), name + ".json");
    }

    private static double Val(TextBox tb, double fallback)
        => UtilityHelpers.TryVal(tb.Text, out var v) ? v : fallback;
}
