using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using DWSIM.PhaseEquilibriumData.Core;

namespace DWSIM.PhaseEquilibriumData.Sources.ThermoML
{
    /// <summary>
    /// Parser for the NIST ThermoML JSON archive (mds2-2422). Structure mirrors the XML schema:
    /// Compound[], Citation, PureOrMixtureData[]. Emits the same AST as <see cref="ThermoMLParser"/>.
    /// NOTE: JSON archive has no CAS field - uses sStandardInChIKey as the CasNumber surrogate.
    /// </summary>
    public sealed class ThermoMLJsonParser
    {
        private readonly Action<string>? _warn;

        public ThermoMLJsonParser(Action<string>? warn = null)
        {
            _warn = warn;
        }

        public ThermoMLFile? Parse(Stream json)
        {
            JObject root;
            try
            {
                using var sr = new StreamReader(json);
                using var jr = new JsonTextReader(sr) { DateParseHandling = DateParseHandling.None };
                var token = JToken.ReadFrom(jr);
                if (token.Type != JTokenType.Object) return null;
                root = (JObject)token;
            }
            catch (Exception ex)
            {
                _warn?.Invoke($"Malformed JSON: {ex.Message}");
                return null;
            }

            var file = new ThermoMLFile();
            ParseCitation(root, file);
            ParseCompounds(root, file);
            ParsePureOrMixtureData(root, file);
            return file;
        }

        private static string? GetStr(JToken? parent, string name)
        {
            if (parent is not JObject obj) return null;
            var v = obj[name];
            if (v == null) return null;
            if (v.Type == JTokenType.String) return (string?)v;
            if (v.Type == JTokenType.Integer || v.Type == JTokenType.Float) return v.ToString(Formatting.None);
            if (v.Type == JTokenType.Array)
            {
                foreach (var e in (JArray)v)
                    if (e.Type == JTokenType.String) return (string?)e;
            }
            return null;
        }

        private static double? GetNum(JToken? parent, string name)
        {
            if (parent is not JObject obj) return null;
            var v = obj[name];
            if (v == null) return null;
            if (v.Type == JTokenType.Integer || v.Type == JTokenType.Float) return (double?)v;
            if (v.Type == JTokenType.String && UnitConversions.TryParseInvariant((string?)v, out var d)) return d;
            return null;
        }

        private static int? GetInt(JToken? parent, string name)
        {
            var n = GetNum(parent, name);
            return n.HasValue ? (int)n.Value : (int?)null;
        }

        private static IEnumerable<JObject> EnumArray(JToken? parent, string name)
        {
            if (parent is not JObject obj) yield break;
            var v = obj[name];
            if (v == null) yield break;
            if (v.Type == JTokenType.Array)
            {
                foreach (var e in (JArray)v)
                    if (e is JObject eo) yield return eo;
            }
            else if (v is JObject single)
            {
                yield return single;
            }
        }

        private void ParseCitation(JObject root, ThermoMLFile file)
        {
            if (root["Citation"] is not JObject cit) return;
            file.Doi = GetStr(cit, "sDOI");
            file.Title = GetStr(cit, "sTitle");
            file.Journal = GetStr(cit, "sPubName");
            file.Year = GetInt(cit, "yrPubYr");
            file.Volume = GetInt(cit, "sVol");
            file.Pages = GetStr(cit, "sPage");
            if (cit["sAuthor"] is JArray authors)
            {
                foreach (var a in authors)
                    if (a.Type == JTokenType.String)
                    {
                        var s = (string?)a;
                        if (!string.IsNullOrWhiteSpace(s)) file.Authors.Add(s!.Trim());
                    }
            }
        }

