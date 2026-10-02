using DWSIM.UnitOperations.UnitOperations;

namespace DWSIM.Automation.FluentAPI.Builders
{
    /// <summary>
    /// Fluent builder for the Shortcut Column unit operation (Fenske-Underwood-Gilliland). Call <see cref="Flowsheet.AddShortcutColumn"/> to obtain one.
    /// Ports: feed on inlet 0, reboiler duty on energy inlet 1, distillate on outlet 0, bottoms on
    /// outlet 1, condenser duty on the energy outlet.
    /// </summary>
    /// <example>
    /// <code>
    /// fs.AddShortcutColumn("T-1")
    ///   .WithKeys("Benzene", "Toluene")
    ///   .WithLightKeyInBottoms(0.01)
    ///   .WithHeavyKeyInDistillate(0.01)
    ///   .WithRefluxRatio(2.0)
    ///   .WithPressures(1.0.Atm(), 1.0.Atm());
    /// </code>
    /// </example>
    public sealed class ShortcutColumnBuilder : UnitOpBuilder<ShortcutColumn, ShortcutColumnBuilder>
    {
        internal ShortcutColumnBuilder(Flowsheet f, ShortcutColumn o) : base(f, o) { }

        /// <summary>Sets the light and heavy key compounds.</summary>
        public ShortcutColumnBuilder WithKeys(string lightKey, string heavyKey)
        {
            Object.m_lightkey = lightKey;
            Object.m_heavykey = heavyKey;
            return this;
        }

        /// <summary>Sets the mole fraction of the light key in the bottoms.</summary>
        public ShortcutColumnBuilder WithLightKeyInBottoms(double moleFraction) { Object.m_lightkeymolarfrac = moleFraction; return this; }

        /// <summary>Sets the mole fraction of the heavy key in the distillate.</summary>
        public ShortcutColumnBuilder WithHeavyKeyInDistillate(double moleFraction) { Object.m_heavykeymolarfrac = moleFraction; return this; }

        /// <summary>Sets the operating reflux ratio, L/D.</summary>
        public ShortcutColumnBuilder WithRefluxRatio(double ratio) { Object.m_refluxratio = ratio; return this; }

        /// <summary>Sets the condenser and reboiler pressures.</summary>
        public ShortcutColumnBuilder WithPressures(Quantity condenser, Quantity reboiler)
        {
            Object.m_condenserpressure = condenser.SI;
            Object.m_boilerpressure = reboiler.SI;
            return this;
        }

        /// <summary>Sets the condenser pressure.</summary>
        public ShortcutColumnBuilder WithCondenserPressure(Quantity p) { Object.m_condenserpressure = p.SI; return this; }

        /// <summary>Sets the reboiler pressure.</summary>
        public ShortcutColumnBuilder WithReboilerPressure(Quantity p) { Object.m_boilerpressure = p.SI; return this; }

        /// <summary>Picks a total or a partial condenser.</summary>
        public ShortcutColumnBuilder WithCondenserType(ShortcutColumn.CondenserType type) { Object.condtype = type; return this; }

        /// <summary>Sets the height of one theoretical stage, used for the column height estimate.</summary>
        public ShortcutColumnBuilder WithStageHeight(Quantity height) { Object.StageHeight = height.SI; return this; }

        /// <summary>Read-back of the minimum reflux ratio, Underwood (populated after <c>Solve</c>).</summary>
        public double MinimumRefluxRatio => Object.m_Rmin;
        /// <summary>Read-back of the minimum number of stages, Fenske (populated after <c>Solve</c>).</summary>
        public double MinimumStages => Object.m_Nmin;
        /// <summary>Read-back of the number of theoretical stages, Gilliland (populated after <c>Solve</c>).</summary>
        public double TheoreticalStages => Object.m_N;
        /// <summary>Read-back of the optimum feed stage (populated after <c>Solve</c>).</summary>
        public double OptimumFeedStage => Object.ofs;
        /// <summary>Read-back of the condenser duty, in kW (populated after <c>Solve</c>).</summary>
        public double CondenserDutyKW => Object.m_Qc;
        /// <summary>Read-back of the reboiler duty, in kW (populated after <c>Solve</c>).</summary>
        public double ReboilerDutyKW => Object.m_Qb;
    }
}
