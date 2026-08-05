using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using DWSIM.PhaseEquilibriumData.Core;

namespace DWSIM.PhaseEquilibriumData.Sources.ThermoML
{
    public sealed class ThermoMLParser
    {
        private readonly Action<string>? _warn;

        public ThermoMLParser(Action<string>? warn = null)
        {
            _warn = warn;
        }

        public ThermoMLFile? Parse(Stream xml)
        {
            XDocument doc;
            try
            {
                doc = XDocument.Load(xml, LoadOptions.None);
            }
            catch (Exception ex)
            {
                _warn?.Invoke($"Malformed XML: {ex.Message}");
                return null;
            }

            var root = doc.Root;
            if (root == null) return null;

            var file = new ThermoMLFile();
            ParseCitation(root, file);
            ParseCompounds(root, file);
            ParsePureOrMixtureData(root, file);
            return file;
        }

        private static IEnumerable<XElement> Desc(XElement parent, string localName)
            => parent.Descendants().Where(e => e.Name.LocalName == localName);

        private static XElement? Child(XElement parent, string localName)
            => parent.Elements().FirstOrDefault(e => e.Name.LocalName == localName);

        private static string? ChildText(XElement parent, string localName)
            => Child(parent, localName)?.Value?.Trim();

        private void ParseCitation(XElement root, ThermoMLFile file)
        {
            var citation = Desc(root, "Citation").FirstOrDefault();
            if (citation == null) return;
            file.Doi = ChildText(citation, "sDOI");
            file.Title = ChildText(citation, "sTitle");
            file.Journal = ChildText(citation, "sPubName");
            if (int.TryParse(ChildText(citation, "yrPubYr"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int yr))
                file.Year = yr;
            if (int.TryParse(ChildText(citation, "sVol"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int vol))
                file.Volume = vol;
            file.Pages = ChildText(citation, "sPage");
            foreach (var a in Desc(citation, "sAuthor"))
            {
                var v = a.Value?.Trim();
                if (!string.IsNullOrEmpty(v)) file.Authors.Add(v!);
            }
        }

        private void ParseCompounds(XElement root, ThermoMLFile file)
        {
            foreach (var comp in Desc(root, "Compound"))
            {
                var regNum = Desc(comp, "nOrgNum").FirstOrDefault()?.Value?.Trim();
                if (!int.TryParse(regNum, NumberStyles.Integer, CultureInfo.InvariantCulture, out int idx))
                    continue;

                var cas = ChildText(comp, "sCASName") != null ? ChildText(comp, "sCASRegistryNum") : ChildText(comp, "sCASRegistryNum");
                if (string.IsNullOrWhiteSpace(cas))
                    cas = ChildText(comp, "sCASRegistryNum");
                var name = ChildText(comp, "sCommonName") ?? ChildText(comp, "sIUPACName") ?? string.Empty;
                var iupac = ChildText(comp, "sIUPACName");
                var smiles = ChildText(comp, "sSmiles");
                var inchiKey = ChildText(comp, "sStandardInChIKey") ?? ChildText(comp, "sInChIKey");
                var formula = ChildText(comp, "sFormulaMolec") ?? ChildText(comp, "sFormula");
                double? mw = null;

                if (string.IsNullOrWhiteSpace(cas))
                {
                    _warn?.Invoke($"Compound nOrgNum={idx} missing CAS; skipping.");
                    continue;
                }

                file.Compounds[idx] = new Compound(cas!.Trim(), name, iupac, smiles, inchiKey, formula, mw);
            }
        }

        private void ParsePureOrMixtureData(XElement root, ThermoMLFile file)
        {
            int pmodOrdinal = 0;
            foreach (var pmod in Desc(root, "PureOrMixtureData"))
            {
                pmodOrdinal++;
                var p = new ThermoMLPureOrMixture { Index = pmodOrdinal };

                var explicitNum = ChildText(pmod, "nPureOrMixtureDataNumber");
                if (int.TryParse(explicitNum, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n))
                    p.Index = n;

                foreach (var phase in Desc(pmod, "ePhase"))
                {
                    var v = phase.Value?.Trim();
                    if (!string.IsNullOrEmpty(v)) p.PhaseIds.Add(v!);
                    if (!string.IsNullOrEmpty(v) && v!.IndexOf("azeotrop", StringComparison.OrdinalIgnoreCase) >= 0)
                        p.HasAzeotropMarker = true;
                }

                foreach (var comp in Desc(pmod, "Component"))
                {
                    var regNum = Desc(comp, "nOrgNum").FirstOrDefault()?.Value?.Trim();
                    if (int.TryParse(regNum, NumberStyles.Integer, CultureInfo.InvariantCulture, out int idx))
                        p.ComponentOrder.Add(idx);
                }

                var varNumberToName = new Dictionary<int, string>();
                var varNumberToKind = new Dictionary<int, ConstraintKind?>();
                int varOrd = 0;
                foreach (var variable in Desc(pmod, "Variable"))
                {
                    varOrd++;
                    var nVarNum = Desc(variable, "nVarNumber").FirstOrDefault()?.Value?.Trim();
                    int vnum = int.TryParse(nVarNum, NumberStyles.Integer, CultureInfo.InvariantCulture, out int vnumP) ? vnumP : varOrd;

                    string name = "Var" + vnum.ToString(CultureInfo.InvariantCulture);
                    ConstraintKind? kind = null;
                    bool isComposition = false;

                    var typeEl = Desc(variable, "VariableType").FirstOrDefault();
                    if (typeEl != null)
                    {
                        var tEl = typeEl.Elements().FirstOrDefault();
                        if (tEl != null)
                        {
                            name = (tEl.Value ?? tEl.Name.LocalName).Trim();
                            kind = MapKind(tEl.Name.LocalName, name);
                            isComposition = tEl.Name.LocalName == "eComponentComposition";
                            if (isComposition &&
                                name.IndexOf("mass", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                p.CompositionIsMassFraction = true;
                            }
                            if (tEl.Name.LocalName == "eVaporOrSublimationPressure" ||
                                tEl.Name.LocalName == "eBubblePointTemperature")
                            {
                                // heuristic markers kept in name
                            }
                            if (name.IndexOf("azeotrop", StringComparison.OrdinalIgnoreCase) >= 0)
                                p.HasAzeotropMarker = true;
                        }
                    }

                    // For composition variables, ThermoML disambiguates via component RegNum and
                    // phase - e.g. binary VLE has separate "Mole fraction" Variables for x1 and y1.
                    // Without a disambiguating suffix they'd collide on the same dictionary key in
                    // the per-point values map and clobber each other. Append ord=N (ordinal into
                    // ComponentOrder) and phase=X so downstream consumers can tell x and y apart.
                    if (isComposition)
                    {
                        int? ordinal = null;
                        var regOrg = variable.Descendants()
                            .FirstOrDefault(e => e.Name.LocalName == "nOrgNum" &&
                                                 e.Ancestors().Any(a => a.Name.LocalName == "RegNum"));
                        if (regOrg != null &&
                            int.TryParse(regOrg.Value?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int orgN))
                        {
                            var idx = p.ComponentOrder.IndexOf(orgN);
                            if (idx >= 0) ordinal = idx + 1;
                        }
                        // ThermoML stores the variable's phase at VarPhaseID/eVarPhase, not as a
                        // direct child of Variable. Fall back to the legacy paths for safety.
                        string? phase = variable.Descendants()
                            .FirstOrDefault(e => e.Name.LocalName == "eVarPhase" &&
                                                 e.Ancestors().Any(a => a.Name.LocalName == "VarPhaseID"))
                            ?.Value?.Trim();
                        if (string.IsNullOrEmpty(phase))
                            phase = Desc(variable, "ePhase").FirstOrDefault()?.Value?.Trim()
                                  ?? Desc(variable, "sPhaseName").FirstOrDefault()?.Value?.Trim();
                        if (ordinal.HasValue) name += " | ord=" + ordinal.Value.ToString(CultureInfo.InvariantCulture);
                        if (!string.IsNullOrEmpty(phase)) name += " | phase=" + phase;
                    }

                    varNumberToName[vnum] = name;
                    varNumberToKind[vnum] = kind;
                    p.VariableNames.Add(name);
                }

                var propNumberToName = new Dictionary<int, string>();
                int propOrd = 0;
                foreach (var prop in Desc(pmod, "Property"))
                {
                    propOrd++;
                    var nPropNum = Desc(prop, "nPropNumber").FirstOrDefault()?.Value?.Trim();
                    int pnum = int.TryParse(nPropNum, NumberStyles.Integer, CultureInfo.InvariantCulture, out int pnumP) ? pnumP : propOrd;
                    var ePropName = Desc(prop, "ePropName").FirstOrDefault()?.Value?.Trim();
                    string pname = !string.IsNullOrEmpty(ePropName) ? ePropName! : ("Prop" + pnum);

                    // y (vapor composition) in ThermoML is frequently reported as a Property rather
                    // than a Variable, keyed on PropPhaseID/ePropPhase=Gas and Property-MethodID/
                    // RegNum/nOrgNum. Without a disambiguating suffix, multiple "Mole fraction"
                    // properties collide on the per-point values dict. Match the composition-
                    // Variable naming so downstream code can filter by ord=N and phase=X.
                    if (pname.IndexOf("fraction", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        int? ordinal = null;
                        var pmid = Desc(prop, "Property-MethodID").FirstOrDefault();
                        if (pmid != null)
                        {
                            var orgEl = pmid.Descendants()
                                .FirstOrDefault(e => e.Name.LocalName == "nOrgNum" &&
                                                     e.Ancestors().Any(a => a.Name.LocalName == "RegNum"));
                            if (orgEl != null &&
                                int.TryParse(orgEl.Value?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int orgN))
                            {
                                var idx = p.ComponentOrder.IndexOf(orgN);
                                if (idx >= 0) ordinal = idx + 1;
                            }
                        }
                        string? phase = Desc(prop, "ePropPhase").FirstOrDefault()?.Value?.Trim();
                        if (ordinal.HasValue) pname += " | ord=" + ordinal.Value.ToString(CultureInfo.InvariantCulture);
                        if (!string.IsNullOrEmpty(phase)) pname += " | phase=" + phase;
                    }

                    propNumberToName[pnum] = pname;
                    p.PropertyNames.Add(pname);
                }

                foreach (var cons in Desc(pmod, "Constraint"))
                {
                    var typeEl = Desc(cons, "ConstraintType").FirstOrDefault();
                    string label = "Constraint";
                    ConstraintKind? kind = null;
                    if (typeEl != null)
                    {
                        var tEl = typeEl.Elements().FirstOrDefault();
                        if (tEl != null)
                        {
                            label = (tEl.Value ?? tEl.Name.LocalName).Trim();
                            kind = MapKind(tEl.Name.LocalName, label);
                        }
                    }
                    var valStr = Desc(cons, "nConstraintValue").FirstOrDefault()?.Value?.Trim();
                    if (!UnitConversions.TryParseInvariant(valStr, out double val)) continue;
                    if (kind == null) continue;

                    string unit = ExtractUnitFromLabel(label);
                    double converted = kind switch
                    {
                        ConstraintKind.Temperature => UnitConversions.TemperatureToK(val, unit),
                        ConstraintKind.Pressure => UnitConversions.PressureToKPa(val, unit),
                        _ => val
                    };
                    int? compIdx = null;
                    var orgNum = Desc(cons, "nOrgNum").FirstOrDefault()?.Value?.Trim();
                    if (int.TryParse(orgNum, NumberStyles.Integer, CultureInfo.InvariantCulture, out int ci))
                        compIdx = ci;
                    p.Constraints.Add(new Constraint(kind.Value, converted, CanonicalUnit(kind.Value), compIdx));
                }

                foreach (var nv in Desc(pmod, "NumValues"))
                {
                    var values = new Dictionary<string, double>(StringComparer.Ordinal);
                    var uncs = new Dictionary<string, double>(StringComparer.Ordinal);

                    foreach (var vv in Desc(nv, "VariableValue"))
                    {
                        var vnumStr = Desc(vv, "nVarNumber").FirstOrDefault()?.Value?.Trim();
                        var valStr = Desc(vv, "nVarValue").FirstOrDefault()?.Value?.Trim();
                        if (!int.TryParse(vnumStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out int vnum)) continue;
                        if (!UnitConversions.TryParseInvariant(valStr, out double v)) continue;

                        string name = varNumberToName.TryGetValue(vnum, out var n2) ? n2 : "Var" + vnum;
                        var kind = varNumberToKind.TryGetValue(vnum, out var k) ? k : null;
                        double converted = kind switch
                        {
                            ConstraintKind.Temperature => UnitConversions.TemperatureToK(v, ExtractUnitFromLabel(name)),
                            ConstraintKind.Pressure => UnitConversions.PressureToKPa(v, ExtractUnitFromLabel(name)),
                            _ => v
                        };
                        values[name] = converted;
                    }

                    foreach (var pv in Desc(nv, "PropertyValue"))
                    {
                        var pnumStr = Desc(pv, "nPropNumber").FirstOrDefault()?.Value?.Trim();
                        var valStr = Desc(pv, "nPropValue").FirstOrDefault()?.Value?.Trim();
                        if (!UnitConversions.TryParseInvariant(valStr, out double v)) continue;
                        string key;
                        if (int.TryParse(pnumStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out int pnum) &&
                            propNumberToName.TryGetValue(pnum, out var pname))
                            key = pname;
                        else
                            key = "Prop" + (pnumStr ?? "?");
                        values[key] = v;
                    }

                    if (values.Count > 0) p.Points.Add(new DataPoint(values, uncs));
                }

                var methodLabel = Desc(pmod, "eMethodName").FirstOrDefault()?.Value
                                 ?? Desc(pmod, "sMethodName").FirstOrDefault()?.Value;
                p.Method = MapMethod(methodLabel);

                file.PureOrMixtureData.Add(p);
            }
        }

        private static ConstraintKind? MapKind(string localName, string label)
        {
            // ThermoML has several pressure/temperature localName variants (eBubblePointTemperature,
            // eVaporOrSublimationPressure, ePartialPressure, …). Treat any localName containing
            // "Temperature" or "Pressure" as the corresponding canonical kind so unit conversion
            // runs and the value is persisted in K / kPa like the primary eTemperature / ePressure.
            if (localName.IndexOf("Temperature", StringComparison.OrdinalIgnoreCase) >= 0)
                return ConstraintKind.Temperature;
            if (localName.IndexOf("Pressure", StringComparison.OrdinalIgnoreCase) >= 0)
                return ConstraintKind.Pressure;
            switch (localName)
            {
                case "eComponentComposition":
                    if (label.IndexOf("mole", StringComparison.OrdinalIgnoreCase) >= 0) return ConstraintKind.MoleFraction;
                    if (label.IndexOf("mass", StringComparison.OrdinalIgnoreCase) >= 0) return ConstraintKind.MassFraction;
                    if (label.IndexOf("molal", StringComparison.OrdinalIgnoreCase) >= 0) return ConstraintKind.Molality;
                    return ConstraintKind.MoleFraction;
                default: return null;
            }
        }

        private static string CanonicalUnit(ConstraintKind k) => k switch
        {
            ConstraintKind.Temperature => "K",
            ConstraintKind.Pressure => "kPa",
            ConstraintKind.MoleFraction => "mole_fraction",
            ConstraintKind.MassFraction => "mass_fraction",
            ConstraintKind.Molality => "mol/kg",
            _ => string.Empty
        };

        private static string ExtractUnitFromLabel(string label)
        {
            var comma = label.IndexOf(',');
            return comma >= 0 && comma + 1 < label.Length
                ? label.Substring(comma + 1).Trim()
                : string.Empty;
        }

        private static MeasurementMethod MapMethod(string? label)
        {
            if (string.IsNullOrWhiteSpace(label)) return MeasurementMethod.Unknown;
            var l = label!.ToLowerInvariant();
            if (l.Contains("static")) return MeasurementMethod.StaticCell;
            if (l.Contains("othmer")) return MeasurementMethod.EbuliometerOthmer;
            if (l.Contains("swieto") || l.Contains("świę")) return MeasurementMethod.EbuliometerSwietoslawski;
            if (l.Contains("recircul")) return MeasurementMethod.RecirculatingStill;
            if (l.Contains("headspace")) return MeasurementMethod.HeadspaceGC;
            if (l.Contains("inverse") && l.Contains("gas")) return MeasurementMethod.InverseGC;
            if (l.Contains("synthes")) return MeasurementMethod.SynthesizedIsothermal;
            if (l.Contains("dsc") || l.Contains("differential scanning")) return MeasurementMethod.DSC;
            if (l.Contains("isopiestic")) return MeasurementMethod.IsopiestiC;
            return MeasurementMethod.Other;
        }
    }
}
