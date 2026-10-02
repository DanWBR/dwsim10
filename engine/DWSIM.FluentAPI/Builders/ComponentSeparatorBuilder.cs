using DWSIM.UnitOperations.UnitOperations;
using DWSIM.UnitOperations.UnitOperations.Auxiliary;

namespace DWSIM.Automation.FluentAPI.Builders
{
    /// <summary>
    /// Fluent builder for the Component Separator unit operation. Call <see cref="Flowsheet.AddComponentSeparator"/> to obtain one.
    /// Each specification sends a share or a flow of one compound to the specified outlet
    /// (<see cref="WithSpecifiedOutlet"/>); the other outlet gets the rest of it. Give every compound
    /// of the feed a specification: the separator only splits the compounds it has one for.
    /// </summary>
    /// <example>
    /// <code>
    /// fs.AddComponentSeparator("X-1")
    ///   .ConnectFeed(feed).ConnectProduct(sweet, 0).ConnectProduct(co2, 1)
    ///   .WithSpecifiedOutlet(0)
    ///   .WithMolarPercent("Methane", 99.0)
    ///   .WithMolarPercent("Carbon dioxide", 2.0);
    /// </code>
    /// </example>
    public sealed class ComponentSeparatorBuilder : UnitOpBuilder<ComponentSeparator, ComponentSeparatorBuilder>
    {
        internal ComponentSeparatorBuilder(Flowsheet f, ComponentSeparator o) : base(f, o) { }

        /// <summary>Picks the outlet port (0 or 1) the specifications refer to.</summary>
        public ComponentSeparatorBuilder WithSpecifiedOutlet(int port)
        {
            if (port != 0 && port != 1)
                throw new System.ArgumentOutOfRangeException(nameof(port), port, "The component separator has outlets 0 and 1.");
            Object.SpecifiedStreamIndex = (byte)port;
            return this;
        }

        /// <summary>
        /// Adds or replaces the specification of one compound. <paramref name="unit"/> is the unit of a
        /// flow specification (for example <c>"kg/s"</c> or <c>"mol/s"</c>); percentages take none.
        /// </summary>
        public ComponentSeparatorBuilder WithSeparationSpec(string compound, SeparationSpec type, double value, string unit = "")
        {
            Object.ComponentSepSpecs[compound] = new ComponentSeparationSpec(compound, type, value, unit ?? "");
            return this;
        }

        /// <summary>Sends <paramref name="percent"/> % of the compound's inlet molar flow to the specified outlet.</summary>
        public ComponentSeparatorBuilder WithMolarPercent(string compound, double percent)
            => WithSeparationSpec(compound, SeparationSpec.PercentInletMolarFlow, percent);

        /// <summary>Sends <paramref name="percent"/> % of the compound's inlet mass flow to the specified outlet.</summary>
        public ComponentSeparatorBuilder WithMassPercent(string compound, double percent)
            => WithSeparationSpec(compound, SeparationSpec.PercentInletMassFlow, percent);

        /// <summary>Sends a fixed molar flow of the compound to the specified outlet.</summary>
        public ComponentSeparatorBuilder WithMolarFlow(string compound, Quantity molarFlow)
            => WithSeparationSpec(compound, SeparationSpec.MolarFlow, molarFlow.SI, "mol/s");

        /// <summary>Sends a fixed mass flow of the compound to the specified outlet.</summary>
        public ComponentSeparatorBuilder WithMassFlow(string compound, Quantity massFlow)
            => WithSeparationSpec(compound, SeparationSpec.MassFlow, massFlow.SI, "kg/s");

        /// <summary>Read-back of the energy imbalance, in kW, that leaves through the energy stream (populated after <c>Solve</c>).</summary>
        public double EnergyImbalanceKW => Object.EnergyImb;
    }
}
