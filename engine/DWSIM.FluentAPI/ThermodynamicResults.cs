using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace DWSIM.Automation.FluentAPI
{
    /// <summary>A single mixture critical point: temperature (K), pressure (Pa), molar volume (m3/mol).</summary>
    public sealed class CriticalPointResult
    {
        /// <summary>Critical temperature in Kelvin.</summary>
        public double TemperatureK { get; }
        /// <summary>Critical pressure in Pascal.</summary>
        public double PressurePa { get; }
        /// <summary>Critical molar volume in m3/mol.</summary>
        public double MolarVolumeM3PerMol { get; }

        internal CriticalPointResult(double t, double p, double v)
        {
            TemperatureK = t;
            PressurePa = p;
            MolarVolumeM3PerMol = v;
        }
    }

    /// <summary>
    /// Result of a phase envelope calculation for a multicomponent mixture.
    /// All temperatures in K, pressures in Pa, enthalpies in kJ/kg, entropies in kJ/(kg*K), volumes in m3/kg.
    /// </summary>
    public sealed class PhaseEnvelopeResult
    {
        /// <summary>Bubble curve temperatures (K).</summary>
        public IReadOnlyList<double> BubbleTemperaturesK { get; }
        /// <summary>Bubble curve pressures (Pa).</summary>
        public IReadOnlyList<double> BubblePressuresPa { get; }
        /// <summary>Bubble curve enthalpies.</summary>
        public IReadOnlyList<double> BubbleEnthalpies { get; }
        /// <summary>Bubble curve entropies.</summary>
        public IReadOnlyList<double> BubbleEntropies { get; }
        /// <summary>Bubble curve specific volumes.</summary>
        public IReadOnlyList<double> BubbleVolumes { get; }

        /// <summary>Dew curve temperatures (K).</summary>
        public IReadOnlyList<double> DewTemperaturesK { get; }
        /// <summary>Dew curve pressures (Pa).</summary>
        public IReadOnlyList<double> DewPressuresPa { get; }
        /// <summary>Dew curve enthalpies.</summary>
        public IReadOnlyList<double> DewEnthalpies { get; }
        /// <summary>Dew curve entropies.</summary>
        public IReadOnlyList<double> DewEntropies { get; }
        /// <summary>Dew curve specific volumes.</summary>
        public IReadOnlyList<double> DewVolumes { get; }

        /// <summary>Critical point(s) identified on the envelope.</summary>
        public IReadOnlyList<CriticalPointResult> CriticalPoints { get; }

        /// <summary>Quality line temperatures (K). Populated when <c>QualityLine = true</c>.</summary>
        public IReadOnlyList<double> QualityTemperaturesK { get; }
        /// <summary>Quality line pressures (Pa). Populated when <c>QualityLine = true</c>.</summary>
        public IReadOnlyList<double> QualityPressuresPa { get; }

        /// <summary>Phase stability/instability curve temperatures (K).</summary>
        public IReadOnlyList<double> StabilityTemperaturesK { get; }
        /// <summary>Phase stability/instability curve pressures (Pa).</summary>
        public IReadOnlyList<double> StabilityPressuresPa { get; }

        /// <summary>Second liquid phase (L2) bubble temperatures (K). Populated when liquid instability is detected.</summary>
        public IReadOnlyList<double> BubbleTemperaturesK_L2 { get; }
        /// <summary>Second liquid phase (L2) bubble pressures (Pa).</summary>
        public IReadOnlyList<double> BubblePressuresPa_L2 { get; }
        /// <summary>Second liquid phase (L2) bubble enthalpies.</summary>
        public IReadOnlyList<double> BubbleEnthalpies_L2 { get; }
        /// <summary>Second liquid phase (L2) bubble entropies.</summary>
        public IReadOnlyList<double> BubbleEntropies_L2 { get; }
        /// <summary>Second liquid phase (L2) bubble volumes.</summary>
        public IReadOnlyList<double> BubbleVolumes_L2 { get; }

        /// <summary>Third liquid phase (L3) bubble temperatures (K).</summary>
        public IReadOnlyList<double> BubbleTemperaturesK_L3 { get; }
        /// <summary>Third liquid phase (L3) bubble pressures (Pa).</summary>
        public IReadOnlyList<double> BubblePressuresPa_L3 { get; }
        /// <summary>Third liquid phase (L3) bubble enthalpies.</summary>
        public IReadOnlyList<double> BubbleEnthalpies_L3 { get; }
        /// <summary>Third liquid phase (L3) bubble entropies.</summary>
        public IReadOnlyList<double> BubbleEntropies_L3 { get; }
        /// <summary>Third liquid phase (L3) bubble volumes.</summary>
        public IReadOnlyList<double> BubbleVolumes_L3 { get; }

        /// <summary>Solid-liquid equilibrium temperatures, first curve (K).</summary>
        public IReadOnlyList<double> SLE_TemperaturesK_1 { get; }
        /// <summary>Solid-liquid equilibrium pressures, first curve (Pa).</summary>
        public IReadOnlyList<double> SLE_PressuresPa_1 { get; }
        /// <summary>Solid-liquid equilibrium temperatures, second curve (K).</summary>
        public IReadOnlyList<double> SLE_TemperaturesK_2 { get; }
        /// <summary>Solid-liquid equilibrium pressures, second curve (Pa).</summary>
        public IReadOnlyList<double> SLE_PressuresPa_2 { get; }

        /// <summary>Widom line (Cp-based) temperatures (K).</summary>
        public IReadOnlyList<double> WidomCp_TemperaturesK { get; }
        /// <summary>Widom line (Cp-based) pressures (Pa).</summary>
        public IReadOnlyList<double> WidomCp_PressuresPa { get; }
        /// <summary>Widom line (isothermal compressibility-based) temperatures (K).</summary>
        public IReadOnlyList<double> WidomBetaT_TemperaturesK { get; }
        /// <summary>Widom line (isothermal compressibility-based) pressures (Pa).</summary>
        public IReadOnlyList<double> WidomBetaT_PressuresPa { get; }

        internal PhaseEnvelopeResult(object[] raw)
        {
            BubbleTemperaturesK = ToDoubleList(raw[0]);
            BubblePressuresPa = ToDoubleList(raw[1]);
            BubbleEnthalpies = ToDoubleList(raw[2]);
            BubbleEntropies = ToDoubleList(raw[3]);
            BubbleVolumes = ToDoubleList(raw[4]);

            DewTemperaturesK = ToDoubleList(raw[5]);
            DewPressuresPa = ToDoubleList(raw[6]);
            DewEnthalpies = ToDoubleList(raw[7]);
            DewEntropies = ToDoubleList(raw[8]);
            DewVolumes = ToDoubleList(raw[9]);

            CriticalPoints = ParseCriticalPoints(raw[15]);

            QualityTemperaturesK = ToDoubleList(raw[16]);
            QualityPressuresPa = ToDoubleList(raw[17]);

            StabilityTemperaturesK = ToDoubleList(raw[18]);
            StabilityPressuresPa = ToDoubleList(raw[19]);

            BubbleTemperaturesK_L2 = ToDoubleList(raw[25]);
            BubblePressuresPa_L2 = ToDoubleList(raw[26]);
            BubbleEnthalpies_L2 = ToDoubleList(raw[27]);
            BubbleEntropies_L2 = ToDoubleList(raw[28]);
            BubbleVolumes_L2 = ToDoubleList(raw[29]);

            BubbleTemperaturesK_L3 = ToDoubleList(raw[30]);
            BubblePressuresPa_L3 = ToDoubleList(raw[31]);
            BubbleEnthalpies_L3 = ToDoubleList(raw[32]);
            BubbleEntropies_L3 = ToDoubleList(raw[33]);
            BubbleVolumes_L3 = ToDoubleList(raw[34]);

            SLE_TemperaturesK_1 = ToDoubleList(raw[35]);
            SLE_PressuresPa_1 = ToDoubleList(raw[36]);
            SLE_TemperaturesK_2 = ToDoubleList(raw[37]);
            SLE_PressuresPa_2 = ToDoubleList(raw[38]);

            WidomCp_TemperaturesK = ToDoubleList(raw[39]);
            WidomCp_PressuresPa = ToDoubleList(raw[40]);
            WidomBetaT_TemperaturesK = ToDoubleList(raw[41]);
            WidomBetaT_PressuresPa = ToDoubleList(raw[42]);
        }

        private static IReadOnlyList<CriticalPointResult> ParseCriticalPoints(object cpObj)
        {
            var al = cpObj as ArrayList;
            if (al == null || al.Count == 0)
                return Array.Empty<CriticalPointResult>();
            var result = new List<CriticalPointResult>(al.Count);
            foreach (var item in al)
            {
                if (item is object[] arr && arr.Length >= 3)
                    result.Add(new CriticalPointResult(
                        Convert.ToDouble(arr[0]),
                        Convert.ToDouble(arr[1]),
                        Convert.ToDouble(arr[2])));
            }
            return result;
        }

        private static IReadOnlyList<double> ToDoubleList(object obj)
        {
            if (obj == null) return Array.Empty<double>();
            if (obj is List<double> ld) return ld;
            if (obj is ArrayList al)
            {
                var list = new List<double>(al.Count);
                foreach (var item in al)
                    list.Add(Convert.ToDouble(item));
                return list;
            }
            return Array.Empty<double>();
        }
    }

    /// <summary>
    /// Result of a binary phase diagram calculation.
    /// Compositions are mole fractions of the first compound (0 to 1).
    /// Temperatures in K, pressures in Pa depending on diagram type.
    /// </summary>
    public sealed class BinaryEnvelopeResult
    {
        /// <summary>Diagram type: "T-x-y", "P-x-y", "(T)x-y", or "(P)x-y".</summary>
        public string DiagramType { get; }

        /// <summary>Composition axis (mole fraction of first compound).</summary>
        public IReadOnlyList<double> X { get; }
        /// <summary>Bubble-point curve values (T in K for T-x-y, P in Pa for P-x-y).</summary>
        public IReadOnlyList<double> Y1 { get; }
        /// <summary>Dew-point curve values (T in K for T-x-y, P in Pa for P-x-y).</summary>
        public IReadOnlyList<double> Y2 { get; }

        /// <summary>LLE first liquid composition (mole fraction). May be empty.</summary>
        public IReadOnlyList<double> LLE_X1 { get; }
        /// <summary>LLE second liquid composition (mole fraction). May be empty.</summary>
        public IReadOnlyList<double> LLE_X2 { get; }
        /// <summary>LLE curve values (T or P). May be empty.</summary>
        public IReadOnlyList<double> LLE_Y { get; }

        /// <summary>SLE first curve composition. T-x-y only, may be empty.</summary>
        public IReadOnlyList<double> SLE_X1 { get; }
        /// <summary>SLE first curve temperatures (K). T-x-y only, may be empty.</summary>
        public IReadOnlyList<double> SLE_Y1 { get; }
        /// <summary>SLE second curve composition. T-x-y only, may be empty.</summary>
        public IReadOnlyList<double> SLE_X2 { get; }
        /// <summary>SLE second curve temperatures (K). T-x-y only, may be empty.</summary>
        public IReadOnlyList<double> SLE_Y2 { get; }

        /// <summary>Critical locus composition. T-x-y only, may be empty.</summary>
        public IReadOnlyList<double> Critical_X { get; }
        /// <summary>Critical locus temperatures (K). T-x-y only, may be empty.</summary>
        public IReadOnlyList<double> Critical_Y { get; }

        internal BinaryEnvelopeResult(string type, object[] raw)
        {
            DiagramType = type;
            X = ToDoubleList(raw.Length > 0 ? raw[0] : null);
            Y1 = ToDoubleList(raw.Length > 1 ? raw[1] : null);
            Y2 = ToDoubleList(raw.Length > 2 ? raw[2] : null);
            LLE_X1 = ToDoubleList(raw.Length > 3 ? raw[3] : null);
            LLE_X2 = ToDoubleList(raw.Length > 4 ? raw[4] : null);
            LLE_Y = ToDoubleList(raw.Length > 5 ? raw[5] : null);
            SLE_X1 = ToDoubleList(raw.Length > 6 ? raw[6] : null);
            SLE_Y1 = ToDoubleList(raw.Length > 7 ? raw[7] : null);
            SLE_X2 = ToDoubleList(raw.Length > 8 ? raw[8] : null);
            SLE_Y2 = ToDoubleList(raw.Length > 9 ? raw[9] : null);
            Critical_X = ToDoubleList(raw.Length > 10 ? raw[10] : null);
            Critical_Y = ToDoubleList(raw.Length > 11 ? raw[11] : null);
        }

        private static IReadOnlyList<double> ToDoubleList(object obj)
        {
            if (obj == null) return Array.Empty<double>();
            if (obj is List<double> ld) return ld;
            if (obj is ArrayList al)
            {
                var list = new List<double>(al.Count);
                foreach (var item in al)
                    list.Add(Convert.ToDouble(item));
                return list;
            }
            return Array.Empty<double>();
        }
    }
}
