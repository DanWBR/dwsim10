using System;
using System.Collections.Generic;
using System.Linq;
using DWSIM.Interfaces;
using DWSIM.Interfaces.Enums;
using PP = DWSIM.Thermodynamics.PropertyPackages;

namespace DWSIM.Automation.FluentAPI
{
    /// <summary>
    /// Fluent surface for tweaking a property package after <see cref="Flowsheet.WithPropertyPackage(string,System.Action{PropertyPackageBuilder})"/>
    /// has instantiated it. Exposes flash-algorithm choice, generic flash settings, and
    /// typed sub-builders that surface the model-specific interaction parameters
    /// (PR/SRK kij, NRTL Aij/alpha, UNIQUAC Aij, Wilson Aij). For anything not covered
    /// by a typed setter, use <see cref="Configure"/> to mutate the underlying object directly.
    /// </summary>
    public sealed class PropertyPackageBuilder
    {
        /// <summary>The owning flowsheet.</summary>
        public Flowsheet Flowsheet { get; }
        /// <summary>The underlying DWSIM property package - escape hatch for advanced settings.</summary>
        public IPropertyPackage Inner { get; }

        internal PropertyPackageBuilder(Flowsheet fs, IPropertyPackage pp)
        {
            Flowsheet = fs;
            Inner = pp;
        }

        /// <summary>Switches the high-level flash strategy: NestedLoops (default), InsideOut or Gibbs minimization.</summary>
        public PropertyPackageBuilder WithFlashApproach(PP.PropertyPackage.FlashCalculationApproachType approach)
        {
            // Reflected to avoid forcing the CAPE-OPEN interface chain on consumers
            // that only want to flip an enum.
            var prop = Inner.GetType().GetProperty("FlashCalculationApproach");
            if (prop != null) prop.SetValue(Inner, approach);
            return this;
        }

        /// <summary>Sets a single entry on the property package's <c>FlashSettings</c> dictionary. Values are stored as strings (DWSIM convention).</summary>
        public PropertyPackageBuilder WithFlashSetting(FlashSetting key, string value)
        {
            var prop = Inner.GetType().GetProperty("FlashSettings");
            var dict = (System.Collections.IDictionary)prop.GetValue(Inner);
            dict[key] = value;
            return this;
        }

        /// <summary>Convenience overload that formats <paramref name="value"/> using invariant culture.</summary>
        public PropertyPackageBuilder WithFlashSetting(FlashSetting key, double value)
            => WithFlashSetting(key, value.ToString(System.Globalization.CultureInfo.InvariantCulture));

        /// <summary>Convenience overload for boolean settings.</summary>
        public PropertyPackageBuilder WithFlashSetting(FlashSetting key, bool value)
            => WithFlashSetting(key, value ? "True" : "False");

        /// <summary>Configures Peng-Robinson (PR / PR78 / PRSV2) interaction parameters via a typed sub-builder.</summary>
        public PropertyPackageBuilder ConfigurePR(Action<PRConfig> configure)
        {
            if (configure == null) return this;
            var c = new PRConfig(this);
            configure(c);
            return this;
        }

        /// <summary>Configures Soave-Redlich-Kwong interaction parameters.</summary>
        public PropertyPackageBuilder ConfigureSRK(Action<SRKConfig> configure)
        {
            if (configure == null) return this;
            var c = new SRKConfig(this);
            configure(c);
            return this;
        }

        /// <summary>Configures NRTL binary parameters (A12, A21, alpha; optionally B12/B21 for T-dependent).</summary>
        public PropertyPackageBuilder ConfigureNRTL(Action<NRTLConfig> configure)
        {
            if (configure == null) return this;
            var c = new NRTLConfig(this);
            configure(c);
            return this;
        }

        /// <summary>Configures UNIQUAC binary parameters (A12, A21; optionally B12/B21).</summary>
        public PropertyPackageBuilder ConfigureUNIQUAC(Action<UNIQUACConfig> configure)
        {
            if (configure == null) return this;
            var c = new UNIQUACConfig(this);
            configure(c);
            return this;
        }

        /// <summary>Configures Wilson binary parameters by CAS number (the underlying model is keyed by CAS).</summary>
        public PropertyPackageBuilder ConfigureWilson(Action<WilsonConfig> configure)
        {
            if (configure == null) return this;
            var c = new WilsonConfig(this);
            configure(c);
            return this;
        }

