using System;
using System.Collections.Generic;
using System.Linq;
using DWSIM.PureCompoundData.Core;
using DWSIM.PureCompoundData.Estimation;
using DWSIM.PureCompoundData.Estimation.Correlations;
using DWSIM.PureCompoundData.Estimation.Fitting;
using DWSIM.PureCompoundData.Estimation.Joback;
using DWSIM.PureCompoundData.Fragmentation;

namespace DWSIM.PureCompoundData.Builder
{
    /// Merges every <see cref="PureCompoundRecord"/> for a single compound, fits curves
    /// from points where needed, and fills remaining gaps via the estimator set (Joback
    /// → Lee-Kesler → Rackett). Returns a <see cref="BuiltConstantProperties"/> POCO
    /// with per-field provenance so the UI can show which source or estimator produced
    /// each number.
    public sealed class ConstantPropertiesBuilder
    {
        public BuiltConstantProperties Build(
            IReadOnlyList<PureCompoundRecord> records,
            Dictionary<string, int>? jobackGroups = null,
            string? smilesOverride = null)
        {
            var built = new BuiltConstantProperties();
            if (records == null || records.Count == 0) return built;

            // Identification: take from first record, prefer non-null across the rest.
            foreach (var r in records)
            {
                var c = r.Compound;
                // phaseq's ThermoML parser falls back to InChIKey when a source has no CAS -
                // reject that so InChIKey doesn't leak into the CAS field downstream.
                var maybeCas = c.CasNumber;
                if (!string.IsNullOrEmpty(built.CasNumber)) { }
                else if (!IsInChIKeyShape(maybeCas)) built.CasNumber = maybeCas;
                if (string.IsNullOrEmpty(built.Name)) built.Name = c.CommonName;
                built.IupacName ??= c.IupacName;
                built.Smiles ??= c.Smiles;
                built.InChIKey ??= string.IsNullOrEmpty(c.InChIKey) && IsInChIKeyShape(maybeCas) ? maybeCas : c.InChIKey;
                built.Formula ??= c.MolecularFormula;
                built.MolecularWeight ??= c.MolecularWeight;
            }
            built.CasNumber ??= "";

            // ThermoML records often lack SMILES. Caller can supply one (e.g. resolved via
            // PubChem from InChIKey) so auto-fragmentation + the estimator chain still fires.
            if (string.IsNullOrWhiteSpace(built.Smiles) && !string.IsNullOrWhiteSpace(smilesOverride))
            {
                built.Smiles = smilesOverride;
                built.Provenance["Smiles"] = new FieldProvenance { Kind = "estimator", Label = "PubChem" };
            }

            if (!built.MolecularWeight.HasValue && !string.IsNullOrEmpty(built.Formula))
            {
                var mw = MolecularWeightFromFormula(built.Formula);
                if (mw > 0)
                {
                    built.MolecularWeight = mw;
                    built.Provenance["MolecularWeight"] = new FieldProvenance
                    { Kind = "estimator", Label = "FormulaAtomicSum" };
                }
            }
            built.Provenance["Identification"] = new FieldProvenance
            { Kind = "source", Label = records[0].SourceProvider };

            // Group records by (Category, Property) and pick best per group:
            // prefer more Fits, then more Points, then most-recent (= latest Year in Citation).
            var groups = records
                .GroupBy(r => (r.Category, r.Property))
                .ToDictionary(g => g.Key, g => g
                    .OrderByDescending(r => r.Fits?.Count ?? 0)
                    .ThenByDescending(r => r.Points?.Count ?? 0)
                    .ThenByDescending(r => r.Citation?.Year ?? 0)
                    .First());

            // Scalar constants (Tc, Pc, Vc, omega, Tb, Tm, HformIG, GformIG).
            TryPickScalar(groups, PropertyCategory.Critical, "Tc", v => built.CriticalTemperature = v, built);
            TryPickScalar(groups, PropertyCategory.Critical, "Pc", v => built.CriticalPressure = v, built);
            TryPickScalar(groups, PropertyCategory.Critical, "Vc", v => built.CriticalVolume = v, built);
            TryPickScalar(groups, PropertyCategory.Critical, "Zc", v => built.CriticalCompressibility = v, built);
            TryPickScalar(groups, PropertyCategory.Acentric, "omega", v => built.AcentricFactor = v, built);
            TryPickScalar(groups, PropertyCategory.NormalBoilingPoint, "Tb", v => built.NormalBoilingPoint = v, built);
            TryPickScalar(groups, PropertyCategory.MeltingPoint, "Tm", v => built.NormalMeltingPoint = v, built);
            TryPickScalar(groups, PropertyCategory.FormationEnergetics, "HformIG", v => built.IgEnthalpyOfFormation25C = v, built);
            TryPickScalar(groups, PropertyCategory.FormationEnergetics, "GformIG", v => built.IgGibbsEnergyOfFormation25C = v, built);
            TryPickScalar(groups, PropertyCategory.EnthalpyOfFusion, "Hfus", v => built.EnthalpyOfFusion = v, built);

            // Some ThermoML submissions report critical density (kg/m3) instead of Vc;
            // convert to molar volume if MW is known.
            if (!built.CriticalVolume.HasValue && built.MolecularWeight.HasValue)
            {
                TryPickScalar(groups, PropertyCategory.Critical, "rhoC",
                    rho => { if (rho > 0) built.CriticalVolume = built.MolecularWeight!.Value / (1000.0 * rho); },
                    built);
            }

            // Vapor pressure: prefer a source-provided DIPPR/Antoine fit; else fit points.
            HandleCurve(
                groups, PropertyCategory.VaporPressure, "Psat",
                (eq, c, tmin, tmax) =>
                {
                    built.VaporPressureEquation = eq;
                    built.VaporPressureCoefficients = c;
                    built.VaporPressureTMin = tmin;
                    built.VaporPressureTMax = tmax;
                },
                "VaporPressure", built);

            HandleCurve(
                groups, PropertyCategory.IdealGasCp, "Cp",
                (eq, c, tmin, tmax) =>
                {
                    built.IdealGasCpEquation = eq;
                    built.IdealGasCpCoefficients = c;
                },
                "IdealGasCp", built);

            HandleCurve(
                groups, PropertyCategory.LiquidDensity, "rho",
                (eq, c, tmin, tmax) =>
                {
                    built.LiquidDensityEquation = eq;
                    built.LiquidDensityCoefficients = c;
                },
                "LiquidDensity", built);

            HandleCurve(
                groups, PropertyCategory.HeatOfVaporization, "HVap",
                (eq, c, tmin, tmax) =>
                {
                    built.HeatOfVaporizationEquation = eq;
                    built.HeatOfVaporizationCoefficients = c;
                },
                "HeatOfVaporization", built);

            // Auto-fragment SMILES into Joback/UNIFAC/Dortmund groups if the caller didn't
            // supply Joback groups explicitly and a SMILES is known.
            if ((jobackGroups == null || jobackGroups.Count == 0) && !string.IsNullOrWhiteSpace(built.Smiles))
            {
                AutoFragment(built);
                if (built.JobackGroups.Count > 0)
                    jobackGroups = new Dictionary<string, int>(built.JobackGroups);
            }

            // Fill remaining gaps with estimators.
            EstimateMissing(built, jobackGroups);

            return built;
        }

