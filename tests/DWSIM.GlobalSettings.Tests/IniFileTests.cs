//    Tests for the settings file reader that replaced Nini.
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
using System.Globalization;
using System.IO;
using System.Threading;
using NUnit.Framework;

namespace DWSIM.GlobalSettings.Tests
{
    [TestFixture]
    public class IniFileTests
    {
        private string _path;

        [SetUp]
        public void SetUp()
        {
            _path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".ini");
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(_path)) File.Delete(_path);
        }

        [Test]
        public void MissingSectionIsNull()
        {
            var source = new IniConfigSource(_path);

            Assert.That(source.Configs["Misc"], Is.Null);
            Assert.That(source.AddConfig("Misc"), Is.Not.Null);
            Assert.That(source.Configs["Misc"], Is.Not.Null);
        }

        [Test]
        public void ReadsTheFormatDwsimWrites()
        {
            File.WriteAllText(_path, string.Join(Environment.NewLine, new[]
            {
                "; a comment",
                "[Misc]",
                "EnableParallelProcessing = True",
                "MaxDegreeOfParallelism = -1",
                "UIScalingFactor = 1.25",
                "PreferredSystemOfUnits = CGS",
                "",
                "[RecentFiles]",
                "0 = C:\\one.dwxmz",
                "1 = C:\\two.dwxmz",
            }));

            var source = new IniConfigSource(_path);

            Assert.That(source.Configs["Misc"].GetBoolean("EnableParallelProcessing", false), Is.True);
            Assert.That(source.Configs["Misc"].GetInt("MaxDegreeOfParallelism", 99), Is.EqualTo(-1));
            Assert.That(source.Configs["Misc"].GetDouble("UIScalingFactor", 1.0), Is.EqualTo(1.25));
            Assert.That(source.Configs["Misc"].Get("PreferredSystemOfUnits", "SI"), Is.EqualTo("CGS"));
            Assert.That(source.Configs["RecentFiles"].GetValues(),
                        Is.EqualTo(new[] { "C:\\one.dwxmz", "C:\\two.dwxmz" }));
        }

        [Test]
        public void DefaultsComeBackWhenTheKeyIsAbsent()
        {
            var source = new IniConfigSource(_path);
            var misc = source.AddConfig("Misc");

            Assert.That(misc.GetBoolean("Nope", true), Is.True);
            Assert.That(misc.GetInt("Nope", 7), Is.EqualTo(7));
            Assert.That(misc.GetDouble("Nope", 2.5), Is.EqualTo(2.5));
            Assert.That(misc.Get("Nope", "fallback"), Is.EqualTo("fallback"));
            Assert.That(misc.Get("Nope"), Is.Null);
        }

        [Test]
        public void SaveAndReadBackKeepsEveryValue()
        {
            var source = new IniConfigSource(_path);

            var misc = source.AddConfig("Misc");
            misc.Set("EnableParallelProcessing", true);
            misc.Set("MaxThreadMultiplier", 8);
            misc.Set("UIScalingFactor", 1.25);
            misc.Set("BackupFolder", "");

            var files = source.AddConfig("RecentFiles");
            files.Set("0", "a.dwxmz");
            files.Set("1", "b.dwxmz");

            source.Save();

            var reread = new IniConfigSource(_path);

            Assert.That(reread.Configs["Misc"].GetBoolean("EnableParallelProcessing", false), Is.True);
            Assert.That(reread.Configs["Misc"].GetInt("MaxThreadMultiplier", 0), Is.EqualTo(8));
            Assert.That(reread.Configs["Misc"].GetDouble("UIScalingFactor", 0.0), Is.EqualTo(1.25));
            Assert.That(reread.Configs["Misc"].Get("BackupFolder", "unset"), Is.EqualTo(""));
            Assert.That(reread.Configs["RecentFiles"].GetValues(),
                        Is.EqualTo(new[] { "a.dwxmz", "b.dwxmz" }));
        }

        [Test]
        public void SettingAKeyTwiceReplacesItInsteadOfDuplicating()
        {
            var source = new IniConfigSource(_path);
            var misc = source.AddConfig("Misc");

            misc.Set("DebugLevel", 1);
            misc.Set("DebugLevel", 3);
            source.Save();

            var reread = new IniConfigSource(_path);

            Assert.That(reread.Configs["Misc"].GetValues().Length, Is.EqualTo(1));
            Assert.That(reread.Configs["Misc"].GetInt("DebugLevel", 0), Is.EqualTo(3));
        }

        [Test]
        public void NumbersDoNotFollowTheMachineCulture()
        {
            var previous = Thread.CurrentThread.CurrentCulture;
            Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE", useUserOverride: false);

            try
            {
                var source = new IniConfigSource(_path);
                source.AddConfig("Misc").Set("UIScalingFactor", 1.25);
                source.Save();

                Assert.That(File.ReadAllText(_path), Does.Contain("1.25"));
                Assert.That(new IniConfigSource(_path).Configs["Misc"].GetDouble("UIScalingFactor", 0.0),
                            Is.EqualTo(1.25));
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = previous;
            }
        }

        [Test]
        public void ANumberWrittenByAnOlderCommaCultureStillReads()
        {
            File.WriteAllText(_path, "[Misc]" + Environment.NewLine + "UIScalingFactor = 1,25" + Environment.NewLine);

            var previous = Thread.CurrentThread.CurrentCulture;
            Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE", useUserOverride: false);

            try
            {
                Assert.That(new IniConfigSource(_path).Configs["Misc"].GetDouble("UIScalingFactor", 0.0),
                            Is.EqualTo(1.25));
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = previous;
            }
        }

        [Test]
        public void BooleansAcceptTheSpellingsNiniAccepted()
        {
            File.WriteAllText(_path, string.Join(Environment.NewLine, new[]
            {
                "[Misc]",
                "a = true",
                "b = FALSE",
                "c = on",
                "d = no",
                "e = 1",
            }));

            var misc = new IniConfigSource(_path).Configs["Misc"];

            Assert.That(misc.GetBoolean("a", false), Is.True);
            Assert.That(misc.GetBoolean("b", true), Is.False);
            Assert.That(misc.GetBoolean("c", false), Is.True);
            Assert.That(misc.GetBoolean("d", true), Is.False);
            Assert.That(misc.GetBoolean("e", false), Is.True);
        }
    }

    [TestFixture]
    public class SettingsTests
    {
        [Test]
        public void LoadAndSaveRoundTripTheSettingsFile()
        {
            var folder = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(folder);

            var path = Path.Combine(folder, "dwsim.ini");

            try
            {
                Settings.PreferredSystemOfUnits = "CGS";
                Settings.MaxThreadMultiplier = 12;
                Settings.UIScalingFactor = 1.5;
                Settings.MostRecentFiles.Clear();
                Settings.MostRecentFiles.Add("first.dwxmz");
                Settings.MostRecentFiles.Add("second.dwxmz");

                Settings.SaveSettings(path);

                Settings.PreferredSystemOfUnits = "SI";
                Settings.MaxThreadMultiplier = 1;
                Settings.UIScalingFactor = 1.0;
                Settings.MostRecentFiles.Clear();

                Settings.LoadSettings(path);

                Assert.That(Settings.PreferredSystemOfUnits, Is.EqualTo("CGS"));
                Assert.That(Settings.MaxThreadMultiplier, Is.EqualTo(12));
                Assert.That(Settings.UIScalingFactor, Is.EqualTo(1.5));
                Assert.That(Settings.MostRecentFiles,
                            Is.EqualTo(new[] { "first.dwxmz", "second.dwxmz" }));
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        [Test]
        public void ThePythonBridgeSaysWhyItIsMissing()
        {
            var error = Assert.Throws<NotSupportedException>(() => Settings.InitializePythonEnvironment());

            Assert.That(error.Message, Does.Contain("IronPython"));
        }
    }
}
