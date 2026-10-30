using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using DWSIM.Interfaces;
using DWSIM.UI.Desktop.Editors;
using ConstantProperties = DWSIM.Thermodynamics.BaseClasses.ConstantProperties;
using PROPS = DWSIM.Thermodynamics.PropertyPackages.Auxiliary.PROPS;
using UserDB = DWSIM.Thermodynamics.Databases.UserDB;

namespace DWSIM.UI.Desktop.Avalonia;

/// <summary>
/// Quick compound creator: a user compound from its essential constants. Opened from a
/// simulation it adds the compound to it; from the welcome screen, or with the second button,
/// it saves the compound to a user database, which every new simulation loads.
///
/// The compound is stored as a "User" compound, the database the property packages read the
/// equations of. What the form does not ask for comes from the inputs (Rackett parameter from the
/// liquid density, boiling point from Lee-Kesler, critical volume from Pitzer) and, with a SMILES,
/// from the structure (Joback ideal gas heat capacity and formation properties, UNIFAC groups).
/// </summary>
public partial class CompoundCreatorWindow : Window
{
    private readonly IFlowsheet? _flowsheet;

    /// <summary>True once the compound went into the simulation.</summary>
    public bool Added { get; private set; }

    public CompoundCreatorWindow() : this(null) { }

    public CompoundCreatorWindow(IFlowsheet? flowsheet)
    {
        _flowsheet = flowsheet;
        InitializeComponent();
        IconHelper.ApplyWindowIcon(this);

        if (_flowsheet == null)
        {
            BtnAdd.IsVisible = false;
            BtnSaveDb.IsDefault = true;
        }

        BtnAdd.Click    += (_, _) => AddToFlowsheet();
        BtnSaveDb.Click += async (_, _) => await SaveToUserDatabaseAsync();
        BtnCancel.Click += (_, _) => Close();
    }

    private void AddToFlowsheet()
    {
        if (_flowsheet == null) return;

        var compound = BuildCompound(out var estimated);
        if (compound == null) return;

        if (_flowsheet.AvailableCompounds.ContainsKey(compound.Name))
        {
            LblStatus.Text = $"A compound called '{compound.Name}' already exists. Choose another name.";
            return;
        }

        var message = CompoundImportSupport.AddToFlowsheet(_flowsheet, compound);
        if (!_flowsheet.SelectedCompounds.ContainsKey(compound.Name))
        {
            LblStatus.Text = message;
            return;
        }

        Added = true;
        _flowsheet.ShowMessage(Summary(compound, estimated, $"'{compound.Name}' added to the simulation."),
            HasIdealGasCp(compound) ? IFlowsheet.MessageType.Information : IFlowsheet.MessageType.Warning);
        Close();
    }

