# Unit Operation Dynamic Properties

In addition to the dedicated dynamic simulation blocks (gauges, controllers), many standard unit operations expose advanced dynamic properties when the flowsheet runs in dynamic mode. These properties control physical phenomena such as wall thermal mass, ambient heat exchange, rotational inertia, and fouling. This section describes the dynamic enhancements available for each unit operation category.

#### Tank

The Tank now supports a rigorous energy balance with ambient heat loss and a closed-tank mode with vapor space pressure tracking. The following dynamic properties are available:

- **Closed Tank**: when enabled, the tank is modeled as a sealed vessel. The pressure is computed from the volume-temperature flash of the accumulated contents rather than being fixed at atmospheric.

- **Ambient Temperature** and **Ambient UA Product**: when UA \> 0, heat exchange with the surroundings is calculated as $Q_{amb}=UA\,(T_{amb}-T_{fluid})$ and added to the energy balance at each time step.

- **Operating Pressure**: reports the current internal pressure (read-only in open-tank mode; computed from flash in closed-tank mode).

#### Vessel (Separator)

The dynamic Vessel model has been corrected to use the accumulation stream (rather than the mixed stream) for liquid fraction and mass flow calculations, ensuring consistent phase tracking during transients. The wall temperature ODE now includes the proper time step and wall thermal mass in the denominator.

#### Heater and Cooler

Both the Heater and Cooler share the same set of dynamic enhancements:

- **Wall Thermal Mass** (J/K): when set to a value greater than zero, the heat source no longer transfers energy directly to the fluid. Instead, the source heats the wall, and the wall exchanges heat with the fluid via an internal UA coupling, introducing a realistic thermal lag.

- **Wall Temperature** (K): the current wall temperature, updated at each time step by the ODE $\frac{dT_{wall}}{dt}=\frac{Q_{source}+Q_{amb}-Q_{fluid}}{M_{wall}\,c_{p,wall}}$ .

- **Ambient Temperature** and **Ambient UA Product**: ambient heat exchange, identical to the Tank model.

#### Valve

The Valve now models actuator dynamics and cavitation detection:

- **Actuator Time Constant** (s): replaces the discrete delay queue with a first-order lag. The actual opening moves toward the target according to $OP(t+\Delta t)=OP+\left(OP_{target}-OP\right)\left(1-e^{-\Delta t/\tau}\right)$ .

- **Opening Setpoint** (%): the target valve opening for the actuator lag.

- **Liquid Pressure Recovery Factor** ( $F_{L}$ ): used for cavitation detection according to IEC 60534.

- **Cavitation Alarm**: set to True when the pressure drop exceeds $F_{L}^{2}(P_{1}-P_{v})$ .

#### Heat Exchanger

The dynamic Heat Exchanger adds fouling growth and wall thermal mass:

- **Fouling Rate**: linear fouling resistance growth rate (m2.K/kW per second). The current fouling resistance increases at each step: $R_{f}(t)=R_{f}(t-\Delta t)+\dot{R}_{f}\,\Delta t$ . The effective overall coefficient becomes $U_{eff}=1/(1/U_{clean}+R_{f})$ .

- **Wall Thermal Mass** (J/K) and **Wall Temperature** (K): when wall thermal mass \> 0, the heat transfer is split into two resistances (hot fluid to wall, wall to cold fluid), and the wall temperature evolves dynamically between the two fluids.

#### Pump, Compressor, and Expander

These three rotating equipment unit operations, previously limited to steady-state delegation in dynamic mode, now have full dynamic models with material accumulation, pressure tracking, and rotational inertia:

- **Volume**: internal casing volume used for the accumulation stream and pressure-volume flash.

- **Rotational Inertia** ( $J$ , kg.m2): moment of inertia of the rotating assembly (motor/generator + impeller/rotor). When \> 0, the speed ramps toward the target according to $J\,d\omega/dt=\pm\tau_{motor}$ instead of changing instantaneously.

- **Current Speed** and **Target Speed** (RPM): current and setpoint rotational speed.

- **Motor Torque** / **Generator Torque** (N.m): available driving or braking torque.

- **Surge Flow Fraction** and **Surge Alarm** (Compressor only): when the volumetric flow drops below the specified fraction of the design flow, the surge alarm is set to True.

#### Rigorous Distillation Column

The rigorous column dynamic model includes the following new capabilities:

- **Souders-Brown Coefficient** ( $C_{SB}$ , m/s): when \> 0, enables flooding and weeping checks on each tray at every time step. Flooding is detected when the vapor velocity exceeds $v_{flood}=C_{SB}\sqrt{(\rho_{L}-\rho_{V})/\rho_{V}}$ ; weeping is detected when it falls below 10% of the flooding velocity.

- **Flooding Alarm** and **Weeping Alarm**: Boolean indicators set when any stage exceeds the respective limit.

- **Apply Murphree Efficiency**: when enabled, each tray’s Murphree stage efficiency is applied to the vapor flow in dynamic mode, reducing the effective mass transfer proportionally.

- **Time Step Discretization**: number of internal sub-steps per integration step, allowing finer resolution for stiff column dynamics.

#### Shortcut Distillation Column

The Shortcut Column now supports dynamic mode with separate condenser and reboiler accumulation volumes. The Fenske-Underwood-Gilliland steady-state calculation is used for initialization; subsequent time steps track the condenser and reboiler inventories independently, exchanging vapor (upward) and reflux liquid (downward) at each step. Available dynamic properties:

- **Condenser Volume** and **Reboiler Volume** (m3): liquid holdup volumes for the reflux drum and reboiler sump.

- **Condenser UA** and **Reboiler UA** (W/K): heat transfer capacity of each heat exchanger.

#### Filter

The Filter now supports dynamic cake growth and periodic backwash cycles:

- **Cake Mass** (kg): accumulated cake mass, growing at each step as $\Delta m_{cake}=W_{solids}\,\eta\,\Delta t$ where $\eta$ is the separation efficiency.

- **Backwash Interval** and **Backwash Duration** (s): when the interval \> 0, the filter alternates between filtration and backwash. During backwash the cake is removed and filtrate flow stops.

The filtrate flow rate is calculated from Darcy’s law using the current cake mass, the specific cake resistance, and the filter medium resistance.

#### Component Separator

The Component Separator now supports dynamic mode with internal holdup. Material accumulates in a volume specified by the **Volume** property, and the component separation fractions are applied to the accumulated content at each time step.

#### Solids Separator

The Solids Separator now supports dynamic mode with solids holdup and batch discharge:

- **Solids Holdup** (kg): current accumulated solids mass, growing according to the configured separation efficiency.

- **Maximum Solids Holdup** (kg): when \> 0, solids accumulate until the maximum is reached, then a batch discharge is triggered and the holdup resets to zero. When set to 0, discharge is continuous.

