//    Custom compound ordering of the flowsheet.
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
using System.IO;
using System.Linq;
using DWSIM.Automation.FluentAPI;
using DWSIM.Interfaces;
using DWSIM.Interfaces.Enums;
using NUnit.Framework;
using FluentFlowsheet = DWSIM.Automation.FluentAPI.Flowsheet;

namespace DWSIM.Engine.SmokeTests
{
    /// <summary>
    /// With the Custom ordering the flowsheet lists its compounds in the user's order, compounds left
    /// out of that order come last, the streams follow it, and the order is saved with the flowsheet.
    /// </summary>
    [TestFixture]
    public class CompoundOrderingTests
    {
        [OneTimeSetUp]
        public void Setup()
        {
            DWSIM.GlobalSettings.Settings.AutomationMode = true;
            DWSIM.GlobalSettings.Settings.InspectorEnabled = false;
            DWSIM.GlobalSettings.Settings.CultureInfo = "en";
        }

        private static FluentFlowsheet Build()
        {
            var fs = FluentFlowsheet.Create("CompoundOrder").WithCompounds("Water", "Methane", "Ethanol", "Propane")
                .WithPropertyPackage(PropertyPackages.PengRobinson);
            fs.AddMaterialStream("S1");
            return fs;
        }

        private static List<string> StreamCompounds(FluentFlowsheet fs)
        {
            var s1 = fs.Inner.SimulationObjects.Values.Single(o => o.GraphicObject.Tag == "S1");
            return s1.GetProperties(PropertyType.RO).Where(p => p.StartsWith("PROP_MS_102/")).Select(p => p.Substring(12)).ToList();
        }

        [Test]
        public void CustomOrderListsCompoundsInTheUsersOrder()
        {
            var fs = Build();
            var options = fs.Inner.FlowsheetOptions;
            var added = fs.Inner.SelectedCompounds.Keys.ToList();

            options.CustomCompoundOrder = new List<string> { "Propane", "Water", "Ethanol" };
            options.CompoundOrderingMode = CompoundOrdering.Custom;

            var expected = new[] { "Propane", "Water", "Ethanol", "Methane" };
            TestContext.WriteLine("as added: " + string.Join(", ", added) + "; custom: " + string.Join(", ", fs.Inner.SelectedCompounds.Keys));
            Assert.That(fs.Inner.SelectedCompounds.Keys, Is.EqualTo(expected), "flowsheet compounds");
            Assert.That(StreamCompounds(fs), Is.EqualTo(expected), "stream properties");

            // the stored list of compounds keeps its order: only the display changes
            Assert.That(((DWSIM.SharedClasses.DWSIM.Flowsheet.FlowsheetVariables)options).SelectedComponents.Keys, Is.EqualTo(added));

            options.CompoundOrderingMode = CompoundOrdering.AsAdded;
            Assert.That(fs.Inner.SelectedCompounds.Keys, Is.EqualTo(added), "back to As Added");
        }

        [Test]
        public void CustomOrderIsSavedWithTheFlowsheet()
        {
            var fs = Build();
            fs.Inner.FlowsheetOptions.CustomCompoundOrder = new List<string> { "Ethanol", "Methane", "Propane", "Water" };
            fs.Inner.FlowsheetOptions.CompoundOrderingMode = CompoundOrdering.Custom;

            var path = Path.Combine(Path.GetTempPath(), "compound-order-" + Guid.NewGuid().ToString("N") + ".dwxmz");
            try
            {
                fs.Save(path);
                var loaded = FluentFlowsheet.Load(path);
                Assert.That(loaded.Inner.FlowsheetOptions.CompoundOrderingMode, Is.EqualTo(CompoundOrdering.Custom));
                Assert.That(loaded.Inner.SelectedCompounds.Keys, Is.EqualTo(new[] { "Ethanol", "Methane", "Propane", "Water" }));
            }
            finally
            {
                try { File.Delete(path); } catch { }
            }
        }
    }
}
