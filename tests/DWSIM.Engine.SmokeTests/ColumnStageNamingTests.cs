using System;
using System.IO;
using System.Linq;
using DWSIM.UnitOperations.UnitOperations;
using NUnit.Framework;

namespace DWSIM.Engine.SmokeTests
{
    /// <summary>Stage names follow the position (Stage1 to StageN from the top, condenser first, reboiler last) whatever
    /// is done to the stage count, and the streams stay on the stages they were attached to.</summary>
    [TestFixture]
    public class ColumnStageNamingTests
    {
        private static DWSIM.DynamicRunner.Flowsheet Load(string filename)
        {
            var folder = TestContext.CurrentContext.TestDirectory;
            while (folder != null && !Directory.Exists(Path.Combine(folder, "tests", "flowsheets")))
                folder = Path.GetDirectoryName(folder);
            Assert.That(folder, Is.Not.Null, "could not find tests/flowsheets above the test directory");
            var flowsheet = new DWSIM.DynamicRunner.Flowsheet(null, null);
            flowsheet.Init();
            flowsheet.LoadZippedXML(Path.Combine(folder, "tests", "flowsheets", filename));
            return flowsheet;
        }

        private static Column TheColumn(DWSIM.DynamicRunner.Flowsheet flowsheet)
        {
            return flowsheet.SimulationObjects.Values.OfType<Column>().First();
        }

        private static System.Collections.Generic.List<DWSIM.UnitOperations.UnitOperations.Auxiliary.SepOps.StreamInformation> InteriorFeeds(Column column)
        {
            int n = column.Stages.Count;
            return column.MaterialStreams.Values
                .Where(si => si.StreamBehavior == DWSIM.UnitOperations.UnitOperations.Auxiliary.SepOps.StreamInformation.Behavior.Feed)
                .Where(si => { int k = column.StageIndex(si.AssociatedStage); return k > 0 && k < n - 1; })
                .ToList();
        }

        private static void AssertNumbered(Column column)
        {
            int n = column.Stages.Count;
            for (int i = 0; i < n; i++)
            {
                var name = column.Stages[i].Name;
                Assert.That(name, Is.EqualTo(column.StageNameFor(i, n)), "stage at position " + i);
                if (i > 0 && i < n - 1) Assert.That(name, Is.EqualTo("Stage" + (i + 1)), "only the ends carry a role");
            }
            Assert.That(column.Stages[0].Name, Does.Contain("("), "the condenser carries its role");
            Assert.That(column.Stages[n - 1].Name, Does.Contain("("), "the reboiler carries its role");
            Assert.That(column.Stages.Select(s => s.Name).Distinct().Count(), Is.EqualTo(n), "no duplicate names");
            Assert.That(column.NumberOfStages, Is.EqualTo(n));
        }

        [Test]
        public void ALoadedColumnIsNumberedFromTheTopAndKeepsItsFeeds()
        {
            var flowsheet = Load("ExtractiveDistillation.dwxmz");
            var column = TheColumn(flowsheet);
            AssertNumbered(column);
            var ids = column.Stages.Select(s => s.ID).ToHashSet();
            // products and duties of older files carry an empty value or a bare "0" and are placed by their behaviour
            foreach (var si in column.MaterialStreams.Values.Concat(column.EnergyStreams.Values))
                if (!string.IsNullOrWhiteSpace(si.AssociatedStage) && !int.TryParse(si.AssociatedStage, out _))
                    Assert.That(ids, Does.Contain(si.AssociatedStage), "streams are attached by stage ID");
        }

