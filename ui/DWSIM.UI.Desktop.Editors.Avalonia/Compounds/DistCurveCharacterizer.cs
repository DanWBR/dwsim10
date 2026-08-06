using System;
using System.Collections.Generic;
using System.Linq;
using DWSIM.Interfaces;
using DWSIM.Thermodynamics.BaseClasses;
using DWSIM.Thermodynamics.PropertyPackages;
using DWSIM.Thermodynamics.Streams;
using DWSIM.Thermodynamics.Utilities.PetroleumCharacterization;
using DWSIM.Thermodynamics.Utilities.PetroleumCharacterization.Methods;
using DWSIM.MathOps.MathEx.Interpolation;
using DWSIM.ExtensionMethods;

using cv = DWSIM.SharedClasses.SystemsOfUnits.Converter;

namespace DWSIM.UI.Desktop.Editors
{

    /// <summary>
    /// Characterizes a petroleum assay from a distillation curve and builds the pseudo compounds.
    /// UI-agnostic: shared by the Eto and the Avalonia distillation curve tools.
    /// Field names follow the petroleum characterization code in DWSIM.Thermodynamics.
    /// </summary>
    public class DistCurveCharacterizer
    {

        private class tmpcomp
        {
            public double tbpm;
            public double tbp0;
            public double tbpf;
            public double fv0;
            public double fvf;
            public double fvm;
        }

        private readonly List<tmpcomp> tccol = new List<tmpcomp>();

        public IFlowsheet Flowsheet;

        /// <summary>Called when a per-compound fitting step fails. The characterization goes on.</summary>
        public Action<string> OnError = (m) => { };

        public string assayname = "MyAssay";

        public string Tccorr = "Riazi-Daubert (1985)", Pccorr = "Riazi-Daubert (1985)",
                      AFcorr = "Lee-Kesler (1976)", MWcorr = "Winn (1956)";

        /// <summary>0 = TBP, 1 = ASTM D86, 2 = ASTM D1160 (vacuum), 3 = ASTM D2887 (simulated).</summary>
        public int tbpcurvetype = 0;
        /// <summary>0 = liquid volume %, 1 = mole %, 2 = weight %.</summary>
        public int curvebasis = 0;
        /// <summary>0 = defined number of cuts, 1 = defined cut temperatures.</summary>
        public int pseudomode = 0;

        public int pseudocuts = 10;
        public List<double> cuttemps = new List<double>();

        public bool hasmwc = false, hassgc = false, hasvisc100c = false, hasvisc210c = false;
        public bool adjustAf = true, adjustZR = true;

        /// <summary>Bulk molar weight and specific gravity. Zero means "not available".</summary>
        public double mwb, sgb;

        public double bulkSulfur, bulkNitrogen, bulkNickel, bulkVanadium, bulkAsphaltenes, bulkWater;
        public double pnaParaffins, pnaNaphthenes, pnaAromatics;

        public string decsep = ".";

        // curve data, in SI units, filled by ParseCurveData
        public List<double> cb = new List<double>(), tbp = new List<double>(), mwc = new List<double>(),
                            sgc = new List<double>(), visc100 = new List<double>(), visc210 = new List<double>();

