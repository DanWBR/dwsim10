using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DWSIM.PureCompoundData.Builder;
using DWSIM.PureCompoundData.Core;
using DWSIM.PureCompoundData.Index;
using ConstantProperties = DWSIM.Thermodynamics.BaseClasses.ConstantProperties;

namespace DWSIM.UI.Desktop.Editors
{

    /// <summary>
    /// Compound import from the local pure-compound database (ThermoData): searching the index,
    /// building a compound out of the records it holds and turning that into a DWSIM compound.
    ///
    /// This is the whole of what the import dialogs do besides showing the results, so the
    /// WinForms and the Avalonia dialogs both drive it and stay UI-only.
    /// </summary>
    public static class PureCompoundImporter
    {

        /// <summary>One compound of the index, as the search groups its records.</summary>
        public sealed class Candidate
        {
            public string CAS { get; set; } = "";
            public string Name { get; set; } = "";
            public int RecordCount { get; set; }

            /// <summary>
            /// True when the index has no real CAS for the compound and stores its InChIKey in
            /// that field instead. Shown labelled, so the number is not mistaken for a CAS.
            /// </summary>
            public bool CASIsInChIKey { get; set; }

            public string Identifier
            {
                get { return CASIsInChIKey ? "InChIKey: " + CAS : CAS; }
            }
        }

        /// <summary>One line of the property table, with where the value came from.</summary>
        public sealed class PropertyRow
        {
            public string Property { get; set; } = "";
            public string Origin { get; set; } = "";
            public string Value { get; set; } = "";
            public string Units { get; set; } = "";
        }

        // ---------------------------------------------------------------------
        // Database
        // ---------------------------------------------------------------------

        public static string DatabasePath
        {
            get { return CachePaths.DefaultDatabasePath(); }
        }

        public static bool DatabaseInstalled
        {
            get { return File.Exists(DatabasePath); }
        }

        /// <summary>
        /// Downloads and installs the pre-built database bundle. The callback receives the bytes
        /// transferred and the total, which is -1 while the server has not declared a length; it
        /// runs on a worker thread, so a UI caller has to marshal it.
        /// </summary>
        public static Task DownloadDatabaseAsync(Action<long, long> progress, CancellationToken token)
        {
            var report = progress == null
                ? null
                : new Progress<(long Bytes, long Total)>(p => progress(p.Bytes, p.Total));

            return PureCompoundBundle.DownloadAndInstallAsync(null, null, report, token);
        }

        // ---------------------------------------------------------------------
        // Search
        // ---------------------------------------------------------------------

        /// <summary>
        /// Finds the compounds matching a CAS number, an InChIKey or a name, most documented
        /// first. The kind of query is decided from the shape of the text, as the index has a
        /// separate lookup for each.
        /// </summary>
        public static List<Candidate> Search(string text)
        {
            var candidates = new List<Candidate>();
            if (string.IsNullOrWhiteSpace(text)) return candidates;

            var query = text.Trim();

            using (var index = new PureCompoundIndex(DatabasePath))
            {
                var records = Query(index, query);
                if (records == null || records.Count == 0) return candidates;

                var groups = records
                    .GroupBy(r => string.IsNullOrEmpty(r.Compound.CasNumber)
                        ? r.Compound.CommonName
                        : r.Compound.CasNumber)
                    .OrderByDescending(g => g.Count());

                foreach (var group in groups)
                {
                    var first = group.First();
                    candidates.Add(new Candidate
                    {
                        CAS = first.Compound.CasNumber ?? "",
                        Name = first.Compound.CommonName ?? "",
                        RecordCount = group.Count(),
                        CASIsInChIKey = IsInChIKeyShape(first.Compound.CasNumber)
                    });
                }
            }

            return candidates;
        }

        private static IReadOnlyList<PureCompoundRecord> Query(PureCompoundIndex index, string query)
        {
            if (System.Text.RegularExpressions.Regex.IsMatch(query, @"^\d+-\d{2}-\d$"))
                return index.CreateQuery().ForCompound(query).Take(5000).Execute();

            if (query.Length == 27 && query.Contains("-"))
                return index.CreateQuery().ForCompoundByInChIKey(query).Take(5000).Execute();

            return index.CreateQuery().ForCompoundByName(query).Take(5000).Execute();
        }

        // ---------------------------------------------------------------------
        // Build
        // ---------------------------------------------------------------------

