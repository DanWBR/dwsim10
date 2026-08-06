using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using DWSIM.Interfaces;
using DWSIM.Interfaces.Enums;
using DWSIM.SharedClasses.SystemsOfUnits;
using DWSIM.Thermodynamics.BaseClasses;
using DWSIM.Thermodynamics.PropertyPackages;
using DWSIM.Thermodynamics.Streams;

namespace DWSIM.UI.Desktop.Avalonia;

/// <summary>
/// Adding a compound to the simulation and taking it out again. The settings window and the
/// setup wizard both offer it, and the streams have to be kept consistent either way.
/// </summary>
internal static class CompoundSelection
{

    public static void Add(IFlowsheet flowsheet, ICompoundConstantProperties compound)
    {
        if (flowsheet.SelectedCompounds.ContainsKey(compound.Name)) return;

        flowsheet.RegisterSnapshot(SnapshotType.Compounds);
        flowsheet.AddCompound(compound.Name);

        foreach (var stream in Streams(flowsheet))
        {
            foreach (var phase in stream.Phases.Values)
            {
                if (phase.Compounds.ContainsKey(compound.Name)) continue;
                phase.Compounds.Add(compound.Name, new Compound(compound.Name, ""));
                phase.Compounds[compound.Name].ConstantProperties = compound;
            }
        }
    }

    /// <summary>
    /// Removes the compound, discounting what it carried from the overall stream flows and
    /// renormalizing what is left, as the Windows settings form does.
    /// </summary>
    public static void Remove(IFlowsheet flowsheet, ICompoundConstantProperties compound)
    {
        if (!flowsheet.SelectedCompounds.ContainsKey(compound.Name)) return;

        flowsheet.RegisterSnapshot(SnapshotType.Compounds);
        flowsheet.SelectedCompounds.Remove(compound.Name);

        foreach (var stream in Streams(flowsheet))
        {
            var overall = stream.Phases[0];
            if (!overall.Compounds.ContainsKey(compound.Name)) continue;

            var comp = overall.Compounds[compound.Name];

            if (overall.Properties.massflow.HasValue)
                overall.Properties.massflow -= comp.MassFlow.GetValueOrDefault();
            if (overall.Properties.molarflow.HasValue)
                overall.Properties.molarflow -= comp.MolarFlow.GetValueOrDefault();
            if (overall.Properties.volumetric_flow.HasValue)
                overall.Properties.volumetric_flow -= comp.VolumetricFlow.GetValueOrDefault();

            foreach (var phase in stream.Phases.Values)
                phase.Compounds.Remove(compound.Name);

            stream.ClearCalculatedProps();
            stream.NormalizeOverallMoleComposition();
            stream.NormalizeOverallMassComposition();
            stream.Calculated = false;
            if (stream.GraphicObject != null) stream.GraphicObject.Calculated = false;
        }

        foreach (var pp in flowsheet.PropertyPackages.Values.OfType<PropertyPackage>())
            if (pp.ForcedSolids.Contains(compound.Name)) pp.ForcedSolids.Remove(compound.Name);
    }

    private static IEnumerable<MaterialStream> Streams(IFlowsheet flowsheet)
        => flowsheet.SimulationObjects.Values.OfType<MaterialStream>().ToList();

}

/// <summary>
/// The units of a system of units, one picker per measure. The settings window and the setup
/// wizard show the same table.
/// </summary>
internal static class UnitSystemEditor
{

    /// <summary>The built-in sets, which are read-only.</summary>
    public static readonly string[] BuiltInNames =
        { "SI", "CGS", "ENG", "C1", "C2", "C3", "C4", "C5", "SI (Engineering)" };