        /// <summary>Escape hatch: applies an arbitrary mutation to the underlying property package.</summary>
        public PropertyPackageBuilder Configure(Action<IPropertyPackage> action)
        {
            action?.Invoke(Inner);
            return this;
        }
    }

    /// <summary>Peng-Robinson (PR) interaction-parameter setter. Also covers PR78 and PRSV2 since they share the same <c>m_pr.InteractionParameters</c> shape.</summary>
    public sealed class PRConfig
    {
        private readonly PropertyPackageBuilder _parent;
        internal PRConfig(PropertyPackageBuilder parent) { _parent = parent; }

        /// <summary>Sets <c>kij</c> for the (compound1, compound2) binary. Symmetric: also writes the reverse entry.</summary>
        public PRConfig WithKij(string compound1, string compound2, double kij)
        {
            object prModel = ResolvePRModel(_parent.Inner);
            SetCubicKij(prModel, compound1, compound2, kij);
            return this;
        }

        private static object ResolvePRModel(IPropertyPackage pp)
        {
            // PR family stores its IP table in field m_pr (regardless of the variant).
            var f = pp.GetType().GetField("m_pr",
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);
            if (f == null) throw new InvalidOperationException(
                $"Property package '{pp.GetType().Name}' does not expose 'm_pr'; ConfigurePR is not applicable here.");
            return f.GetValue(pp);
        }

        internal static void SetCubicKij(object cubicModel, string c1, string c2, double kij)
        {
            // PR_IPData / SRK_IPData both expose a 'kij' double field.
            var ipDictProp = cubicModel.GetType().GetProperty("InteractionParameters");
            var ipDict = ipDictProp.GetValue(cubicModel);
            EnsureNestedEntry(ipDict, c1, c2, () => CreateIPDataWithKij(cubicModel, kij));
            EnsureNestedEntry(ipDict, c2, c1, () => CreateIPDataWithKij(cubicModel, kij));
            // If entries already existed, overwrite the kij field on both directions.
            UpdateKij(ipDict, c1, c2, kij);
            UpdateKij(ipDict, c2, c1, kij);
        }

        private static object CreateIPDataWithKij(object cubicModel, double kij)
        {
            // Find the IPData type by inspecting the inner dict's value type.
            var ipDictProp = cubicModel.GetType().GetProperty("InteractionParameters");
            var outerType = ipDictProp.PropertyType.GetGenericArguments()[1]; // Dictionary<string, IPData>
            var ipDataType = outerType.GetGenericArguments()[1];
            var inst = Activator.CreateInstance(ipDataType);
            var kijProp = ipDataType.GetProperty("kij") ?? ipDataType.GetField("kij") as System.Reflection.MemberInfo;
            if (kijProp is System.Reflection.PropertyInfo p) p.SetValue(inst, kij);
            else ((System.Reflection.FieldInfo)kijProp).SetValue(inst, kij);
            return inst;
        }

        private static void EnsureNestedEntry(object outerDict, string key1, string key2, Func<object> factory)
        {
            var dictType = outerDict.GetType();
            var containsKey = dictType.GetMethod("ContainsKey");
            var item = dictType.GetProperty("Item");
            if (!(bool)containsKey.Invoke(outerDict, new object[] { key1 }))
            {
                var innerType = dictType.GetGenericArguments()[1];
                var innerInst = Activator.CreateInstance(innerType);
                item.SetValue(outerDict, innerInst, new object[] { key1 });
            }
            var inner = item.GetValue(outerDict, new object[] { key1 });
            var innerContains = inner.GetType().GetMethod("ContainsKey");
            var innerItem = inner.GetType().GetProperty("Item");
            if (!(bool)innerContains.Invoke(inner, new object[] { key2 }))
                innerItem.SetValue(inner, factory(), new object[] { key2 });
        }

        private static void UpdateKij(object outerDict, string key1, string key2, double kij)
        {
            var item = outerDict.GetType().GetProperty("Item");
            var inner = item.GetValue(outerDict, new object[] { key1 });
            var innerItem = inner.GetType().GetProperty("Item");
            var ipData = innerItem.GetValue(inner, new object[] { key2 });
            var kijMember = ipData.GetType().GetProperty("kij");
            if (kijMember != null) kijMember.SetValue(ipData, kij);
            else ipData.GetType().GetField("kij").SetValue(ipData, kij);
        }
    }

