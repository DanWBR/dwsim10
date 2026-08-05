using System;
using System.Collections.Generic;
using DWSIM.PureCompoundData.Core;

namespace DWSIM.PureCompoundData.Estimation.Correlations
{
    /// Lee-Kesler acentric-factor correlation (Reid-Prausnitz-Poling Eq. 2-3.3):
    ///   omega = (ln(Pc/1.01325) - 5.92714 + 6.09648/Tbr + 1.28862*ln(Tbr) - 0.169347*Tbr^6) /
    ///           (15.2518 - 15.6875/Tbr - 13.4721*ln(Tbr) + 0.43577*Tbr^6)
    /// with Tbr = Tb/Tc and Pc in bar (standard atmospheric pressure 1.01325 bar).
    public sealed class LeeKeslerAcentric : IPropertyEstimator
    {
        public string Name => "LeeKesler";
        public IReadOnlyList<PropertyCategory> Provides => new[] { PropertyCategory.Acentric };
        public IReadOnlyList<PropertyCategory> Requires => new[]
        {
            PropertyCategory.Critical, PropertyCategory.NormalBoilingPoint
        };

        public EstimationResult Estimate(CompoundInputs inputs)
        {
            var r = new EstimationResult();
            if (!(inputs.Tc.HasValue && inputs.Pc.HasValue && inputs.Tb.HasValue)) return r;

            double tc = inputs.Tc.Value;
            double pcBar = inputs.Pc.Value / 1e5;
            double tbr = inputs.Tb.Value / tc;
            if (tbr <= 0 || tbr >= 1) return r;

            // omega = -(ln(Pc/1 atm) + f0(Tbr)) / f1(Tbr)
            // with f0(Tr) = 5.92714 - 6.09648/Tr - 1.28862*ln(Tr) + 0.169347*Tr^6
            //      f1(Tr) = 15.2518 - 15.6875/Tr - 13.4721*ln(Tr) + 0.43577*Tr^6
            double f0 = 5.92714 - 6.09648 / tbr - 1.28862 * Math.Log(tbr)
                         + 0.169347 * Math.Pow(tbr, 6);
            double f1 = 15.2518 - 15.6875 / tbr - 13.4721 * Math.Log(tbr)
                         + 0.43577 * Math.Pow(tbr, 6);
            if (Math.Abs(f1) < 1e-12) return r;

            r.Values["omega"] = -(Math.Log(pcBar / 1.01325) + f0) / f1;
            r.Units["omega"] = "-";
            return r;
        }
    }
}
