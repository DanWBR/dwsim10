//    The event transitions of a dynamic simulation: a value that ramps from where it was to where
//    the event puts it, instead of stepping there.
//
//    This is https://github.com/DanWBR/dwsim/issues/1070. Any transition other than a step ended
//    the run at once, because the interpolation reads the flowsheet state out of the integrator's
//    historian and the historian was written by a Compress/Decompress pair that did not round
//    trip: Compress read its MemoryStream while the GZipStream was still open and buffering, so
//    the payload held nothing but its own length prefix, and Decompress filled a buffer with a
//    single Read and returned the rest of it as zeros. What reached XDocument.Parse was a run of
//    NUL, and the reporter saw "hexadecimal value 0x00, is an invalid character".

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using DWSIM.ExtensionMethods;
using DWSIM.Interfaces;
using DWSIM.Interfaces.Enums;
using static DWSIM.Interfaces.Enums.Dynamics;

namespace DWSIM.Engine.SmokeTests
{
    [TestFixture]
    public class DynamicsEventTransitionTests
    {
        [OneTimeSetUp]
        public void Setup()
        {
            DWSIM.GlobalSettings.Settings.AutomationMode = true;
            DWSIM.GlobalSettings.Settings.InspectorEnabled = false;
            DWSIM.GlobalSettings.Settings.CultureInfo = "en";

            FlowsheetBase.FlowsheetBase.AddPropPacks();
        }

        [TestCase(1)]
        [TestCase(1000)]
        [TestCase(250000)]
        public void ATextRoundTripsThroughCompression(int length)
        {
            var text = string.Concat(Enumerable.Repeat("<a>abcdefghij</a>", (length / 17) + 1))
                             .Substring(0, length);

            var back = text.Compress().Decompress();

            Assert.That(back, Has.Length.EqualTo(text.Length));
            Assert.That(back, Is.EqualTo(text));
            Assert.That(back, Does.Not.Contain('\0'),
                        "a partial read left the tail of the buffer as zeros");
        }

        [Test]
        public void CompressionKeepsCharactersOutsideAscii()
        {
            // Object names and descriptions carry them, and ASCII turned every one into a
            // question mark on the way through the historian.
            const string text = "<obj name=\"Válvula de expansão\" desc=\"pressão\"/>";

            Assert.That(text.Compress().Decompress(), Is.EqualTo(text));
        }

        /// <summary>
        /// Two events on a material stream's mass flow: a step to 20 kg/s at t = 0, then a linear
        /// transition to 50 kg/s at t = 60 s. Halfway through, the flow has to be halfway.
        /// </summary>
        [Test]
        public void ALinearTransitionRampsBetweenTheTwoEvents()
        {
            var flowsheet = new DWSIM.DynamicRunner.Flowsheet(null, null);
            flowsheet.Init();
            flowsheet.AddCompound("Water");

            var pp = new DWSIM.Thermodynamics.PropertyPackages.SteamTablesPropertyPackage
            {
                Flowsheet = flowsheet
            };
            flowsheet.AddPropertyPackage(pp);

            var obj = flowsheet.AddObject(
                DWSIM.Interfaces.Enums.GraphicObjects.ObjectType.MaterialStream, 0, 0, "feed");
            var ms = (DWSIM.Thermodynamics.Streams.MaterialStream)flowsheet.SimulationObjects[obj.Name];
            ms.SetFlowsheet(flowsheet);
            ms.SetPropertyPackage(pp);
            ms.SetMassFlow(20.0);

            var t0 = new DateTime();

            var step = new DWSIM.DynamicsManager.DynamicEvent
            {
                ID = "step",
                Description = "init",
                TimeStamp = t0,
                EventType = DynamicsEventType.ChangeProperty,
                SimulationObjectID = ms.Name,
                SimulationObjectProperty = "PROP_MS_2",
                SimulationObjectPropertyValue = "20",
                SimulationObjectPropertyUnits = "kg/s",
                TransitionType = DynamicsEventTransitionType.StepChange,
                TransitionReference = DynamicsEventTransitionReferenceType.InitialState
            };

            var ramp = new DWSIM.DynamicsManager.DynamicEvent
            {
                ID = "ramp",
                Description = "ramp",
                TimeStamp = t0.AddSeconds(60),
                EventType = DynamicsEventType.ChangeProperty,
                SimulationObjectID = ms.Name,
                SimulationObjectProperty = "PROP_MS_2",
                SimulationObjectPropertyValue = "50",
                SimulationObjectPropertyUnits = "kg/s",
                TransitionType = DynamicsEventTransitionType.LinearChange,
                TransitionReference = DynamicsEventTransitionReferenceType.PreviousEvent
            };

            var eventset = new DWSIM.DynamicsManager.EventSet { ID = "set", Description = "set" };
            eventset.Events.Add(step.ID, step);
            eventset.Events.Add(ramp.ID, ramp);

            var manager = flowsheet.DynamicsManager;

            // Nothing recorded yet, which is where the run begins: no value to interpolate, and
            // no exception either.
            Assert.That(manager.GetPropertyValuesFromEvents(
                            flowsheet, t0.AddSeconds(1), new Dictionary<DateTime, string>(), eventset),
                        Is.Empty, "an empty historian is a reason to wait, not to fail");

            var historian = new Dictionary<DateTime, string>
            {
                [t0] = flowsheet.GetSnapshot(SnapshotType.ObjectData).ToString().Compress()
            };

            foreach (var (seconds, expected) in new[] { (1.0, 20.5), (30.0, 35.0), (60.0, 50.0) })
            {
                var props = manager.GetPropertyValuesFromEvents(
                    flowsheet, t0.AddSeconds(seconds), historian, eventset);

                Assert.That(props, Has.Count.EqualTo(1), $"at t = {seconds} s");
                Assert.That(props[0].Item2, Is.EqualTo("PROP_MS_2"));

                // The values are in SI, which for a mass flow is kg/s.
                Assert.That(props[0].Item3, Is.EqualTo(expected).Within(1e-6), $"at t = {seconds} s");
            }
        }
    }
}
