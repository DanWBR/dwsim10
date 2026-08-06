using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using DWSIM.Interfaces;
using Compound = DWSIM.Thermodynamics.BaseClasses.Compound;
using ConstantProperties = DWSIM.Thermodynamics.BaseClasses.ConstantProperties;
using MaterialStream = DWSIM.Thermodynamics.Streams.MaterialStream;

namespace DWSIM.UI.Desktop.Avalonia;

/// <summary>
/// What the three compound import windows have in common: putting the downloaded compound in
/// the simulation and writing it out as JSON.
/// </summary>
internal static class CompoundImportSupport
{

    /// <summary>
    /// Registers the compound with the flowsheet, selects it and adds it to every phase of
    /// every material stream. Returns the message to show the user.
    /// </summary>
    public static string AddToFlowsheet(IFlowsheet flowsheet, ConstantProperties compound)
    {
        if (compound == null || string.IsNullOrEmpty(compound.Name))
            return "The compound has no name and cannot be added.";

        if (flowsheet.AvailableCompounds.ContainsKey(compound.Name))
            return $"'{compound.Name}' is already in the compound list.";

        try
        {
            flowsheet.RegisterSnapshot(Interfaces.Enums.SnapshotType.Compounds);

            flowsheet.AvailableCompounds.Add(compound.Name, compound);
            flowsheet.SelectedCompounds.Add(compound.Name, compound);

            foreach (var stream in flowsheet.SimulationObjects.Values.OfType<MaterialStream>().ToList())
            {
                foreach (var phase in stream.Phases.Values)
                {
                    if (phase.Compounds.ContainsKey(compound.Name)) continue;
                    phase.Compounds.Add(compound.Name, new Compound(compound.Name, ""));
                    phase.Compounds[compound.Name].ConstantProperties = compound;
                }
            }

            flowsheet.UpdateOpenEditForms();
            flowsheet.UpdateInterface();

            return $"'{compound.Name}' added to the simulation.";
        }
        catch (Exception ex)
        {
            return "Could not add the compound: " + ex.Message;
        }
    }

    /// <summary>Writes the compound to a JSON file, in the format the JSON importer reads.</summary>
    public static async Task<string> ExportJsonAsync(Window owner, ConstantProperties compound)
    {
        if (compound == null) return "Nothing to export.";

        var file = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export Compound to JSON",
            SuggestedFileName = compound.Name + ".json",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("JSON compound files") { Patterns = new[] { "*.json" } }
            }
        });

        if (file == null) return "";

        try
        {
            System.IO.File.WriteAllText(file.Path.LocalPath,
                Newtonsoft.Json.JsonConvert.SerializeObject(compound, Newtonsoft.Json.Formatting.Indented));
            return "Compound exported to " + file.Path.LocalPath;
        }
        catch (Exception ex)
        {
            return "Could not export the compound: " + ex.Message;
        }
    }

    /// <summary>
    /// The table of what the compound carries, shown before it is imported: one row per
    /// property with a tick when the download brought a value for it.
    /// </summary>
    public static DataGrid BuildChecklistGrid()
    {
        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            CanUserSortColumns = false,
            IsReadOnly = true,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal
        };

        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Available",
            Binding = new global::Avalonia.Data.Binding(nameof(ChecklistRow.Mark)),
            Width = new DataGridLength(80)
        });

        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Property",
            Binding = new global::Avalonia.Data.Binding(nameof(ChecklistRow.Property)),
            Width = new DataGridLength(1, DataGridLengthUnitType.Star)
        });

        return grid;
    }

    /// <summary>One line of the availability table.</summary>
    public sealed class ChecklistRow
    {
        public string Mark { get; set; } = "";
        public string Property { get; set; } = "";
    }

    public static List<ChecklistRow> Checklist(ConstantProperties compound)
    {
        return DWSIM.UI.Desktop.Editors.CompoundDataChecklist.For(compound)
            .Select(x => new ChecklistRow { Mark = x.Available ? "yes" : "no", Property = x.Property })
            .ToList();
    }

}
