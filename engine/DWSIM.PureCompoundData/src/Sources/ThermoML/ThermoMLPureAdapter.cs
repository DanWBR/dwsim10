using System;
using System.Collections.Generic;
using System.Linq;
using DWSIM.PureCompoundData.Core;
using Peq = DWSIM.PhaseEquilibriumData.Core;
using PeqTml = DWSIM.PhaseEquilibriumData.Sources.ThermoML;

namespace DWSIM.PureCompoundData.Sources.ThermoML
{
    /// Walks a parsed phaseq <see cref="PeqTml.ThermoMLFile"/> and emits one
    /// <see cref="PureCompoundRecord"/> per (pmod, recognized property) pair for pmods
    /// with a single component.
    internal static class ThermoMLPureAdapter
    {
        internal const string ProviderName = "ThermoML";

        internal static IEnumerable<PureCompoundRecord> Adapt(PeqTml.ThermoMLFile file)
        {
            if (file == null) yield break;

            var citation = new Citation(
                file.Doi,
                file.Title,
                file.Authors.ToArray(),
                file.Journal,
                file.Year,
                file.Volume,
                file.Pages);

            foreach (var pmod in file.PureOrMixtureData)
            {
                if (pmod.ComponentOrder.Count != 1) continue;
                if (!file.Compounds.TryGetValue(pmod.ComponentOrder[0], out var peqCompound))
                    continue;

                var compound = MapCompound(peqCompound);
                var phase = PropertyNameMapper.DetectPhase(pmod.PhaseIds);
                var phaseCode = PropertyNameMapper.PhaseCode(phase);
                var method = MapMethod(pmod.Method);

                // Per-pmod T/P constraint fallbacks, in K / kPa (phaseq canonicalizes these).
                double? pmodTK = null, pmodPkPa = null;
                foreach (var c in pmod.Constraints)
                {
                    if (c.Kind == Peq.ConstraintKind.Temperature) pmodTK = c.Value;
                    else if (c.Kind == Peq.ConstraintKind.Pressure) pmodPkPa = c.Value;
                }

                // Discover which PropertyNames (ThermoML ePropName) map to recognized
                // properties. Fall back to VariableNames for older files where vapor
                // pressure / density appears as a Variable rather than a Property.
                var propertyVars = new List<(string Label, PropertyNameMapper.Mapped M)>();
                foreach (var v in pmod.PropertyNames)
                {
                    if (PropertyNameMapper.TryMap(v, phase, out var m))
                        propertyVars.Add((v, m));
                }
                foreach (var v in pmod.VariableNames)
                {
                    if (PropertyNameMapper.TryMap(v, phase, out var m))
                        propertyVars.Add((v, m));
                }

                if (propertyVars.Count == 0) continue;

                // Temperature variable key - phaseq stores the full label as the dictionary key.
                string? tKey = pmod.VariableNames.FirstOrDefault(PropertyNameMapper.IsTemperature);
                string? pKey = pmod.VariableNames.FirstOrDefault(PropertyNameMapper.IsPressure);

                foreach (var (label, m) in propertyVars)
                {
                    var points = new List<PropertyPoint>();
                    foreach (var dp in pmod.Points)
                    {
                        if (!dp.Values.TryGetValue(label, out var rawValue)) continue;
                        var value = rawValue * m.UnitFactor;

                        double tK;
                        if (tKey != null && dp.Values.TryGetValue(tKey, out var tv))
                            tK = tv;
                        else if (pmodTK.HasValue)
                            tK = pmodTK.Value;
                        else if (m.IsScalar)
                            // Scalar measurements (Tc, Pc, Vc, Tb, Tm, Hfus, omega) carry no
                            // T variable - the value is the quantity itself. Parked at 0.
                            tK = 0.0;
                        else
                            continue; // T-dependent and no temperature → skip point

                        double? pPa = null;
                        if (pKey != null && dp.Values.TryGetValue(pKey, out var pv))
                            pPa = pv * 1000.0; // kPa -> Pa
                        else if (pmodPkPa.HasValue)
                            pPa = pmodPkPa.Value * 1000.0;

                        points.Add(new PropertyPoint(tK, pPa, value, null, phaseCode));
                    }

                    if (points.Count == 0) continue;

                    double tMin = points[0].T, tMax = points[0].T;
                    for (int i = 1; i < points.Count; i++)
                    {
                        if (points[i].T < tMin) tMin = points[i].T;
                        if (points[i].T > tMax) tMax = points[i].T;
                    }

                    var id = IdHasher.ComputeRecordId(
                        ProviderName,
                        compound.CasNumber,
                        m.Category,
                        m.PropertyCode,
                        file.Doi,
                        pmod.Index);

                    yield return new PureCompoundRecord(
                        id,
                        compound,
                        m.Category,
                        m.PropertyCode,
                        ScalarValue: points.Count == 1 ? (double?)points[0].Value : null,
                        Points: points,
                        Unit: m.Unit,
                        TMin: tMin,
                        TMax: tMax,
                        Fits: null,
                        Method: method,
                        Citation: citation,
                        SourceProvider: ProviderName);
                }
            }
        }

        private static Compound MapCompound(Peq.Compound c)
            => new Compound(
                c.CasNumber,
                c.CommonName,
                c.IupacName,
                c.Smiles,
                c.InChIKey,
                c.MolecularFormula,
                c.MolecularWeight);

        private static MeasurementMethod MapMethod(Peq.MeasurementMethod m) => m switch
        {
            Peq.MeasurementMethod.StaticCell => MeasurementMethod.StaticCell,
            Peq.MeasurementMethod.EbuliometerOthmer => MeasurementMethod.EbuliometerOthmer,
            Peq.MeasurementMethod.EbuliometerSwietoslawski => MeasurementMethod.EbuliometerSwietoslawski,
            Peq.MeasurementMethod.RecirculatingStill => MeasurementMethod.RecirculatingStill,
            Peq.MeasurementMethod.HeadspaceGC => MeasurementMethod.HeadspaceGC,
            Peq.MeasurementMethod.InverseGC => MeasurementMethod.InverseGC,
            Peq.MeasurementMethod.DSC => MeasurementMethod.DSC,
            Peq.MeasurementMethod.Unknown => MeasurementMethod.Unknown,
            _ => MeasurementMethod.Other
        };
    }
}