        private static void TryPickScalar(
            Dictionary<(PropertyCategory, string), PureCompoundRecord> groups,
            PropertyCategory cat, string prop,
            Action<double> setter,
            BuiltConstantProperties built)
        {
            if (!groups.TryGetValue((cat, prop), out var r)) return;
            if (!r.ScalarValue.HasValue) return;
            setter(r.ScalarValue.Value);
            built.Provenance[prop] = new FieldProvenance
            {
                Kind = "source",
                Label = r.SourceProvider,
                Doi = r.Citation?.Doi,
                Method = r.Method.ToString()
            };
        }

        private static void HandleCurve(
            Dictionary<(PropertyCategory, string), PureCompoundRecord> groups,
            PropertyCategory cat, string prop,
            Action<int, double[], double?, double?> setter,
            string fieldKey,
            BuiltConstantProperties built)
        {
            if (!groups.TryGetValue((cat, prop), out var r)) return;

            if (r.Fits != null && r.Fits.Count > 0)
            {
                var f = r.Fits[0];
                setter(f.DwsimEquationNumber, f.Coefficients, f.TMin, f.TMax);
                built.Provenance[fieldKey] = new FieldProvenance
                {
                    Kind = "source",
                    Label = r.SourceProvider,
                    Doi = r.Citation?.Doi
                };
                return;
            }

            if (r.Points == null || r.Points.Count < 3) return;

            if (cat == PropertyCategory.VaporPressure)
            {
                var pts = r.Points.Select(p => (p.T, p.Value)).ToList();
                var fit = Dippr101Fitter.Fit(pts);
                if (fit == null) return;
                setter(101, new[] { fit.A, fit.B, fit.C, fit.D, fit.E }, fit.TMinK, fit.TMaxK);
                built.Provenance[fieldKey] = new FieldProvenance
                {
                    Kind = "fit",
                    Label = "DIPPR101",
                    Doi = r.Citation?.Doi
                };
            }
            // Other categories: curve fitting not yet implemented - leave coefficients null.
        }