        /// <summary>
        /// Collects every record of one compound and builds its properties, filling the gaps
        /// with the estimator set. When the records carry no SMILES the structure is resolved
        /// from PubChem first: fragmentation, and with it Joback / Lee-Kesler / Rackett, only
        /// runs when the structure is known.
        /// </summary>
        public static BuiltConstantProperties Build(Candidate candidate)
        {
            if (candidate == null) return null;

            using (var index = new PureCompoundIndex(DatabasePath))
            {
                var records = string.IsNullOrEmpty(candidate.CAS)
                    ? index.CreateQuery().ForCompoundByName(candidate.Name).Take(5000).Execute()
                    : index.CreateQuery().ForCompound(candidate.CAS).Take(5000).Execute();

                if (records == null || records.Count == 0) return null;

                return new ConstantPropertiesBuilder().Build(records, null, ResolveSmiles(records));
            }
        }

        private static string ResolveSmiles(IReadOnlyList<PureCompoundRecord> records)
        {
            if (records.Any(r => !string.IsNullOrWhiteSpace(r.Compound.Smiles))) return null;

            var compound = records[0].Compound;

            var key = compound.InChIKey;
            if (string.IsNullOrWhiteSpace(key) && IsInChIKeyShape(compound.CasNumber))
                key = compound.CasNumber;

            string smiles = null;

            if (!string.IsNullOrWhiteSpace(key))
                smiles = PubChemResolver.SmilesFromInChIKey(key);

            if (string.IsNullOrWhiteSpace(smiles) &&
                !string.IsNullOrEmpty(compound.CasNumber) && !IsInChIKeyShape(compound.CasNumber))
                smiles = PubChemResolver.SmilesFromCas(compound.CasNumber);

            if (string.IsNullOrWhiteSpace(smiles) && !string.IsNullOrEmpty(compound.CommonName))
                smiles = PubChemResolver.SmilesFromName(compound.CommonName);

            return smiles;
        }

        // ---------------------------------------------------------------------
        // Describe
        // ---------------------------------------------------------------------

        /// <summary>The property table shown before the compound is imported.</summary>
        public static List<PropertyRow> Describe(BuiltConstantProperties b)
        {
            var rows = new List<PropertyRow>();
            if (b == null) return rows;

            var cas = IsInChIKeyShape(b.CasNumber) ? "" : b.CasNumber;
            var inchi = !string.IsNullOrEmpty(b.InChIKey)
                ? b.InChIKey
                : (IsInChIKeyShape(b.CasNumber) ? b.CasNumber : "");

            Add(rows, "CAS Number", "Identification", cas, "");
            Add(rows, "InChIKey", "Identification", inchi, "");
            Add(rows, "Name", "Identification", b.Name, "");
            Add(rows, "Formula", "Identification", b.Formula, "");
            Add(rows, "Molecular Weight", "Identification", Format(b.MolecularWeight), "g/mol");

            Add(rows, "Critical Temperature", Provenance(b, "Tc"), Format(b.CriticalTemperature), "K");
            Add(rows, "Critical Pressure", Provenance(b, "Pc"), Format(b.CriticalPressure), "Pa");
            Add(rows, "Critical Volume", Provenance(b, "Vc"), Format(b.CriticalVolume), "m3/mol");
            Add(rows, "Critical Compressibility", Provenance(b, "Zc"), Format(b.CriticalCompressibility), "-");
            Add(rows, "Acentric Factor", Provenance(b, "omega"), Format(b.AcentricFactor), "-");

            Add(rows, "Normal Boiling Point", Provenance(b, "Tb"), Format(b.NormalBoilingPoint), "K");
            Add(rows, "Normal Melting Point", Provenance(b, "Tm"), Format(b.NormalMeltingPoint), "K");
            Add(rows, "Enthalpy of Formation (IG)", Provenance(b, "HformIG"), Format(b.IgEnthalpyOfFormation25C), "J/mol");
            Add(rows, "Gibbs Energy of Formation (IG)", Provenance(b, "GformIG"), Format(b.IgGibbsEnergyOfFormation25C), "J/mol");

            AddEquation(rows, b, "Vapor Pressure Equation", "VaporPressure",
                b.VaporPressureEquation, b.VaporPressureCoefficients);
            AddEquation(rows, b, "Ideal Gas Cp Equation", "IdealGasCp",
                b.IdealGasCpEquation, b.IdealGasCpCoefficients);
            AddEquation(rows, b, "Liquid Density Equation", "LiquidDensity",
                b.LiquidDensityCoefficients == null ? null : b.LiquidDensityEquation, b.LiquidDensityCoefficients);
            AddEquation(rows, b, "Heat of Vaporization Equation", "HeatOfVaporization",
                b.HeatOfVaporizationEquation, b.HeatOfVaporizationCoefficients);

            return rows;
        }

        private static void Add(List<PropertyRow> rows, string property, string origin, string value, string units)
        {
            rows.Add(new PropertyRow { Property = property, Origin = origin ?? "", Value = value ?? "", Units = units });
        }

