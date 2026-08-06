using System;
using System.Collections.Generic;
using DWSIM.Interfaces;
using DWSIM.Thermodynamics.BaseClasses;
using DWSIM.Thermodynamics.Streams;

using cv = DWSIM.SharedClasses.SystemsOfUnits.Converter;

namespace DWSIM.UI.Desktop.Editors
{

    /// <summary>A temperature-dependent property curve of a pure compound, in display units.</summary>
    public class CompoundCurve
    {
        public string Title = "";
        public string XTitle = "";
        public string YTitle = "";
        public List<double> X = new List<double>();
        public List<double> Y = new List<double>();

        public bool HasData => X.Count > 1;
    }

    /// <summary>
    /// Builds the temperature-dependent property curves of a pure compound over the validity
    /// range of each correlation. UI-agnostic: shared by the Eto compound viewer and the
    /// Avalonia pure compound properties utility.
    /// </summary>
    public class CompoundCurveBuilder
    {

        private readonly ConstantProperties compound;
        private readonly IUnitsOfMeasure su;
        private readonly DWSIM.Thermodynamics.PropertyPackages.RaoultPropertyPackage pp;

        public CompoundCurveBuilder(IFlowsheet flowsheet, ICompoundConstantProperties cp)
        {
            compound = (ConstantProperties)cp;
            su = flowsheet.FlowsheetOptions.SelectedUnitSystem;

            pp = new DWSIM.Thermodynamics.PropertyPackages.RaoultPropertyPackage(false);

            var ms = new MaterialStream("", "", flowsheet, pp);
            foreach (var phase in ms.Phases.Values)
            {
                phase.Compounds.Add(compound.Name, new Compound(compound.Name, ""));
                phase.Compounds[compound.Name].ConstantProperties = compound;
            }
            ms.EqualizeOverallComposition();

            pp.CurrentMaterialStream = ms;
        }

        /// <summary>Ions, salts and black oil pseudocompounds have no correlations to plot.</summary>
        public bool HasCurves => !compound.IsSalt && !compound.IsIon && !compound.IsBlackOil;