    /// <summary>Soave-Redlich-Kwong interaction-parameter setter. Mirrors <see cref="PRConfig"/> but reflects on <c>m_pr</c> of an <see cref="PP.SoaveRedlichKwong"/> instance.</summary>
    public sealed class SRKConfig
    {
        private readonly PropertyPackageBuilder _parent;
        internal SRKConfig(PropertyPackageBuilder parent) { _parent = parent; }

        /// <summary>Sets the symmetric binary interaction parameter k<sub>ij</sub> for the SRK pair (<paramref name="compound1"/>, <paramref name="compound2"/>).</summary>
        public SRKConfig WithKij(string compound1, string compound2, double kij)
        {
            var f = _parent.Inner.GetType().GetField("m_pr",
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);
            if (f == null) throw new InvalidOperationException("SRKConfig requires an SRK property package.");
            PRConfig.SetCubicKij(f.GetValue(_parent.Inner), compound1, compound2, kij);
            return this;
        }
    }

    /// <summary>NRTL binary parameter setter. Sets A12/A21 (cal/mol), alpha12 (non-randomness, ~0.3 typical) and optional B12/B21 (T-dependent terms).</summary>
    public sealed class NRTLConfig
    {
        private readonly PropertyPackageBuilder _parent;
        internal NRTLConfig(PropertyPackageBuilder parent) { _parent = parent; }

        /// <summary>Sets the NRTL binary parameters for the (<paramref name="compound1"/>, <paramref name="compound2"/>) pair: <paramref name="a12"/>/<paramref name="a21"/> in cal/mol, non-randomness <paramref name="alpha12"/>, optional T-dependent <paramref name="b12"/>/<paramref name="b21"/>.</summary>
        public NRTLConfig WithBinary(string compound1, string compound2,
            double a12, double a21, double alpha12 = 0.3,
            double b12 = 0.0, double b21 = 0.0)
        {
            var f = _parent.Inner.GetType().GetProperty("m_uni");
            if (f == null) throw new InvalidOperationException("NRTLConfig requires an NRTL property package.");
            var model = f.GetValue(_parent.Inner);
            ActivityConfig.SetActivityBinary(model,
                compound1, compound2,
                ("A12", a12), ("A21", a21), ("alpha12", alpha12),
                ("B12", b12), ("B21", b21));
            return this;
        }
    }

    /// <summary>UNIQUAC binary parameter setter (A12/A21 in cal/mol; optional B12/B21 T-dependent terms).</summary>
    public sealed class UNIQUACConfig
    {
        private readonly PropertyPackageBuilder _parent;
        internal UNIQUACConfig(PropertyPackageBuilder parent) { _parent = parent; }

        /// <summary>Sets the UNIQUAC binary parameters for the (<paramref name="compound1"/>, <paramref name="compound2"/>) pair: <paramref name="a12"/>/<paramref name="a21"/> in cal/mol, optional T-dependent <paramref name="b12"/>/<paramref name="b21"/>.</summary>
        public UNIQUACConfig WithBinary(string compound1, string compound2,
            double a12, double a21,
            double b12 = 0.0, double b21 = 0.0)
        {
            var f = _parent.Inner.GetType().GetProperty("m_uni");
            if (f == null) throw new InvalidOperationException("UNIQUACConfig requires a UNIQUAC property package.");
            var model = f.GetValue(_parent.Inner);
            ActivityConfig.SetActivityBinary(model,
                compound1, compound2,
                ("A12", a12), ("A21", a21),
                ("B12", b12), ("B21", b21));
            return this;
        }
    }

    /// <summary>Wilson binary parameter setter. Wilson stores its BIPs in a CAS-keyed <c>Dictionary&lt;string, Dictionary&lt;string, double[]&gt;&gt;</c>; this helper sets <c>{A12, A21}</c> for the given CAS pair.</summary>
    public sealed class WilsonConfig
    {
        private readonly PropertyPackageBuilder _parent;
        internal WilsonConfig(PropertyPackageBuilder parent) { _parent = parent; }