        private void ParseCompounds(JObject root, ThermoMLFile file)
        {
            foreach (var comp in EnumArray(root, "Compound"))
            {
                int? idx = null;
                if (comp["RegNum"] is JObject reg)
                    idx = GetInt(reg, "nOrgNum");
                if (!idx.HasValue) continue;

                var name = GetStr(comp, "sCommonName") ?? GetStr(comp, "sIUPACName") ?? string.Empty;
                var inchiKey = GetStr(comp, "sStandardInChIKey") ?? GetStr(comp, "sInChIKey");
                var inchi = GetStr(comp, "sStandardInChI");
                var formula = GetStr(comp, "sFormulaMolec") ?? GetStr(comp, "sFormula");

                // JSON archive has no CAS - use InChIKey as the canonical chemical identifier.
                var key = inchiKey;
                if (string.IsNullOrWhiteSpace(key))
                {
                    _warn?.Invoke($"Compound nOrgNum={idx} missing sStandardInChIKey; skipping.");
                    continue;
                }

                file.Compounds[idx.Value] = new Compound(key!.Trim(), name, null, null, inchiKey, formula, null);
            }
        }

        private void ParsePureOrMixtureData(JObject root, ThermoMLFile file)
        {
            int pmodOrdinal = 0;
            foreach (var pmod in EnumArray(root, "PureOrMixtureData"))
            {
                pmodOrdinal++;
                var p = new ThermoMLPureOrMixture { Index = pmodOrdinal };
                var explicitNum = GetInt(pmod, "nPureOrMixtureDataNumber");
                if (explicitNum.HasValue) p.Index = explicitNum.Value;

                foreach (var ph in EnumArray(pmod, "PhaseID"))
                {
                    var phaseStr = GetStr(ph, "ePhase");
                    if (string.IsNullOrEmpty(phaseStr)) continue;
                    p.PhaseIds.Add(phaseStr!);
                    if (phaseStr!.IndexOf("azeotrop", StringComparison.OrdinalIgnoreCase) >= 0)
                        p.HasAzeotropMarker = true;
                }

                foreach (var c in EnumArray(pmod, "Component"))
                {
                    if (c["RegNum"] is not JObject reg) continue;
                    var n = GetInt(reg, "nOrgNum");
                    if (n.HasValue) p.ComponentOrder.Add(n.Value);
                }

                var varNumberToName = new Dictionary<int, string>();
                var varNumberToKind = new Dictionary<int, ConstraintKind?>();
                int varOrd = 0;
                foreach (var variable in EnumArray(pmod, "Variable"))
                {
                    varOrd++;
                    int vnum = GetInt(variable, "nVarNumber") ?? varOrd;
                    string name = "Var" + vnum.ToString(CultureInfo.InvariantCulture);
                    ConstraintKind? kind = null;
                    bool isComposition = false;

                    JObject? vid = variable["VariableID"] as JObject;
                    if (vid != null && vid["VariableType"] is JObject vt)
                    {
                        var (typeKey, typeLabel) = ExtractTypeKeyLabel(vt);
                        if (typeKey != null)
                        {
                            name = typeLabel ?? typeKey;
                            kind = MapKind(typeKey, name);
                            isComposition = typeKey == "eComponentComposition";
                            if (isComposition &&
                                name.IndexOf("mass", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                p.CompositionIsMassFraction = true;
                            }
                            if (name.IndexOf("azeotrop", StringComparison.OrdinalIgnoreCase) >= 0)
                                p.HasAzeotropMarker = true;
                        }
                    }

                    // See note in ThermoMLParser.cs: composition variables need ord=N/phase=X
                    // suffixes so x1 and y1 don't collide in the per-point values dictionary.
                    if (isComposition)
                    {
                        int? ordinal = null;
                        if (vid != null && vid["RegNum"] is JObject vreg)
                        {
                            var orgN = GetInt(vreg, "nOrgNum");
                            if (orgN.HasValue)
                            {
                                var idx = p.ComponentOrder.IndexOf(orgN.Value);
                                if (idx >= 0) ordinal = idx + 1;
                            }
                        }
                        string? phase = null;
                        if (variable["VarPhaseID"] is JObject vpid)
                            phase = GetStr(vpid, "eVarPhase") ?? GetStr(vpid, "ePhase") ?? GetStr(vpid, "sPhaseName");
                        phase ??= GetStr(variable, "ePhase") ?? GetStr(variable, "sPhaseName");
                        if (phase == null && vid != null)
                            phase = GetStr(vid, "ePhase") ?? GetStr(vid, "sPhaseName");
                        if (ordinal.HasValue) name += " | ord=" + ordinal.Value.ToString(CultureInfo.InvariantCulture);
                        if (!string.IsNullOrEmpty(phase)) name += " | phase=" + phase;
                    }

                    varNumberToName[vnum] = name;
                    varNumberToKind[vnum] = kind;
                    p.VariableNames.Add(name);
                }

                var propNumberToName = new Dictionary<int, string>();
                int propOrd = 0;
                foreach (var prop in EnumArray(pmod, "Property"))
                {
                    propOrd++;
                    int pnum = GetInt(prop, "nPropNumber") ?? propOrd;
                    string? pname = null;
                    JObject? pmidObj = prop["Property-MethodID"] as JObject;
                    if (pmidObj != null && pmidObj["PropertyGroup"] is JObject pgrp)
                    {
                        foreach (var kv in pgrp)
                        {
                            if (kv.Value is JObject sub)
                            {
                                var ep = GetStr(sub, "ePropName");
                                if (!string.IsNullOrWhiteSpace(ep)) { pname = ep; break; }
                            }
                        }
                    }
                    if (string.IsNullOrWhiteSpace(pname)) pname = "Prop" + pnum;

                    // Composition properties (y in VLE) collide under a bare "Mole fraction"
                    // key. Enrich with component ordinal (Property-MethodID/RegNum/nOrgNum) and
                    // phase (PropPhaseID/ePropPhase), same shape as composition Variable names.
                    if (pname!.IndexOf("fraction", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        int? ordinal = null;
                        if (pmidObj != null && pmidObj["RegNum"] is JObject preg)
                        {
                            var orgN = GetInt(preg, "nOrgNum");
                            if (orgN.HasValue)
                            {
                                var idx = p.ComponentOrder.IndexOf(orgN.Value);
                                if (idx >= 0) ordinal = idx + 1;
                            }
                        }
                        string? phase = null;
                        if (prop["PropPhaseID"] is JObject ppid)
                            phase = GetStr(ppid, "ePropPhase") ?? GetStr(ppid, "ePhase");
                        if (ordinal.HasValue) pname += " | ord=" + ordinal.Value.ToString(CultureInfo.InvariantCulture);
                        if (!string.IsNullOrEmpty(phase)) pname += " | phase=" + phase;
                    }

                    propNumberToName[pnum] = pname!;
                    p.PropertyNames.Add(pname!);
                }

                foreach (var cons in EnumArray(pmod, "Constraint"))
                {
                    string label = "Constraint";
                    ConstraintKind? kind = null;
                    if (cons["ConstraintID"] is JObject cid && cid["ConstraintType"] is JObject ct)
                    {
                        var (typeKey, typeLabel) = ExtractTypeKeyLabel(ct);
                        if (typeKey != null)
                        {
                            label = typeLabel ?? typeKey;
                            kind = MapKind(typeKey, label);
                        }
                    }
                    var val = GetNum(cons, "nConstraintValue");
                    if (!val.HasValue || kind == null) continue;
                    string unit = ExtractUnitFromLabel(label);
                    double converted = kind switch
                    {
                        ConstraintKind.Temperature => UnitConversions.TemperatureToK(val.Value, unit),
                        ConstraintKind.Pressure => UnitConversions.PressureToKPa(val.Value, unit),
                        _ => val.Value
                    };
                    int? compIdx = null;
                    if (cons["ConstraintID"] is JObject cid2 && cid2["RegNum"] is JObject creg)
                    {
                        compIdx = GetInt(creg, "nOrgNum");
                    }
                    p.Constraints.Add(new Constraint(kind.Value, converted, CanonicalUnit(kind.Value), compIdx));
                }

                foreach (var nv in EnumArray(pmod, "NumValues"))
                {
                    var values = new Dictionary<string, double>(StringComparer.Ordinal);
                    var uncs = new Dictionary<string, double>(StringComparer.Ordinal);

                    foreach (var vv in EnumArray(nv, "VariableValue"))
                    {
                        var vnum = GetInt(vv, "nVarNumber");
                        var val = GetNum(vv, "nVarValue");
                        if (!vnum.HasValue || !val.HasValue) continue;
                        string name = varNumberToName.TryGetValue(vnum.Value, out var n2) ? n2 : "Var" + vnum.Value;
                        var kind = varNumberToKind.TryGetValue(vnum.Value, out var k) ? k : null;
                        double converted = kind switch
                        {
                            ConstraintKind.Temperature => UnitConversions.TemperatureToK(val.Value, ExtractUnitFromLabel(name)),
                            ConstraintKind.Pressure => UnitConversions.PressureToKPa(val.Value, ExtractUnitFromLabel(name)),
                            _ => val.Value
                        };
                        values[name] = converted;
                    }

                    foreach (var pv in EnumArray(nv, "PropertyValue"))
                    {
                        var pnum = GetInt(pv, "nPropNumber");
                        var val = GetNum(pv, "nPropValue");
                        if (!val.HasValue) continue;
                        string key;
                        if (pnum.HasValue && propNumberToName.TryGetValue(pnum.Value, out var pname))
                            key = pname;
                        else
                            key = "Prop" + (pnum?.ToString(CultureInfo.InvariantCulture) ?? "?");
                        values[key] = val.Value;
                    }

                    if (values.Count > 0) p.Points.Add(new DataPoint(values, uncs));
                }

                // Method - may appear under Property[].PropertyMethodID.PropertyMethodName.eMethodName
                // or as sMethodName inside a Property entry. Best-effort.
                string? methodLabel = null;
                foreach (var prop in EnumArray(pmod, "Property"))
                {
                    if (prop["PropertyMethodID"] is JObject pmid &&
                        pmid["PropertyMethodName"] is JObject pmname)
                    {
                        methodLabel = GetStr(pmname, "eMethodName") ?? GetStr(pmname, "sMethodName");
                        if (methodLabel != null) break;
                    }
                }
                p.Method = MapMethod(methodLabel);
                file.PureOrMixtureData.Add(p);
            }
        }

        /// <summary>
        /// A <c>VariableType</c>/<c>ConstraintType</c> object has exactly one meaningful child
        /// whose name is the type discriminator (<c>eTemperature</c>, <c>ePressure</c>,
        /// <c>eComponentComposition</c>, …) and whose value is the label (or an array of labels).
        /// Returns <c>(discriminatorKey, firstLabel)</c>.
        /// </summary>
        private static (string? key, string? label) ExtractTypeKeyLabel(JObject typeObj)
        {
            foreach (var prop in typeObj.Properties())
            {
                if (prop.Name == "tml_elements") continue;
                var v = prop.Value;
                if (v.Type == JTokenType.String) return (prop.Name, (string?)v);
                if (v.Type == JTokenType.Array)
                {
                    foreach (var e in (JArray)v)
                        if (e.Type == JTokenType.String) return (prop.Name, (string?)e);
                    return (prop.Name, null);
                }
                return (prop.Name, null);
            }
            return (null, null);
        }

        private static ConstraintKind? MapKind(string localName, string label)
        {
            // See note in ThermoMLParser.cs: accept Temperature/Pressure variants by localName
            // substring so eBubblePointTemperature / eVaporOrSublimationPressure / etc. are
            // normalized to K / kPa like the canonical eTemperature / ePressure.
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