        private static void EstimateMissing(BuiltConstantProperties built, Dictionary<string, int>? groups)
        {
            // Stage 1: Joback (needs only group counts)
            if (groups != null && groups.Count > 0 &&
                (built.CriticalTemperature == null || built.CriticalPressure == null ||
                 built.NormalBoilingPoint == null || built.IgEnthalpyOfFormation25C == null))
            {
                var inputs = new CompoundInputs { MolecularWeight = built.MolecularWeight };
                foreach (var kv in groups) inputs.JobackGroups[kv.Key] = kv.Value;

                var r = new JobackEstimator().Estimate(inputs);
                FillFromEstimator(built, r, "Joback",
                    ("Tc", v => built.CriticalTemperature = v, () => built.CriticalTemperature),
                    ("Pc", v => built.CriticalPressure = v, () => built.CriticalPressure),
                    ("Vc", v => built.CriticalVolume = v, () => built.CriticalVolume),
                    ("Tb", v => built.NormalBoilingPoint = v, () => built.NormalBoilingPoint),
                    ("Tm", v => built.NormalMeltingPoint = v, () => built.NormalMeltingPoint),
                    ("HformIG", v => built.IgEnthalpyOfFormation25C = v, () => built.IgEnthalpyOfFormation25C),
                    ("GformIG", v => built.IgGibbsEnergyOfFormation25C = v, () => built.IgGibbsEnergyOfFormation25C),
                    ("Hfus", v => built.EnthalpyOfFusion = v, () => built.EnthalpyOfFusion));

                if (built.IdealGasCpCoefficients == null && r.Fits.TryGetValue("IdealGasCpPoly", out var poly))
                {
                    // DWSIM eq 4: Cp = A + B*T + C*T^2 + D*T^3 with Cp in J/(kmol·K).
                    // Joback output is J/(mol·K) - scale by 1000 to match DWSIM's storage.
                    built.IdealGasCpEquation = 4;
                    built.IdealGasCpCoefficients = new[] { poly[0] * 1000, poly[1] * 1000, poly[2] * 1000, poly[3] * 1000 };
                    built.Provenance["IdealGasCp"] = new FieldProvenance { Kind = "estimator", Label = "Joback" };
                }
            }

            // Critical compressibility from Tc/Pc/Vc if nothing carried it in.
            if (!built.CriticalCompressibility.HasValue &&
                built.CriticalTemperature.HasValue && built.CriticalPressure.HasValue && built.CriticalVolume.HasValue)
            {
                const double R = 8.314;
                built.CriticalCompressibility = built.CriticalPressure.Value * built.CriticalVolume.Value
                                                / (R * built.CriticalTemperature.Value);
                built.Provenance["Zc"] = new FieldProvenance { Kind = "estimator", Label = "Pc·Vc/(R·Tc)" };
            }

            // Stage 2: Lee-Kesler for omega (needs Tc, Pc, Tb)
            if (built.AcentricFactor == null &&
                built.CriticalTemperature.HasValue && built.CriticalPressure.HasValue &&
                built.NormalBoilingPoint.HasValue)
            {
                var inputs = new CompoundInputs
                {
                    Tc = built.CriticalTemperature,
                    Pc = built.CriticalPressure,
                    Tb = built.NormalBoilingPoint
                };
                var r = new LeeKeslerAcentric().Estimate(inputs);
                if (r.Values.TryGetValue("omega", out var om))
                {
                    built.AcentricFactor = om;
                    built.Provenance["omega"] = new FieldProvenance { Kind = "estimator", Label = "LeeKesler" };
                }
            }

            // Stage 3: Rackett rhoL if nothing yet (needs Tc, Pc, Zc or omega, MW)
            if (built.LiquidDensityCoefficients == null &&
                built.CriticalTemperature.HasValue && built.CriticalPressure.HasValue &&
                (built.CriticalCompressibility.HasValue || built.AcentricFactor.HasValue) &&
                built.MolecularWeight.HasValue)
            {
                var inputs = new CompoundInputs
                {
                    Tc = built.CriticalTemperature,
                    Pc = built.CriticalPressure,
                    Zc = built.CriticalCompressibility,
                    Acentric = built.AcentricFactor,
                    MolecularWeight = built.MolecularWeight
                };
                var r = new RackettLiquidDensity().Estimate(inputs);
                if (r.Fits.TryGetValue("RackettParams", out var p))
                {
                    built.LiquidDensityEquation = 0; // custom Rackett placeholder eqn number
                    built.LiquidDensityCoefficients = p;
                    built.Provenance["LiquidDensity"] = new FieldProvenance
                    { Kind = "estimator", Label = "Rackett" };
                }
            }

            // Stage 4: if no vapor pressure curve was supplied or fitted, mark eq = 0 so
            // DWSIM's User-DB Psat evaluator falls through to its built-in Lee-Kesler
            // Pvp(T, Tc, Pc, omega). Requires Tc/Pc/omega - which Joback + Lee-Kesler
            // produce above. This is the path the Eto CompoundCreator relies on too.
            if (built.VaporPressureCoefficients == null &&
                built.CriticalTemperature.HasValue && built.CriticalPressure.HasValue && built.AcentricFactor.HasValue)
            {
                built.VaporPressureEquation = 0;
                built.Provenance["VaporPressure"] = new FieldProvenance { Kind = "estimator", Label = "LeeKesler (runtime)" };
            }
        }

