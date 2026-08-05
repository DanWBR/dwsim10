using System.Collections.Generic;

namespace DWSIM.PureCompoundData.Estimation.Joback
{
    /// Group-contribution coefficients for the Joback method.
    /// Values from Reid, Prausnitz, Poling, "The Properties of Gases and Liquids", 4th ed., Tables 2-3 / 2-7.
    /// Units: Tc, Tb, Tm in K; Pc in bar (we convert to Pa at call site); Vc in cm3/mol;
    /// Hform, Gform, Hfus in kJ/mol; Cp A..D for Cp (J/mol/K) = A + B*T + C*T^2 + D*T^3 with T in K.
    internal static class JobackGroupTable
    {
        internal sealed class Contribution
        {
            public double Tc, Pc, Vc, Tb, Tm, Hform, Gform, Hfus;
            public double CpA, CpB, CpC, CpD;
            /// Total number of atoms (heavy + implicit H) contributed by this group - needed
            /// for Joback's Pc formula which sums atoms over the whole molecule.
            public int AtomCount;
        }

        internal static readonly IReadOnlyDictionary<string, Contribution> Groups =
            new Dictionary<string, Contribution>
            {
                // Non-ring (atoms = heavy + implicit H)
                ["-CH3"] = New(0.0141, -0.0012, 65, 23.58, -5.10, -76.45, -43.96, 0.908,
                               19.500, -8.08e-3, 1.53e-4, -9.67e-8, 4),
                [">CH2"] = New(0.0189, 0.0000, 56, 22.88, 11.27, -20.64, 8.42, 2.590,
                               -0.909, 9.50e-2, -5.44e-5, 1.19e-8, 3),
                [">CH-"] = New(0.0164, 0.0020, 41, 21.74, 12.64, 29.89, 58.36, 0.749,
                              -23.00, 2.04e-1, -2.65e-4, 1.20e-7, 2),
                [">C<"] = New(0.0067, 0.0043, 27, 18.25, 46.43, 82.23, 116.02, -1.460,
                              -66.20, 4.27e-1, -6.41e-4, 3.01e-7, 1),
                ["=CH2"] = New(0.0113, -0.0028, 56, 18.18, -4.32, -9.63, 3.77, -0.473,
                              23.600, -3.81e-2, 1.72e-4, -1.03e-7, 3),
                ["=CH-"] = New(0.0129, -0.0006, 46, 24.96, 8.73, 37.97, 48.53, 2.691,
                              -8.000, 1.05e-1, -9.63e-5, 3.56e-8, 2),
                ["=C<"] = New(0.0117, 0.0011, 38, 24.14, 11.14, 83.99, 92.36, 1.724,
                              -28.10, 2.08e-1, -3.06e-4, 1.46e-7, 1),
                ["=CH- (ring)"] = New(0.0082, 0.0011, 41, 26.73, 8.13, 2.09, 11.30, 2.544,
                                      -2.140, 5.74e-2, -1.64e-6, -1.59e-8, 2),
                ["=C< (ring)"] = New(0.0143, 0.0008, 32, 31.01, 37.02, 46.43, 54.05, 3.059,
                                     -8.250, 1.01e-1, -1.42e-4, 6.78e-8, 1),
                ["-OH (alcohol)"] = New(0.0741, 0.0112, 28, 92.88, 44.45, -208.04, -189.20, 2.406,
                                        25.700, -6.91e-2, 1.77e-4, -9.88e-8, 2),
                ["-OH (phenol)"] = New(0.0240, 0.0184, -25, 76.34, 82.83, -221.65, -197.37, 4.490,
                                       -2.810, 1.11e-1, -1.16e-4, 4.94e-8, 2),
                ["-O- (non-ring)"] = New(0.0168, 0.0015, 18, 22.42, 22.23, -132.22, -105.00, 1.188,
                                         25.500, -6.32e-2, 1.11e-4, -5.48e-8, 1),
                [">C=O (non-ring)"] = New(0.0380, 0.0031, 62, 76.75, 61.20, -133.22, -120.50, 4.787,
                                          6.45, 6.70e-2, -3.57e-5, 2.86e-9, 2),
                ["O=CH- (aldehyde)"] = New(0.0379, 0.0030, 82, 72.24, 36.90, -162.03, -143.48, 2.115,
                                            30.900, -3.36e-2, 1.60e-4, -9.88e-8, 3),
                ["-COOH (acid)"] = New(0.0791, 0.0077, 89, 169.09, 155.50, -426.72, -387.87, 11.051,
                                       24.100, 4.27e-2, 8.04e-5, -6.87e-8, 4),
                ["-COO- (ester)"] = New(0.0481, 0.0005, 82, 81.10, 53.60, -337.92, -301.95, 6.959,
                                        24.500, 4.02e-2, 4.02e-5, -4.52e-8, 3),
                ["-NH2"] = New(0.0243, 0.0109, 38, 73.23, 66.89, -22.02, 14.07, 4.884,
                               26.900, -4.12e-2, 1.64e-4, -9.76e-8, 3),
                [">NH (non-ring)"] = New(0.0295, 0.0077, 35, 50.17, 52.66, 53.47, 89.39, 4.323,
                                          -1.210, 7.62e-2, -4.86e-5, 1.05e-8, 2),
                ["-F"] = New(0.0111, -0.0057, 27, -0.03, -15.78, -251.92, -247.19, 1.398,
                             26.500, -9.13e-2, 1.91e-4, -1.03e-7, 1),
                ["-Cl"] = New(0.0105, -0.0049, 58, 38.13, 13.55, -71.55, -64.31, 2.515,
                              33.300, -9.63e-2, 1.87e-4, -9.96e-8, 1),
                ["-Br"] = New(0.0133, 0.0057, 71, 66.86, 43.43, -29.48, -38.06, 3.603,
                              28.600, -6.49e-2, 1.36e-4, -7.45e-8, 1),
                ["-SH"] = New(0.0031, 0.0084, 63, 63.56, 20.09, -17.33, -22.99, 2.376,
                              35.300, -7.58e-2, 1.85e-4, -1.03e-7, 2),
            };

        private static Contribution New(double tc, double pc, double vc, double tb, double tm,
                                         double hf, double gf, double hfus,
                                         double a, double b, double c, double d,
                                         int atoms)
            => new Contribution { Tc = tc, Pc = pc, Vc = vc, Tb = tb, Tm = tm,
                                  Hform = hf, Gform = gf, Hfus = hfus,
                                  CpA = a, CpB = b, CpC = c, CpD = d,
                                  AtomCount = atoms };
    }
}
