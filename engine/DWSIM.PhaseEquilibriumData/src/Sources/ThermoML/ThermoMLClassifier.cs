using System;
using System.Linq;
using DWSIM.PhaseEquilibriumData.Core;

namespace DWSIM.PhaseEquilibriumData.Sources.ThermoML
{
    /// <summary>Pure function: takes a parsed PureOrMixtureData AST and returns an EquilibriumType.</summary>
    public static class ThermoMLClassifier
    {
        public static EquilibriumType Classify(ThermoMLPureOrMixture pmod)
        {
            if (pmod == null) throw new ArgumentNullException(nameof(pmod));

            if (pmod.HasAzeotropMarker ||
                pmod.PhaseIds.Any(p => p.IndexOf("azeotrop", StringComparison.OrdinalIgnoreCase) >= 0))
                return EquilibriumType.VLE_Azeotropic;

            int liquidCount = pmod.PhaseIds.Count(p =>
                p.IndexOf("liquid", StringComparison.OrdinalIgnoreCase) >= 0);
            bool hasGas = pmod.PhaseIds.Any(p =>
                p.IndexOf("gas", StringComparison.OrdinalIgnoreCase) >= 0
                || p.IndexOf("vapor", StringComparison.OrdinalIgnoreCase) >= 0
                || p.IndexOf("vapour", StringComparison.OrdinalIgnoreCase) >= 0);
            bool hasSolid = pmod.PhaseIds.Any(p =>
                p.IndexOf("crystal", StringComparison.OrdinalIgnoreCase) >= 0
                || p.IndexOf("solid", StringComparison.OrdinalIgnoreCase) >= 0);

            bool hasTemp = pmod.Constraints.Any(c => c.Kind == ConstraintKind.Temperature);
            bool hasPres = pmod.Constraints.Any(c => c.Kind == ConstraintKind.Pressure);

            if (liquidCount >= 1 && hasGas && hasSolid) return EquilibriumType.Unknown;

            if (liquidCount >= 1 && hasGas)
            {
                if (liquidCount >= 2) return EquilibriumType.VLLE;
                if (hasPres && !hasTemp) return EquilibriumType.VLE_Isobaric;
                if (hasTemp && !hasPres) return EquilibriumType.VLE_Isothermal;
                return EquilibriumType.Unknown;
            }

            if (liquidCount >= 2 && !hasGas) return EquilibriumType.LLE;
            if (liquidCount >= 1 && hasSolid) return EquilibriumType.SLE;
            if (hasGas && hasSolid) return EquilibriumType.SVE;

            return EquilibriumType.Unknown;
        }
    }
}
