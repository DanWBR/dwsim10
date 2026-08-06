using System;
using System.IO;
using DWSIM.Interfaces;
using DWSIM.Thermodynamics.BaseClasses;
using PseudoBuilder = DWSIM.Thermodynamics.Utilities.PetroleumCharacterization.Methods.PseudoBuilder;

namespace DWSIM.Automation.FluentAPI
{
    /// <summary>Fluent endpoints for adding non-database compounds to a flowsheet.</summary>
    public static class CompoundExtensions
    {
        /// <summary>
        /// Adds a petroleum pseudo-component to the flowsheet, computing its critical
        /// properties, acentric factor, formation enthalpy/entropy, vaporisation enthalpy
        /// and Chao-Seader parameters from <paramref name="normalBoilingPoint"/>,
        /// <paramref name="specificGravity"/> and <paramref name="molarWeight"/> via
        /// <see cref="PseudoBuilder.FinalizeCompoundProperties"/>.
        /// </summary>
        /// <param name="fs">Flowsheet to add the component to.</param>
        /// <param name="name">Display name (must be unique within the flowsheet).</param>
        /// <param name="normalBoilingPoint">Mean atmospheric NBP (e.g. <c>650.0.Kelvin()</c>).</param>
        /// <param name="specificGravity">SG at 60/60 °F (water = 1.0). Typical petroleum cuts: 0.65–0.95.</param>
        /// <param name="molarWeight">Molar weight in g/mol.</param>
        /// <param name="tcMethod">Tc correlation: "Riazi-Daubert (1985)" (default), "Riazi (2005)", "Lee-Kesler (1976)", "Twu (1984)", "Farah (2006)" or "PNA-Weighted (Riazi)".</param>
        /// <param name="pcMethod">Pc correlation (same options).</param>
        /// <param name="acentricMethod">ω correlation: "Lee-Kesler (1976)" (default) or "Korsten (2000)".</param>
        /// <param name="paraffinFrac">Optional measured paraffin mass fraction (0–1).</param>
        /// <param name="naphtenicFrac">Optional measured naphtenic mass fraction.</param>
        /// <param name="aromaticFrac">Optional measured aromatic mass fraction.</param>
        /// <param name="refractiveIndexN20">Optional measured n_D at 20 °C.</param>
        public static Flowsheet WithPseudoComponent(this Flowsheet fs,
            string name,
            Quantity normalBoilingPoint,
            double specificGravity,
            double molarWeight,
            string tcMethod = "Riazi-Daubert (1985)",
            string pcMethod = "Riazi-Daubert (1985)",
            string acentricMethod = "Lee-Kesler (1976)",
            double? paraffinFrac = null,
            double? naphtenicFrac = null,
            double? aromaticFrac = null,
            double? refractiveIndexN20 = null)
        {
            if (fs == null) throw new ArgumentNullException(nameof(fs));
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Pseudo name is required", nameof(name));
            if (specificGravity <= 0) throw new ArgumentOutOfRangeException(nameof(specificGravity));
            if (molarWeight <= 0) throw new ArgumentOutOfRangeException(nameof(molarWeight));

            var cp = new ConstantProperties
            {
                Name = name,
                CAS_Number = name,
                NBP = normalBoilingPoint.SI,
                PF_SG = specificGravity,
                PF_MM = molarWeight,
            };
            if (paraffinFrac.HasValue) cp.PF_xP = paraffinFrac.Value;
            if (naphtenicFrac.HasValue) cp.PF_xN = naphtenicFrac.Value;
            if (aromaticFrac.HasValue) cp.PF_xA = aromaticFrac.Value;
            if (refractiveIndexN20.HasValue) cp.PF_n20 = refractiveIndexN20.Value;

            // PseudoBuilder needs PF_vA / PF_vB (kinematic viscosities @ 100 °F / 210 °F) for
            // Farah Tc/Pc; for the default Riazi correlations they are unused but FinalizeCompoundProperties
            // touches them via .GetValueOrDefault, so leaving them as default null is safe.
            PseudoBuilder.FinalizeCompoundProperties(cp, tcMethod, pcMethod, acentricMethod);

            RegisterCompound(fs, cp);
            return fs;
        }

        /// <summary>
        /// Adds a compound by deserialising a UserDB-style JSON file (same schema as the
        /// files under <c>addcomps/</c>) and registering it with the flowsheet. Useful
        /// for compounds the user maintains outside the standard databases - for example,
        /// values produced by an external ThermoML or PubChem pipeline serialised to disk.
        /// </summary>
        public static Flowsheet WithCompoundFromJson(this Flowsheet fs, string filepath)
        {
            if (fs == null) throw new ArgumentNullException(nameof(fs));
            if (string.IsNullOrWhiteSpace(filepath)) throw new ArgumentException("filepath is required", nameof(filepath));
            if (!File.Exists(filepath)) throw new FileNotFoundException("Compound JSON not found", filepath);

            var json = File.ReadAllText(filepath);
            var cp = Newtonsoft.Json.JsonConvert.DeserializeObject<ConstantProperties>(json);
            if (cp == null || string.IsNullOrEmpty(cp.Name))
                throw new InvalidDataException("Compound JSON missing required 'Name' field.");
            cp.OriginalDB = "User";
            cp.CurrentDB = "User";

            RegisterCompound(fs, cp);
            return fs;
        }

        /// <summary>
        /// Adds an externally-built compound. Use this when you have a
        /// <see cref="ICompoundConstantProperties"/> produced from any source
        /// (ThermoML import, DWSIM.PureCompoundData index, custom estimator pipeline).
        /// The compound is registered both with the global compound catalog and with the
        /// flowsheet's selected-components collection.
        /// </summary>
        public static Flowsheet WithCompound(this Flowsheet fs, ICompoundConstantProperties compound)
        {
            if (fs == null) throw new ArgumentNullException(nameof(fs));
            if (compound == null) throw new ArgumentNullException(nameof(compound));
            if (string.IsNullOrWhiteSpace(compound.Name)) throw new ArgumentException("Compound.Name must be set", nameof(compound));
            RegisterCompound(fs, compound);
            return fs;
        }

        private static void RegisterCompound(Flowsheet fs, ICompoundConstantProperties cp)
        {
            // Process-wide catalog (shared by all Fluent-API flowsheets in the AppDomain).
            var catalog = Bootstrap.Automation.AvailableCompounds;
            if (!catalog.ContainsKey(cp.Name)) catalog.Add(cp.Name, cp);

            // Per-flowsheet selection.
            var selected = fs.Inner.SelectedCompounds;
            if (!selected.ContainsKey(cp.Name)) selected.Add(cp.Name, cp);
        }
    }
}
