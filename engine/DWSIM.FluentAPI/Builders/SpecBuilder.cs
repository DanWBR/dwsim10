using System;
using DWSIM.Interfaces.Enums;
using DWSIM.UnitOperations.SpecialOps;

namespace DWSIM.Automation.FluentAPI.Builders
{
    /// <summary>
    /// Fluent builder for the Spec logical block: it writes a target property as an expression of a
    /// source property, Y = f(X). Call <see cref="Flowsheet.AddSpec"/> to obtain one. X and Y are
    /// read and written in the flowsheet's unit system.
    /// </summary>
    /// <example>
    /// <code>
    /// fs.AddSpec("SPEC-1")
    ///   .WithSource("CW supply", "PROP_MS_0")
    ///   .WithTarget("C-1", "PROP_CL_2")
    ///   .WithExpression("X + 10")
    ///   .WithFlowsheetCalculationMode(SpecCalcMode.BeforeTargetObject);
    /// </code>
    /// </example>
    public sealed class SpecBuilder : UnitOpBuilder<Spec, SpecBuilder>
    {
        internal SpecBuilder(Flowsheet f, Spec o) : base(f, o) { }

        /// <summary>Sets the source variable, X in the expression.</summary>
        public SpecBuilder WithSource(string objectTag, string propertyId)
        {
            Object.SourceObjectData = SpecialOpLinks.Describe(Flowsheet, objectTag, propertyId, null, out var obj);
            Object.SourceObject = SpecialOpLinks.AsBase(obj);
            SpecialOpLinks.AttachSpec(Object, obj, SpecVarType.Source);
            if (Object.GraphicObject is DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.SpecGraphic g)
                g.ConnectedToSv = SpecialOpLinks.Shape(obj);
            return this;
        }

        /// <summary>Sets the target variable, Y, which the spec writes.</summary>
        public SpecBuilder WithTarget(string objectTag, string propertyId)
        {
            Object.TargetObjectData = SpecialOpLinks.Describe(Flowsheet, objectTag, propertyId, null, out var obj);
            Object.TargetObject = SpecialOpLinks.AsBase(obj);
            SpecialOpLinks.AttachSpec(Object, obj, SpecVarType.Target);
            if (Object.GraphicObject is DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.SpecGraphic g)
                g.ConnectedToTv = SpecialOpLinks.Shape(obj);
            return this;
        }

        /// <summary>Sets the expression of the target in terms of X (the source) and Y (the target's current value), for example <c>"X + 10"</c>.</summary>
        public SpecBuilder WithExpression(string expression)
        {
            if (string.IsNullOrWhiteSpace(expression)) throw new ArgumentException("The expression is empty.", nameof(expression));
            Object.Expression = expression;
            return this;
        }

        /// <summary>Clamps the value written to the target between <paramref name="minimum"/> and <paramref name="maximum"/>, in the flowsheet's units.</summary>
        public SpecBuilder WithLimits(double minimum, double maximum)
        {
            if (minimum > maximum) throw new ArgumentException("The minimum must not exceed the maximum.", nameof(minimum));
            Object.MinVal = minimum;
            Object.MaxVal = maximum;
            return this;
        }

        /// <summary>
        /// Sets when the solver evaluates specs: after the source object (the default) or before the
        /// target object, for instance. This is a flowsheet setting and applies to every spec whose own
        /// mode is <see cref="SpecCalcMode2.GlobalSetting"/> (see <see cref="WithCalculationMode"/>).
        /// </summary>
        public SpecBuilder WithFlowsheetCalculationMode(SpecCalcMode mode)
        {
            Flowsheet.Inner.FlowsheetOptions.SpecCalculationMode = mode;
            return this;
        }

        /// <summary>
        /// Sets when the solver evaluates this spec, overriding the flowsheet setting for it alone.
        /// <see cref="SpecCalcMode2.BeforeObject"/> and <see cref="SpecCalcMode2.AfterObject"/> run the
        /// spec right before or after the object tagged <paramref name="referenceObjectTag"/>, which is
        /// required for those two modes and ignored by the others.
        /// <see cref="SpecCalcMode2.GlobalSetting"/> (the default) follows the flowsheet setting.
        /// </summary>
        public SpecBuilder WithCalculationMode(SpecCalcMode2 mode, string referenceObjectTag = null)
        {
            if (mode == SpecCalcMode2.BeforeObject || mode == SpecCalcMode2.AfterObject)
            {
                if (string.IsNullOrWhiteSpace(referenceObjectTag))
                    throw new ArgumentException("BeforeObject and AfterObject need the tag of the reference object.", nameof(referenceObjectTag));
                Object.ReferenceObjectID = Flowsheet.ResolveByTag(referenceObjectTag).Name;
            }
            Object.SpecCalculationMode = mode;
            return this;
        }
    }
}
