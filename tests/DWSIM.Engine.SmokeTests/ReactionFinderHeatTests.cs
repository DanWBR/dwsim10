//    Heat of reaction of the reactions suggested by the Reaction Finder.
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
using System.Linq;
using DWSIM.Automation.FluentAPI;
using DWSIM.Extensions.ReactionFinder.Logic;
using DWSIM.Interfaces.Enums;
using NUnit.Framework;
using FluentFlowsheet = DWSIM.Automation.FluentAPI.Flowsheet;

namespace DWSIM.Engine.SmokeTests
{
    /// <summary>
    /// The reactions the Reaction Finder suggests carry their heat of reaction, so an adiabatic
    /// conversion reactor burning methane in air heats up.
    /// </summary>
    [TestFixture]
    public class ReactionFinderHeatTests
    {
        [OneTimeSetUp]
        public void Setup()
        {
            DWSIM.GlobalSettings.Settings.AutomationMode = true;
            DWSIM.GlobalSettings.Settings.InspectorEnabled = false;
            DWSIM.GlobalSettings.Settings.CultureInfo = "en";
        }

        [Test]
        public void MethaneCombustionFromTheFinderHeatsTheAdiabaticReactor()
        {
            var fs = FluentFlowsheet.Create("FinderCombustion")
                .WithCompounds("Methane", "Oxygen", "Nitrogen", "Carbon dioxide", "Water")
                .WithPropertyPackage(PropertyPackages.PengRobinson);

            var rxn = ReactionSuggester.SuggestAll(fs.Inner)
                .Select(s => s.Reaction)
                .Single(r => r.ReactionType == ReactionType.Conversion && r.Name == "Combustion of Methane");

            // CH4 + 2 O2 -> CO2 + 2 H2O, about -802.6 MJ/kmol of methane
            Assert.That(rxn.ReactionHeat, Is.EqualTo(-802600.0).Within(1).Percent);
            Assert.That(Math.Abs(rxn.StoichBalance), Is.LessThan(0.01));

            fs.Inner.AddReaction(rxn);
            fs.Inner.AddReactionToSet(rxn.ID, "DefaultSet", true, 0);

            // 100 kg/h of methane and 1800 kg/h of air (21 % O2) at 20 C and 1 atm
            var feed = fs.AddMaterialStream("feed")
                .WithTemperature(293.15.Kelvin())
                .WithPressure(1.0.Atm())
                .WithMolarFlow(19.062.MolPerSecond())
                .SetCompoundMolarFlow("Methane", 1.7316)
                .SetCompoundMolarFlow("Oxygen", 3.6400)
                .SetCompoundMolarFlow("Nitrogen", 13.690)
                .SetCompoundMolarFlow("Carbon dioxide", 0.0)
                .SetCompoundMolarFlow("Water", 0.0);

            var gas = fs.AddMaterialStream("gas");
            var liq = fs.AddMaterialStream("liquid");
            var heat = fs.AddEnergyStream("heat");

            fs.AddConversionReactor("R-1")
                .Adiabatic()
                .WithReactionSet("DefaultSet")
                .WithPressureDrop(0.0.Pascal())
                .ConnectFeed(feed, 0)
                .ConnectProduct(gas, 0)
                .ConnectProduct(liq, 1)
                .ConnectEnergyFeed(heat, 1);

            fs.Solve();

            Assert.That(gas.TemperatureK, Is.GreaterThan(2000.0));
        }
    }
}