        private static void AddEquation(List<PropertyRow> rows, BuiltConstantProperties b,
                                        string caption, string key, int? equation, double[] coefficients)
        {
            if (coefficients == null) return;

            var value = "Eq " + equation.GetValueOrDefault() + " [" +
                        string.Join(", ", coefficients.Select(x => x.ToString("G4"))) + "]";

            Add(rows, caption, Provenance(b, key), value, "");
        }

        private static string Format(double? value)
        {
            return value.HasValue ? value.Value.ToString("G6") : "";
        }

        private static string Provenance(BuiltConstantProperties b, string key)
        {
            FieldProvenance provenance;
            if (b.Provenance.TryGetValue(key, out provenance) && provenance != null)
                return provenance.Kind + ": " + provenance.Label;
            return "";
        }

        // ---------------------------------------------------------------------
        // Map
        // ---------------------------------------------------------------------

        /// <summary>Turns the built properties into a DWSIM compound ready to be added.</summary>
        public static ConstantProperties ToConstantProperties(BuiltConstantProperties b)
        {
            if (b == null) return null;

            var cp = new ConstantProperties();

            cp.CAS_Number = b.CasNumber ?? "";
            cp.Name = b.Name ?? "";
            cp.Formula = b.Formula ?? "";
            cp.SMILES = b.Smiles ?? "";
            cp.InChI = b.InChIKey ?? "";

            // DWSIM's property-evaluation paths (Psat, IG Cp, liquid density, HVap) dispatch by
            // OriginalDB name. "User" hits the branch that reads the A..E coefficients and the
            // equation strings filled in below. A provider-specific name would fall through to
            // Lee-Kesler, which needs Tc/Pc/omega that pure-compound data does not always carry.
            cp.OriginalDB = "User";
            cp.CurrentDB = "User";
            cp.Comments = BuildComments(b);

            if (b.MolecularWeight.HasValue) cp.Molar_Weight = b.MolecularWeight.Value;
            if (b.CriticalTemperature.HasValue) cp.Critical_Temperature = b.CriticalTemperature.Value;
            if (b.CriticalPressure.HasValue) cp.Critical_Pressure = b.CriticalPressure.Value;
            // m3/mol to m3/kmol
            if (b.CriticalVolume.HasValue) cp.Critical_Volume = b.CriticalVolume.Value * 1000.0;
            if (b.CriticalCompressibility.HasValue) cp.Critical_Compressibility = b.CriticalCompressibility.Value;
            if (b.AcentricFactor.HasValue) cp.Acentric_Factor = b.AcentricFactor.Value;

            if (b.NormalBoilingPoint.HasValue)
            {
                cp.Normal_Boiling_Point = b.NormalBoilingPoint.Value;
                cp.NBP = b.NormalBoilingPoint.Value;
            }
            if (b.NormalMeltingPoint.HasValue) cp.TemperatureOfFusion = b.NormalMeltingPoint.Value;
            // DWSIM stores the fusion enthalpy in kJ/mol, the builder in J/mol
            if (b.EnthalpyOfFusion.HasValue) cp.EnthalpyOfFusionAtTf = b.EnthalpyOfFusion.Value / 1000.0;

            // DWSIM stores the formation properties in kJ/kg, the builder in J/mol
            if (b.IgEnthalpyOfFormation25C.HasValue && b.MolecularWeight.GetValueOrDefault() > 0)
                cp.IG_Enthalpy_of_Formation_25C = b.IgEnthalpyOfFormation25C.Value / b.MolecularWeight.Value;
            if (b.IgGibbsEnergyOfFormation25C.HasValue && b.MolecularWeight.GetValueOrDefault() > 0)
                cp.IG_Gibbs_Energy_of_Formation_25C = b.IgGibbsEnergyOfFormation25C.Value / b.MolecularWeight.Value;

            if (b.VaporPressureEquation.HasValue)
                cp.VaporPressureEquation = b.VaporPressureEquation.Value.ToString();
            SetCoefficients(b.VaporPressureCoefficients,
                v => cp.Vapor_Pressure_Constant_A = v, v => cp.Vapor_Pressure_Constant_B = v,
                v => cp.Vapor_Pressure_Constant_C = v, v => cp.Vapor_Pressure_Constant_D = v,
                v => cp.Vapor_Pressure_Constant_E = v);
            if (b.VaporPressureTMin.HasValue) cp.Vapor_Pressure_TMIN = b.VaporPressureTMin.Value;
            if (b.VaporPressureTMax.HasValue) cp.Vapor_Pressure_TMAX = b.VaporPressureTMax.Value;

            if (b.IdealGasCpEquation.HasValue)
                cp.IdealgasCpEquation = b.IdealGasCpEquation.Value.ToString();
            SetCoefficients(b.IdealGasCpCoefficients,
                v => cp.Ideal_Gas_Heat_Capacity_Const_A = v, v => cp.Ideal_Gas_Heat_Capacity_Const_B = v,
                v => cp.Ideal_Gas_Heat_Capacity_Const_C = v, v => cp.Ideal_Gas_Heat_Capacity_Const_D = v,
                v => cp.Ideal_Gas_Heat_Capacity_Const_E = v);

            if (b.LiquidDensityEquation.HasValue)
                cp.LiquidDensityEquation = b.LiquidDensityEquation.Value.ToString();
            SetCoefficients(b.LiquidDensityCoefficients,
                v => cp.Liquid_Density_Const_A = v, v => cp.Liquid_Density_Const_B = v,
                v => cp.Liquid_Density_Const_C = v, v => cp.Liquid_Density_Const_D = v,
                v => cp.Liquid_Density_Const_E = v);

            // The builder names the groups the way the fragmenter does; DWSIM keys them by
            // numeric subgroup ID, so they go through the tables shipped with the engine.
            AddGroups(b.UnifacGroups, GroupIdMap.Unifac(), cp.UNIFACGroups);
            AddGroups(b.DortmundGroups, GroupIdMap.Dortmund(), cp.MODFACGroups);
            AddGroups(b.DortmundGroups, GroupIdMap.NistMfac(), cp.NISTMODFACGroups);

            return cp;
        }

