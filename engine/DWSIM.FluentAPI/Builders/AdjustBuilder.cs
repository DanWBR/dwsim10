using System;
using DWSIM.Interfaces.Enums;
using DWSIM.UnitOperations.SpecialOps;

namespace DWSIM.Automation.FluentAPI.Builders
{
    /// <summary>
    /// Fluent builder for the Adjust logical block: it varies one property of one object (the
    /// manipulated variable) until a property of another (the controlled variable) reaches a target.
    /// Call <see cref="Flowsheet.AddAdjust"/> to obtain one. Property IDs are the ones
    /// <see cref="Flowsheet.Properties"/> lists, for example <c>PROP_HT_3</c> for a heater duty or
    /// <c>PROP_MS_0</c> for a stream temperature.
    /// </summary>
    /// <remarks>
    /// The flowsheet solver runs an adjust through its simultaneous adjust solver, which
    /// <see cref="Flowsheet.AddAdjust"/> turns on for the new block. The solver reads the target and the
    /// tolerance; the iteration limit is the flowsheet's.
    /// </remarks>
    /// <example>
    /// <code>
    /// fs.AddAdjust("ADJ-1")
    ///   .Manipulates("H-1", "PROP_HT_3")
    ///   .Controls("Hot water", "PROP_MS_0")
    ///   .WithTargetValue(80.0.Celsius())
    ///   .WithTolerance(0.01);
    /// </code>
    /// </example>
    public sealed class AdjustBuilder : UnitOpBuilder<Adjust, AdjustBuilder>
    {
        internal AdjustBuilder(Flowsheet f, Adjust o) : base(f, o) { }

        /// <summary>Sets the manipulated variable: the property the adjust changes.</summary>
        public AdjustBuilder Manipulates(string objectTag, string propertyId)
        {
            Object.ManipulatedObjectData = SpecialOpLinks.Describe(Flowsheet, objectTag, propertyId, null, out var obj);
            Object.ManipulatedObject = SpecialOpLinks.AsBase(obj);
            SpecialOpLinks.AttachAdjust(Object, obj, AdjustVarType.Manipulated);
            if (Object.GraphicObject is DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.AdjustGraphic g)
                g.ConnectedToMv = SpecialOpLinks.Shape(obj);
            return this;
        }

        /// <summary>Sets the controlled variable: the property brought to the target.</summary>
        public AdjustBuilder Controls(string objectTag, string propertyId)
        {
            Object.ControlledObjectData = SpecialOpLinks.Describe(Flowsheet, objectTag, propertyId, null, out var obj);
            Object.ControlledObject = SpecialOpLinks.AsBase(obj);
            SpecialOpLinks.AttachAdjust(Object, obj, AdjustVarType.Controlled);
            if (Object.GraphicObject is DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.AdjustGraphic g)
                g.ConnectedToCv = SpecialOpLinks.Shape(obj);
            return this;
        }

        /// <summary>
        /// Makes the target relative to a reference variable: the controlled variable is brought to
        /// the reference plus the target value.
        /// </summary>
        public AdjustBuilder References(string objectTag, string propertyId)
        {
            Object.ReferencedObjectData = SpecialOpLinks.Describe(Flowsheet, objectTag, propertyId, null, out var obj);
            Object.ReferenceObject = SpecialOpLinks.AsBase(obj);
            Object.Referenced = true;
            SpecialOpLinks.AttachAdjust(Object, obj, AdjustVarType.Reference);
            if (Object.GraphicObject is DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.AdjustGraphic g)
                g.ConnectedToRv = SpecialOpLinks.Shape(obj);
            return this;
        }

        /// <summary>
        /// Sets the target of the controlled variable. With <see cref="References"/> it is the
        /// difference above the reference (a temperature difference given as kelvin).
        /// </summary>
        public AdjustBuilder WithTargetValue(Quantity target) { Object.AdjustValue = target.SI; return this; }

        /// <summary>Sets the target of the controlled variable, in SI units.</summary>
        public AdjustBuilder WithTargetValue(double siValue) { Object.AdjustValue = siValue; return this; }

        /// <summary>Sets the convergence tolerance, in the controlled variable's units in the flowsheet's unit system.</summary>
        public AdjustBuilder WithTolerance(double tolerance)
        {
            if (!(tolerance > 0)) throw new ArgumentOutOfRangeException(nameof(tolerance), tolerance, "The tolerance must be positive.");
            Object.Tolerance = tolerance;
            return this;
        }

        /// <summary>
        /// Includes the adjust in the flowsheet's simultaneous adjust solver (the default for a block
        /// added through the builder). Without it the solver leaves the adjust alone.
        /// </summary>
        public AdjustBuilder SolvedSimultaneously(bool simultaneous = true) { Object.SimultaneousAdjust = simultaneous; return this; }

        /// <summary>The target, in SI units.</summary>
        public double TargetValue => Object.AdjustValue;

        /// <summary>The controlled variable's current value, in SI units (populated after <c>Solve</c>).</summary>
        public double ControlledValue => ValueOf(Object.ControlledObjectData);

        /// <summary>The manipulated variable's current value, in SI units (populated after <c>Solve</c>).</summary>
        public double ManipulatedValue => ValueOf(Object.ManipulatedObjectData);

        private double ValueOf(DWSIM.Interfaces.ISpecialOpObjectInfo info)
        {
            if (info == null || string.IsNullOrEmpty(info.ID) || !Flowsheet.Inner.SimulationObjects.ContainsKey(info.ID))
                return double.NaN;
            var v = Flowsheet.Inner.SimulationObjects[info.ID].GetPropertyValue(info.PropertyName);
            return v == null ? double.NaN : Convert.ToDouble(v, System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
