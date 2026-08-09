//    The CAPE-OPEN persistence of a property package: what a host simulator saves into its own
//    file when it embeds one of DWSIM's packages, and reads back when the file is reopened.

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using DWSIM.Interfaces.Interfaces2;
using DWSIM.Thermodynamics.PropertyPackages;
using NUnit.Framework;

namespace DWSIM.Engine.SmokeTests
{
    /// <summary>
    /// A COM stream over a MemoryStream, which is what the host would hand the package.
    /// </summary>
    /// <remarks>
    /// Only the four members the persistence uses do anything. The engine has a wrapper of its own
    /// for the other direction, but it is Friend, so this is the smallest thing that lets a test
    /// stand where the host stands.
    /// </remarks>
    internal sealed class MemoryComStream : IStream
    {
        public readonly MemoryStream Buffer = new MemoryStream();

        public void Read(byte[] pv, int cb, IntPtr pcbRead)
        {
            var read = Buffer.Read(pv, 0, cb);

            if (pcbRead != IntPtr.Zero) Marshal.WriteInt32(pcbRead, read);
        }

        public void Write(byte[] pv, int cb, IntPtr pcbWritten)
        {
            Buffer.Write(pv, 0, cb);

            if (pcbWritten != IntPtr.Zero) Marshal.WriteInt32(pcbWritten, cb);
        }

        public void Seek(long dlibMove, int dwOrigin, IntPtr plibNewPosition)
        {
            var position = Buffer.Seek(dlibMove, (SeekOrigin)dwOrigin);

            if (plibNewPosition != IntPtr.Zero) Marshal.WriteInt64(plibNewPosition, position);
        }

        public void Stat(out System.Runtime.InteropServices.ComTypes.STATSTG pstatstg, int grfStatFlag)
        {
            pstatstg = new System.Runtime.InteropServices.ComTypes.STATSTG { cbSize = Buffer.Length };
        }

        public void SetSize(long libNewSize) => Buffer.SetLength(libNewSize);

        public void Commit(int grfCommitFlags) => Buffer.Flush();

        public void Clone(out IStream ppstm) => throw new NotSupportedException();
        public void CopyTo(IStream pstm, long cb, IntPtr pcbRead, IntPtr pcbWritten) => throw new NotSupportedException();
        public void LockRegion(long libOffset, long cb, int dwLockType) => throw new NotSupportedException();
        public void Revert() => throw new NotSupportedException();
        public void UnlockRegion(long libOffset, long cb, int dwLockType) => throw new NotSupportedException();
    }

    [TestFixture]
    public class CapeOpenPersistenceTests
    {
        private static PengRobinsonPropertyPackage APackageWithACompound()
        {
            DWSIM.GlobalSettings.Settings.AutomationMode = true;
            DWSIM.GlobalSettings.Settings.CultureInfo = "en";

            var pp = new PengRobinsonPropertyPackage();

            var water = new DWSIM.Thermodynamics.BaseClasses.ConstantProperties
            {
                Name = "Water",
                CAS_Number = "7732-18-5",
                Formula = "H2O",
                Molar_Weight = 18.015,
                Critical_Temperature = 647.13,
                Critical_Pressure = 2.2055e7
            };

            pp._availablecomps.Add(water.Name, water);
            pp._selectedcomps.Add(water.Name, water);

            return pp;
        }

        /// <summary>
        /// Save and load, which is the whole contract: a host that saves its file and reopens it
        /// has to get the package back.
        /// </summary>
        [Test]
        public void APropertyPackageSurvivesTheRoundTrip()
        {
            var saved = APackageWithACompound();

            saved.Tag = "the tag";
            saved.UseHenryConstants = true;

            var stream = new MemoryComStream();

            ((IPersistStreamInit)saved).Save(stream, true);

            TestContext.WriteLine("persisted {0} bytes", stream.Buffer.Length);

            Assert.That(stream.Buffer.Length, Is.GreaterThan(0));

            stream.Buffer.Position = 0;

            var restored = new PengRobinsonPropertyPackage();

            ((IPersistStreamInit)restored).Load(stream);

            Assert.That(restored._selectedcomps.ContainsKey("Water"), Is.True,
                        "the selected compounds did not come back");
            Assert.That(restored._selectedcomps["Water"].Molar_Weight, Is.EqualTo(18.015).Within(1e-9));
            Assert.That(restored._availablecomps.ContainsKey("Water"), Is.True);
            Assert.That(restored.Tag, Is.EqualTo("the tag"));
            Assert.That(restored.UseHenryConstants, Is.True);
        }

        /// <summary>
        /// The interaction parameters are the part a host cannot recompute, so they are the part
        /// that matters most: a package restored without them answers different numbers and says
        /// nothing about it.
        /// </summary>
        [Test]
        public void TheInteractionParametersSurviveTheRoundTrip()
        {
            // The name is what SaveData switches on to decide which model's parameters to write,
            // and a package built by hand does not have one. See the note in the test below.
            var saved = new NRTLPropertyPackage { ComponentName = "NRTL" };

            saved.m_uni.InteractionParameters.Add("Testonium",
                new System.Collections.Generic.Dictionary<string, DWSIM.Thermodynamics.PropertyPackages.Auxiliary.NRTL_IPData>());

            saved.m_uni.InteractionParameters["Testonium"].Add("Ethanol",
                new DWSIM.Thermodynamics.PropertyPackages.Auxiliary.NRTL_IPData
                {
                    A12 = 1234.5,
                    A21 = -678.9,
                    alpha12 = 0.3
                });

            var stream = new MemoryComStream();

            ((IPersistStreamInit)saved).Save(stream, true);

            stream.Buffer.Position = 0;

            var restored = new NRTLPropertyPackage { ComponentName = "NRTL" };

            ((IPersistStreamInit)restored).Load(stream);

            Assert.That(restored.m_uni.InteractionParameters.ContainsKey("Testonium"), Is.True,
                        "the interaction parameters did not come back");

            var ip = restored.m_uni.InteractionParameters["Testonium"]["Ethanol"];

            TestContext.WriteLine("A12 {0}, A21 {1}, alpha {2}", ip.A12, ip.A21, ip.alpha12);

            Assert.That(ip.A12, Is.EqualTo(1234.5).Within(1e-9));
            Assert.That(ip.A21, Is.EqualTo(-678.9).Within(1e-9));
            Assert.That(ip.alpha12, Is.EqualTo(0.3).Within(1e-9));
        }
    }
}