        /// <summary>
        /// Reads the pasted curve table. Column order is: basis, boiling point, then molar weight,
        /// specific gravity, viscosity @ 100 F and viscosity @ 210 F for each curve that is enabled.
        /// </summary>
        public void ParseCurveData(string[] datalines, IUnitsOfMeasure su)
        {

            cb.Clear();
            tbp.Clear();
            sgc.Clear();
            mwc.Clear();
            visc100.Clear();
            visc210.Clear();

            foreach (string line in datalines)
            {
                if (line.Trim() == "") continue;
                var val = line.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                tbp.Add(cv.ConvertToSI(su.temperature, val[1].ToDoubleWithSeparator(decsep)));
                cb.Add(val[0].ToDoubleWithSeparator(decsep) / 100);
                if (hasmwc)
                {
                    mwc.Add(val[2].ToDoubleWithSeparator(decsep));
                    if (hassgc)
                    {
                        sgc.Add(val[3].ToDoubleWithSeparator(decsep));
                        if (hasvisc100c)
                        {
                            visc100.Add(cv.ConvertToSI(su.cinematic_viscosity, val[4].ToDoubleWithSeparator(decsep)));
                            if (hasvisc210c)
                            {
                                visc210.Add(cv.ConvertToSI(su.cinematic_viscosity, val[5].ToDoubleWithSeparator(decsep)));
                            }
                        }
                        else
                        {
                            if (hasvisc210c)
                            {
                                visc210.Add(cv.ConvertToSI(su.cinematic_viscosity, val[4].ToDoubleWithSeparator(decsep)));
                            }
                        }
                    }
                    else
                    {
                        if (hasvisc100c)
                        {
                            visc100.Add(cv.ConvertToSI(su.cinematic_viscosity, val[3].ToDoubleWithSeparator(decsep)));
                        }
                        else
                        {
                            if (hasvisc210c)
                            {
                                visc210.Add(cv.ConvertToSI(su.cinematic_viscosity, val[3].ToDoubleWithSeparator(decsep)));
                            }
                        }
                    }
                }
                else
                {
                    if (hassgc)
                    {
                        sgc.Add(val[2].ToDoubleWithSeparator(decsep));
                        if (hasvisc100c)
                        {
                            visc100.Add(cv.ConvertToSI(su.cinematic_viscosity, val[3].ToDoubleWithSeparator(decsep)));
                            if (hasvisc210c)
                            {
                                visc210.Add(cv.ConvertToSI(su.cinematic_viscosity, val[4].ToDoubleWithSeparator(decsep)));
                            }
                        }
                        else
                        {
                            if (hasvisc210c)
                            {
                                visc210.Add(cv.ConvertToSI(su.cinematic_viscosity, val[3].ToDoubleWithSeparator(decsep)));
                            }
                        }
                    }
                    else
                    {
                        if (hasvisc100c)
                        {
                            visc100.Add(cv.ConvertToSI(su.cinematic_viscosity, val[2].ToDoubleWithSeparator(decsep)));
                            if (hasvisc210c)
                            {
                                visc210.Add(cv.ConvertToSI(su.cinematic_viscosity, val[3].ToDoubleWithSeparator(decsep)));
                            }
                        }
                        else
                        {
                            if (hasvisc210c)
                            {
                                visc210.Add(cv.ConvertToSI(su.cinematic_viscosity, val[2].ToDoubleWithSeparator(decsep)));
                            }
                        }
                    }
                }
            }

        }