        [Test]
        public void ChangingTheStageCountKeepsTheNumberingAndTheFeedStages()
        {
            var flowsheet = Load("ExtractiveDistillation.dwxmz");
            var column = TheColumn(flowsheet);
            int n = column.Stages.Count;
            var feeds = InteriorFeeds(column);
            Assert.That(feeds, Is.Not.Empty);
            var feedStageObjects = feeds.Select(si => column.Stages[column.StageIndex(si.AssociatedStage)]).ToList();

            column.SetNumberOfStages(n + 3);
            AssertNumbered(column);
            Assert.That(column.Stages.Count, Is.EqualTo(n + 3));
            for (int k = 0; k < feeds.Count; k++)
                Assert.That(ReferenceEquals(column.Stages[column.StageIndex(feeds[k].AssociatedStage)], feedStageObjects[k]), "feed stays on its stage after growing");

            column.SetNumberOfStages(n - 2);
            AssertNumbered(column);
            Assert.That(column.Stages.Count, Is.EqualTo(n - 2));
            for (int k = 0; k < feeds.Count; k++)
                Assert.That(ReferenceEquals(column.Stages[column.StageIndex(feeds[k].AssociatedStage)], feedStageObjects[k]), "feed stays on its stage after shrinking");
            TestContext.Out.WriteLine(string.Join(", ", column.Stages.Select(s => s.Name)));
        }

        [Test]
        public void ANameTypedByTheUserSurvivesAndTheOthersRenumber()
        {
            var flowsheet = Load("ExtractiveDistillation.dwxmz");
            var column = TheColumn(flowsheet);
            int n = column.Stages.Count;
            column.Stages[3].Name = "Solvent inlet";
            column.SetNumberOfStages(n + 2);
            Assert.That(column.Stages[3].Name, Is.EqualTo("Solvent inlet"));
            for (int i = 0; i < column.Stages.Count; i++)
                if (i != 3) Assert.That(column.Stages[i].Name, Is.EqualTo(column.StageNameFor(i, column.Stages.Count)));
            Assert.That(column.IsAutomaticStageName("Stage7"), Is.True);
            Assert.That(column.IsAutomaticStageName("Stage1 (Condenser)"), Is.True);
            Assert.That(column.IsAutomaticStageName("Solvent inlet"), Is.False);
        }

        [Test]
        public void AStreamAttachedByAnOldNameFollowsItsStageThroughARenumbering()
        {
            var flowsheet = Load("ExtractiveDistillation.dwxmz");
            var column = TheColumn(flowsheet);
            var feed = InteriorFeeds(column).First();
            var stage = column.Stages[column.StageIndex(feed.AssociatedStage)];
            int n = column.Stages.Count, k = column.Stages.IndexOf(stage);
            // an older file: the names follow the old convention (condenser, Estágio_i with the index, reboiler) and the
            // stream carries the stage name
            column.Stages[0].Name = "Condensador";
            for (int i = 1; i < n - 1; i++) column.Stages[i].Name = "Estágio_" + i;
            column.Stages[n - 1].Name = "Refervedor";
            feed.AssociatedStage = "Estágio_" + k;
            column.ResolveStageReferences();
            column.RefreshStageNames();
            Assert.That(feed.AssociatedStage, Is.EqualTo(stage.ID));
            Assert.That(column.StageIndex(feed.AssociatedStage), Is.EqualTo(k));
            Assert.That(stage.Name, Is.EqualTo("Stage" + (k + 1)));
            AssertNumbered(column);
        }

        [Test]
        public void TheReductionSkipsStagesThatCarryStreams()
        {
            var flowsheet = Load("ExtractiveDistillation.dwxmz");
            var column = TheColumn(flowsheet);
            int n = column.Stages.Count;
            var feed = InteriorFeeds(column).First();
            // put the feed on the last tray above the reboiler, where the old code removed stages from
            feed.AssociatedStage = column.Stages[n - 2].ID;
            var stage = column.Stages[n - 2];
            column.SetNumberOfStages(n - 3);
            Assert.That(column.Stages.Count, Is.EqualTo(n - 3));
            Assert.That(column.Stages, Does.Contain(stage), "the stage with the feed was kept");
            Assert.That(column.StageIndex(feed.AssociatedStage), Is.EqualTo(n - 5), "the feed sits on the last tray of the shorter column");
            AssertNumbered(column);
        }
    }
}
