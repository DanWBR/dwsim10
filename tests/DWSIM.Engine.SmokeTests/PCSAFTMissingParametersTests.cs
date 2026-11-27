//    PC-SAFT and compounds of the flowsheet without PC-SAFT parameters.
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

using System.Linq;
using DWSIM.Automation.FluentAPI;
using DWSIM.Thermodynamics.AdvancedEOS;
using NUnit.Framework;
using FluentFlowsheet = DWSIM.Automation.FluentAPI.Flowsheet;

namespace DWSIM.Engine.SmokeTests
{
    /// <summary>
    /// The compound list belongs to the whole flowsheet, so a PC-SAFT section can share it with a section on
    /// another package that carries a compound PC-SAFT has no parameters for (elemental sulfur next to an
    /// acid gas absorber). The package refused every stream as soon as the list had such a compound; now it
    /// refuses only a stream that carries it, and the streams without it come out as if it weren't listed.
    /// </summary>
    [TestFixture]
    public class PCSAFTMissingParametersTests
    {
        private const string PCSAFT = "PC-SAFT (with Association Support) (.NET Code)";

        [OneTimeSetUp]
        public void Setup()
        {
            DWSIM.GlobalSettings.Settings.AutomationMode = true;
            DWSIM.GlobalSettings.Settings.InspectorEnabled = false;
            DWSIM.GlobalSettings.Settings.CultureInfo = "en";
        }

        private static (FluentFlowsheet fs, DWSIM.Thermodynamics.Streams.MaterialStream gas) AcidGas(params string[] compounds)
        {
            var fs = FluentFlowsheet.Create("acid gas").WithCompounds(compounds).WithPropertyPackage(PCSAFT);
            var gas = fs.AddMaterialStream("acid gas").At(40.0.Celsius(), (1.6e5).Pascal())
                .SetCompoundMolarFlow("Hydrogen sulfide", 9.0).SetCompoundMolarFlow("Methane", 0.5).SetCompoundMolarFlow("Water", 0.5);
            return (fs, gas.Object);
        }

        private static PCSAFT2PropertyPackage Package(FluentFlowsheet fs) =>
            (PCSAFT2PropertyPackage)fs.Inner.PropertyPackages.Values.Single();

        [Test]
        public void AStreamWithoutTheCompoundIgnoresIt()
        {
            var (reference, refGas) = AcidGas("Methane", "Hydrogen sulfide", "Water");
            var refErrors = reference.TrySolve();
            Assert.That(refErrors, Is.Empty, string.Join("; ", refErrors.Select(e => e.Message)));

            var (fs, gas) = AcidGas("Methane", "Hydrogen sulfide", "Water", "Sulfur");
            var pp = Package(fs);
            var sulfurCas = fs.Inner.SelectedCompounds["Sulfur"].CAS_Number;
            Assert.That(pp.HasCompoundParameters(sulfurCas), Is.False, "PC-SAFT has no parameters for elemental sulfur");

            var errors = fs.TrySolve();
            Assert.That(errors, Is.Empty, string.Join("; ", errors.Select(e => e.Message)));

            double Vf(DWSIM.Thermodynamics.Streams.MaterialStream s) => s.Phases[2].Properties.molarfraction.GetValueOrDefault();
            TestContext.WriteLine("vapour fraction {0:R} / {1:R}, H {2:R} / {3:R} kJ/kg, rho {4:R} / {5:R} kg/m3",
                Vf(refGas), Vf(gas), refGas.GetMassEnthalpy(), gas.GetMassEnthalpy(),
                refGas.Phases[0].Properties.density.GetValueOrDefault(), gas.Phases[0].Properties.density.GetValueOrDefault());
            Assert.That(Vf(gas), Is.EqualTo(Vf(refGas)).Within(1e-9));
            Assert.That(gas.GetMassEnthalpy(), Is.EqualTo(refGas.GetMassEnthalpy()).Within(1e-9 * System.Math.Abs(refGas.GetMassEnthalpy()) + 1e-9));
            Assert.That(gas.Phases[0].Properties.density.GetValueOrDefault(),
                Is.EqualTo(refGas.Phases[0].Properties.density.GetValueOrDefault()).Within(1e-9).Percent);

            Assert.That(pp.CompoundParameters.ContainsKey(sulfurCas), Is.False, "the stand-in parameters stay out of the package");
        }

        [Test]
        public void AStreamThatCarriesTheCompoundIsStillRefused()
        {
            var (fs, gas) = AcidGas("Methane", "Hydrogen sulfide", "Water", "Sulfur");
            var sulfur = fs.AddMaterialStream("sulfur").At(150.0.Celsius(), (1.6e5).Pascal()).SetCompoundMolarFlow("Sulfur", 1.0);
            var errors = fs.TrySolve();
            var messages = string.Join("; ", errors.Select(e => (e.InnerException ?? e).Message));
            TestContext.WriteLine(messages);
            Assert.That(messages, Does.Contain("Missing PC-SAFT parameters for Sulfur"));
        }
    }
}
