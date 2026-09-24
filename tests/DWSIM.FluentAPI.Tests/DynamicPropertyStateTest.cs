using System;
using DWSIM.Automation.FluentAPI;
using Flowsheet = DWSIM.Automation.FluentAPI.Flowsheet;

namespace DWSIM.FluentAPI.Tests
{
    /// <summary>
    /// A dynamic property set from a script has to come back from a stored flowsheet state, whatever type the
    /// script handed over.
    /// </summary>
    /// <remarks>
    /// pythonnet passes a python int as a <c>Python.Runtime.PyInt</c> wrapper. The object's SaveData wrote the
    /// value with its type name and Json.NET could not serialize the wrapper, so the property was dropped from
    /// the state: "Reset Contents" set to 1 before StoreCurrentStateAs came back as the file's 0 when a schedule
    /// started from that state, and the reactor kept its old holdup. <see cref="ScriptingInt"/> stands in for
    /// the wrapper here.
    /// </remarks>
    internal static class DynamicPropertyStateTest
    {
        private const string Property = "Reset Content";

        public static void Run()
        {
            var fs = Flowsheet.Create("FluentDynamicPropertyState")
                .WithCompound("Water")
                .WithPropertyPackage(PropertyPackages.SteamTables);

            var feed = fs.AddMaterialStream("feed")
                .At(25.Celsius(), 1.Atm())
                .WithMassFlow(1.0.KgPerSecond())
                .AsFlowSpec();

            var outlet = fs.AddMaterialStream("outlet")
                .At(25.Celsius(), 1.Atm())
                .AsPressureSpec();

            fs.AddTank("TK-01")
                .WithVolume(2.0.CubicMeters())
                .WithHeight(2.0.Meters())
                .ConnectFeed(feed, 0)
                .ConnectProduct(outlet, 0);

            fs.AutoLayout();
            fs.Solve();

            foreach (var value in new object[] { new ScriptingInt(1), 1, 1L, 1.0f, 1.0, true })
            {
                var label = value.GetType().Name;

                var tank = Tank(fs);
                tank.SetDynamicProperty(Property, value);

                var stored = tank.GetDynamicProperty(Property);
                if (!(stored is double) && !(stored is bool))
                    throw new Exception(label + ": stored as " + stored.GetType().FullName + ", expected Double or Boolean.");

                fs.Dynamics.StoreCurrentStateAs("Set");
                tank.SetDynamicProperty(Property, 0);

                fs.Inner.LoadProcessData(fs.Inner.StoredSolutions["Set"]);

                var restored = Tank(fs).GetDynamicProperty(Property);
                if (restored == null)
                    throw new Exception(label + ": the property is missing from the restored state.");
                if (!Convert.ToBoolean(restored))
                    throw new Exception(label + ": restored as " + restored + " (" + restored.GetType().FullName +
                        "), the value set before the state was stored was lost.");

                Console.WriteLine(label + " -> stored " + stored.GetType().Name + ", restored " + restored + " (" +
                    restored.GetType().Name + ")");
            }

            // a text value stays text
            Tank(fs).SetDynamicProperty("Note", "kept");
            fs.Dynamics.StoreCurrentStateAs("Text");
            fs.Inner.LoadProcessData(fs.Inner.StoredSolutions["Text"]);
            if (!"kept".Equals(Tank(fs).GetDynamicProperty("Note")))
                throw new Exception("A string dynamic property did not come back from the stored state.");
        }

        private static DWSIM.SharedClasses.UnitOperations.BaseClass Tank(Flowsheet fs) =>
            (DWSIM.SharedClasses.UnitOperations.BaseClass)fs.Inner.GetFlowsheetSimulationObject("TK-01");

        /// <summary>
        /// Stands in for pythonnet's PyInt: an IConvertible with an Int64 type code, whose type name the engine
        /// cannot resolve on load and which Json.NET cannot serialize.
        /// </summary>
        private sealed class ScriptingInt : IConvertible
        {
            private readonly long _value;

            public ScriptingInt(long value) { _value = value; }

            /// <summary>Json.NET reads the public properties; a foreign runtime handle cannot be read that way.</summary>
            public IntPtr Handle => throw new InvalidOperationException("The handle belongs to another runtime.");

            private IConvertible C => _value;

            public TypeCode GetTypeCode() => TypeCode.Int64;
            public bool ToBoolean(IFormatProvider provider) => C.ToBoolean(provider);
            public byte ToByte(IFormatProvider provider) => C.ToByte(provider);
            public char ToChar(IFormatProvider provider) => C.ToChar(provider);
            public DateTime ToDateTime(IFormatProvider provider) => C.ToDateTime(provider);
            public decimal ToDecimal(IFormatProvider provider) => C.ToDecimal(provider);
            public double ToDouble(IFormatProvider provider) => C.ToDouble(provider);
            public short ToInt16(IFormatProvider provider) => C.ToInt16(provider);
            public int ToInt32(IFormatProvider provider) => C.ToInt32(provider);
            public long ToInt64(IFormatProvider provider) => C.ToInt64(provider);
            public sbyte ToSByte(IFormatProvider provider) => C.ToSByte(provider);
            public float ToSingle(IFormatProvider provider) => C.ToSingle(provider);
            public string ToString(IFormatProvider provider) => C.ToString(provider);
            public object ToType(Type conversionType, IFormatProvider provider) => C.ToType(conversionType, provider);
            public ushort ToUInt16(IFormatProvider provider) => C.ToUInt16(provider);
            public uint ToUInt32(IFormatProvider provider) => C.ToUInt32(provider);
            public ulong ToUInt64(IFormatProvider provider) => C.ToUInt64(provider);
            public override string ToString() => _value.ToString();
        }
    }
}