        public List<CompoundCurve> Build()
        {

            var curves = new List<CompoundCurve>();

            if (!HasCurves) return curves;

            double tc = compound.Critical_Temperature;
            double nbp = compound.Normal_Boiling_Point;
            double tfus = compound.TemperatureOfFusion;

            curves.Add(Curve("Ideal Gas Heat Capacity", su.heatCapacityCp, 200, 1500,
                T => pp.AUX_CPi(compound.Name, T)));

            curves.Add(Curve("Vapor Viscosity", su.viscosity,
                compound.Vapor_Viscosity_Tmin != 0 ? 0.6 * compound.Vapor_Viscosity_Tmin : 0.6 * tc,
                compound.Vapor_Viscosity_Tmin != 0 ? compound.Vapor_Viscosity_Tmax : tc,
                T => pp.AUX_VAPVISCi(compound, T)));

            curves.Add(Curve("Vapor Thermal Conductivity", su.thermalConductivity,
                Fallback(compound.Vapor_Thermal_Conductivity_Tmin, nbp),
                Fallback(compound.Vapor_Thermal_Conductivity_Tmax, tc),
                T => pp.AUX_VAPTHERMCONDi(compound, T, 101325)));

            curves.Add(Curve("Liquid Heat Capacity", su.heatCapacityCp,
                Fallback(compound.Liquid_Heat_Capacity_Tmin, tfus, nbp * 0.6),
                Fallback(compound.Liquid_Heat_Capacity_Tmax, nbp * 0.99),
                T => pp.AUX_LIQ_Cpi(compound, T)));

            curves.Add(Curve("Heat of Vaporization", su.enthalpy,
                Fallback(compound.HVap_TMIN, 0.6 * tc),
                Fallback(compound.HVap_TMAX, tc * 0.999),
                T => Convert.ToDouble(pp.AUX_HVAPi(compound.Name, T))));

            curves.Add(Curve("Vapor Pressure", su.pressure,
                Fallback(compound.Vapor_Pressure_TMIN, 0.4 * tc),
                Fallback(compound.Vapor_Pressure_TMAX, tc),
                T => Convert.ToDouble(pp.AUX_PVAPi(compound.Name, T))));

            curves.Add(Curve("Surface Tension", su.surfaceTension,
                Fallback(compound.Surface_Tension_Tmin, tfus, nbp * 0.6),
                Fallback(compound.Surface_Tension_Tmax, nbp * 0.999),
                T => pp.AUX_SURFTi(compound, T)));

            curves.Add(Curve("Liquid Viscosity", su.viscosity, 0.6 * tc, tc,
                T => Convert.ToDouble(pp.AUX_LIQVISCi(compound.Name, T, 101325))));

            curves.Add(Curve("Liquid Density", su.density,
                Fallback(compound.Liquid_Density_Tmin, tfus, nbp * 0.6),
                Fallback(compound.Liquid_Density_Tmax, nbp * 0.999),
                T => pp.AUX_LIQDENSi(compound, T)));

            curves.Add(Curve("Liquid Thermal Conductivity", su.thermalConductivity,
                Fallback(compound.Liquid_Thermal_Conductivity_Tmin, tfus, nbp * 0.6),
                Fallback(compound.Liquid_Thermal_Conductivity_Tmax, nbp * 0.999),
                T => pp.AUX_LIQTHERMCONDi(compound, T)));

            curves.Add(Curve("Solid Density", su.density,
                Fallback(compound.Solid_Density_Tmin, 50),
                SolidTmax(compound.Solid_Density_Tmax, tfus, nbp),
                T => pp.AUX_SOLIDDENSi(compound, T)));

            curves.Add(Curve("Solid Heat Capacity", su.heatCapacityCp,
                Fallback(compound.Solid_Heat_Capacity_Tmin, 50),
                SolidTmax(compound.Solid_Heat_Capacity_Tmax, tfus, nbp),
                T => pp.AUX_SolidHeatCapacity(compound, T)));

            return curves;

        }

        /// <summary>First non-zero value of the candidates, in order.</summary>
        private static double Fallback(params double[] candidates)
        {
            foreach (var c in candidates) if (c != 0) return c;
            return 0.0;
        }

        /// <summary>
        /// Upper limit of the solid correlations: the tabulated limit, else the fusion
        /// temperature. A compound with no fusion temperature falls back to a fraction of its
        /// normal boiling point regardless of the tabulated limit.
        /// </summary>
        private static double SolidTmax(double tabulated, double tfus, double nbp)
        {
            if (tfus == 0) return nbp * 0.3;
            return tabulated != 0 ? tabulated : tfus;
        }

        /// <summary>
        /// Samples one property over 51 points. Points the correlation cannot evaluate are
        /// dropped, which is how a compound with a partial data set still yields a usable plot.
        /// </summary>
        private CompoundCurve Curve(string title, string yunits, double Tmin, double Tmax, Func<double, double> f)
        {
            var curve = new CompoundCurve
            {
                Title = title,
                XTitle = "Temperature (" + su.temperature + ")",
                YTitle = title + " (" + yunits + ")"
            };

            if (Tmax <= Tmin) return curve;

            var delta = (Tmax - Tmin) / 50;
            var T = Tmin;

            for (int i = 0; i < 51; i++)
            {
                try
                {
                    var y = cv.ConvertFromSI(yunits, f(T));
                    if (!double.IsNaN(y) && !double.IsInfinity(y))
                    {
                        curve.X.Add(cv.ConvertFromSI(su.temperature, T));
                        curve.Y.Add(y);
                    }
                }
                catch (Exception) { }
                T += delta;
            }

            return curve;
        }

    }

}
