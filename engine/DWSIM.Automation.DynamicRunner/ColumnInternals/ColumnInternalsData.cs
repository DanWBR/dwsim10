//    Column internals rating: data model, packing catalogue and case file.
//    Copyright 2026 Daniel Wagner Oliveira de Medeiros
//
//    This file is part of DWSIM.
//
//    DWSIM is free software: you can redistribute it and/or modify
//    it under the terms of the GNU General Public License as published by
//    the Free Software Foundation, either version 3 of the License, or
//    (at your option) any later version.
//
//    DWSIM is distributed in the hope that it will be useful,
//    but WITHOUT ANY WARRANTY; without even the implied warranty of
//    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//    GNU General Public License for more details.
//
//    You should have received a copy of the GNU General Public License
//    along with DWSIM.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace DWSIM.Automation.DynamicRunner.ColumnInternals
{
    /// <summary>The kind of internal a section of the column carries.</summary>
    public enum InternalType
    {
        SieveTray = 0,
        ValveTray = 1,
        BubbleCapTray = 2,
        RandomPacking = 3,
        StructuredPacking = 4
    }

    /// <summary>Which correlation rates the capacity and the pressure drop of a packed section.</summary>
    public enum PackingModel
    {
        /// <summary>Robbins (1991) pressure drop with the Kister and Gill (1991) flood pressure drop.</summary>
        RobbinsKisterGill = 0,
        /// <summary>Billet and Schultes (1999) holdup, loading, flooding and pressure drop.</summary>
        BilletSchultes = 1,
        /// <summary>Rocha, Bravo and Fair (1993) holdup, pressure drop and flooding of structured packings.</summary>
        RochaBravoFair = 2
    }

    /// <summary>Which correlation gives the HETP of a packed section.</summary>
    public enum HetpModel
    {
        /// <summary>Onda, Takeuchi and Okumoto (1968) with the Bravo and Fair (1982) area for large packings.</summary>
        Onda = 0,
        /// <summary>Billet and Schultes (1999) two-film model with the packing constants C_L and C_V.</summary>
        BilletSchultes = 1,
        /// <summary>Rules of thumb only (Porter and Jenkins for random, Kister for structured packings).</summary>
        RuleOfThumb = 2,
        /// <summary>Rocha, Bravo and Fair (1996) mass transfer model of structured packings.</summary>
        RochaBravoFair = 3
    }

    /// <summary>Which correlation gives the entrainment flooding velocity of a tray.</summary>
    public enum TrayFloodModel
    {
        /// <summary>Fair (1961) chart, as the Lygeros and Magoulas (1986) fit; the industry standard.</summary>
        Fair = 0,
        /// <summary>Kister and Haas (1990), recommended by Kister for sieve and valve trays.</summary>
        KisterHaas = 1
    }

    /// <summary>How the valves of a valve tray are held (Klein 1982): the legs add to the weight the vapour has to lift.</summary>
    public enum ValveLegs
    {
        ThreeLegs = 0,
        FourLegs = 1,
        Caged = 2
    }

    /// <summary>Which procedure rates a valve tray.</summary>
    public enum ValveTrayModel
    {
        /// <summary>Fair or Kister-Haas flooding with Klein's dry pressure drop (Kister's recommendation).</summary>
        Klein = 0,
        /// <summary>Glitsch Ballast Tray Design Manual, Bulletin 4900 (6th ed., 1993): CAF capacity, downcomer design velocity, dry and total pressure drop, backup and leakage point.</summary>
        Glitsch = 1
    }

    /// <summary>How the cap pressure drop of a bubble-cap tray is computed.</summary>
    public enum BubbleCapMethod
    {
        /// <summary>Bolles (1956): riser, reversal and annulus drop from K_c and the slot opening.</summary>
        Bolles = 0,
        /// <summary>Modified Dauphine relations (Bolles 1956 after Dauphine): riser, reversal and dry slot drops corrected for the wet cap.</summary>
        Dauphine = 1
    }

    /// <summary>One packing of the catalogue. Fp and Fpd in 1/m, a in m2/m3, epsilon in m3/m3; the
    /// Billet and Schultes constants are NaN when the packing was not characterised by them.</summary>
    public class PackingData
    {
        public string Name = "";
        public string Material = "";
        public string Size = "";
        public bool Structured = false;
        /// <summary>Packing factor for the GPDC / Kister-Gill route, 1/m (Perry 7th Table 14-7, Seader Table 6.8).</summary>
        public double Fp = double.NaN;
        /// <summary>Dry packing factor for the Robbins pressure drop, 1/m (Robbins 1991, Perry 7th Table 14-7b).</summary>
        public double Fpd = double.NaN;
        /// <summary>Specific surface area, m2/m3.</summary>
        public double a = double.NaN;
        /// <summary>Void fraction, m3/m3.</summary>
        public double Epsilon = double.NaN;
        /// <summary>Nominal size, m (0 when unknown); used by the rules of thumb and by Onda.</summary>
        public double NominalSize = 0.0;
        public double Ch = double.NaN, Cp = double.NaN, CL = double.NaN, CV = double.NaN, Cs = double.NaN, CFl = double.NaN;
        /// <summary>Corrugation angle from the horizontal, degrees (structured packings; 45 unless stated).</summary>
        public double CorrugationAngle = 45.0;
        /// <summary>Corrugation side (channel side) S of a structured packing, m; NaN = estimated from a and epsilon.</summary>
        public double CorrugationSide = double.NaN;
        /// <summary>Surface enhancement factor F_SE of Rocha, Bravo and Fair (0.35 for embossed sheet metal, the value the authors give for Flexipac, Gempak, Intalox and Mellapak).</summary>
        public double SurfaceEnhancement = 0.35;
        public string Source = "";

        /// <summary>Corrugation side used by the Rocha-Bravo-Fair model: the catalogue value, or 4.5 epsilon / a, which
        /// reproduces the measured sides of the common sheet-metal packings within about 15 %.</summary>
        public double EffectiveCorrugationSide
        {
            get
            {
                if (!double.IsNaN(CorrugationSide) && CorrugationSide > 0) return CorrugationSide;
                if (double.IsNaN(a) || a <= 0) return double.NaN;
                return 4.5 * (double.IsNaN(Epsilon) ? 0.95 : Epsilon) / a;
            }
        }

        public string DisplayName { get { return (Name + " " + Material + " " + Size).Trim(); } }

        public bool HasBilletHydraulics { get { return !double.IsNaN(Ch) && !double.IsNaN(Cp) && !double.IsNaN(Cs); } }
        public bool HasBilletMassTransfer { get { return !double.IsNaN(CL) && !double.IsNaN(CV); } }

        public PackingData Clone() { return (PackingData)MemberwiseClone(); }
    }

    /// <summary>
    /// Packing catalogue. Random packings: Seader and Henley, Separation Process Principles, 2nd ed.,
    /// Table 6.8 (Billet and Schultes constants, a, epsilon, and Fp in ft2/ft3 converted to 1/m) and
    /// Perry's Chemical Engineers' Handbook, 7th ed., Table 14-7b (Fp of Kister and Gill and Fpd of
    /// Robbins, 1/m). Structured packings: Seader Table 6.8 and Perry Table 14-7a.
    /// </summary>
    public static class PackingCatalogue
    {
        private static List<PackingData> _all;

        public static IList<PackingData> All
        {
            get
            {
                if (_all == null) _all = Build();
                return _all;
            }
        }

        public static PackingData Find(string displayName)
        {
            foreach (var p in All) if (string.Equals(p.DisplayName, displayName, StringComparison.OrdinalIgnoreCase)) return p;
            return null;
        }

        private const double FtToM = 3.280839895; // ft2/ft3 = 1/ft -> 1/m

        // Seader Table 6.8 row: Fp in ft2/ft3 (NaN when blank), a, eps, then the six Billet constants (NaN when blank)
        private static PackingData S(string name, string mat, string size, double fpFt, double a, double eps,
            double ch, double cp, double cl, double cv, double cs, double cfl, double sizeM, bool structured = false)
        {
            return new PackingData
            {
                Name = name, Material = mat, Size = size, Structured = structured,
                Fp = double.IsNaN(fpFt) ? double.NaN : fpFt * FtToM, a = a, Epsilon = eps,
                Ch = ch, Cp = cp, CL = cl, CV = cv, Cs = cs, CFl = cfl, NominalSize = sizeM,
                Source = "Seader & Henley 2nd ed., Table 6.8 (Billet & Schultes)"
            };
        }

        // Perry Table 14-7b row: Fp and Fpd in 1/m (NaN when blank)
        private static PackingData P(string name, string mat, string size, double fp, double fpd, double a, double voidPct, double sizeM, bool structured = false)
        {
            return new PackingData
            {
                Name = name, Material = mat, Size = size, Structured = structured,
                Fp = fp, Fpd = fpd, a = a, Epsilon = voidPct / 100.0, NominalSize = sizeM,
                Source = structured ? "Perry 7th ed., Table 14-7a (Kister & Gill)" : "Perry 7th ed., Table 14-7b (Kister & Gill; Robbins)"
            };
        }

        private const double N = double.NaN;

        private static List<PackingData> Build()
        {
            var list = new List<PackingData>
            {
                // ---------------- random packings, Seader Table 6.8 ----------------
                S("Berl saddles", "Ceramic", "25 mm", 110, 260.0, 0.680, 0.620, N, 1.246, 0.387, N, N, 0.025),
                S("Berl saddles", "Ceramic", "13 mm", 240, 545.0, 0.650, 0.833, N, 1.364, 0.232, N, N, 0.013),
                S("Bialecki rings", "Metal", "50 mm", N, 121.0, 0.966, 0.798, 0.719, 1.721, 0.302, 2.916, 1.896, 0.050),
                S("Bialecki rings", "Metal", "35 mm", N, 155.0, 0.967, 0.787, 1.011, 1.412, 0.390, 2.753, 1.885, 0.035),
                S("Bialecki rings", "Metal", "25 mm", N, 210.0, 0.956, 0.692, 0.891, 1.461, 0.331, 2.521, 1.856, 0.025),
                S("DIN-PAK rings", "Plastic", "70 mm", N, 110.7, 0.938, 0.991, 0.378, 1.527, 0.326, 2.970, 1.912, 0.070),
                S("DIN-PAK rings", "Plastic", "47 mm", N, 131.2, 0.923, 1.173, 0.514, 1.690, 0.354, 2.929, 1.991, 0.047),
                S("Envi Pac rings", "Plastic", "80 mm, no. 3", N, 60.0, 0.955, 0.641, 0.358, 1.603, 0.257, 2.846, 1.522, 0.080),
                S("Envi Pac rings", "Plastic", "60 mm, no. 2", N, 98.4, 0.961, 0.794, 0.338, 1.522, 0.296, 2.987, 1.864, 0.060),
                S("Envi Pac rings", "Plastic", "32 mm, no. 1", N, 138.9, 0.936, 1.039, 0.549, 1.517, 0.459, 2.944, 2.012, 0.032),
                S("Glitsch rings", "Metal", "30 PMK", N, 180.5, 0.975, 0.930, 0.851, 1.920, 0.450, 2.694, 1.900, 0.030),
                S("Glitsch rings", "Metal", "30 P", N, 164.0, 0.959, 0.851, 1.056, 1.577, 0.398, 2.564, 1.760, 0.030),
                S("Glitsch CMR rings", "Metal", "1.5 in", N, 174.9, 0.974, 0.935, 0.632, N, N, 2.697, 1.841, 0.038),
                S("Glitsch CMR rings", "Metal", "1.5 in, T", N, 188.0, 0.972, 0.870, 0.627, N, N, 2.790, 1.870, 0.038),
                S("Glitsch CMR rings", "Metal", "1.0 in", N, 232.5, 0.971, 1.040, 0.641, N, N, 2.703, 1.996, 0.025),
                S("Glitsch CMR rings", "Metal", "0.5 in", N, 356.0, 0.952, N, 0.882, 2.038, 0.495, 2.644, 2.178, 0.013),
                S("Cascade minirings", "Metal", "1.5 in CMR", 29, 174.9, 0.974, 0.935, N, N, N, N, N, 0.038),
                S("Cascade minirings", "Metal", "1.0 in CMR", 40, 232.5, 0.971, 1.040, N, N, N, N, N, 0.025),
                S("Hackettes", "Plastic", "45 mm", N, 139.5, 0.928, 0.643, 0.399, N, N, 2.832, 1.966, 0.045),
                S("Hiflow rings", "Ceramic", "75 mm", 15, 54.1, 0.868, N, 0.435, N, N, N, N, 0.075),
                S("Hiflow rings", "Ceramic", "50 mm", 29, 89.7, 0.809, N, 0.538, 1.377, 0.379, 2.819, 1.694, 0.050),
                S("Hiflow rings", "Ceramic", "38 mm", 37, 111.8, 0.788, N, 0.621, 1.659, 0.464, 2.840, 1.930, 0.038),
                S("Hiflow rings", "Ceramic", "20 mm, 6 stg.", N, 265.8, 0.776, 0.958, N, N, N, N, N, 0.020),
                S("Hiflow rings", "Ceramic", "20 mm, 4 stg.", N, 261.2, 0.779, 1.167, 0.628, 1.744, 0.465, N, N, 0.020),
                S("Hiflow rings", "Metal", "50 mm", 16, 92.3, 0.977, 0.876, 0.421, 1.168, 0.408, 2.702, 1.626, 0.050),
                S("Hiflow rings", "Metal", "25 mm", 42, 202.9, 0.962, 0.799, 0.689, 1.641, 0.402, 2.918, 2.177, 0.025),
                S("Hiflow rings", "Plastic", "90 mm", 9, 69.7, 0.968, N, 0.276, N, N, N, N, 0.090),
                S("Hiflow rings", "Plastic", "50 mm, hydr.", N, 118.4, 0.925, N, 0.311, 1.553, 0.369, 2.894, 1.871, 0.050),
                S("Hiflow rings", "Plastic", "50 mm", 20, 117.1, 0.924, 1.038, 0.327, 1.487, 0.345, N, N, 0.050),
                S("Hiflow rings", "Plastic", "25 mm", N, 194.5, 0.918, N, 0.741, 1.577, 0.390, 2.841, 1.989, 0.025),
                S("Hiflow rings, super", "Plastic", "50 mm, S", N, 82.0, 0.942, N, 0.414, 1.219, 0.342, 2.866, 1.702, 0.050),
                S("Hiflow saddles", "Plastic", "50 mm", N, 86.4, 0.938, N, 0.454, N, N, N, N, 0.050),
                S("Intalox saddles", "Ceramic", "50 mm", 40, 114.6, 0.761, N, 0.747, N, N, N, N, 0.050),
                S("Intalox saddles", "Plastic", "50 mm", 28, 122.1, 0.908, N, 0.758, N, N, N, N, 0.050),
                S("NORPAC rings", "Plastic", "50 mm", 14, 86.8, 0.947, 0.651, 0.350, 1.080, 0.322, 2.959, 1.786, 0.050),
                S("NORPAC rings", "Plastic", "35 mm", 21, 141.8, 0.944, 0.587, 0.371, 0.756, 0.425, 3.179, 2.242, 0.035),
                S("NORPAC rings", "Plastic", "25 mm, type B", N, 202.0, 0.953, 0.601, 0.397, 0.883, 0.366, 3.277, 2.472, 0.025),
                S("NORPAC rings", "Plastic", "25 mm, 10 stg.", N, 197.9, 0.920, N, 0.383, 0.976, 0.410, 2.865, 2.083, 0.025),
                S("NORPAC rings", "Plastic", "25 mm", 31, 180.0, 0.927, 0.601, N, N, N, N, N, 0.025),
                S("NORPAC rings", "Plastic", "22 mm", N, 249.0, 0.913, N, 0.397, N, N, N, N, 0.022),
                S("NORPAC rings", "Plastic", "15 mm", N, 311.4, 0.918, 0.343, 0.365, N, N, N, N, 0.015),
                S("Pall rings", "Ceramic", "50 mm", 43, 155.2, 0.754, 1.066, 0.233, 1.278, 0.333, 3.793, 3.024, 0.050),
                S("Pall rings", "Metal", "50 mm", 27, 112.6, 0.951, 0.784, 0.763, 1.192, 0.410, 2.725, 1.580, 0.050),
                S("Pall rings", "Metal", "35 mm", 40, 139.4, 0.965, 0.644, 0.967, 1.012, 0.341, 2.629, 1.679, 0.035),
                S("Pall rings", "Metal", "25 mm", 56, 223.5, 0.954, 0.719, 0.957, 1.440, 0.336, 2.627, 2.083, 0.025),
                S("Pall rings", "Metal", "15 mm", 70, 368.4, 0.933, 0.590, 0.990, N, N, N, N, 0.015),
                S("Pall rings", "Plastic", "50 mm", 26, 111.1, 0.919, 0.593, 0.698, 1.239, 0.368, 2.816, 1.757, 0.050),
                S("Pall rings", "Plastic", "35 mm", 40, 151.1, 0.906, 0.718, 0.927, 0.856, 0.380, 2.654, 1.742, 0.035),
                S("Pall rings", "Plastic", "25 mm", 55, 225.0, 0.887, 0.528, 0.865, 0.905, 0.446, 2.696, 2.064, 0.025),
                S("Raflux rings", "Plastic", "15 mm", N, 307.9, 0.894, 0.491, 0.595, 1.913, 0.370, 2.825, 2.400, 0.015),
                S("Ralu flow", "Plastic", "1", N, 165.0, 0.940, 0.640, 0.485, 1.486, 0.360, 3.612, 2.401, 0.025),
                S("Ralu flow", "Plastic", "2", N, 100.0, 0.945, 0.640, 0.350, 1.270, 0.320, 3.412, 2.174, 0.050),
                S("Ralu rings", "Plastic", "50 mm, hydr.", N, 94.3, 0.939, 0.439, N, 1.481, 0.341, N, N, 0.050),
                S("Ralu rings", "Plastic", "50 mm", N, 95.2, 0.983, 0.640, 0.468, 1.520, 0.303, 2.843, 1.812, 0.050),
                S("Ralu rings", "Plastic", "38 mm", N, 150.0, 0.930, 0.640, 0.672, 1.320, 0.333, 2.843, 1.812, 0.038),
                S("Ralu rings", "Plastic", "25 mm", N, 190.0, 0.940, 0.719, 0.800, 1.320, 0.333, 2.841, 1.989, 0.025),
                S("Ralu rings", "Metal", "50 mm", N, 105.0, 0.975, 0.784, 0.763, 1.192, 0.345, 2.725, 1.580, 0.050),
                S("Ralu rings", "Metal", "38 mm", N, 135.0, 0.965, 0.644, 1.003, 1.277, 0.341, 2.629, 1.679, 0.038),
                S("Ralu rings", "Metal", "25 mm", N, 215.0, 0.960, 0.714, 0.957, 1.440, 0.336, 2.627, 2.083, 0.025),
                S("Raschig rings", "Carbon", "25 mm", N, 202.2, 0.720, 0.623, N, 1.379, 0.471, N, N, 0.025),
                S("Raschig rings", "Ceramic", "25 mm", 179, 190.0, 0.680, 0.577, 1.329, 1.361, 0.412, 2.454, 1.899, 0.025),
                S("Raschig rings", "Ceramic", "15 mm", 380, 312.0, 0.690, 0.648, N, 1.276, 0.401, N, N, 0.015),
                S("Raschig rings", "Ceramic", "10 mm", 1000, 440.0, 0.650, 0.791, N, 1.303, 0.272, N, N, 0.010),
                S("Raschig rings", "Ceramic", "6 mm", 1600, 771.9, 0.620, 1.094, N, 1.130, N, N, N, 0.006),
                S("Raschig rings", "Metal", "15 mm", 170, 378.4, 0.917, 0.455, N, N, N, N, N, 0.015),
                S("Raschig Super-rings", "Metal", "0.3", N, 315.0, 0.960, 0.750, 0.760, 1.500, 0.450, 3.560, 2.340, 0.020),
                S("Raschig Super-rings", "Metal", "0.5", N, 250.0, 0.975, 0.620, 0.780, 1.450, 0.430, 3.350, 2.200, 0.025),
                S("Raschig Super-rings", "Metal", "1", N, 160.0, 0.980, 0.750, 0.500, 1.290, 0.440, 3.491, 2.200, 0.040),
                S("Raschig Super-rings", "Metal", "2", N, 97.6, 0.985, 0.720, 0.464, 1.323, 0.400, 3.326, 2.096, 0.060),
                S("Raschig Super-rings", "Metal", "3", N, 80.0, 0.982, 0.620, 0.430, 0.850, 0.300, 3.260, 2.100, 0.080),
                S("Raschig Super-rings", "Plastic", "2", N, 100.0, 0.960, 0.720, 0.377, 1.250, 0.337, 3.326, 2.096, 0.060),
                S("Tellerettes", "Plastic", "25 mm", 40, 190.0, 0.930, 0.588, 0.538, 0.899, N, 2.913, 2.132, 0.025),
                S("Top-Pak rings", "Aluminum", "50 mm", N, 105.5, 0.956, 0.881, 0.604, 1.326, 0.389, 2.528, 1.579, 0.050),
                S("VSP rings", "Metal", "50 mm, no. 2", N, 104.6, 0.980, 1.135, 0.773, 1.222, 0.420, 2.806, 1.689, 0.050),
                S("VSP rings", "Metal", "25 mm, no. 1", N, 199.6, 0.975, 1.369, 0.782, 1.376, 0.405, 2.755, 1.970, 0.025),
                // ---------------- random packings, Perry Table 14-7b (Fp, Fpd) ----------------
                P("Raschig rings", "Ceramic", "13 mm", 1900, 1705, 370, 64, 0.013),
                P("Raschig rings", "Ceramic", "25 mm (Perry)", 587, 492, 190, 74, 0.025),
                P("Raschig rings", "Ceramic", "50 mm", 213, 230, 92, 74, 0.050),
                P("Raschig rings", "Ceramic", "75 mm", 121, N, 62, 75, 0.075),
                P("Raschig rings", "Metal", "19 mm", 984, N, 245, 80, 0.019),
                P("Raschig rings", "Metal", "25 mm", 472, 492, 185, 86, 0.025),
                P("Raschig rings", "Metal", "50 mm", 187, 223, 95, 92, 0.050),
                P("Raschig rings", "Metal", "75 mm", 105, N, 66, 95, 0.075),
                P("Pall rings", "Metal", "16 mm", 256, 262, N, 92, 0.016),
                P("Pall rings", "Metal", "25 mm (Perry)", 183, 174, 205, 94, 0.025),
                P("Pall rings", "Metal", "38 mm", 131, 91, 130, 95, 0.038),
                P("Pall rings", "Metal", "50 mm (Perry)", 89, 79, 115, 96, 0.050),
                P("Pall rings", "Metal", "90 mm", 59, 46, 92, 97, 0.090),
                P("Cascade mini rings (CMR)", "Metal", "1", 131, 102, 250, 96, 0.025),
                P("Cascade mini rings (CMR)", "Metal", "1.5", 95, N, 144, 97, 0.038),
                P("Cascade mini rings (CMR)", "Metal", "2.5", 72, 79, 123, 98, 0.064),
                P("Cascade mini rings (CMR)", "Metal", "3", 46, 43, 103, 98, 0.076),
                P("Cascade mini rings (CMR)", "Plastic", "1A", 98, 92, 185, 94, 0.025),
                P("Cascade mini rings (CMR)", "Plastic", "3A", 39, 33, 74, 96, 0.076),
                P("Berl saddles", "Ceramic", "6 mm", N, 2950, 900, 60, 0.006),
                P("Berl saddles", "Ceramic", "13 mm (Perry)", 790, 900, 465, 62, 0.013),
                P("Berl saddles", "Ceramic", "25 mm (Perry)", 360, 308, 250, 68, 0.025),
                P("Berl saddles", "Ceramic", "38 mm", 215, 154, 150, 71, 0.038),
                P("Berl saddles", "Ceramic", "50 mm", 150, 102, 105, 72, 0.050),
                P("Intalox saddles", "Ceramic", "13 mm", N, 613, 623, 71, 0.013),
                P("Intalox saddles", "Ceramic", "25 mm", 302, 208, 256, 73, 0.025),
                P("Intalox saddles", "Ceramic", "50 mm (Perry)", 131, 121, 118, 76, 0.050),
                P("Intalox saddles", "Ceramic", "75 mm", 72, 66, 92, 79, 0.075),
                P("Fleximax", "Metal", "300", 85, 85, 141, 98, 0.050),
                P("Fleximax", "Metal", "400", 56, 56, 85, 98, 0.075),
                P("Metal Intalox (IMTP)", "Metal", "25 mm", 134, 141, 230, 97, 0.025),
                P("Metal Intalox (IMTP)", "Metal", "40 mm", 79, 85, 154, 97, 0.040),
                P("Metal Intalox (IMTP)", "Metal", "50 mm", 59, 56, 98, 98, 0.050),
                P("Metal Intalox (IMTP)", "Metal", "70 mm", 39, N, 56, 98, 0.070),
                P("Nutter rings", "Metal", "1", 98, 98, 168, 98, 0.025),
                P("Nutter rings", "Metal", "2", 59, 56, 96, 98, 0.050),
                P("Nutter rings", "Metal", "2.5", 52, 49, 83, 98, 0.064),
                P("Nutter rings", "Metal", "3.0", 43, 36, 66, 98, 0.076),
                P("Pall rings", "Plastic", "25 mm (Perry)", 180, 180, 206, 90, 0.025),
                P("Pall rings", "Plastic", "50 mm (Perry)", 85, 82, 102, 92, 0.050),
                P("Pall rings", "Plastic", "90 mm", 56, 39, 85, 92, 0.090),
                P("Intalox saddles", "Plastic", "1", 131, 131, 207, 90, 0.025),
                P("Intalox saddles", "Plastic", "2", 92, 85, 108, 93, 0.050),
                P("Snowflake", "Plastic", "", 43, N, 92, 95, 0.075),
                P("Nor-Pac", "Plastic", "25 mm, 1", 82, N, 180, 92, 0.025),
                P("Nor-Pac", "Plastic", "38 mm, 1.5", 56, N, 144, 93, 0.038),
                P("Nor-Pac", "Plastic", "50 mm, 2.0", 39, N, 102, 94, 0.050),
                P("Tri-Pack", "Plastic", "25 mm, 1", 82, N, 180, 92, 0.025),
                P("Tri-Pack", "Plastic", "50 mm, 2", 39, N, 102, 94, 0.050),
                P("VSP", "Metal", "25 mm, 1 (Perry)", 105, N, 206, 98, 0.025),
                P("VSP", "Metal", "50 mm, 2 (Perry)", 69, N, 112, 96, 0.050),
                // ---------------- structured packings, Seader Table 6.8 ----------------
                S("Euroform", "Plastic", "PN-110", N, 110.0, 0.936, 0.511, 0.250, 0.973, 0.167, 3.075, 1.975, 0, true),
                S("Gempak", "Metal", "A2 T-304", N, 202.0, 0.977, 0.678, 0.344, N, N, 2.986, 2.099, 0, true),
                S("Impulse", "Ceramic", "100", N, 91.4, 0.838, 1.900, 0.417, 1.317, 0.327, 2.664, 1.655, 0, true),
                S("Impulse", "Metal", "250", N, 250.0, 0.975, 0.431, 0.262, 0.983, 0.270, 2.610, 1.996, 0, true),
                S("Koch-Sulzer", "Metal", "CY", 70, N, N, N, N, N, N, N, N, 0, true),
                S("Koch-Sulzer", "Metal", "BX", 21, N, N, N, N, N, N, N, N, 0, true),
                S("Mellapak", "Plastic", "250 Y", 22, 250.0, 0.970, 0.554, 0.292, N, N, 3.157, 2.464, 0, true),
                S("Montz", "Metal", "B1-100", N, 100.0, 0.987, 0.626, N, N, N, N, N, 0, true),
                S("Montz", "Metal", "B1-200", N, 200.0, 0.979, 0.547, 0.355, 0.971, 0.390, 3.116, 2.339, 0, true),
                S("Montz", "Metal", "B1-300", 33, 300.0, 0.930, 0.482, 0.295, 1.165, 0.422, 3.098, 2.464, 0, true),
                S("Montz", "Plastic", "C1-200", N, 200.0, 0.954, N, 0.453, 1.006, 0.412, N, N, 0, true),
                S("Montz", "Plastic", "C2-200", N, 200.0, 0.900, N, 0.481, 0.739, N, 2.653, 1.973, 0, true),
                S("Ralu Pak", "Metal", "YC-250", N, 250.0, 0.945, 0.650, 0.191, 1.334, 0.385, 3.178, 2.558, 0, true),
                // ---------------- structured packings, Perry Table 14-7a ----------------
                P("Flexipac", "Sheet metal", "1", 108, N, 558, 91, 0, true),
                P("Flexipac", "Sheet metal", "2", 72, N, 223, 93, 0, true),
                P("Flexipac", "Sheet metal", "3", 52, N, 134, 96, 0, true),
                P("Flexiramic", "Ceramic", "28", 131, N, 282, 70, 0, true),
                P("Flexiramic", "Ceramic", "48", 79, N, 157, 74, 0, true),
                P("Flexiramic", "Ceramic", "88", 49, N, 102, 85, 0, true),
                P("Gempak", "Sheet metal", "4A", 105, N, 446, 92, 0, true),
                P("Gempak", "Sheet metal", "3A", 69, N, 335, 93, 0, true),
                P("Gempak", "Sheet metal", "2A", 53, N, 223, 95, 0, true),
                P("Intalox structured", "Sheet metal", "1T", 66, N, 315, 95, 0, true),
                P("Intalox structured", "Sheet metal", "2T", 56, N, 213, 97, 0, true),
                P("Intalox structured", "Sheet metal", "3T", 43, N, 177, 97, 0, true),
                P("Max-Pak", "Sheet metal", "", 39, N, 229, 95, 0, true),
                P("Mellapak", "Sheet metal", "125Y", 33, N, 125, 97, 0, true),
                P("Mellapak", "Sheet metal", "250Y", 66, N, 250, 95, 0, true),
                P("Mellapak", "Sheet metal", "350Y", 75, N, 350, 93, 0, true),
                P("Mellapak", "Sheet metal", "500Y", N, N, 500, 91, 0, true),
                P("Montz-Pak", "Sheet metal", "B1-250", 72, N, 250, 95, 0, true),
                P("Ralu Pak", "Sheet metal", "250YC", N, N, 250, 95, 0, true),
                P("Sulzer", "Gauze", "AX", N, N, 250, 95, 0, true),
                P("Sulzer", "Gauze", "BX", 69, N, 492, 90, 0, true),
                P("Sulzer", "Gauze", "CY", N, N, 700, 85, 0, true)
            };
            // corrugation sides measured by Fair and Bravo (Chem. Eng. Progr. 86(1), 19, 1990; Ludwig vol. 2 Table 9-38)
            var sides = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                { "Flexipac Sheet metal 2", 0.0177 }, { "Gempak Sheet metal 2A", 0.0180 }, { "Intalox structured Sheet metal 2T", 0.0223 },
                { "Montz Metal B1-200", 0.0250 }, { "Montz-Pak Sheet metal B1-250", 0.0250 }, { "Mellapak Sheet metal 250Y", 0.0171 },
                { "Mellapak Plastic 250 Y", 0.0171 }, { "Sulzer Gauze BX", 0.0088 }, { "Koch-Sulzer Metal BX", 0.0088 }
            };
            foreach (var p in list)
            {
                if (p.Structured && p.Size.EndsWith("X", StringComparison.Ordinal)) p.CorrugationAngle = 60.0;
                double side;
                if (p.Structured && sides.TryGetValue(p.DisplayName, out side)) p.CorrugationSide = side;
            }
            return list;
        }
    }

    /// <summary>A range of stages carrying one kind of internal, with its geometry.</summary>
    public class InternalsSection
    {
        public string Name = "Section";
        /// <summary>First and last stage of the section, 1-based as the column shows them (the condenser is 1).</summary>
        public int FromStage = 2;
        public int ToStage = 2;
        public InternalType Type = InternalType.SieveTray;

        // ---- geometry shared by trays and packings (m) ----
        /// <summary>Column diameter, m. 0 = size the section for the target fraction of flood.</summary>
        public double Diameter = 0.0;

        // ---- trays ----
        public double TraySpacing = 0.5;
        /// <summary>Downcomer area as a fraction of the total cross section (single pass).</summary>
        public double DowncomerAreaFraction = 0.12;
        public double WeirHeight = 0.05;
        public double HoleDiameter = 0.005;
        /// <summary>Hole area as a fraction of the active (bubbling) area.</summary>
        public double HoleAreaFraction = 0.10;
        public double PlateThickness = 0.005;
        /// <summary>Clearance under the downcomer apron, m. 0 = weir height minus 10 mm.</summary>
        public double DowncomerClearance = 0.0;
        /// <summary>Kister's system (foaming) derating factor applied to the flooding velocity, 1 = none.</summary>
        public double SystemFactor = 1.0;
        public TrayFloodModel FloodModel = TrayFloodModel.Fair;
        /// <summary>Fraction of the open valve area over the active area (valve trays, Kister-Haas).</summary>
        public double ValveOpenAreaFraction = 0.12;

        // ---- valve trays (Klein 1982 / Bolles 1976 dry pressure drop) ----
        /// <summary>Valves per square metre of active area (12 to 16 per ft2 is usual; 130 to 170 per m2).</summary>
        public double ValvesPerArea = 130.0;
        /// <summary>Diameter of the deck hole under each valve, m (standardised at 1.5 in).</summary>
        public double ValveHoleDiameter = 0.0381;
        /// <summary>Valve metal thickness, m (16 gauge, 1.5 mm, is the common light valve).</summary>
        public double ValveThickness = 0.0015;
        /// <summary>Valve metal density, kg/m3 (carbon steel 7850, stainless 8030, aluminium 2700).</summary>
        public double ValveDensity = 7850.0;
        public ValveLegs ValveLegs = ValveLegs.FourLegs;
        /// <summary>True for a venturi (contoured) orifice, false for a flat orifice (Glitsch V-4 against V-1).</summary>
        public bool ValveVenturi = false;
        public ValveTrayModel ValveModel = ValveTrayModel.Klein;

        // ---- bubble-cap trays (Bolles 1956) ----
        /// <summary>Inside diameter of the cap, m.</summary>
        public double CapDiameter = 0.098;
        /// <summary>Inside diameter of the riser, m.</summary>
        public double RiserDiameter = 0.068;
        /// <summary>Cap pitch (centre to centre) over the cap outside diameter, triangular layout.</summary>
        public double CapPitchRatio = 1.4;
        public int SlotsPerCap = 50;
        /// <summary>Slot width at the base and slot height, m.</summary>
        public double SlotWidth = 0.0032;
        public double SlotHeight = 0.038;
        /// <summary>Slot top width over the base width: 1 for rectangular slots, 0 for triangular, between for trapezoidal.</summary>
        public double SlotTopWidthRatio = 1.0;
        public BubbleCapMethod CapMethod = BubbleCapMethod.Bolles;
        /// <summary>Riser height above the tray floor and height of the inside of the cap above the tray floor, m (Dauphine).</summary>
        public double RiserHeight = 0.076;
        public double CapInsideHeight = 0.100;
        /// <summary>Static slot seal: top of the outlet weir above the top of the slots, m.</summary>
        public double StaticSeal = 0.0127;
        /// <summary>Cap skirt clearance above the tray floor, m.</summary>
        public double SkirtClearance = 0.019;
        /// <summary>Liquid gradient across the tray, m. 0 = estimated from the Davies correlation as Bolles charts it.</summary>
        public double LiquidGradient = 0.0;

        // ---- packings ----
        /// <summary>Catalogue display name, or empty for the user-defined packing below.</summary>
        public string PackingName = "";
        public PackingData CustomPacking = null;
        /// <summary>Packed height of the section, m. 0 = compute from HETP times the number of stages.</summary>
        public double BedHeight = 0.0;
        public PackingModel PackingModel = PackingModel.RobbinsKisterGill;
        public HetpModel HetpModel = HetpModel.Onda;
        /// <summary>Liquid and vapour diffusivities of the transferring component, m2/s. 0 = estimated.</summary>
        public double LiquidDiffusivity = 0.0;
        public double VapourDiffusivity = 0.0;

        public PackingData ResolvePacking()
        {
            if (CustomPacking != null) return CustomPacking;
            if (!string.IsNullOrEmpty(PackingName)) return PackingCatalogue.Find(PackingName);
            return null;
        }

        public bool IsTray { get { return Type == InternalType.SieveTray || Type == InternalType.ValveTray || Type == InternalType.BubbleCapTray; } }

        public InternalsSection Clone()
        {
            var c = (InternalsSection)MemberwiseClone();
            if (CustomPacking != null) c.CustomPacking = CustomPacking.Clone();
            return c;
        }
    }

    /// <summary>The whole case: which column, its sections and the design targets. Saved as XML.</summary>
    public class ColumnInternalsInput
    {
        public const string FileExtension = ".dwint";

        public string ColumnName = "";
        public List<InternalsSection> Sections = new List<InternalsSection>();
        /// <summary>Design fraction of flood used when a section is sized (diameter = 0).</summary>
        public double TargetFloodFractionTrays = 0.80;
        public double TargetFloodFractionPackings = 0.70;
        /// <summary>Minimum downcomer residence time accepted, s.</summary>
        public double MinDowncomerResidenceTime = 3.0;
        /// <summary>Turndown ratio checked for weeping (minimum / design vapour rate).</summary>
        public double Turndown = 0.7;
        /// <summary>Rating and solving passes of the automatic iteration (rate, write the pressures and efficiencies into
        /// the column, solve the flowsheet, rate again) and the relative change of the column pressure drop that stops it.</summary>
        public int MaxIterations = 6;
        public double IterationTolerance = 0.02;
        /// <summary>What the automatic iteration writes into the column.</summary>
        public bool IteratePressures = true;
        public bool IterateEfficiencies = true;
        /// <summary>Whether the iteration re-stages the packed sections that have a bed height (stages = bed height / HETP).</summary>
        public bool IterateStages = true;

        public ColumnInternalsInput Clone()
        {
            var c = (ColumnInternalsInput)MemberwiseClone();
            c.Sections = Sections.Select(s => s.Clone()).ToList();
            return c;
        }

        public void CopyFrom(ColumnInternalsInput other)
        {
            var c = other.Clone();
            ColumnName = c.ColumnName; Sections = c.Sections;
            TargetFloodFractionTrays = c.TargetFloodFractionTrays; TargetFloodFractionPackings = c.TargetFloodFractionPackings;
            MinDowncomerResidenceTime = c.MinDowncomerResidenceTime; Turndown = c.Turndown;
            MaxIterations = c.MaxIterations; IterationTolerance = c.IterationTolerance;
            IteratePressures = c.IteratePressures; IterateEfficiencies = c.IterateEfficiencies; IterateStages = c.IterateStages;
        }

        private static readonly CultureInfo CI = CultureInfo.InvariantCulture;
        private static string D(double v) { return v.ToString("R", CI); }
        private static double PD(XElement e, string name, double def)
        {
            var x = e.Element(name);
            double v;
            return x != null && double.TryParse(x.Value, NumberStyles.Float, CI, out v) ? v : def;
        }
        private static int PI(XElement e, string name, int def)
        {
            var x = e.Element(name);
            int v;
            return x != null && int.TryParse(x.Value, NumberStyles.Integer, CI, out v) ? v : def;
        }
        private static string PS(XElement e, string name, string def)
        {
            var x = e.Element(name);
            return x != null ? x.Value : def;
        }

        public XElement ToXml()
        {
            var root = new XElement("ColumnInternalsCase",
                new XElement("Version", "1"),
                new XElement("ColumnName", ColumnName),
                new XElement("TargetFloodFractionTrays", D(TargetFloodFractionTrays)),
                new XElement("TargetFloodFractionPackings", D(TargetFloodFractionPackings)),
                new XElement("MinDowncomerResidenceTime", D(MinDowncomerResidenceTime)),
                new XElement("Turndown", D(Turndown)),
                new XElement("MaxIterations", MaxIterations.ToString(CI)),
                new XElement("IterationTolerance", D(IterationTolerance)),
                new XElement("IteratePressures", IteratePressures.ToString()),
                new XElement("IterateEfficiencies", IterateEfficiencies.ToString()),
                new XElement("IterateStages", IterateStages.ToString()));
            var secs = new XElement("Sections");
            foreach (var s in Sections)
            {
                var e = new XElement("Section",
                    new XElement("Name", s.Name),
                    new XElement("FromStage", s.FromStage.ToString(CI)),
                    new XElement("ToStage", s.ToStage.ToString(CI)),
                    new XElement("Type", s.Type.ToString()),
                    new XElement("Diameter", D(s.Diameter)),
                    new XElement("TraySpacing", D(s.TraySpacing)),
                    new XElement("DowncomerAreaFraction", D(s.DowncomerAreaFraction)),
                    new XElement("WeirHeight", D(s.WeirHeight)),
                    new XElement("HoleDiameter", D(s.HoleDiameter)),
                    new XElement("HoleAreaFraction", D(s.HoleAreaFraction)),
                    new XElement("PlateThickness", D(s.PlateThickness)),
                    new XElement("DowncomerClearance", D(s.DowncomerClearance)),
                    new XElement("SystemFactor", D(s.SystemFactor)),
                    new XElement("FloodModel", s.FloodModel.ToString()),
                    new XElement("ValveOpenAreaFraction", D(s.ValveOpenAreaFraction)),
                    new XElement("ValvesPerArea", D(s.ValvesPerArea)),
                    new XElement("ValveHoleDiameter", D(s.ValveHoleDiameter)),
                    new XElement("ValveThickness", D(s.ValveThickness)),
                    new XElement("ValveDensity", D(s.ValveDensity)),
                    new XElement("ValveLegs", s.ValveLegs.ToString()),
                    new XElement("ValveVenturi", s.ValveVenturi.ToString()),
                    new XElement("ValveModel", s.ValveModel.ToString()),
                    new XElement("CapDiameter", D(s.CapDiameter)),
                    new XElement("RiserDiameter", D(s.RiserDiameter)),
                    new XElement("CapPitchRatio", D(s.CapPitchRatio)),
                    new XElement("SlotsPerCap", s.SlotsPerCap.ToString(CI)),
                    new XElement("SlotWidth", D(s.SlotWidth)),
                    new XElement("SlotHeight", D(s.SlotHeight)),
                    new XElement("SlotTopWidthRatio", D(s.SlotTopWidthRatio)),
                    new XElement("CapMethod", s.CapMethod.ToString()),
                    new XElement("RiserHeight", D(s.RiserHeight)),
                    new XElement("CapInsideHeight", D(s.CapInsideHeight)),
                    new XElement("StaticSeal", D(s.StaticSeal)),
                    new XElement("SkirtClearance", D(s.SkirtClearance)),
                    new XElement("LiquidGradient", D(s.LiquidGradient)),
                    new XElement("PackingName", s.PackingName),
                    new XElement("BedHeight", D(s.BedHeight)),
                    new XElement("PackingModel", s.PackingModel.ToString()),
                    new XElement("HetpModel", s.HetpModel.ToString()),
                    new XElement("LiquidDiffusivity", D(s.LiquidDiffusivity)),
                    new XElement("VapourDiffusivity", D(s.VapourDiffusivity)));
                if (s.CustomPacking != null)
                {
                    var p = s.CustomPacking;
                    e.Add(new XElement("CustomPacking",
                        new XElement("Name", p.Name), new XElement("Material", p.Material), new XElement("Size", p.Size),
                        new XElement("Structured", p.Structured.ToString()),
                        new XElement("Fp", D(p.Fp)), new XElement("Fpd", D(p.Fpd)), new XElement("a", D(p.a)),
                        new XElement("Epsilon", D(p.Epsilon)), new XElement("NominalSize", D(p.NominalSize)),
                        new XElement("Ch", D(p.Ch)), new XElement("Cp", D(p.Cp)), new XElement("CL", D(p.CL)),
                        new XElement("CV", D(p.CV)), new XElement("Cs", D(p.Cs)), new XElement("CFl", D(p.CFl)),
                        new XElement("CorrugationAngle", D(p.CorrugationAngle)),
                        new XElement("CorrugationSide", D(p.CorrugationSide)),
                        new XElement("SurfaceEnhancement", D(p.SurfaceEnhancement))));
                }
                secs.Add(e);
            }
            root.Add(secs);
            return root;
        }

        public static ColumnInternalsInput FromXml(XElement root)
        {
            var inp = new ColumnInternalsInput();
            inp.ColumnName = PS(root, "ColumnName", "");
            inp.TargetFloodFractionTrays = PD(root, "TargetFloodFractionTrays", 0.8);
            inp.TargetFloodFractionPackings = PD(root, "TargetFloodFractionPackings", 0.7);
            inp.MinDowncomerResidenceTime = PD(root, "MinDowncomerResidenceTime", 3.0);
            inp.Turndown = PD(root, "Turndown", 0.7);
            inp.MaxIterations = Math.Max(1, PI(root, "MaxIterations", 6));
            inp.IterationTolerance = PD(root, "IterationTolerance", 0.02);
            bool bp, be;
            inp.IteratePressures = !bool.TryParse(PS(root, "IteratePressures", "True"), out bp) || bp;
            inp.IterateEfficiencies = !bool.TryParse(PS(root, "IterateEfficiencies", "True"), out be) || be;
            bool bs;
            inp.IterateStages = !bool.TryParse(PS(root, "IterateStages", "True"), out bs) || bs;
            var secs = root.Element("Sections");
            if (secs != null)
            {
                foreach (var e in secs.Elements("Section"))
                {
                    var s = new InternalsSection();
                    s.Name = PS(e, "Name", "Section");
                    s.FromStage = PI(e, "FromStage", 2);
                    s.ToStage = PI(e, "ToStage", 2);
                    InternalType t;
                    if (Enum.TryParse(PS(e, "Type", "SieveTray"), out t)) s.Type = t;
                    s.Diameter = PD(e, "Diameter", 0);
                    s.TraySpacing = PD(e, "TraySpacing", 0.5);
                    s.DowncomerAreaFraction = PD(e, "DowncomerAreaFraction", 0.12);
                    s.WeirHeight = PD(e, "WeirHeight", 0.05);
                    s.HoleDiameter = PD(e, "HoleDiameter", 0.005);
                    s.HoleAreaFraction = PD(e, "HoleAreaFraction", 0.10);
                    s.PlateThickness = PD(e, "PlateThickness", 0.005);
                    s.DowncomerClearance = PD(e, "DowncomerClearance", 0);
                    s.SystemFactor = PD(e, "SystemFactor", 1.0);
                    TrayFloodModel fm;
                    if (Enum.TryParse(PS(e, "FloodModel", "Fair"), out fm)) s.FloodModel = fm;
                    s.ValveOpenAreaFraction = PD(e, "ValveOpenAreaFraction", 0.12);
                    s.ValvesPerArea = PD(e, "ValvesPerArea", s.ValvesPerArea);
                    s.ValveHoleDiameter = PD(e, "ValveHoleDiameter", s.ValveHoleDiameter);
                    s.ValveThickness = PD(e, "ValveThickness", s.ValveThickness);
                    s.ValveDensity = PD(e, "ValveDensity", s.ValveDensity);
                    ValveLegs vl;
                    if (Enum.TryParse(PS(e, "ValveLegs", "FourLegs"), out vl)) s.ValveLegs = vl;
                    bool vv; s.ValveVenturi = bool.TryParse(PS(e, "ValveVenturi", "False"), out vv) && vv;
                    ValveTrayModel vm;
                    if (Enum.TryParse(PS(e, "ValveModel", "Klein"), out vm)) s.ValveModel = vm;
                    s.CapDiameter = PD(e, "CapDiameter", s.CapDiameter);
                    s.RiserDiameter = PD(e, "RiserDiameter", s.RiserDiameter);
                    s.CapPitchRatio = PD(e, "CapPitchRatio", s.CapPitchRatio);
                    s.SlotsPerCap = Math.Max(1, PI(e, "SlotsPerCap", s.SlotsPerCap));
                    s.SlotWidth = PD(e, "SlotWidth", s.SlotWidth);
                    s.SlotHeight = PD(e, "SlotHeight", s.SlotHeight);
                    s.SlotTopWidthRatio = Math.Max(0, Math.Min(1, PD(e, "SlotTopWidthRatio", 1.0)));
                    BubbleCapMethod cm;
                    if (Enum.TryParse(PS(e, "CapMethod", "Bolles"), out cm)) s.CapMethod = cm;
                    s.RiserHeight = PD(e, "RiserHeight", s.RiserHeight);
                    s.CapInsideHeight = PD(e, "CapInsideHeight", s.CapInsideHeight);
                    s.StaticSeal = PD(e, "StaticSeal", s.StaticSeal);
                    s.SkirtClearance = PD(e, "SkirtClearance", s.SkirtClearance);
                    s.LiquidGradient = PD(e, "LiquidGradient", 0);
                    s.PackingName = PS(e, "PackingName", "");
                    s.BedHeight = PD(e, "BedHeight", 0);
                    PackingModel pm;
                    if (Enum.TryParse(PS(e, "PackingModel", "RobbinsKisterGill"), out pm)) s.PackingModel = pm;
                    HetpModel hm;
                    if (Enum.TryParse(PS(e, "HetpModel", "Onda"), out hm)) s.HetpModel = hm;
                    s.LiquidDiffusivity = PD(e, "LiquidDiffusivity", 0);
                    s.VapourDiffusivity = PD(e, "VapourDiffusivity", 0);
                    var cp = e.Element("CustomPacking");
                    if (cp != null)
                    {
                        var p = new PackingData();
                        p.Name = PS(cp, "Name", ""); p.Material = PS(cp, "Material", ""); p.Size = PS(cp, "Size", "");
                        bool st; p.Structured = bool.TryParse(PS(cp, "Structured", "False"), out st) && st;
                        p.Fp = PD(cp, "Fp", double.NaN); p.Fpd = PD(cp, "Fpd", double.NaN); p.a = PD(cp, "a", double.NaN);
                        p.Epsilon = PD(cp, "Epsilon", double.NaN); p.NominalSize = PD(cp, "NominalSize", 0);
                        p.Ch = PD(cp, "Ch", double.NaN); p.Cp = PD(cp, "Cp", double.NaN); p.CL = PD(cp, "CL", double.NaN);
                        p.CV = PD(cp, "CV", double.NaN); p.Cs = PD(cp, "Cs", double.NaN); p.CFl = PD(cp, "CFl", double.NaN);
                        p.CorrugationAngle = PD(cp, "CorrugationAngle", 45);
                        p.CorrugationSide = PD(cp, "CorrugationSide", double.NaN);
                        p.SurfaceEnhancement = PD(cp, "SurfaceEnhancement", 0.35);
                        p.Source = "User";
                        s.CustomPacking = p;
                    }
                    inp.Sections.Add(s);
                }
            }
            return inp;
        }

        public void SaveToFile(string path)
        {
            var doc = new XDocument(new XDeclaration("1.0", "utf-8", "yes"), ToXml());
            doc.Save(path);
        }

        public static ColumnInternalsInput LoadFromFile(string path)
        {
            var doc = XDocument.Load(path);
            if (doc.Root == null || doc.Root.Name != "ColumnInternalsCase")
                throw new InvalidDataException("The file is not a column internals case.");
            return FromXml(doc.Root);
        }
    }

    /// <summary>Stage properties the rating reads: flows of the vapour rising through the stage and of the
    /// liquid leaving it, and the phase properties at the stage conditions. SI units.</summary>
    public class StageProperties
    {
        /// <summary>1-based stage number as the column shows it.</summary>
        public int Stage;
        public double T, P;
        /// <summary>Molar flows, mol/s.</summary>
        public double VaporMolarFlow, LiquidMolarFlow;
        /// <summary>Mass flows, kg/s.</summary>
        public double VaporMassFlow, LiquidMassFlow;
        public double VaporMW, LiquidMW;
        public double VaporDensity, LiquidDensity;
        public double VaporViscosity, LiquidViscosity;
        public double SurfaceTension;
        /// <summary>Diffusivities of the transferring component, m2/s (estimates unless the section gives them).</summary>
        public double VaporDiffusivity, LiquidDiffusivity;
        /// <summary>Stripping factor lambda = m V / L of the key component on the stage (K V / L).</summary>
        public double StrippingFactor = 1.0;
        /// <summary>Relative volatility of the light key to the heavy key on the stage (K_lk / K_hk), for O'Connell.</summary>
        public double RelativeVolatility = double.NaN;

        public double VaporVolumetricFlow { get { return VaporDensity > 0 ? VaporMassFlow / VaporDensity : 0; } }
        public double LiquidVolumetricFlow { get { return LiquidDensity > 0 ? LiquidMassFlow / LiquidDensity : 0; } }
    }

    /// <summary>Rating of one stage. Fields that do not apply to the internal are NaN.</summary>
    public class StageRating
    {
        public int Stage;
        public string SectionName = "";
        public InternalType Type;
        public double Diameter;

        public double FlowParameter;
        public double VaporLoad;            // m3/s
        public double LiquidLoad;           // m3/s
        public double CapacityFactor;       // C = u_n sqrt(rho_V/(rho_L-rho_V)), m/s (trays: net area)
        public double FloodingVelocity;     // m/s
        public double FloodFraction;        // -
        public double PressureDrop;         // Pa per tray, or Pa per metre of packing
        public double PressureDropTotal;    // Pa over the stage (trays: same as PressureDrop; packings: over the HETP)

        // trays
        public double NetVelocity = double.NaN;
        public double HoleVelocity = double.NaN;
        public double WeepPointVelocity = double.NaN;
        public double WeepRatio = double.NaN;           // u_h / u_h,min at design
        public double WeepRatioTurndown = double.NaN;   // at turndown
        public double WeirCrest = double.NaN;           // mm liquid
        public double DryPressureDrop = double.NaN;     // mm liquid
        public double TotalHead = double.NaN;           // mm liquid
        public double DowncomerBackup = double.NaN;     // mm liquid
        public double DowncomerBackupLimit = double.NaN;// mm liquid
        public double DowncomerResidenceTime = double.NaN; // s
        public double Entrainment = double.NaN;         // fractional entrainment psi
        public double EntrainmentEfficiencyFactor = double.NaN; // E_a / E_mv (Colburn)
        public double DowncomerVelocity = double.NaN;   // m/s
        public double OConnellEfficiency = double.NaN;  // overall column efficiency by O'Connell at the stage conditions
        public double AeratedLiquidHead = double.NaN;   // mm liquid, h_L of the valve and bubble-cap balances

        // valve trays
        public double ClosedBalanceVelocity = double.NaN; // hole velocity at which the valves start to open, m/s
        public double OpenBalanceVelocity = double.NaN;   // hole velocity at which all valves are open, m/s
        public double UnitReference = double.NaN;         // u_h / u_h,open balance ("unit reference" of the valve tray)
        public double DowncomerFloodFraction = double.NaN; // Glitsch: liquid load over the downcomer design velocity times the downcomer area
        public double DryDropLimitRatio = double.NaN;      // Glitsch: dry pressure drop over the 0.2 x tray spacing capacity limit
        /// <summary>Valves closed, opening or open; slots open fraction of bubble caps. Empty for sieve trays.</summary>
        public string Regime = "";

        // bubble-cap trays
        public double CapPressureDrop = double.NaN;     // mm liquid, riser + reversal + annulus (h_pc; Dauphine: h_r + h_ra)
        public double CapDropLimit = double.NaN;        // mm liquid, Dauphine: wet cap drop above which the vapour blows under the shroud ring
        public double SlotOpening = double.NaN;         // mm liquid, h_s
        public double SlotOpeningFraction = double.NaN; // h_s / slot height
        public double SlotLoad = double.NaN;            // vapour load over the maximum slot capacity
        public double LiquidGradient = double.NaN;      // mm liquid, delta across the tray
        public double VaporDistributionRatio = double.NaN; // delta / cap drop (Bolles: keep below 0.5)
        public double DynamicSeal = double.NaN;         // mm liquid over the top of the slots

        // packings
        public double LiquidHoldup = double.NaN;        // m3/m3
        public double LoadingVelocity = double.NaN;     // m/s (Billet)
        public double HETP = double.NaN;                // m
        public double HETPRuleOfThumb = double.NaN;     // m
        public double HG = double.NaN, HL = double.NaN, HOG = double.NaN; // m
        public double WettingRatio = double.NaN;        // u_L / u_L,min
        public double FFactor = double.NaN;             // u_V sqrt(rho_V), Pa^0.5

        public List<string> Warnings = new List<string>();
    }

    /// <summary>Rating of a section: its stages plus the section-level figures.</summary>
    public class SectionRating
    {
        public InternalsSection Section;
        public double Diameter;
        public double RequiredDiameter;     // for the target fraction of flood
        public int LimitingStage;
        public double MaxFloodFraction;
        public double TotalPressureDrop;    // Pa over the section
        public double BedHeight = double.NaN;   // m (packings)
        public double AverageHETP = double.NaN; // m
        public List<StageRating> Stages = new List<StageRating>();
        public List<string> Warnings = new List<string>();
    }

    public class ColumnInternalsResult
    {
        public string ColumnName = "";
        public List<StageProperties> StageProperties = new List<StageProperties>();
        public List<SectionRating> Sections = new List<SectionRating>();
        public List<string> Log = new List<string>();
        public double TotalPressureDrop;
        public double TotalHeight;
        /// <summary>Passes of the automatic iteration that produced this result (0 = a single rating).</summary>
        public int Iterations;
        public bool Converged = true;
        /// <summary>The case the last pass used (its stage ranges follow the re-staging); null for a single rating.</summary>
        public ColumnInternalsInput Input;
    }
}
