using System.Collections.Generic;
using ConstantProperties = DWSIM.Thermodynamics.BaseClasses.ConstantProperties;

namespace DWSIM.UI.Desktop.Editors
{

    /// <summary>
    /// What a downloaded compound does and does not carry, in the order the import dialogs show
    /// it. Both the online (Cheméo) and the ChEDL Thermo importers check the same list.
    /// </summary>
    public static class CompoundDataChecklist
    {

        public sealed class Entry
        {
            public string Property { get; set; } = "";
            public bool Available { get; set; }
        }

        public static List<Entry> For(ConstantProperties c)
        {
            var entries = new List<Entry>();
            if (c == null) return entries;

            Add(entries, "Name", !string.IsNullOrEmpty(c.Name));
            Add(entries, "CAS Number", !string.IsNullOrEmpty(c.CAS_Number));
            Add(entries, "Formula", !string.IsNullOrEmpty(c.Formula));
            Add(entries, "InChI String", !string.IsNullOrEmpty(c.InChI));
            Add(entries, "SMILES String", !string.IsNullOrEmpty(c.SMILES));

            Add(entries, "Molecular Weight", c.Molar_Weight != 0.0);
            Add(entries, "Normal Boiling Point", c.Normal_Boiling_Point != 0.0);
            Add(entries, "Fusion Temperature", c.TemperatureOfFusion != 0.0);

            Add(entries, "Critical Temperature", c.Critical_Temperature != 0.0);
            Add(entries, "Critical Pressure", c.Critical_Pressure != 0.0);
            Add(entries, "Critical Volume", c.Critical_Volume != 0.0);
            Add(entries, "Critical Compressibility", c.Critical_Compressibility != 0.0);
            Add(entries, "Acentric Factor", c.Acentric_Factor != 0.0);

            Add(entries, "Rackett Compressibility Factor", c.Z_Rackett != 0.0);

            Add(entries, "Enthalpy of Formation (IG)", c.IG_Enthalpy_of_Formation_25C != 0.0);
            Add(entries, "Entropy of Formation (IG)", c.IG_Entropy_of_Formation_25C != 0.0);
            Add(entries, "Gibbs Energy of Formation (IG)", c.IG_Gibbs_Energy_of_Formation_25C != 0.0);

            Add(entries, "UNIQUAC Q Parameter", c.UNIQUAC_Q != 0.0);
            Add(entries, "UNIQUAC R Parameter", c.UNIQUAC_R != 0.0);

            Add(entries, "Dipole Moment", c.Dipole_Moment != 0.0);
            Add(entries, "Chao Seader Solubility Parameter", c.Chao_Seader_Solubility_Parameter != 0.0);

            Add(entries, "Vapor Pressure Curve Data", c.Vapor_Pressure_Constant_A != 0.0);
            Add(entries, "Ideal Gas Heat Capacity Curve Data", c.Ideal_Gas_Heat_Capacity_Const_A != 0.0);
            Add(entries, "Liquid Phase Heat Capacity Curve Data", c.Liquid_Heat_Capacity_Const_A != 0.0);
            Add(entries, "Vapor Phase Viscosity Curve Data", c.Vapor_Viscosity_Const_A != 0.0);
            Add(entries, "Liquid Phase Viscosity Curve Data", c.Liquid_Viscosity_Const_A != 0.0);
            Add(entries, "Vapor Phase Thermal Conductivity Curve Data", c.Vapor_Thermal_Conductivity_Const_A != 0.0);
            Add(entries, "Liquid Phase Thermal Conductivity Curve Data", c.Liquid_Thermal_Conductivity_Const_A != 0.0);
            Add(entries, "Surface Tension Curve Data", c.Surface_Tension_Const_A != 0.0);
            Add(entries, "Liquid Density Data", c.Liquid_Density_Const_A != 0.0);
            Add(entries, "Heat of Vaporization Data", c.HVap_A != 0.0);

            Add(entries, "Original UNIFAC Structure Data", c.UNIFACGroups != null && c.UNIFACGroups.Count > 0);
            Add(entries, "Modified UNIFAC (Dortmund) Structure Data", c.MODFACGroups != null && c.MODFACGroups.Count > 0);

            return entries;
        }

        private static void Add(List<Entry> entries, string property, bool available)
        {
            entries.Add(new Entry { Property = property, Available = available });
        }

    }

}