        public Dictionary<string, Compound> GenerateCompounds(IUnitsOfMeasure su)
        {

            //generate pseudos from number or temperature cuts

            int i = 0;
            int method = pseudomode;
            double[] tbp2 = null;
            double[] tbpx = null;

            double[] coeff;
            object[] obj = null;
            double Tmin, Tmax;

            //generate polynomial from input data

            if (tbpcurvetype == 0)
            {
                //tbp
                tbp2 = tbp.ToArray();
                tbpx = cb.ToArray();
            }
            else if (tbpcurvetype == 1)
            {
                //d86
                //interpolate to obtain points
                double[] w = null;
                ratinterpolation.buildfloaterhormannrationalinterpolant(cb.ToArray(), cb.Count, 1, ref w);
                double T0 = polinterpolation.barycentricinterpolation(cb.ToArray(), tbp.ToArray(), w, cb.Count, 0.0d);
                double T10 = polinterpolation.barycentricinterpolation(cb.ToArray(), tbp.ToArray(), w, cb.Count, 0.1d);
                double T30 = polinterpolation.barycentricinterpolation(cb.ToArray(), tbp.ToArray(), w, cb.Count, 0.3d);
                double T50 = polinterpolation.barycentricinterpolation(cb.ToArray(), tbp.ToArray(), w, cb.Count, 0.5d);
                double T70 = polinterpolation.barycentricinterpolation(cb.ToArray(), tbp.ToArray(), w, cb.Count, 0.7d);
                double T90 = polinterpolation.barycentricinterpolation(cb.ToArray(), tbp.ToArray(), w, cb.Count, 0.9d);
                double T100 = polinterpolation.barycentricinterpolation(cb.ToArray(), tbp.ToArray(), w, cb.Count, 1.0d);
                //tbp
                tbp2 = DistillationCurveConversion.ASTMD86ToPEV_Riazi(new double[] { T0, T10, T30, T50, T70, T90, T100 });
                tbpx = new double[] { 1E-06, 0.1, 0.3, 0.5, 0.7, 0.9, 1.0 };
            }
            else if (tbpcurvetype == 2)
            {
                //vacuum
                //interpolate to obtain points
                double[] w = null;
                ratinterpolation.buildfloaterhormannrationalinterpolant(cb.ToArray(), cb.Count, 1, ref w);
                double T0 = polinterpolation.barycentricinterpolation(cb.ToArray(), tbp.ToArray(), w, cb.Count, 0);
                double T10 = polinterpolation.barycentricinterpolation(cb.ToArray(), tbp.ToArray(), w, cb.Count, 0.1);
                double T30 = polinterpolation.barycentricinterpolation(cb.ToArray(), tbp.ToArray(), w, cb.Count, 0.3);
                double T50 = polinterpolation.barycentricinterpolation(cb.ToArray(), tbp.ToArray(), w, cb.Count, 0.5);
                double T70 = polinterpolation.barycentricinterpolation(cb.ToArray(), tbp.ToArray(), w, cb.Count, 0.7);
                double T90 = polinterpolation.barycentricinterpolation(cb.ToArray(), tbp.ToArray(), w, cb.Count, 0.9);
                double T100 = polinterpolation.barycentricinterpolation(cb.ToArray(), tbp.ToArray(), w, cb.Count, 1.0);
                //tbp
                tbp2 = DistillationCurveConversion.ASTMD1160ToPEVsub_Wauquier(new double[] { T0, T10, T30, T50, T70, T90, T100 });
                double K = 12.0;
                for (int j = 0; j <= 6; j++)
                {
                    tbp2[j] = DistillationCurveConversion.PEVsubToPEV_MaxwellBonnel(tbp2[j], 1333, K);
                }
                tbpx = new double[] { 1E-06, 0.1, 0.3, 0.5, 0.7, 0.9, 1.0 };
            }
            else if (tbpcurvetype == 3)
            {
                //simulated
                //interpolate to obtain points
                double[] w = null;
                ratinterpolation.buildfloaterhormannrationalinterpolant(cb.ToArray(), cb.Count, 1, ref w);
                double T5 = polinterpolation.barycentricinterpolation(cb.ToArray(), tbp.ToArray(), w, cb.Count, 0.05);
                double T10 = polinterpolation.barycentricinterpolation(cb.ToArray(), tbp.ToArray(), w, cb.Count, 0.1);
                double T30 = polinterpolation.barycentricinterpolation(cb.ToArray(), tbp.ToArray(), w, cb.Count, 0.3);
                double T50 = polinterpolation.barycentricinterpolation(cb.ToArray(), tbp.ToArray(), w, cb.Count, 0.5);
                double T70 = polinterpolation.barycentricinterpolation(cb.ToArray(), tbp.ToArray(), w, cb.Count, 0.7);
                double T90 = polinterpolation.barycentricinterpolation(cb.ToArray(), tbp.ToArray(), w, cb.Count, 0.9);
                double T95 = polinterpolation.barycentricinterpolation(cb.ToArray(), tbp.ToArray(), w, cb.Count, 0.95);
                double T100 = polinterpolation.barycentricinterpolation(cb.ToArray(), tbp.ToArray(), w, cb.Count, 1.0);
                //tbp
                tbp2 = DistillationCurveConversion.ASTMD2887ToPEV_Daubert(new double[] { T5, T10, T30, T50, T70, T90, T95, T100 });
                tbpx = new double[] { 0.05, 0.1, 0.3, 0.5, 0.7, 0.9, 0.95, 1.0 };
            }

            Tmin = tbp2.Min();
            Tmax = tbp2.Max();

            //y = 10358x5 - 15934x4 + 11822x3 - 4720,2x2 + 1398,2x + 269,23
            //R² = 1

            double[] inest = new double[7];

            if (tbpcurvetype == 1)
            {
                double[] w2 = null;
                ratinterpolation.buildfloaterhormannrationalinterpolant(tbpx, tbpx.Length, 1, ref w2);
                inest[0] = polinterpolation.barycentricinterpolation(tbpx, tbp2, w2, tbpx.Length, 0);
            }
            else
            {
                inest[0] = Tmin;
            }
            inest[1] = 1398;
            inest[2] = 4720;
            inest[3] = 11821;
            inest[4] = 15933;
            inest[5] = 10358;
            inest[6] = -3000;

            DistillationCurveConversion.TBPFit lmfit = new DistillationCurveConversion.TBPFit();
            obj = (object[])lmfit.GetCoeffs(tbpx, tbp2, inest, 1E-10, 1E-08, 1E-08, 1000);
            coeff = (double[])obj[0];

            //TBP(K) = aa + bb*fv + cc*fv^2 + dd*fv^3 + ee*fv^4 + ff*fv^5 (fv 0 ~ 1)

            //create pseudos

            if (method == 0)
            {
                int np = Convert.ToInt32(pseudocuts);
                double deltaT = (Tmax - Tmin) / (np);
                double t0 = Tmin;
                double fv0 = tbpx.Min();
                tccol.Clear();
                for (i = 0; i <= np - 1; i++)
                {
                    tmpcomp tc = new tmpcomp();
                    tc.tbp0 = t0;
                    tc.tbpf = t0 + deltaT;
                    tc.fv0 = GetFV(coeff, fv0, tc.tbp0);
                    tc.fvf = GetFV(coeff, fv0, tc.tbpf);
                    tc.fvm = tc.fv0 + (tc.fvf - tc.fv0) / 2;
                    tc.tbpm = GetT(coeff, tc.fvm);
                    tccol.Add(tc);
                    t0 = t0 + deltaT;
                    fv0 = tc.fvf;
                }
            }
            else
            {
                int np = cuttemps.Count + 1;
                double t0 = Tmin;
                double fv0 = tbpx.Min();
                tccol.Clear();
                for (i = 0; i <= np - 1; i++)
                {
                    tmpcomp tc = new tmpcomp();
                    tc.tbp0 = t0;
                    if (i == np - 1)
                    {
                        tc.tbpf = Tmax;
                    }
                    else
                    {
                        tc.tbpf = cv.ConvertToSI(su.temperature, cuttemps[i]);
                    }
                    tc.fv0 = GetFV(coeff, fv0, tc.tbp0);
                    tc.fvf = GetFV(coeff, fv0, tc.tbpf);
                    tc.fvm = tc.fv0 + (tc.fvf - tc.fv0) / 2;
                    tc.tbpm = GetT(coeff, tc.fvm);
                    tccol.Add(tc);
                    fv0 = tc.fvf;
                    if (i < np - 1)
                        t0 = cv.ConvertToSI(su.temperature, cuttemps[i]);
                }
            }

            GL methods2 = new GL();

            Dictionary<string, Compound> ccol = new Dictionary<string, Compound>();

            i = 0;

            foreach (tmpcomp tc in tccol)
            {
                ConstantProperties cprops = new ConstantProperties();

                cprops.NBP = tc.tbpm;
                cprops.OriginalDB = "Petroleum Assay: " + assayname;
                cprops.CurrentDB = "Petroleum Assay: " + assayname;

                //SG
                if (!hassgc)
                {
                    if (Math.Abs(cprops.PF_MM.GetValueOrDefault()) < 1e-10)
                    {
                        if (cprops.NBP.GetValueOrDefault() < 1080)
                        {
                            cprops.PF_MM = Math.Pow((1.0 / 0.01964 * (6.97996 - Math.Log(1080.0 - cprops.NBP.GetValueOrDefault()))), 1.5);
                        }
                        else
                        {
                            cprops.PF_MM = Math.Pow((1.0 / 0.01964 * (6.97996 + Math.Log(-1080.0 + cprops.NBP.GetValueOrDefault()))), 1.5);
                        }
                    }
                    cprops.PF_SG = PropertyMethods.d15_Riazi(cprops.PF_MM.GetValueOrDefault());
                }
                else
                {
                    double[] w = null;
                    ratinterpolation.buildfloaterhormannrationalinterpolant(cb.ToArray(), cb.Count, 1, ref w);
                    cprops.PF_SG = polinterpolation.barycentricinterpolation(cb.ToArray(), sgc.ToArray(), w, cb.Count(), tc.fvm);
                }

                //MW
                if (!hasmwc)
                {
                    switch (MWcorr)
                    {
                        case "Winn (1956)":
                            cprops.PF_MM = PropertyMethods.MW_Winn(cprops.NBP.GetValueOrDefault(), cprops.PF_SG.GetValueOrDefault());
                            break;
                        case "Riazi (1986)":
                            cprops.PF_MM = PropertyMethods.MW_Riazi(cprops.NBP.GetValueOrDefault(), cprops.PF_SG.GetValueOrDefault());
                            break;
                        case "Lee-Kesler (1974)":
                            cprops.PF_MM = PropertyMethods.MW_LeeKesler(cprops.NBP.GetValueOrDefault(), cprops.PF_SG.GetValueOrDefault());
                            break;
                    }
                }
                else
                {
                    double[] w = null;
                    ratinterpolation.buildfloaterhormannrationalinterpolant(cb.ToArray(), cb.Count(), 1, ref w);
                    cprops.PF_MM = polinterpolation.barycentricinterpolation(cb.ToArray(), mwc.ToArray(), w, cb.Count(), tc.fvm);
                }

                cprops.Molar_Weight = cprops.PF_MM.GetValueOrDefault();

                char[] trimchars = new char[] { ' ', '_', ',', ';', ':' };

                if (Double.IsNaN(cprops.NBP.GetValueOrDefault()))
                {
                    cprops.Name = "C" + assayname.Trim(trimchars).ToString() + "_NBP_" + i.ToString();
                    cprops.CAS_Number = assayname.Trim(trimchars) + "-" + i.ToString();
                }
                else
                {
                    cprops.Name = "C" + assayname.Trim(trimchars).ToString() + "_NBP_" + Convert.ToInt32(cprops.NBP.GetValueOrDefault() - 273.15).ToString();
                    cprops.CAS_Number = assayname.Trim(trimchars) + "-" + Convert.ToInt32(cprops.NBP.GetValueOrDefault()).ToString();
                }

                i += 1;

                Compound subst = new Compound(cprops.Name, "");

                subst.ConstantProperties = cprops;
                subst.Name = cprops.Name;
                subst.PetroleumFraction = true;

                ccol.Add(cprops.Name, subst);

            }

            CalculateMolarFractions(ccol);

            if (mwb > 1E-10)
            {
                double mixtMW = 0;
                foreach (var c in ccol.Values)
                {
                    mixtMW += c.MoleFraction.GetValueOrDefault() * c.ConstantProperties.Molar_Weight;
                }
                double facm = mwb / mixtMW;
                foreach (var c in ccol.Values)
                {
                    c.ConstantProperties.Molar_Weight *= facm;
                }
            }

            if (sgb > 1E-10)
            {
                double mixtD = 0;
                foreach (var c in ccol.Values)
                {
                    mixtD += c.MassFraction.GetValueOrDefault() * c.ConstantProperties.PF_SG.GetValueOrDefault();
                }
                double facd = 141.5 / (131.5 + sgb) / mixtD;
                foreach (var c in ccol.Values)
                {
                    c.ConstantProperties.PF_SG *= facd;
                }
            }

            i = 0;

            foreach (var subst in ccol.Values)
            {
                ConstantProperties cprops = (ConstantProperties)subst.ConstantProperties;

                tmpcomp tc = tccol[i];

                //VISC
                if (!hasvisc100c)
                {
                    cprops.PF_Tv1 = 311;
                    cprops.PF_Tv2 = 372;
                    cprops.PF_v1 = PropertyMethods.Visc37_Abbott(cprops.NBP.GetValueOrDefault(), cprops.PF_SG.GetValueOrDefault());
                    cprops.PF_v2 = PropertyMethods.Visc98_Abbott(cprops.NBP.GetValueOrDefault(), cprops.PF_SG.GetValueOrDefault());
                }
                else
                {
                    double[] w = null;
                    ratinterpolation.buildfloaterhormannrationalinterpolant(cb.ToArray(), visc100.Count, 1, ref w);
                    cprops.PF_v1 = polinterpolation.barycentricinterpolation(cb.ToArray(), visc100.ToArray(), w, cb.Count, tc.fvm);
                    ratinterpolation.buildfloaterhormannrationalinterpolant(cb.ToArray(), visc210.Count, 1, ref w);
                    cprops.PF_v2 = polinterpolation.barycentricinterpolation(cb.ToArray(), visc210.ToArray(), w, cb.Count, tc.fvm);
                    cprops.PF_Tv1 = (100 - 32) / 9 * 5 + 273.15;
                    cprops.PF_Tv2 = (210 - 32) / 9 * 5 + 273.15;
                }

                cprops.PF_vA = PropertyMethods.ViscWaltherASTM_A(cprops.PF_Tv1.GetValueOrDefault(), cprops.PF_v1.GetValueOrDefault(), cprops.PF_Tv2.GetValueOrDefault(), cprops.PF_v2.GetValueOrDefault());
                cprops.PF_vB = PropertyMethods.ViscWaltherASTM_B(cprops.PF_Tv1.GetValueOrDefault(), cprops.PF_v1.GetValueOrDefault(), cprops.PF_Tv2.GetValueOrDefault(), cprops.PF_v2.GetValueOrDefault());

                //Tc
                switch (Tccorr)
                {
                    case "Riazi-Daubert (1985)":
                        cprops.Critical_Temperature = PropertyMethods.Tc_RiaziDaubert(cprops.NBP.GetValueOrDefault(), cprops.PF_SG.GetValueOrDefault());
                        break;
                    case "Lee-Kesler (1976)":
                        cprops.Critical_Temperature = PropertyMethods.Tc_LeeKesler(cprops.NBP.GetValueOrDefault(), cprops.PF_SG.GetValueOrDefault());
                        break;
                    case "Farah (2006)":
                        cprops.Critical_Temperature = PropertyMethods.Tc_Farah(cprops.PF_vA.GetValueOrDefault(), cprops.PF_vB.GetValueOrDefault(), cprops.NBP.GetValueOrDefault(), cprops.PF_SG.GetValueOrDefault());
                        break;
                    case "Riazi (2005)":
                        cprops.Critical_Temperature = PropertyMethods.Tc_Riazi(cprops.NBP.GetValueOrDefault(), cprops.PF_SG.GetValueOrDefault());
                        break;
                }

                //Pc
                switch (Pccorr)
                {
                    case "Riazi-Daubert (1985)":
                        cprops.Critical_Pressure = PropertyMethods.Pc_RiaziDaubert(cprops.NBP.GetValueOrDefault(), cprops.PF_SG.GetValueOrDefault());
                        break;
                    case "Lee-Kesler (1976)":
                        cprops.Critical_Pressure = PropertyMethods.Pc_LeeKesler(cprops.NBP.GetValueOrDefault(), cprops.PF_SG.GetValueOrDefault());
                        break;
                    case "Farah (2006)":
                        cprops.Critical_Pressure = PropertyMethods.Pc_Farah(cprops.PF_vA.GetValueOrDefault(), cprops.PF_vB.GetValueOrDefault(), cprops.NBP.GetValueOrDefault(), cprops.PF_SG.GetValueOrDefault());
                        break;
                }

                //Af
                switch (AFcorr)
                {
                    case "Lee-Kesler (1976)":
                        cprops.Acentric_Factor = PropertyMethods.AcentricFactor_LeeKesler(cprops.Critical_Temperature, cprops.Critical_Pressure, cprops.NBP.GetValueOrDefault());
                        break;
                    case "Korsten (2000)":
                        cprops.Acentric_Factor = PropertyMethods.AcentricFactor_Korsten(cprops.Critical_Temperature, cprops.Critical_Pressure, cprops.NBP.GetValueOrDefault());
                        break;
                }

                cprops.Normal_Boiling_Point = cprops.NBP.GetValueOrDefault();

                cprops.IsPF = 1;
                cprops.PF_Watson_K = Math.Pow((1.8 * cprops.NBP.GetValueOrDefault()), 0.33333) / cprops.PF_SG.GetValueOrDefault();

                var tmp = (double[])methods2.calculate_Hf_Sf(cprops.PF_SG.GetValueOrDefault(), cprops.Molar_Weight, cprops.NBP.GetValueOrDefault());

                cprops.IG_Enthalpy_of_Formation_25C = tmp[0];
                cprops.IG_Entropy_of_Formation_25C = tmp[1];
                cprops.IG_Gibbs_Energy_of_Formation_25C = tmp[0] - 298.15 * tmp[1];

                cprops.Formula = "C" + Convert.ToDouble(tmp[2]).ToString("N2") + "H" + Convert.ToDouble(tmp[3]).ToString("N2");

                DWSIM.Thermodynamics.Utilities.Hypos.Methods.HYP methods = new DWSIM.Thermodynamics.Utilities.Hypos.Methods.HYP();

                cprops.HVap_A = methods.DHvb_Vetere(cprops.Critical_Temperature, cprops.Critical_Pressure, cprops.Normal_Boiling_Point) / cprops.Molar_Weight;

                cprops.Critical_Compressibility = DWSIM.Thermodynamics.PropertyPackages.Auxiliary.PROPS.Zc1(cprops.Acentric_Factor);
                cprops.Critical_Volume = 8314 * cprops.Critical_Compressibility * cprops.Critical_Temperature / cprops.Critical_Pressure;
                cprops.Z_Rackett = DWSIM.Thermodynamics.PropertyPackages.Auxiliary.PROPS.Zc1(cprops.Acentric_Factor);
                if (cprops.Z_Rackett < 0)
                {
                    cprops.Z_Rackett = 0.2;
                }

                cprops.Chao_Seader_Acentricity = cprops.Acentric_Factor;
                cprops.Chao_Seader_Solubility_Parameter = Math.Pow(((cprops.HVap_A * cprops.Molar_Weight - 8.314 * cprops.Normal_Boiling_Point) * 238.846 * DWSIM.Thermodynamics.PropertyPackages.Auxiliary.PROPS.liq_dens_rackett(cprops.Normal_Boiling_Point, cprops.Critical_Temperature, cprops.Critical_Pressure, cprops.Acentric_Factor, cprops.Molar_Weight) / cprops.Molar_Weight / 1000000.0), 0.5);
                cprops.Chao_Seader_Liquid_Molar_Volume = 1 / DWSIM.Thermodynamics.PropertyPackages.Auxiliary.PROPS.liq_dens_rackett(cprops.Normal_Boiling_Point, cprops.Critical_Temperature, cprops.Critical_Pressure, cprops.Acentric_Factor, cprops.Molar_Weight) * cprops.Molar_Weight / 1000 * 1000000.0;

                methods = null;

                cprops.ID = 30000 + i + 1;

                if (pnaParaffins > 0 || pnaNaphthenes > 0 || pnaAromatics > 0)
                {
                    cprops.BO_PNA_P = pnaParaffins;
                    cprops.BO_PNA_N = pnaNaphthenes;
                    cprops.BO_PNA_A = pnaAromatics;
                }

                i += 1;

            }

            //Adjust Acentric Factors and Rackett parameters to fit NBP and Density

            DensityFitting dfit = new DensityFitting();
            PRVSFitting prvsfit = new PRVSFitting();
            SRKVSFitting srkvsfit = new SRKVSFitting();
            NBPFitting nbpfit = new NBPFitting() { Flowsheet = Flowsheet };
            MaterialStream tms = new MaterialStream("", "");
            PropertyPackage pp;
            double fzra = 0;
            double fw = 0;
            double fprvs = 0;
            double fsrkvs = 0;

            if (Flowsheet != null && Flowsheet.PropertyPackages.Count > 0)
            {
                pp = (PropertyPackage)Flowsheet.PropertyPackages.Values.First();
            }
            else
            {
                pp = new PengRobinsonPropertyPackage();
            }

            foreach (var c in ccol.Values)
            {
                tms.Phases[0].Compounds.Add(c.Name, c);
            }

            bool recalcVc = false;

            i = 0;
            foreach (var c in ccol.Values)
            {
                if (adjustAf)
                {
                    nbpfit._pp = pp;
                    nbpfit._ms = tms;
                    nbpfit._idx = i;
                    if (c.ConstantProperties.Acentric_Factor < 0)
                    {
                        c.ConstantProperties.Acentric_Factor = 0.5;
                        recalcVc = true;
                    }
                    try
                    {
                        fw = nbpfit.MinimizeError();
                    }
                    catch (Exception ex)
                    {
                        OnError("Error fitting Acentric Factor for compound '" + c.Name + "': " + ex.Message);
                    }
                    c.ConstantProperties.Acentric_Factor *= fw;
                }
                c.ConstantProperties.Z_Rackett = DWSIM.Thermodynamics.PropertyPackages.Auxiliary.PROPS.Zc1(c.ConstantProperties.Acentric_Factor);
                if (c.ConstantProperties.Z_Rackett < 0)
                {
                    c.ConstantProperties.Z_Rackett = 0.2;
                    recalcVc = true;
                }
                c.ConstantProperties.Critical_Compressibility = DWSIM.Thermodynamics.PropertyPackages.Auxiliary.PROPS.Zc1(c.ConstantProperties.Acentric_Factor);
                c.ConstantProperties.Critical_Volume = DWSIM.Thermodynamics.PropertyPackages.Auxiliary.PROPS.Vc(c.ConstantProperties.Critical_Temperature, c.ConstantProperties.Critical_Pressure, c.ConstantProperties.Acentric_Factor, c.ConstantProperties.Critical_Compressibility);
                if (adjustZR)
                {
                    dfit._comp = c;
                    try
                    {
                        fzra = dfit.MinimizeError();
                    }
                    catch (Exception ex)
                    {
                        OnError("Error fitting Rackett Parameter for compound '" + c.Name + "': " + ex.Message);
                    }
                    c.ConstantProperties.Z_Rackett *= fzra;
                }
                if (c.ConstantProperties.Critical_Compressibility < 0 | recalcVc)
                {
                    c.ConstantProperties.Critical_Compressibility = c.ConstantProperties.Z_Rackett;
                    c.ConstantProperties.Critical_Volume = DWSIM.Thermodynamics.PropertyPackages.Auxiliary.PROPS.Vc(c.ConstantProperties.Critical_Temperature, c.ConstantProperties.Critical_Pressure, c.ConstantProperties.Acentric_Factor, c.ConstantProperties.Critical_Compressibility);
                }

                c.ConstantProperties.PR_Volume_Translation_Coefficient = 1;
                prvsfit._comp = c;
                fprvs = prvsfit.MinimizeError();
                if (Math.Abs(fprvs) < 99.0)
                    c.ConstantProperties.PR_Volume_Translation_Coefficient *= fprvs;
                else
                    c.ConstantProperties.PR_Volume_Translation_Coefficient = 0.0;
                c.ConstantProperties.SRK_Volume_Translation_Coefficient = 1;
                srkvsfit._comp = c;
                fsrkvs = srkvsfit.MinimizeError();
                if (Math.Abs(fsrkvs) < 99.0)
                    c.ConstantProperties.SRK_Volume_Translation_Coefficient *= fsrkvs;
                else
                    c.ConstantProperties.SRK_Volume_Translation_Coefficient = 0.0;
                recalcVc = false;
                i += 1;
            }

            return ccol;

        }

