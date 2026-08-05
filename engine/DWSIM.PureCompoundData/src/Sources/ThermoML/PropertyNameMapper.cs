using System;
using System.Collections.Generic;
using DWSIM.PureCompoundData.Core;

namespace DWSIM.PureCompoundData.Sources.ThermoML
{
    /// Maps ThermoML variable/property labels (e.g. "Vapor or sublimation pressure, kPa")
    /// to a <see cref="PropertyCategory"/>, a stable short property code, and the canonical
    /// unit we store in <see cref="PureCompoundRecord"/>.
    /// The mapping consults the pmod's phase list to disambiguate liquid vs vapor vs solid
    /// when the ThermoML label itself is phase-agnostic (e.g. viscosity, density, Cp).
    internal static class PropertyNameMapper
    {
        internal enum Phase { Unknown, Liquid, Vapor, Solid }

        internal static Phase DetectPhase(IReadOnlyList<string> phaseIds)
        {
            foreach (var p in phaseIds)
            {
                if (p == null) continue;
                if (Contains(p, "liquid")) return Phase.Liquid;
                if (Contains(p, "crystal") || Contains(p, "solid")) return Phase.Solid;
                if (Contains(p, "gas") || Contains(p, "vapor") || Contains(p, "vapour"))
                    return Phase.Vapor;
            }
            return Phase.Unknown;
        }

        internal static string? PhaseCode(Phase p) => p switch
        {
            Phase.Liquid => "L",
            Phase.Vapor => "V",
            Phase.Solid => "S",
            _ => null
        };

        internal readonly struct Mapped
        {
            public Mapped(PropertyCategory category, string propertyCode, string unit, double unitFactor, bool isScalar = false)
            {
                Category = category;
                PropertyCode = propertyCode;
                Unit = unit;
                UnitFactor = unitFactor;
                IsScalar = isScalar;
            }
            public PropertyCategory Category { get; }
            public string PropertyCode { get; }
            public string Unit { get; }
            /// Multiplier to convert the raw ThermoML value into <see cref="Unit"/>.
            public double UnitFactor { get; }
            /// True for point-constant properties (Tc, Pc, Vc, Tb, Tm, Hfus, omega) where
            /// the measurement itself is the quantity and there's no T/P variable.
            public bool IsScalar { get; }
        }

        internal static bool TryMap(string label, Phase phase, out Mapped mapped)
        {
            mapped = default;
            if (string.IsNullOrWhiteSpace(label)) return false;
            var l = label!;

            // Scalar constants - match before the T-dependent categories because
            // "critical pressure" would otherwise be swallowed by the "pressure" check,
            // and "normal boiling temperature" must not leak into vapor-pressure matching.
            if (Contains(l, "critical temperature"))
            {
                mapped = new Mapped(PropertyCategory.Critical, "Tc", "K", 1.0, isScalar: true);
                return true;
            }
            if (Contains(l, "critical pressure"))
            {
                var f = Contains(l, "kpa") ? 1000.0 : Contains(l, "mpa") ? 1e6 : 1.0;
                mapped = new Mapped(PropertyCategory.Critical, "Pc", "Pa", f, isScalar: true);
                return true;
            }
            if (Contains(l, "critical molar volume"))
            {
                mapped = new Mapped(PropertyCategory.Critical, "Vc", "m3/mol", 1.0, isScalar: true);
                return true;
            }
            if (Contains(l, "critical density"))
            {
                // kg/m3 - caller converts to Vc using MW; keep raw for now.
                mapped = new Mapped(PropertyCategory.Critical, "rhoC", "kg/m3", 1.0, isScalar: true);
                return true;
            }
            if (Contains(l, "normal boiling temperature") || Contains(l, "boiling temperature"))
            {
                mapped = new Mapped(PropertyCategory.NormalBoilingPoint, "Tb", "K", 1.0, isScalar: true);
                return true;
            }
            if (Contains(l, "normal melting temperature") ||
                Contains(l, "temperature of fusion") ||
                Contains(l, "melting temperature") ||
                Contains(l, "triple-point temperature") ||
                Contains(l, "triple point temperature"))
            {
                mapped = new Mapped(PropertyCategory.MeltingPoint, "Tm", "K", 1.0, isScalar: true);
                return true;
            }
            if (Contains(l, "enthalpy of fusion") || Contains(l, "enthalpy of melting"))
            {
                var f = Contains(l, "kj/mol") ? 1000.0 : 1.0;
                mapped = new Mapped(PropertyCategory.EnthalpyOfFusion, "Hfus", "J/mol", f, isScalar: true);
                return true;
            }
            if (Contains(l, "acentric factor") || Contains(l, "pitzer acentric"))
            {
                mapped = new Mapped(PropertyCategory.Acentric, "omega", "-", 1.0, isScalar: true);
                return true;
            }

            if (Contains(l, "vapor or sublimation pressure") ||
                Contains(l, "vapor pressure") ||
                Contains(l, "sublimation pressure"))
            {
                var factor = Contains(l, "kpa") ? 1000.0 : Contains(l, "mpa") ? 1e6 : 1.0;
                mapped = new Mapped(PropertyCategory.VaporPressure, "Psat", "Pa", factor);
                return true;
            }

            if (Contains(l, "mass density"))
            {
                var cat = phase switch
                {
                    Phase.Vapor => PropertyCategory.VaporDensity,
                    Phase.Solid => PropertyCategory.SolidDensity,
                    _ => PropertyCategory.LiquidDensity
                };
                mapped = new Mapped(cat, "rho", "kg/m3", 1.0);
                return true;
            }

            if (Contains(l, "viscosity"))
            {
                var cat = phase == Phase.Vapor ? PropertyCategory.VaporViscosity
                                               : PropertyCategory.LiquidViscosity;
                var factor = Contains(l, "mpa*s") || Contains(l, "mpa.s") ? 1e-3 : 1.0;
                mapped = new Mapped(cat, "mu", "Pa*s", factor);
                return true;
            }

            if (Contains(l, "thermal conductivity"))
            {
                var cat = phase == Phase.Vapor ? PropertyCategory.VaporThermalConductivity
                                               : PropertyCategory.LiquidThermalConductivity;
                mapped = new Mapped(cat, "lambda", "W/m/K", 1.0);
                return true;
            }

            if (Contains(l, "surface tension"))
            {
                var factor = Contains(l, "mn/m") ? 1e-3 : 1.0;
                mapped = new Mapped(PropertyCategory.SurfaceTension, "sigma", "N/m", factor);
                return true;
            }

            if (Contains(l, "enthalpy of vaporization") || Contains(l, "enthalpy of sublimation"))
            {
                var factor = Contains(l, "kj/mol") ? 1000.0 : 1.0;
                mapped = new Mapped(PropertyCategory.HeatOfVaporization, "HVap", "J/mol", factor);
                return true;
            }

            if (Contains(l, "heat capacity"))
            {
                var cat = phase switch
                {
                    Phase.Vapor => PropertyCategory.IdealGasCp,
                    Phase.Solid => PropertyCategory.SolidCp,
                    _ => PropertyCategory.LiquidCp
                };
                mapped = new Mapped(cat, "Cp", "J/mol/K", 1.0);
                return true;
            }

            return false;
        }

        internal static bool IsTemperature(string label)
            => !string.IsNullOrWhiteSpace(label) && Contains(label, "temperature");

        internal static bool IsPressure(string label)
            => !string.IsNullOrWhiteSpace(label) && Contains(label, "pressure") &&
               !Contains(label, "vapor") && !Contains(label, "sublimation");

        private static bool Contains(string hay, string needle)
            => hay.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
