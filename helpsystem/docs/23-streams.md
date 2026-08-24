# Streams

#### Material Stream

The Material Stream represents a flow of matter entering, leaving, or passing between unit operations in the flowsheet. Its thermodynamic state is determined by a flash calculation whose type is selected from the following options:

- Temperature and Pressure

- Pressure and Enthalpy

- Pressure and Entropy

- Pressure and Vapor Fraction

- Temperature and Vapor Fraction

The selected flash specification determines which two intensive state variables must be provided; DWSIM calculates the remaining thermodynamic properties from these:

- Temperature: stream temperature

- Pressure: stream absolute pressure

- Enthalpy: stream specific enthalpy

- Entropy: stream’s specific entropy

- Molar Fraction (Vapor Phase): the vapor phase mole fraction of the stream

###### Composition

The stream composition can be specified on any of the following bases: mole fraction, mass fraction, mole flow, mass flow, standard liquid volumetric fraction, molality, or molarity. A material stream’s composition and state variables are editable only when it has no upstream connection—that is, when it serves as a feed to the flowsheet rather than as an outlet of a unit operation. When a stream receives its properties from an upstream unit operation, it becomes read-only: all its properties are computed by that upstream block.

When specifying composition as mole or mass fractions, the values must sum to unity. DWSIM normalizes the input only after the user presses the "Apply / Commit Changes" button.

The molarity and molality input options are intended for electrolyte simulations. For molarity, the solute amounts are entered in moles and the solvent volume in litres; for molality, the solute amounts are entered in moles and the solvent mass in kilograms.

###### Flow

At least one flow specification—mass, molar, or volumetric—must be provided. The remaining two are calculated from the equation of state once temperature and pressure are known. When the composition is given as individual component mole or mass flows, the total stream flow rate is computed as their sum. When the composition is given as fractions, a separate total flow rate must be specified at the stream level.

##### Calculation Method

Once the required state variables, composition, and flow rate are specified, the material stream is solved in the following sequence:

1.  A phase-equilibrium (flash) calculation determines the number of phases and the distribution of each component among them.

2.  Thermodynamic and transport properties of each phase (density, enthalpy, viscosity, etc.) are evaluated individually using the selected property package.

3.  Bulk mixture properties are computed as phase-fraction-weighted averages of the individual phase properties.

By default, a Temperature–Pressure (TP) flash is performed, but an alternative specification (e.g., Pressure–Enthalpy) can be selected in the simulation configuration. When the stream is in read-only mode (i.e., it is an outlet of an upstream unit operation), the flash type is determined by the parameters provided by that operation; in most cases this is also a TP flash.

####### Overriding the Equilibrium Phase

You can override the equilibrium phase by setting the **Force Phase** property to the desired value (*Global Definition*, *Vapor*, *Liquid* or *Solid*. *Global Definition* is selected by default and reads the value from the Flowsheet Settings. The default setting for the Flowsheet-wide property is *Do Not Force*.

When this property is set to one of the phase names (*Vapor*, *Liquid* or *Solid*), the equilibrium calculation is bypassed and all compounds are put into the selected phase with the same composition as the mixture.

##### Output parameters

The following items are calculated:

- Component distribution among phases. Compositions can be displayed as either mole or mass fractions via the Composition Basis drop-down.

- Phase properties: specific enthalpy, specific entropy, molecular weight, density, volumetric flow rate @ T and P, phase molar and mass fraction, compressibility factor, constant-pressure heat capacity (Cp), Cp/Cv, thermal conductivity, surface tension (liquid phase only), kinematic viscosity, dynamic viscosity.

- Mixture properties: specific enthalpy, specific entropy, molecular weight, density, thermal conductivity.

If the flash algorithm setting ”Calculate bubble and dew points at stream conditions” is activated, these are also shown in the ”Mixture” properties section.

#### Energy Stream

The Energy Stream represents a rate of energy transfer (power) entering or leaving the simulation boundary. It conveys heat duties, shaft work, or electrical power consumed or produced by unit operations. Because DWSIM solves steady-state balances, the energy stream is specified as power (energy per unit time) rather than as a total energy quantity.

The power value is either specified directly by the user (e.g., to impose a fixed duty on a heater) or calculated by the connected unit operation as a result of its energy balance.

###### Input Parameters

Energy: Energy by unit of time (power) which is represented by the stream;

###### Output Parameters

There are no output parameters for this object.