        /// <summary>Sets the Wilson binary parameters {<paramref name="a12"/>, <paramref name="a21"/>} for the CAS-keyed pair (<paramref name="cas1"/>, <paramref name="cas2"/>).</summary>
        public WilsonConfig WithBinaryByCAS(string cas1, string cas2, double a12, double a21)
        {
            var prop = _parent.Inner.GetType().GetProperty("WilsonM");
            if (prop == null) throw new InvalidOperationException("WilsonConfig requires a Wilson property package.");
            var model = prop.GetValue(_parent.Inner);
            var bipsProp = model.GetType().GetProperty("BIPs");
            var bips = (Dictionary<string, Dictionary<string, double[]>>)bipsProp.GetValue(model);
            if (!bips.ContainsKey(cas1)) bips[cas1] = new Dictionary<string, double[]>();
            bips[cas1][cas2] = new[] { a12, a21 };
            return this;
        }
    }

    /// <summary>Shared helper for activity-coefficient model binaries (NRTL / UNIQUAC). Reflects on the inner model's <c>InteractionParameters</c> dictionary and writes named scalar fields on the IP record.</summary>
    internal static class ActivityConfig
    {
        public static void SetActivityBinary(object activityModel, string c1, string c2, params (string field, double value)[] fields)
        {
            var ipDictProp = activityModel.GetType().GetProperty("InteractionParameters");
            var ipDict = ipDictProp.GetValue(activityModel);
            EnsureRecord(ipDict, c1, c2, fields);
        }

        private static void EnsureRecord(object outerDict, string key1, string key2, (string field, double value)[] fields)
        {
            var dictType = outerDict.GetType();
            var containsKey = dictType.GetMethod("ContainsKey");
            var item = dictType.GetProperty("Item");
            if (!(bool)containsKey.Invoke(outerDict, new object[] { key1 }))
            {
                var innerType = dictType.GetGenericArguments()[1];
                item.SetValue(outerDict, Activator.CreateInstance(innerType), new object[] { key1 });
            }
            var inner = item.GetValue(outerDict, new object[] { key1 });
            var innerContains = inner.GetType().GetMethod("ContainsKey");
            var innerItem = inner.GetType().GetProperty("Item");
            if (!(bool)innerContains.Invoke(inner, new object[] { key2 }))
            {
                var ipDataType = inner.GetType().GetGenericArguments()[1];
                innerItem.SetValue(inner, Activator.CreateInstance(ipDataType), new object[] { key2 });
            }
            var record = innerItem.GetValue(inner, new object[] { key2 });
            var recordType = record.GetType();
            foreach (var (name, value) in fields)
            {
                var member = (System.Reflection.MemberInfo)recordType.GetField(name)
                                    ?? recordType.GetProperty(name);
                if (member is System.Reflection.FieldInfo fi) fi.SetValue(record, value);
                else if (member is System.Reflection.PropertyInfo pi && pi.CanWrite) pi.SetValue(record, value);
            }
        }
    }

    /// <summary>Extension methods that wire the configurator into <see cref="Flowsheet.WithPropertyPackage(string)"/>.</summary>
    public static class PropertyPackageConfigExtensions
    {
        /// <summary>
        /// Adds a property package and configures it via a typed builder. Equivalent to calling
        /// <see cref="Flowsheet.WithPropertyPackage(string)"/> followed by mutating the most-recently-added
        /// property package; failing to find it is treated as a programmer error.
        /// </summary>
        public static Flowsheet WithPropertyPackage(this Flowsheet fs, string name, Action<PropertyPackageBuilder> configure)
        {
            if (fs == null) throw new ArgumentNullException(nameof(fs));
            fs.WithPropertyPackage(name);
            if (configure == null) return fs;

            var pp = fs.Inner.PropertyPackages.Values.LastOrDefault()
                  ?? throw new InvalidOperationException("Property package was not registered after WithPropertyPackage.");
            configure(new PropertyPackageBuilder(fs, pp));
            return fs;
        }

        /// <summary>Configures the most-recently-added property package without changing it. Intended to follow a parameterless <see cref="Flowsheet.WithPropertyPackage(string)"/> call.</summary>
        public static Flowsheet ConfigurePropertyPackage(this Flowsheet fs, Action<PropertyPackageBuilder> configure)
        {
            if (fs == null) throw new ArgumentNullException(nameof(fs));
            if (configure == null) return fs;
            var pp = fs.Inner.PropertyPackages.Values.LastOrDefault()
                  ?? throw new InvalidOperationException("No property package registered yet - call WithPropertyPackage first.");
            configure(new PropertyPackageBuilder(fs, pp));
            return fs;
        }
    }
}