    public static readonly (string Caption, UnitOfMeasure Measure, string Property)[] Rows =
    {
        ("Temperature", UnitOfMeasure.temperature, "temperature"),
        ("Pressure", UnitOfMeasure.pressure, "pressure"),
        ("Mass Flow Rate", UnitOfMeasure.massflow, "massflow"),
        ("Molar Flow Rate", UnitOfMeasure.molarflow, "molarflow"),
        ("Volumetric flow rate", UnitOfMeasure.volumetricFlow, "volumetricFlow"),
        ("Specific Enthalpy", UnitOfMeasure.enthalpy, "enthalpy"),
        ("Specific Entropy", UnitOfMeasure.entropy, "entropy"),
        ("Molecular Weight", UnitOfMeasure.molecularWeight, "molecularWeight"),
        ("Density", UnitOfMeasure.density, "density"),
        ("Surface Tension", UnitOfMeasure.surfaceTension, "surfaceTension"),
        ("Heat Capacity", UnitOfMeasure.heatCapacityCp, "heatCapacityCp"),
        ("Thermal Conductivity", UnitOfMeasure.thermalConductivity, "thermalConductivity"),
        ("Kinematic Viscosity", UnitOfMeasure.cinematic_viscosity, "cinematic_viscosity"),
        ("Dynamic Viscosity", UnitOfMeasure.viscosity, "viscosity"),
        ("Temperature Difference", UnitOfMeasure.deltaT, "deltaT"),
        ("Pressure Difference", UnitOfMeasure.deltaP, "deltaP"),
        ("Length/Head", UnitOfMeasure.head, "head"),
        ("Power / Heat Duty / Energy Flow", UnitOfMeasure.heatflow, "heatflow"),
        ("Time", UnitOfMeasure.time, "time"),
        ("Volume", UnitOfMeasure.volume, "volume"),
        ("Molar Volume", UnitOfMeasure.molar_volume, "molar_volume"),
        ("Area", UnitOfMeasure.area, "area"),
        ("Diameter/Thickness", UnitOfMeasure.diameter, "diameter"),
        ("Force", UnitOfMeasure.force, "force"),
        ("Acceleration", UnitOfMeasure.accel, "accel"),
        ("Heat Transfer Coefficient", UnitOfMeasure.heat_transf_coeff, "heat_transf_coeff"),
        ("Molar Concentration", UnitOfMeasure.molar_conc, "molar_conc"),
        ("Mass Concentration", UnitOfMeasure.mass_conc, "mass_conc"),
        ("Reaction Rate", UnitOfMeasure.reac_rate, "reac_rate"),
        ("Specific Volume", UnitOfMeasure.spec_vol, "spec_vol"),
        ("Molar Enthalpy", UnitOfMeasure.molar_enthalpy, "molar_enthalpy"),
        ("Molar Entropy", UnitOfMeasure.molar_entropy, "molar_entropy"),
        ("Velocity", UnitOfMeasure.velocity, "velocity"),
        ("Fouling Factor", UnitOfMeasure.foulingfactor, "foulingfactor"),
        ("Specific Cake Resistance", UnitOfMeasure.cakeresistance, "cakeresistance"),
        ("Filter Medium Resistance", UnitOfMeasure.mediumresistance, "mediumresistance"),
        ("Isothermal Compressibility", UnitOfMeasure.compressibility, "compressibility"),
        ("Joule Thomson Coefficient", UnitOfMeasure.jouleThomsonCoefficient, "jouleThomsonCoefficient"),
        ("Conductance", UnitOfMeasure.conductance, "conductance"),
        ("Distance / Length", UnitOfMeasure.distance, "distance"),
        ("Heat/Energy", UnitOfMeasure.heat, "heat"),
        ("Mass", UnitOfMeasure.mass, "mass"),
        ("Moles", UnitOfMeasure.mole, "mole"),
        ("Specific Power", UnitOfMeasure.specificpower, "specific_power")
    };

    public static bool IsBuiltIn(IUnitsOfMeasure system)
        => BuiltInNames.Contains(system.Name);

    /// <summary>Fills the grid with one picker per measure, two pairs per row.</summary>
    public static void Fill(Grid host, IUnitsOfMeasure system, Action? onChanged = null)
    {
        var readOnly = IsBuiltIn(system);

        host.Children.Clear();
        host.RowDefinitions.Clear();

        var rows = (Rows.Length + 1) / 2;
        for (int i = 0; i < rows; i++) host.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        for (int i = 0; i < Rows.Length; i++)
        {
            var row = i / 2;
            var column = (i % 2) * 2;
            var entry = Rows[i];

            var property = typeof(Units).GetProperty(entry.Property,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (property == null) continue;

            List<string> options;
            try { options = system.GetUnitSet(entry.Measure) ?? new List<string>(); }
            catch (Exception) { continue; }
            if (options.Count == 0) continue;

            var label = new TextBlock
            {
                Text = entry.Caption,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(column == 0 ? 0 : 16, 2, 8, 2)
            };
            Grid.SetRow(label, row);
            Grid.SetColumn(label, column);
            host.Children.Add(label);

            var current = property.GetValue(system) as string ?? "";
            var picker = new ComboBox
            {
                ItemsSource = options,
                SelectedIndex = Math.Max(0, options.IndexOf(current)),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 2, 0, 2),
                IsEnabled = !readOnly
            };

            var target = system;
            var prop = property;
            picker.SelectionChanged += (_, _) =>
            {
                if (picker.SelectedItem is not string unit) return;
                prop.SetValue(target, unit);
                onChanged?.Invoke();
            };

            Grid.SetRow(picker, row);
            Grid.SetColumn(picker, column + 1);
            host.Children.Add(picker);
        }
    }

    /// <summary>Copies every unit of one system into another, leaving the name alone.</summary>
    public static void CopyUnits(IUnitsOfMeasure from, IUnitsOfMeasure to)
    {
        foreach (var prop in typeof(Units).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (prop.PropertyType != typeof(string) || !prop.CanRead || !prop.CanWrite) continue;
            if (prop.Name == "Name") continue;
            try { prop.SetValue(to, prop.GetValue(from)); } catch (Exception) { }
        }
    }

    public static string UniqueName(IEnumerable<IUnitsOfMeasure> systems, string baseName)
    {
        var name = baseName;
        var i = 1;
        while (systems.Any(x => string.Equals(x.Name, name, StringComparison.CurrentCultureIgnoreCase)))
            name = baseName + "_" + i++;
        return name;
    }

}