        private static void AutoFragment(BuiltConstantProperties built)
        {
            if (string.IsNullOrWhiteSpace(built.Smiles)) return;
            TryFill(built.Smiles!, GroupDefinitions.Joback, built.JobackGroups);
            TryFill(built.Smiles!, GroupDefinitions.Unifac, built.UnifacGroups);
            TryFill(built.Smiles!, GroupDefinitions.Dortmund, built.DortmundGroups);
            if (built.JobackGroups.Count > 0 || built.UnifacGroups.Count > 0 || built.DortmundGroups.Count > 0)
            {
                built.Provenance["Groups"] = new FieldProvenance
                { Kind = "estimator", Label = "SmilesFragmenter (NCDK + ugropy SMARTS)" };
            }
        }

        private static void TryFill(string smiles, GroupDefinitions defs, Dictionary<string, int> target)
        {
            var r = SmilesFragmenter.Fragment(smiles, defs);
            if (r == null || !r.IsComplete) return;
            foreach (var kv in r.Groups) target[kv.Key] = kv.Value;
        }

        private static bool IsInChIKeyShape(string? s)
        {
            if (string.IsNullOrEmpty(s) || s!.Length != 27) return false;
            if (s[14] != '-' || s[25] != '-') return false;
            for (int i = 0; i < s.Length; i++)
            {
                if (i == 14 || i == 25) continue;
                var ch = s[i];
                if (ch < 'A' || ch > 'Z') return false;
            }
            return true;
        }

        private static readonly Dictionary<string, double> AtomicWeights = new Dictionary<string, double>
        {
            {"H",1.008},{"He",4.0026},{"Li",6.94},{"Be",9.0122},{"B",10.81},{"C",12.011},
            {"N",14.007},{"O",15.999},{"F",18.998},{"Ne",20.180},{"Na",22.990},{"Mg",24.305},
            {"Al",26.982},{"Si",28.085},{"P",30.974},{"S",32.06},{"Cl",35.45},{"Ar",39.948},
            {"K",39.098},{"Ca",40.078},{"Ti",47.867},{"Cr",51.996},{"Mn",54.938},{"Fe",55.845},
            {"Ni",58.693},{"Co",58.933},{"Cu",63.546},{"Zn",65.38},{"Br",79.904},{"I",126.904}
        };

        private static double MolecularWeightFromFormula(string formula)
        {
            double total = 0;
            int i = 0;
            while (i < formula.Length)
            {
                if (!char.IsUpper(formula[i])) { i++; continue; }
                int j = i + 1;
                if (j < formula.Length && char.IsLower(formula[j])) j++;
                var sym = formula.Substring(i, j - i);
                int k = j;
                while (k < formula.Length && char.IsDigit(formula[k])) k++;
                int count = 1;
                if (k > j) int.TryParse(formula.Substring(j, k - j), out count);
                if (!AtomicWeights.TryGetValue(sym, out var aw)) return 0;
                total += aw * count;
                i = k;
            }
            return total;
        }

        private static void FillFromEstimator(
            BuiltConstantProperties built,
            EstimationResult result,
            string estimatorName,
            params (string Key, Action<double> Set, Func<double?> Get)[] fields)
        {
            foreach (var f in fields)
            {
                if (f.Get() != null) continue;
                if (!result.Values.TryGetValue(f.Key, out var v)) continue;
                f.Set(v);
                built.Provenance[f.Key] = new FieldProvenance
                { Kind = "estimator", Label = estimatorName };
            }
        }
    }
}