        /// <summary>Builds the assay record from the parsed curve and the bulk sample data.</summary>
        public DWSIM.SharedClasses.Utilities.PetroleumCharacterization.Assay.Assay BuildAssay()
        {
            var api = sgb == 0.0 ? 0.0 : 141.5 / sgb - 131.5;
            var assay = new DWSIM.SharedClasses.Utilities.PetroleumCharacterization.Assay.Assay(12.0, mwb, api, 310.928, 372.039, tbpcurvetype, "",
                new System.Collections.ArrayList(cb), new System.Collections.ArrayList(tbp), new System.Collections.ArrayList(mwc),
                new System.Collections.ArrayList(sgc), new System.Collections.ArrayList(visc100), new System.Collections.ArrayList(visc210));
            assay.BulkSulfurWtPct = bulkSulfur;
            assay.BulkNitrogenWtPct = bulkNitrogen;
            assay.BulkNiPpm = bulkNickel;
            assay.BulkVPpm = bulkVanadium;
            assay.BulkAsphaltenesWtPct = bulkAsphaltenes;
            assay.BSWVolPct = bulkWater;
            return assay;
        }

        private double GetFV(double[] coeffs, double fv0, double t)
        {

            //TBP(K) = aa + bb*fv + cc*fv^2 + dd*fv^3 + ee*fv^4 + ff*fv^5 + gg*fv^6 (fv 0 ~ 1)

            double f = 0;
            double df = 0;
            int cnt = 0;
            double fv = fv0;
            do
            {
                f = -t + (coeffs[0] + coeffs[1] * fv + coeffs[2] * Math.Pow(fv, 2) + coeffs[3] * Math.Pow(fv, 3) + coeffs[4] * Math.Pow(fv, 4) + coeffs[5] * Math.Pow(fv, 5) + coeffs[6] * Math.Pow(fv, 6));
                df = coeffs[1] + 2 * coeffs[2] * fv + 3 * coeffs[3] * Math.Pow(fv, 2) + 4 * coeffs[4] * Math.Pow(fv, 3) + 5 * coeffs[5] * Math.Pow(fv, 4) + 6 * coeffs[6] * Math.Pow(fv, 5);
                fv = -f / df * 0.3 + fv;
                if (fv < 0)
                    fv = Math.Abs(fv);
                cnt += 1;
            } while (!(Math.Abs(f) < 1E-09 | cnt >= 1000));

            return fv;

        }