    private async Task SaveToUserDatabaseAsync()
    {
        var compound = BuildCompound(out var estimated);
        if (compound == null) return;

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save to User Database (new or existing file)",
            SuggestedFileName = "user_compounds.xml",
            DefaultExtension = "xml",
            ShowOverwritePrompt = false,
            FileTypeChoices = new[] { new FilePickerFileType("DWSIM User Database") { Patterns = new[] { "*.xml" } } }
        });
        var path = file?.TryGetLocalPath();
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            if (File.Exists(path) && UserDB.ReadComps(path).Any(c => c.Name == compound.Name))
            {
                LblStatus.Text = $"'{Path.GetFileName(path)}' already holds a compound called '{compound.Name}'.";
                return;
            }

            // the database writer appends to the XML it is handed, or starts one on an empty stream
            using (var stream = new MemoryStream())
            {
                if (File.Exists(path))
                {
                    using (var existing = File.OpenRead(path)) existing.CopyTo(stream);
                    stream.Position = 0;
                }
                UserDB.AddCompounds(new ICompoundConstantProperties[] { compound }, stream, true);
                File.WriteAllBytes(path, stream.ToArray());
            }

            var databases = DWSIM.GlobalSettings.Settings.UserDatabases;
            if (!databases.Contains(path))
            {
                databases.Add(path);
                try { DWSIM.GlobalSettings.Settings.SaveSettings("dwsim_newui.ini"); } catch { }
            }

            LblStatus.Text = Summary(compound, estimated,
                $"'{compound.Name}' saved to {Path.GetFileName(path)}, which is in the list of user databases: new simulations offer it with the other compounds.");
        }
        catch (Exception ex)
        {
            LblStatus.Text = "Could not save the compound: " + ex.Message;
        }
    }

    /// <summary>
    /// Reads the form into a user compound and fills what it leaves out. Null, with the reason
    /// in the status line, when an input is missing or out of range.
    /// </summary>
    private ConstantProperties? BuildCompound(out List<string> estimated)
    {
        estimated = new List<string>();

        var name = TbName.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(name)) { LblStatus.Text = "Name is required."; return null; }

        if (!TryParse(TbMW.Text, out var mw) || mw <= 0) { LblStatus.Text = "Molecular Weight must be > 0."; return null; }
        if (!TryParse(TbTc.Text, out var tc) || tc <= 0) { LblStatus.Text = "Critical Temperature must be > 0."; return null; }
        if (!TryParse(TbPc.Text, out var pc) || pc <= 0) { LblStatus.Text = "Critical Pressure must be > 0."; return null; }
        if (!TryParse(TbOmega.Text, out var omega)) { LblStatus.Text = "Acentric Factor must be numeric."; return null; }

        var hasVc = TryParse(TbVc.Text, out var vc) && vc > 0;
        var hasTb = TryParse(TbTb.Text, out var tb) && tb > 0;
        var hasRho = TryParse(TbRhoL.Text, out var rho) && rho > 0;
        TryParse(TbHform.Text, out var hform);

        const double R = 8314.46; // J/(kmol.K)

        double zc;
        if (hasVc)
        {
            zc = pc * vc / (R * tc);
            if (zc < 0.1 || zc > 0.6)
            {
                LblStatus.Text = $"The critical volume gives a critical compressibility of {zc:G3}, where 0.2 to 0.4 is expected. " +
                                 "The field is in m³/kmol: 1 cm³/mol is 0.001 m³/kmol.";
                return null;
            }
        }
        else
        {
            // Pitzer
            zc = 0.291 - 0.080 * omega;
            vc = zc * R * tc / pc;
            estimated.Add("critical volume");
        }

        if (hasTb && tb >= tc) { LblStatus.Text = "The normal boiling point must be below the critical temperature."; return null; }
        if (!hasTb)
        {
            tb = LeeKeslerBoilingPoint(tc, pc, omega);
            if (double.IsNaN(tb))
            {
                LblStatus.Text = "The critical pressure is below 1 atm: enter the normal boiling point.";
                return null;
            }
            estimated.Add("normal boiling point");
        }

        var compound = new ConstantProperties
        {
            Name = name,
            CAS_Number = TbCAS.Text?.Trim() ?? "",
            Formula = TbFormula.Text?.Trim() ?? "",
            SMILES = TbSmiles.Text?.Trim() ?? "",
            // the property packages read the equations of the "User" database and estimate what
            // they leave empty (Lee-Kesler vapour pressure, Rackett density, Vetere enthalpy of
            // vaporization); a name of their own would fall through to zero
            OriginalDB = "User",
            CurrentDB = "User",
            ID = NewId(),
            Molar_Weight = mw,
            Critical_Temperature = tc,
            Critical_Pressure = pc,
            Critical_Volume = vc,
            Critical_Compressibility = zc,
            Acentric_Factor = omega,
            Normal_Boiling_Point = tb,
            NBP = tb,
            IG_Enthalpy_of_Formation_25C = hform,
        };

        // Rackett parameter that reproduces the density at 15 °C
        const double Tstd = 288.15;
        if (hasRho && Tstd < tc)
        {
            var v = mw / rho; // m3/kmol
            compound.Z_Rackett = Math.Pow(v * pc / (R * tc), 1.0 / (1.0 + Math.Pow(1.0 - Tstd / tc, 2.0 / 7.0)));
        }

        try { estimated.AddRange(PureCompoundImporter.EstimateFromStructure(compound)); }
        catch { /* a SMILES the fragmenter cannot read leaves the structure estimates out */ }

        compound.Comments = "Created with the Compound Creator on " + DateTime.Now.ToString("yyyy-MM-dd") + "." +
                            (estimated.Count > 0 ? " Estimated: " + string.Join(", ", estimated) + "." : "");

        return compound;
    }

    private static bool HasIdealGasCp(ConstantProperties compound) => !string.IsNullOrEmpty(compound.IdealgasCpEquation);

    private static string Summary(ConstantProperties compound, List<string> estimated, string head)
    {
        var text = head;
        if (estimated.Count > 0) text += " Estimated: " + string.Join(", ", estimated) + ".";
        if (!HasIdealGasCp(compound))
            text += " Without a SMILES the ideal gas heat capacity is the 3.5 R default: give a SMILES, or enter the equation " +
                    "with Edit Selected Compound in the simulation settings.";
        return text;
    }

    /// <summary>An ID no compound of the simulation uses; the property packages cache by it.</summary>
    private int NewId()
    {
        var used = _flowsheet?.AvailableCompounds.Values.Select(c => c.ID).ToHashSet() ?? new HashSet<int>();
        int id;
        do { id = Random.Shared.Next(100000, int.MaxValue); } while (used.Contains(id));
        return id;
    }

    /// <summary>Temperature where the Lee-Kesler vapour pressure reaches 1 atm; NaN when Pc is below it.</summary>
    private static double LeeKeslerBoilingPoint(double tc, double pc, double omega)
    {
        const double Patm = 101325.0;
        if (pc <= Patm) return double.NaN;
        double Pvap(double t) => Convert.ToDouble(PROPS.Pvp_leekesler(t, tc, pc, omega), CultureInfo.InvariantCulture);
        double lo = 0.2 * tc, hi = 0.9999 * tc;
        if (Pvap(lo) > Patm) return double.NaN;
        for (var i = 0; i < 100; i++)
        {
            var mid = 0.5 * (lo + hi);
            if (Pvap(mid) > Patm) hi = mid; else lo = mid;
            if (hi - lo < 1e-6) break;
        }
        return 0.5 * (lo + hi);
    }

    /// <summary>Parses a number in the regional format or with a decimal point.</summary>
    private static bool TryParse(string? text, out double value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        return double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value)
            || double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }
}