        private static void SetCoefficients(double[] values, params Action<double>[] setters)
        {
            if (values == null) return;
            for (int i = 0; i < setters.Length && i < values.Length; i++) setters[i](values[i]);
        }

        private static void AddGroups(Dictionary<string, int> groups,
                                      Dictionary<string, string> map,
                                      System.Collections.SortedList target)
        {
            if (groups == null || groups.Count == 0 || target == null) return;

            foreach (var group in groups)
            {
                string id;
                if (map.TryGetValue(group.Key, out id) && !target.Contains(id))
                    target.Add(id, group.Value);
            }
        }

        /// <summary>
        /// Per-property provenance, written into the compound comments: whether each number is
        /// experimental (with its citation), a curve fit or an estimator correlation.
        /// </summary>
        private static string BuildComments(BuiltConstantProperties b)
        {
            var text = new System.Text.StringBuilder();
            text.AppendLine("Imported via DWSIM.PureCompoundData on " +
                            DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm") + " UTC.");
            text.AppendLine();

            var sources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var estimators = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var citations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in b.Provenance)
            {
                var provenance = item.Value;
                if (provenance == null) continue;

                switch (provenance.Kind)
                {
                    case "source":
                        if (!string.IsNullOrEmpty(provenance.Label)) sources.Add(provenance.Label);
                        if (!string.IsNullOrEmpty(provenance.Doi)) citations.Add(provenance.Doi);
                        break;
                    case "estimator":
                        if (!string.IsNullOrEmpty(provenance.Label)) estimators.Add(provenance.Label);
                        break;
                    case "fit":
                        if (!string.IsNullOrEmpty(provenance.Label)) estimators.Add(provenance.Label + " (fit)");
                        if (!string.IsNullOrEmpty(provenance.Doi)) citations.Add(provenance.Doi);
                        break;
                }
            }

            if (sources.Count > 0) text.AppendLine("Data sources: " + string.Join(", ", sources));
            if (estimators.Count > 0) text.AppendLine("Estimators / fits: " + string.Join(", ", estimators));

            text.AppendLine();
            text.AppendLine("Property provenance:");
            foreach (var item in b.Provenance.OrderBy(x => x.Key))
            {
                var provenance = item.Value;
                if (provenance == null) continue;

                var line = "  " + item.Key + " - " + provenance.Kind + ": " + provenance.Label;
                if (!string.IsNullOrEmpty(provenance.Method)) line += " [" + provenance.Method + "]";
                if (!string.IsNullOrEmpty(provenance.Doi)) line += " doi:" + provenance.Doi;
                text.AppendLine(line);
            }

            if (citations.Count > 0)
            {
                text.AppendLine();
                text.AppendLine("Citations (DOI):");
                foreach (var doi in citations) text.AppendLine("  " + doi);
            }

            return text.ToString().TrimEnd();
        }

        /// <summary>True for a 27 character InChIKey, which the index stores in the CAS field.</summary>
        public static bool IsInChIKeyShape(string text)
        {
            if (string.IsNullOrEmpty(text) || text.Length != 27) return false;
            if (text[14] != '-' || text[25] != '-') return false;

            for (int i = 0; i < text.Length; i++)
            {
                if (i == 14 || i == 25) continue;
                if (text[i] < 'A' || text[i] > 'Z') return false;
            }

            return true;
        }

    }

}