        private double GetT(double[] coeffs, double fv)
        {

            //TBP(K) = aa + bb*fv + cc*fv^2 + dd*fv^3 + ee*fv^4 + ff*fv^5 + gg*fv^6 (fv 0 ~ 1)

            return (coeffs[0] + coeffs[1] * fv + coeffs[2] * Math.Pow(fv, 2) + coeffs[3] * Math.Pow(fv, 3) + coeffs[4] * Math.Pow(fv, 4) + coeffs[5] * Math.Pow(fv, 5) + coeffs[6] * Math.Pow(fv, 6));

        }

        public void CalculateMolarFractions(Dictionary<string, Compound> ccol)
        {
            double sum1 = 0;
            double fm = 0;
            double fv = 0;
            double fw = 0;
            int i = 0;

            switch (curvebasis)
            {
                case 0:
                    //liquid volume percent
                    i = 0;
                    sum1 = 0;
                    foreach (var subst in ccol.Values)
                    {
                        fv = (tccol[i].fvf - tccol[i].fv0) / (tccol[tccol.Count - 1].fvf - tccol[0].fv0);
                        fw = fv * subst.ConstantProperties.PF_SG.GetValueOrDefault();
                        fm = fw / subst.ConstantProperties.Molar_Weight;
                        sum1 += fm;
                        i = i + 1;
                    }

                    i = 0;
                    foreach (var subst in ccol.Values)
                    {
                        fv = (tccol[i].fvf - tccol[i].fv0) / (tccol[tccol.Count - 1].fvf - tccol[0].fv0);
                        fw = fv * subst.ConstantProperties.PF_SG.GetValueOrDefault();
                        fm = fw / subst.ConstantProperties.Molar_Weight;
                        subst.MoleFraction = fm / sum1;
                        i = i + 1;
                    }

                    break;
                case 1:
                    //mole percent
                    i = 0;
                    foreach (var subst in ccol.Values)
                    {
                        subst.MoleFraction = (tccol[i].fvf - tccol[i].fv0) / (tccol[tccol.Count - 1].fvf - tccol[0].fv0);
                        i = i + 1;
                    }

                    break;
                case 2:
                    //weight percent
                    i = 0;
                    sum1 = 0;
                    foreach (var subst in ccol.Values)
                    {
                        fw = (tccol[i].fvf - tccol[i].fv0) / (tccol[tccol.Count - 1].fvf - tccol[0].fv0);
                        fm = fw / subst.ConstantProperties.Molar_Weight;
                        sum1 += fm;
                        i = i + 1;
                    }

                    i = 0;
                    foreach (var subst in ccol.Values)
                    {
                        fw = (tccol[i].fvf - tccol[i].fv0) / (tccol[tccol.Count - 1].fvf - tccol[0].fv0);
                        fm = fw / subst.ConstantProperties.Molar_Weight;
                        subst.MoleFraction = fm / sum1;
                        i = i + 1;
                    }

                    break;
            }

            double wxtotal = 0;

            foreach (var subst in ccol.Values)
            {
                wxtotal += subst.MoleFraction.GetValueOrDefault() * subst.ConstantProperties.Molar_Weight;
            }

            foreach (var subst in ccol.Values)
            {
                subst.MassFraction = subst.MoleFraction * subst.ConstantProperties.Molar_Weight / wxtotal;
            }

        }

    }

}
