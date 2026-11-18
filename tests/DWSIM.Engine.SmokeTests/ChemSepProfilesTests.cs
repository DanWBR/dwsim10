//    Stage profiles of a ChemSep column run as a CAPE-OPEN unit operation, read from the input file
//    ChemSep keeps in its persisted data.

using System.Linq;
using DWSIM.UnitOperations.UnitOperations;
using NUnit.Framework;

namespace DWSIM.Engine.SmokeTests
{
    [TestFixture]
    public class ChemSepProfilesTests
    {
        // a solved three-stage column (condenser, one tray, reboiler) with two compounds and a liquid side
        // draw on stage 2, in the layout of a ChemSep .sep file; the compositions come in blocks of stages
        private const string Sep =
            "[ChemSep]\r\nVersion=8.49\r\n\r\n" +
            "[Components]\r\n2 Number of Components\r\n" +
            "52 1102 Library Offset, Index DT=2023-12-23 CHK=1 CAS=64-17-5 CID=Ethanol\r\nName=Ethanol\r\nLib=x\r\n" +
            "14 1921 Library Offset, Index DT=2023-12-23 CHK=1 CAS=7732-18-5 CID=Water\r\nName=Water\r\nLib=x\r\n\r\n" +
            "[Operation]\r\n2 Operation Column\r\n1 Condenser Total\r\n1 Reboiler Partial (Liquid product)\r\n3 Stages\r\n\r\n" +
            "[End of Input]\r\n\r\n" +
            "[Results]\r\n  1 Converged\r\n     7 Iterations\r\n\r\n" +
            "[Profiles]\r\n\r\n Stage  Temperature      Pressure     Vapour Flow     Liquid Flow   Duties\r\n\r\n" +
            "   1         351.00             1.013250E+05     0.000000E+00     2.000000E-01     -1.000000E+06\r\n" +
            "   2         360.00             1.020000E+05     3.000000E-01     2.500000E-01      0.000000E+00\r\n" +
            "   3         370.00             1.030000E+05     3.000000E-01     1.000000E-01      1.100000E+06\r\n\r\n" +
            "[Vapour phase compositions]\r\n\r\n Component     Mole fractions on stages:        1 to  2\r\n\r\n" +
            "           1  8.000000000E-01  6.000000000E-01\r\n           2  2.000000000E-01  4.000000000E-01\r\n\r\n" +
            " Component     Mole fractions on stages:        3 to  3\r\n\r\n" +
            "           1  3.000000000E-01\r\n           2  7.000000000E-01\r\n\r\n" +
            "[Liquid phase compositions]\r\n\r\n Component     Mole fractions on stages:        1 to  3\r\n\r\n" +
            "           1  8.000000000E-01  4.000000000E-01  1.000000000E-01\r\n" +
            "           2  2.000000000E-01  6.000000000E-01  9.000000000E-01\r\n\r\n" +
            "[Side stream 1 from stage 2]                \r\n\r\n" +
            "            2 Stage Number\r\n  5.0000000000000E-02 Flow rate\r\n  3.6E+02 Temperature [K]\r\n" +
            "  0.0000000000000E+00 Vapour fraction [-]\r\n\r\n" +
            "[End of Results]\r\n";

        [Test]
        public void ReadsStagesInSIUnitsAndMatchesCompoundsByCAS()
        {
            // the flowsheet lists water first: the ChemSep order must be mapped to it
            var p = CapeOpenUO.ParseChemSepProfiles(Sep, new[] { "Water", "Ethanol" }, new[] { "7732-18-5", "64-17-5" });

            Assert.That(p.NumberOfStages, Is.EqualTo(3));
            Assert.That(p.Converged, Is.True);
            Assert.That(p.Iterations, Is.EqualTo(7));
            Assert.That(p.StageNames, Is.EqualTo(new[] { "Condenser", "Stage 2", "Reboiler" }));
            Assert.That(p.Temperature, Is.EqualTo(new[] { 351.0, 360.0, 370.0 }));
            Assert.That(p.Pressure[1], Is.EqualTo(102000.0));
            // kmol/s in the file, mol/s here
            Assert.That(p.LiquidFlow[1], Is.EqualTo(250.0).Within(1e-9));
            Assert.That(p.VaporFlow[2], Is.EqualTo(300.0).Within(1e-9));
            Assert.That(p.Duty[0], Is.EqualTo(-1.0e6));
            Assert.That(p.LiquidSideDraw, Is.EqualTo(new[] { 0.0, 50.0, 0.0 }).Within(1e-9));

            Assert.That(p.Compounds, Is.EqualTo(new[] { "Water", "Ethanol" }));
            Assert.That(p.X[1], Is.EqualTo(new[] { 0.6, 0.4 }).Within(1e-12));
            // the second block of stages
            Assert.That(p.Y[2], Is.EqualTo(new[] { 0.7, 0.3 }).Within(1e-12));
            // the file doesn't carry K-values, so they come from y/x
            Assert.That(p.K[1][1], Is.EqualTo(0.6 / 0.4).Within(1e-12));
        }

        [Test]
        public void KeepsChemSepNamesWithoutAFlowsheet()
        {
            var p = CapeOpenUO.ParseChemSepProfiles(Sep, null, null);

            Assert.That(p.Compounds, Is.EqualTo(new[] { "Ethanol", "Water" }));
            Assert.That(p.X.Select(x => x[0]).ToArray(), Is.EqualTo(new[] { 0.8, 0.4, 0.1 }).Within(1e-12));
            Assert.That(p.ToCsv().Split('\n')[0], Does.Contain("x Ethanol"));
        }

        [Test]
        public void ColumnWithoutResultsIsReported()
        {
            var input = Sep.Substring(0, Sep.IndexOf("[Results]"));

            Assert.That(() => CapeOpenUO.ParseChemSepProfiles(input, null, null),
                Throws.Exception.With.Message.Contains("Solve it first"));
        }
    }
}
