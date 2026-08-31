==================================================
DWSIM - Open Source Process Simulator
Version 10.2
Copyright (c) 2017-2026 Daniel Wagner and contributors
Copyright (c) 2008-2016 Daniel Wagner, Gregor Reichert, Gustavo Leon
==================================================

DWSIM is a software for modeling, simulation and optimization of steady-state and dynamic chemical processes.

==================================================
DISCLAIMER
==================================================

The data and information within DWSIM has been obtained from a wide variety of literature sources.  While reasonable care has been exercised in the collection of data and testing of this software, the author of DWSIM disclaims any warranty, expressed or implied, as to the accuracy or reliability of the data or calculations contained therein. The results of calculations obtained from DWSIM yield approximate results, which will not always be suitable for every application.

The software is designed for use by trained professional personnel and is not a substitute for sound professional judgment.  It is the sole responsibility of the user to validate the data presented by DWSIM and to determine whether the results of this program are accurate and suitable for any specific purpose.  No guarantee of accuracy or fitness for any purpose is expressed or implied.  The author strongly recommends that the data be checked against other sources and/or methods before use and application.  The author shall not be held liable for any direct, indirect, consequential or incidental damages incurred through use of the data or calculations. 

==================================================
LICENSE
==================================================

DWSIM is licensed under the GNU General Public License (GPL) Version 3.
 
==================================================
SYSTEM REQUIREMENTS
==================================================

OS:             

Windows: 64-bit 7/8/10
Linux: 64-bit Debian distro or equivalent
macOS: 10.7 (OS X Lion) or newer

Software:

Microsoft .NET Framework 4.6.2 or newer (Windows) 
Microsoft .NET 8 and IPOPT (Linux)

CPU:			

1.0 GHz dual-core processor (minimum)

Memory:         

2 GB RAM

Graphics Card:	

128 MB with OpenGL hardware acceleration

Display:        

1280x800 display resolution is recommended as a minimum

Disk space:		

750-2000 MB for program files

==================================================
VERSION HISTORY / CHANGELOG
==================================================

The full changelog including souce code changes can be viewed at https://github.com/DanWBR/dwsim/commits/windows

Version 10.2

- [NEW] Cross-platform interface: property tables, master tables, spreadsheet tables, charts, text blocks, rectangles, buttons and pictures can be placed on a flowsheet and configured
- [NEW] Cross-platform interface: utilities can be attached to a flowsheet object, and a simulation that already carries them no longer loses them on opening
- [NEW] Cross-platform interface: the DWSIM Assistant opens docked to the right, loads from the executable folder and starts its server in the background, with its button on the menu bar
- [NEW] Cross-platform interface: the user compound databases can be managed from the welcome screen - add, create, remove databases and view or delete their compounds
- [NEW] Cross-platform interface: backup copies are offered back at startup when the previous run did not close normally
- [NEW] Assay Manager: assays saved and loaded as XML, with the previous binary format still read
- [NEW] Forced Solids tab back on the advanced property package editor, next to Property Overrides
- [NEW] Cross-platform interface: the Natural Gas Properties and Heat of Combustion Calculator plugins are back, on the Plugins menu
- [NEW] Pipe network: producing wells with a black-oil IPR (BSW and gas-oil ratio), gas lift injection, ESP/BCSS downhole pump and a critical-flow choke
- [NEW] Pipe network: the real DWSIM hydraulic and thermal pipe editors embedded in each network pipe
- [NEW] Pipe network: valves, compressor, bridge, non-return valve and an inline separator handled by the nodal solver
- [NEW] Pipe network: nodal analysis plot (IPR x VLP), gas-lift allocation optimizer, well controls and field production-target allocation
- [NEW] Pipe network: hydraulic profile plots, composite multi-pipe profiles, a bulk well table editor and a field production report in oilfield units
- [NEW] Pipe network: flow-assurance screens - erosion, hydrate, wax appearance, asphaltene stability and Turner liquid-loading
- [NEW] Pipe network: scaling and corrosion analysis coupled to the segments, with a formatted report
- [NEW] Pipe network graph tools: tabbed chart/data layout with copy and export (PNG for charts, CSV for tables)
- [NEW] Pipe network: a deviation survey (measured depth, inclination, azimuth) builds vertical, horizontal or directional tubing by minimum curvature, on the full pressure/temperature balance
- [NEW] Pipe network: Fetkovich and Darcy radial inflow performance relations added alongside linear PI and Vogel
- [NEW] Pipe network: gas-lift valve mechanics - injection rate from Thornhill-Craver critical orifice flow through the valve port
- [NEW] Pipe network: a Production Well template (reservoir, IPR, tubing, wellhead) on the designer
- [NEW] Pipe network: seven new samples - production well, wellhead choke, water injection, multi-well pad, pressure-reducing valve, pipeline pump stations and gas-condensate well
- [NEW] Black Oil Compound Creator with laboratory PVT calibration, on both interfaces
- [NEW] Case Library tab on the welcome screen, next to FOSSEE, on both interfaces
- [NEW] Cross-platform interface: a results report from the Results menu, grouped by unit-operation type, as HTML, CSV or plain text
- [NEW] Anaerobic Digester (ADM1-Full): a feed-alkalinity input so a buffered substrate (such as manure) sets the charge-balance pH and gives a methane-richer biogas
- [NEW] Pipe network designer: a snap-to-grid canvas (on by default, saved with the network), property and master property tables on the canvas, and an Insert menu for the well and flow-control equipment
- [NEW] MCP Server and Assistant API: configure and read any unit operation (including Recycle, Energy Recycle, Spec and Adjust) by whichever name the caller knows a property by, and read back its calculated results
- [NEW] MCP Server and Assistant API: run and control a dynamic simulation, and check a flowsheet for what is wrong with it without solving
- [NEW] Object palette: a third-party unit operation (a DLL in the unitops folder that is not part of the base or Patreon edition) is grouped into its own section by the developer or suite it declares, on both interfaces
- [NEW] Pipe network: takes part in a dynamic simulation, quasi-steady - re-solved in steady state at every pressure-flow step against that instant's boundaries, with a re-solve rate, a fail mode and a wall-clock ceiling per step (higher subscription tier)
- [NEW] Pipe network: the valve opening, choke bean, ESP frequency, pressure control valve setting and gas-lift casing pressure follow a first-order lag with dead time instead of stepping to a command
- [NEW] Pipe network: every block exposes its properties to the flowsheet as "<block>: <property>", so a PID can read a node pressure and write a valve opening
- [NEW] Dynamics Wizard on both interfaces: reports what stands between a converged flowsheet and a dynamic run, proposes a value for each finding, and creates the integrator, schedule and monitored variables
- [NEW] Pipe network: a dynamic sample - a water transmission main feeding a break tank through a pressure-reducing valve, with the tank level held by a controller moving the valve setting through its actuator
- [NEW] Pipe network: a pressure-temperature property table for a fixed composition - the branch interpolates a tabulated flash (vapour fraction, phase densities, viscosities, enthalpy and per-compound phase compositions) instead of flashing at every increment
- [CHG] IPOPT is now a managed solver shipped with DWSIM, checked against the native library over five thousand problems
- [CHG] Gibbs reactor initial estimate solved by a managed simplex instead of the native lpsolve55
- [CHG] Petalas-Aziz two-phase pressure drop converted from a native library to managed code
- [CHG] Automation and Fluent API: column specification types accept their common spellings
- [CHG] Gibbs reactor: the optimiser stops once the answer is good enough, instead of running on to a tolerance no solver reaches
- [CHG] Reaktoro property package and Gibbs reactor now call Reaktoro 2 directly; no Python distribution is needed any more
- [CHG] CoolProp updated to 8.0.1 and available on Linux and macOS, on x64 and ARM, not only x64 Windows
- [CHG] CAPE-OPEN: the state a host simulator saves for an embedded DWSIM component is now XML instead of binary serialization
- [CHG] PFR and CSTR: rate expressions are compiled once per calculation instead of at every integration step; the methane steam reforming sample solves in 0.17 s instead of 11.0 s
- [CHG] Property correlations, kij expressions and equilibrium constants written as text are compiled once instead of at every evaluation
- [CHG] Cross-platform interface: extenders, unit operations, property packages and plugins are read from the folder holding the executable, as on Windows
- [CHG] Cross-platform interface: text boxes, combo boxes, spinners and check boxes brought down to the height of the Windows fields
- [CHG] Cross-platform interface: the compound search ranks matches by similarity so the exact name rises to the top, selects the best match, and adds it on Enter
- [CHG] Cross-platform interface: uses the native system font (Segoe UI on Windows, San Francisco on macOS) for sharper text
- [CHG] Patreon benefits bar: an Annual Membership option, and the join buttons are hidden once a valid key is verified
- [CHG] Windows installers offer to remove a previous version first, keeping user simulations, custom compounds and added extensions
- [CHG] Vapour thermal conductivity corrected for pressure with the Stiel-Thodos dense-gas method
- [CHG] MCP Server: address simulation objects by tag, list their ids and canvas positions, and rename them
- [CHG] Compositional pipe-network nodal solver accelerated with a response-surface model and a more stable outer loop
- [CHG] Automatic and natural flowsheet layout orient objects along flow and lay recycle loops out as a rectangle
- [CHG] Cross-platform interface: an interface scaling factor scales fonts, controls and menu icons together (issue #17)
- [CHG] Cross-platform interface: the docked object editor gets a close button and roomier rows
- [CHG] Pipe network designer: an equipment editor opens instantly, building its Results tab only when it is shown
- [CHG] Refreshed User Guide and a rebuilt offline help, generated from the integrated help system, including the pipe network chapter
- [CHG] Pipe: Weymouth and Panhandle A/B single-phase gas pipeline pressure-drop equations, with a configurable pipeline efficiency factor
- [CHG] Pipe network: the compositional nodal solver re-solves an edited network much faster by evaluating pipes directly on a warm start and only refining cold solves
- [CHG] Salt precipitation: the solid-liquid flash gets a solubility product for salts, the SVLE outer loop no longer re-concentrates what it precipitates, and dissolved salts are kept in the pressure/vapour-fraction flash basis
- [CHG] PID controller: an optional manipulated-variable span scales the output on that variable's own scale around a bias, for a loop whose two variables differ in unit and magnitude
- [CHG] Pipe network: the compositional outer loop stops once it stops improving and delivers its most self-consistent iterate, cutting the well cases to about a third of the time
- [CHG] Automation interface: a flowsheet obtained through it can be given the repaint handler the solver calls, so a host can colour objects by status while a solve runs
- [CHG] Pipe network: an option evaluates every pipe on two grids and extrapolates away the leading discretisation error, reaching a converged answer from the discretisation already drawn for about twice the cost (off by default)
- [CHG] Pipe network: the nodal solver uses an analytic Beggs-Brill pressure-drop gradient, so multiphase branches converge with fewer correlation evaluations
- [CHG] Pipe network: an option relaxes each pipe's energy balance by Wegstein's method, reaching the same answer in far fewer passes on pipes that exchange heat (off by default)
- [CHG] Pipe network: the default pipe temperature convergence tolerance is 0.01 K, was 0.1 K
- [CHG] Pipe network: a compositional pipe also re-flashes on how far the fluid has moved, not only on a step count
- [CHG] Pipe network: a branch can read tabulated fluid properties instead of flashing, and stops tabulating when its table does not pay for itself
- [CHG] Pipe network: the pipe flash and energy-balance settings are on the network property list, with the tolerances and iteration limits
- [FIX] A product of a CAPE-OPEN unit operation was re-flashed by the flowsheet property package, discarding the phase split the unit had computed
- [FIX] Object editors were cut off on a display set above 96 dots per inch; the distillation column stage table was the most visible case
- [FIX] A flowsheet opened from the desktop on macOS started DWSIM with nothing loaded and reported an unsupported file
- [FIX] Peng-Robinson, PR78 and SRK: the lowest-Gibbs root selection could settle on a root below the covolume, which describes no state
- [FIX] CAPE-OPEN property packages could not drive a plug flow reactor, and a simulation saved on one silently reopened on a different property package
- [FIX] Peng-Robinson, PR78 and SRK: the compressibility factor was always taken from the smallest root of the cubic, so a gas could be reported as a liquid
- [FIX] Equilibrium reactor: only the activity and fugacity bases converged; every other basis reported zero conversion, and the concentration bases were rejected outright
- [FIX] Heat exchanger: property overrides were ignored in the internal iterations, so the two sides of the energy balance disagreed
- [FIX] Compound creator: the regression of vapour pressure and liquid viscosity started from a fixed guess that overflows outside room temperature
- [FIX] Shell and Tube heat exchanger: the number of shells in series was ignored by the heat transfer area and by the LMTD correction factor, so putting shells in series changed nothing; simulations with more than one shell will report a higher duty
- [FIX] Heat exchangers: the heat exchange profile was drawn for the maximum possible duty, so the Minimum Internal Temperature Approach was zero on every exchanger; it now follows the actual duty and is signed
- [FIX] CAPE-OPEN: DWSIM released the stream belonging to the simulator hosting it, which could corrupt the host process
- [FIX] CAPE-OPEN: a property package saved by a host could never be loaded back; the save and the load disagreed on where the data was
- [FIX] CAPE-OPEN: a hosted property package was never sent the Terminate call the standard requires, and its interaction parameters were not saved
- [FIX] CoolProp packages: a property asked for outside the accepted range ended the calculation instead of extrapolating
- [FIX] CoolProp incompressible mixtures: the solvent vapour viscosity returned the liquid value, thirty times too large
- [FIX] Binary phase envelope: the dew curve dipped below the bubble curve on heteroazeotropic systems
- [FIX] PRSV2, PRSV2-VL, Lee-Kesler-Plocker, Chao-Seader and Grayson-Streed: stream enthalpy and entropy were missing the ideal-gas part, so energy balances and PH/PS flashes were wrong; existing simulations on these packages will report different, correct temperatures
- [FIX] Dew point calculations could return the metastable liquid branch on partially miscible systems
- [FIX] Water Electrolyzer: waste heat was added as a power instead of a specific enthalpy, overstating the temperature rise
- [FIX] Shell and Tube heat exchanger: switching to Fouling Factor Calculation discarded the specified outlet temperatures
- [FIX] Heat exchanger with steam tables on one side: the maximum duty came out negative and the exchanger did not calculate
- [FIX] Steam Tables (IAPWS-IF97): states above 1073.15 K returned a physically impossible enthalpy instead of being refused
- [FIX] Dynamic simulation: event transitions other than Step ended the run with a null character error
- [FIX] Dynamic simulation: the historian time slider could not restore a recorded state
- [FIX] Flowsheet Unit Operation: a sub-flowsheet that failed to load reported an unrelated null reference later
- [FIX] Control Valve: the opening table was interpolated against the opening column, so the tabulated Kv was ignored
- [FIX] Cross-platform interface: the phase envelope dew curve drew a stray segment far past the critical point instead of ending there
- [FIX] Cross-platform interface: the phase envelope failed with "B = infinity" for mixtures whose compounds have no fusion temperature
- [FIX] Cross-platform interface: external unit operations (bioreactor, anaerobic digester) could not be added from the palette or the context menu
- [FIX] Cross-platform interface: premium, refining and biochemical unit operations were spread across the generic palette sections instead of their own
- [FIX] Cross-platform interface: the compressor and expander performance-curve editor did not open on the expander and could not create curves
- [FIX] Cross-platform interface: deleting a material stream now releases the connected ports and removes the connection lines, and it no longer stays in the inlet/outlet lists (#22, #24)
- [FIX] Cross-platform interface: undocking a panel no longer leaves its content blank when it is docked again (#25)
- [FIX] Cross-platform interface: the DWSIM Assistant helper is now closed when DWSIM exits (#23)
- [FIX] Cross-platform interface: connect mode now connects two objects by clicking the source and then the target (#21)
- [FIX] Cross-platform interface: the property-package selector no longer adds a package on type-ahead, only on a deliberate pick (#18)
- [FIX] Flash (PV/PH/PS): an unconverged temperature is now rejected instead of flowing on as a misleading error; the PV flash is seeded near the dew point
- [FIX] macOS: the app no longer crashes on the embedded web view, and the assistant helper is shipped executable (#13)
- [FIX] Cross-platform interface: a tool opening a web page falls back to the system browser where no embedded view is available
- [FIX] Extensions folder: an already-loaded extension assembly is no longer loaded a second time
- [FIX] Steam Tables: a flash is kept inside the range the correlations are fitted to
- [FIX] Anaerobic Digester: the biogas composition partitions CO2, NH3 and water vapour correctly
- [FIX] Dynamic Heat Exchanger: the cell pressure is floored at the minimum pressure
- [FIX] Cross-platform interface: the Script unit operation editor opens a script edit window, and Flowsheet.WriteMessage works from scripts
- [FIX] Cross-platform interface: the Script Manager opens under the Semi theme, extension menu-bar buttons appear after a flowsheet loads, and the sample scripts run on Python 3
- [FIX] Pipe network: electrolyte scaling and corrosion speciation corrected
- [FIX] Pipe network: solve gating, translated property names, zoom-to-fit at any display scaling, and Results populated on every block after the solve
- [FIX] Pipe network: boundary streams attached to the block ports, profile-plot crash fixed, live solver progress, and headless-saved networks reload
- [FIX] Pipe network nodal solver: a full Newton step is required before convergence, and the control-valve loop tolerance was tightened
- [FIX] Membership: the higher of the cached and live subscription level is honoured (issue #26)
- [FIX] Missing biomass and extra compounds restored in the shared distribution content (issue #28)
- [FIX] Cross-platform interface: the master property table editor keeps its object and property selection
- [FIX] Inserted property tables and charts on a flowsheet draw instead of reporting an error
- [FIX] Reaction Manager no longer fails when adding a component that is not already in the reaction
- [FIX] Pipe network designer: the Solve and Abort toolbar buttons are no longer oversized
- [FIX] Pipe network designer (Windows): Ctrl, Shift and Alt reach the canvas again, restoring multi-selection and Shift-drag offset
- [FIX] Distillation and absorption columns in dynamic mode: the column seeds its tray and sump holdups from the steady-state solution and no longer fails with a "needs to be (re)initialized" error
- [FIX] Dynamic Properties and user-defined property editors no longer throw when opened and now list their properties
- [FIX] Anaerobic Digester (ADM1-Full): the substrate is characterised from its elemental formula and fed as a composite particulate (carbohydrate/protein/lipid plus inerts), so the conversion is realistic and its nitrogen is released as ammonia instead of everything behaving like fully-degradable carbohydrate
- [FIX] Anaerobic Digester: hydraulic loading is taken from the total feed instead of the flashed liquid phase, so a slurry feed no longer gets an unrealistically long retention time
- [FIX] A chemical formula with fractional atom counts is parsed correctly, so a lumped biomass formula no longer drops its sulfur and the digester now produces H2S
- [FIX] Cross-platform editors: a typed decimal such as 0.965 was misread as 965 in a locale whose group separator is a period, corrupting the composition; numeric input is now parsed without a thousands separator
- [FIX] Property package editor: clicking Configure on a CAPE-OPEN property package no longer crashes
- [FIX] Compound Creator: the Standard Enthalpy of Formation label is no longer clipped
- [FIX] Pipe network: cloning a non-empty network no longer throws, the network block and its boundary streams are laid out on the flowsheet, and editing a network property no longer overwrites a Source feed
- [FIX] Cubic equations of state (Peng-Robinson, SRK): near the critical point the liquid root could fall at or below the covolume and give an impossible liquid density; the physical root is now selected above the covolume with a density limit and the trivial single-phase solution is rejected (issue #40)
- [FIX] Distillation: a column with no pressure drop specified left its stage pressures at zero and failed to solve; the stage pressures are repaired from the condenser and reboiler pressures (issue #38)
- [FIX] Vapor Compression Chiller: the saturated vapour and liquid states are taken from the vapour-fraction flash and converted to a molar basis, and the shaft power is written to the shaft port, which is connectable and takes a duty specification
- [FIX] Fired Heater: it is fired for the duty it is asked to deliver, writes the resulting fuel flow back to its fuel stream, and no longer rejects the geometry it ships with
- [FIX] Fired Heater: a burner lightly fired for its installed surface area no longer fails with a NaN flue gas temperature; the radiant, shield and convection sections cannot absorb more heat than the fuel releases
- [FIX] Advanced Heat Exchanger: Simulation mode solves for the exchanger's own duty
- [FIX] Refining unit operations: the outlet temperature and mass balances are closed and the duty ports are connectable
- [FIX] Neutralization reactor: its energy balance is closed
- [FIX] Precipitation reactor: it no longer invents a sludge stream, and the reordering of its solid outlet was dropped
- [FIX] Zeolite adsorber: the raffinate carries the feed pressure instead of its enthalpy
- [FIX] Restriction orifice: it iterates on the flow it is solving for
- [FIX] Air Cooler 2: an internal name-resolution error is fixed
- [FIX] Material and energy stream switches report an unset routing expression instead of failing silently
- [FIX] Pipe network: loading a saved network repopulates instead of accumulating duplicate blocks, a boundary that ran backwards is re-specified, and a newly added block gets its connectors
- [FIX] Cross-platform interface: the Equipment Database catalog import dialogs are owned by the host window
- [FIX] A simulation saved by a host that never used the spreadsheet could not be reopened
- [FIX] Anaerobic Digester (ADM1-Full): an unphysical hydraulic load is rejected instead of integrating into a meaningless steady state that still reports convergence
- [FIX] Pipe network: compositional networks that would not converge now do - relaxation follows the residual trend, Beggs-Brill gains its holdup clamp and its transition inclination correction, and a rate the pipe cannot be evaluated at is approached through a continuous barrier
- [FIX] Pipe network: a boundary running against the way it is drawn is refused before anything is written, so a refused solve leaves the flowsheet untouched
- [FIX] Pipe network: the critical (sonic) choke transition is smooth instead of a kink the solver could not follow
- [FIX] PID controller: a controller driven from automation, or restored from a file whose editor was never opened, did not resolve the object it manipulates
- [FIX] Phase envelope: a package with no analytical critical point, or an envelope whose bubble and dew curves do not intersect, falls back to the pseudo-critical point instead of failing
- [FIX] An empty graphic-object entry in an older file cost the whole flowsheet its objects and its connections
- [FIX] Cross-platform interface: editor rows that were unreadable on the dark theme
- [FIX] Welcome screen: the FOSSEE flowsheet listing is fetched over https
- [FIX] Splash screen: fonts taken from the system message-box font
- [FIX] Cross-platform interface: the editors' "Linked to" row shows the spec or adjust attached to the object, as the Windows editor does, and is hidden when there is none
- [FIX] Windows interface: dragging objects from the palette onto the flowsheet leaked a GDI handle on every mouse move and froze the application after a few drags ("A generic error occurred in GDI+"); the drag cursor is built once now (issue #44)
- [FIX] Pipe network: compositional results move with this release's liquid-density correction; every compositional sample was re-solved and now carries results matching what the engine computes, with rates shifting by up to 16%
- [FIX] Anaerobic Digester (ADM1): a fixed outlet temperature now drives the biochemistry, acid-base equilibria and gas transfer, which used to run at the feed temperature whatever the Thermal Mode said
- [FIX] Property packages: a compound with an undefined heat of vaporisation (a normal boiling point above its critical temperature, or a NaN from a bad critical pressure) no longer poisons mixture enthalpy and pulls a mixer outlet to the enthalpy datum temperature
- [FIX] Pipe network: a stuck compositional solve no longer reports itself converged - outer convergence now also requires the inner GGA to converge; the choke exposes a Critical Flow Ratio
- [FIX] Pipe network: a critical (sonic) branch switches regime in the solver (rate held at the critical value, energy equation dropped) instead of being pushed back by a penalty barrier
- [FIX] Pipe network: the direct-evaluation vs surrogate choice is latched for the life of the warm start, so a network returns the same result every solve
- [FIX] Mixer: an inlet carrying zero mass flow (the empty phase of an upstream split, say) no longer fails the mixer with a NaN-enthalpy error; it is skipped (issue #45)
- [FIX] Python Script: the Python 3 standard library is bundled and on the interpreter search path, so a Python Script unit operation and flowsheet scripts can import stdlib modules (pathlib, json) on every platform (issue #46)
- [FIX] Spreadsheet: a cell reading a property or cross-referencing another cell no longer shows a stale or zero value on first open; the sheet is recalculated once after its data and custom functions are in place
- [FIX] OPC-UA Client plugin: opening it no longer fails with a missing Opc.Ua.ClientControls assembly; the resolver now probes a plugin subfolder (plugins/opclibraries) for its private dependencies
- [FIX] Dynamic simulation: the dynamic-mode property window now opens for a tank, gas-liquid separator, reactor, distillation column or pipe instead of failing with an invalid-cast error (issue #49)
- [FIX] Dynamic simulation: clicking a monitored variable in the integrator settings no longer crashes the cross-platform interface (issue #50)
- [FIX] Pipe network editor: the temperature and pressure tolerance fields were loaded into each other's box; the stored values are worth checking in simulations saved earlier
- [FIX] Pipe segment: no heat transfer was reported, and the increment stopped iterating, where the fluid crossed the ambient temperature along an increment
- [FIX] Pipe network: the nodal solver's outer loop tests temperature against its own tolerance instead of one meant for pressure; a gas condensate now returns the same answer when solved repeatedly

Version 10.1

- [NEW] Implemented analytical derivatives for PR, PR78, SRK, NRTL, UNIQUAC, UNIFAC and other thermodynamic models
- [NEW] Analytical true critical point for cubic equations of state (PR, PR78, SRK, PRSV2)
- [NEW] Liquid-liquid equilibrium region (binodal, spinodal and consolute point) on the binary phase diagram
- [NEW] Anaerobic Digester: sulfur balance and H2S in biogas exposed in the editors and Fluent API
- [NEW] Electrolyte NRTL and Extended UNIQUAC: vapour-liquid equilibrium, so absorbers and strippers can be solved on these packages
- [NEW] Biomass Compound Creator: sulfur in the elemental formula, carried into the molar weight, the degree of reduction and the shipped biomass compounds
- [NEW] BioReactor: sulfur carrier compound closes the sulfur balance across the growth stoichiometry
- [NEW] Anaerobic Digester (ADM1-S): kinetic sulfate reduction with four sulfate-reducing populations competing with the methanogens, and H2S inhibition
- [CHG] Pump performance curves can now be scaled to the operating speed through the affinity laws
- [CHG] Liquid-liquid flash: spinodal seeding and Newton acceleration near the critical solution temperature
- [CHG] Analytical derivatives wired into the Electrolyte NRTL, Extended UNIQUAC and Glycol packages; sour-water stripper solves in about a third of the time
- [CHG] Analytical temperature derivatives for PRSV2-VL, Chao-Seader and Grayson-Streed
- [CHG] Electrolyte speciation about three times cheaper per call, with a warm start from recent solutions
- [CHG] Newton solver: line search now requires an Armijo sufficient decrease, instead of accepting a step that leaves the residual unchanged
- [FIX] Pump in Performance Curves mode: shaft power was zero when no power curve was supplied; flow rates outside the curve range are now rejected
- [FIX] Anaerobic Digester (ADM1 Full): integrator reached its step limit early and reported an early transient as steady state
- [FIX] Anaerobic Digester (ADM1): carbon and nitrogen balances did not close and pH inhibition left high pH uninhibited
- [FIX] Three-phase (VLLE) flash could stop before converging and mislabel the pressure/temperature outputs
- [FIX] Liquid-liquid flash failed to converge near and above the consolute temperature
- [FIX] Indicators (gauges): monitored value was passed to other blocks in SI units while labelled with the gauge display units
- [FIX] Equilibrium reactions: a Keq expression using Log(T), Exp() or Sqrt() silently evaluated to K = 1
- [FIX] Absorption column: top product was accounted for as a liquid distillate, so the closing mass balance rejected the solution
- [FIX] Absorption column: initial liquid traffic came from the feed connection order instead of the feed positions
- [FIX] Absorption column: steam-stripped columns did not converge; bottom-stage estimate and Sum-Rates temperature step corrected
- [FIX] Electrolyte NRTL and Extended UNIQUAC: equilibrium solver was missing the component balances, so speciation depended on initialisation
- [FIX] Electrolyte NRTL: activity coefficients were evaluated at the composition last loaded instead of the one requested
- [FIX] Electrolyte NRTL: the shared activity model was read and written concurrently by parallel column stages
- [FIX] Sour Water: vapour enthalpy gave a negative heat capacity above about 500 K, and the pressure-enthalpy flash could settle above the boiling point
- [FIX] Electrolyte NRTL and Extended UNIQUAC: speciation could fail at a composition it had just solved, because each solution seeds the next one
- [FIX] Electrolyte NRTL and Extended UNIQUAC: pressure-enthalpy and pressure-entropy flashes aborted on a single unevaluable point, leaving column products uncalculated
- [FIX] Electrolyte NRTL and Extended UNIQUAC: the three speciation retry passes were not distinct, so retrying could not recover a failure
- [FIX] BioReactor: growth stoichiometry was solved per mole of substrate but applied per C-mol, so mass was created in proportion to the substrate carbon number
- [FIX] Biomass compounds: stored molar weights and degrees of reduction disagreed with their own elemental formulas
- [FIX] Equilibrium and Gibbs reactors: a Newton solve whose step had collapsed reported convergence and stopped the damping ladder from trying larger steps

Version 10.0

- [NEW] Fully-featured AI Assistant chat window and AI Insights/Troubleshooting tools
- [NEW] Enhanced Heat Balance model for Separator Vessel 
- [NEW] Supports loading of simulations in ZIP file format
- [NEW] Dynamic mode for Pipe Segment
- [NEW] Dynamic mode for Absorption Column
- [NEW] Pressure Safety Valve (PSV) Dynamic Unit Operation
- [NEW] Information Carrier logical op
- [NEW] Immiscible Liquids option for flash algorithms
- [NEW] Heat of Combustion plugin for Cross-Platform UI
- [NEW] Clock sync feature for Dynamic Integrator
- [NEW] Dynamic mode historian controls 
- [NEW] Display PID correction factors
- [NEW] Dynamic Properties editor overhaul (Classic UI)
- [NEW] New calculation algorithm for CSTR - old algorithm is still available as an option
- [NEW] Change Feed and Recycle Stream properties directly from the Material Streams table
- [NEW] Added new flowsheet information fields (Process Type, Process Description, Key Compounds, Reactants and Products)
- [NEW] Implemented Calculated MITA for Heat Exchanger (DanWBR/dwsim#902)
- [CHG] Redesigned file loading bar (Classic UI)
- [CHG] Distillation Column initial estimates show a message when no solution is available
- [FIX] Fixed Sensitivity Analysis utility solver not breaking on errors (Classic UI)
- [FIX] Fixed negative flow issues with PFR model
- [FIX] Fixed Co-current max heat exchange calculation in Heat Exchanger
- [FIX] #898: Crash coping Text Block (Cross-Platform UI)
- [FIX] #937: Rate and Advanced Kinetics Specification
- [FIX] #968: PressureDrop not varying with Internal Diameter and Length (Pipe UO)
- [FIX] #974: Steam Tables Package causes separation error in v9.0.5
- [FIX] #996: Mixer outlet specific enthalpy becomes zero when one inlet stream flow goes to zero
- [FIX] Fixed issues with Flowsheet UO
- [FIX] Fixed mass balance in reactors when volumetric flow is defined
- [FIX] Fixed Vessel inlet mass balance in dynamic mode
- [FIX] Fixed Conversion/Equilibrium reactor balance info
- [FIX] Fixed PH/PS flash error message
- [FIX] Fixed property units on indicators
- [FIX] Fixed displayed Vessel volume
- [FIX] Fixed an issue with the Valve dynamic model
- [FIX] Fixed issues with Steam Tables Property Package
- [FIX] Fixed kW to TR conversion
- [FIX] Fixed L to m3 conversion

Version 9.0.5

- Implemented Unit Operation state restore after error (Beta)
- Implemented Hydrocarbon solubility in water for Nested Loops Immiscible Flash
- Fixed an issue with Nested Loops Three-Phase Flash
- Fixed an issue with Bulk Petroleum Characterization Tool (Classic UI)
- Fixed an issue with Orifice Plate Unit Operation
- Fixed issue with Flowsheet Unit Operation
- Fixed issue #875: Conversion and Concentration Profile Tab missing from Results Section in PFR
- Fixed issue #801: Buttons in Element Matrix editor is missing
- Fixed issue #887: Error running a flowsheet block that uses a kinetics reaction script
- Fixed issue #893: Option to Change Integration Step/ODE Solver Volume Step for PFR in macOS/Windows Cross Platform OS is not available
- Other minor fixes and UI enhancements

Version 9.0.4

- Added ft3/h to volumetric units (#862)
- Fixed an issue with bubble/dew point calculation
- Fixed saving on close behavior (Classic UI)
- Fixed custom calculation order behavior (Classic UI)
- Fixed Property/Master Table configuration not being saved on macOS (#860)
- Fixed crash on macOS when adding objects to PFD (#858)

Version 9.0.3

- Added Shortcut column Height and Diameter calculation
- Distribute waste heat proportionally on water electrolyzer outlet streams (#838)
- Added missing Object Layout undo/redo
- Exposed outlet temperature on PFR (#806)
- Updated translations
- Fixed CAPE-OPEN UO crash on Linux/macOS (#837)
- Fixed issue #833 (wrong unit for condenser spec)
- Fixed issue #834 (PVF Flash spec issue/bug)
- Fixed issue #831 (Dynamic control Tank Level missing liquid level property)
- Fixed Dynamic Integrator input issue (Classic UI)
- Fixed PFR profile exporting
- Fixed issue with mass balance in compound separator
- Fixed single-phase Flash
- Fixed image export scaling (CPUI)
- Fixed issue with Sensitivity Analysis utility (Classic UI)
- Fixed Flowsheet UO not stopping solver on errors

Version 9.0.2

- New: User-defined Systems of Units can be now renamed in Simulation Settings panel (Classic UI)
- New: Implemented new calculation modes for vessel unit op
- New: Expose PFR integration step
- New: Display NPSH Available for Pumps
- New: Material Stream list order by (Classic UI)
- New: Five-point stencil numerical derivatives for NL PV flash
- Changed: Property table UI redesign  (Cross-Platform UI) #752
- Changed: Remove backup files on user closing recovery window
- Changed: Redesigned layout of Kin/HetCat reaction editors (Classic UI)
- Changed: Flowsheet busy message; added TR as power unit
- Bugfix: Classic UI: Material Stream Component Results Transposed #761
- Bugfix: Need convert to SI for TC, PC and fix overriding MW calculation in Form FormBulkAddPseudos #764
- Bugfix: Cross Plateform UI Crash on no schedule launch #779
- Bugfix: Hot Fluid Flow Conductance in Heat Exchanger Dynamic Mode Properties #772
- Bugfix: Conversion reactor - output energy stream is editable #783
- Bugfix: Fixed issue with Spreadsheet solving

Version 9.0.1

- Bugfix: Crash then NRTL added as second properties package #751
- Bugfix: New floating windows placement on multi-monitor setup #747
- Bugfix: No conversion table after running the simulation  #746
- Bugfix: Crash the application when you click Help #745
- Bugfix: App crash right click on rectangle #742
- Bugfix: Can't select and open last opened file from recent list #741
- Bugfix: No cyrrilic fonts available #740
- Bugfix: Crash on Click Object Pallette Apple Silicon #739
- Bugfix: WPF rendering should be disabled in linux builds #737
- Bugfix: Cannot select an objects on flowchart #736

Version 9.0.0

- New: Cross-Platform UI overhaul + GTK 3 and .NET 8 support for Linux
- New: Pipe Segment model updates: added wall and insulation temperatures, absorbed radiation
- New: Import json compounds from File load items
- New: Implemented fixed chart limits for dynamic integrators (#697)
- New: Added CPUI editor for PID Controller #715
- New: Added CPUI editor for Input objects #715
- Changed: Optimized memory consumption
- Changed: Reworked Rigorous Column initial estimates editor controls (Classic UI)
- Changed: Recycle Op now defaults to legacy mode solving
- Bugfix: Nested Loops flash
- Bugfix: Rigorous Column stage naming
- Bugfix: Solver busy message
- Bugfix: Zedgraph file saving
- Bugfix: Thermal conductivity does not accept copy paste from excel #695
- Bugfix: Fixed issue with CO2 equilibrium calculations
- Bugfix: Fixed chemeo error message
- Bugfix: Mass Flow Rates Mismatch in PFR -Inlet and Outlet Mass Flow Rates are Different #726
- Bugfix: Copying Element in the flowsheet #698
- Bugfix: Fitting Loss for Elbows/Bends #708
- Bugfix: Carbon Dioxide Bug and JT data #717
- Bugfix: Compound separator produces negative molar flow #713
- Bugfix: Excel Add-In: Can't select a Flash Algorithm / Can't configure flash algorithms settings #700

Version 8.9.0

- Cross-Platform UI overhaul + added GTK 3 and .NET 8 support for Linux
- Fixed a bug in Nested Loops flash
- Recycle Op now defaults to legacy mode solving
- Fixed solver busy message
- Reworked Rigorous Column initial estimates editor controls (Classic UI)
- Fixed zedgraph file saving
- Fixed rigorous column stage naming

Version 8.8.3

- Added spreadsheet object to Automation3 API
- Added MJ/h to heat flow units (#675)
- Updated IronPython version to v3.4.1
- Fixed Cantera Interop sample
- Fixed Biodiesel Combustion (Cantera) sample
- Fixed issue with Sensitivity Analysis utility (Classic UI) (#684)
- Fixed issue with Optimization utility (Classic UI) (#688)
- Fixed enthalpy and entropy calculation for Seawater Property Package (#682)
- Fixed Pipe Segment external diameter calculation
- Fixed issue with loading reaction sets for internal flowsheets in Flowsheet UO
- Fixed issue with CAPE-OPEN Property Package equilibrium calculations
- Missing compound parameter warnings are now optional

Version 8.8.2

- Optimized memory consumption and CPU usage
- Added mass balance residual and total energy balance to flowsheet results
- Added phase volumetric fractions
- Set default System of Units  (Classic UI) 
- Fixed issues with Sensitivity Analysis (Classic UI)
- Fixed ironpython script debugging
- Fixed/disabled display of dynamic properties in steady state
- Tentative fix for Excel.exe not closing in Spreadsheet UO after calculation
- Fixed memory leaks
- Fixed issue with Heat Exchanger UO
- Other fixes and enhancements

Version 8.8.1

- Control Panel for Controller Logical Block in Cross-Platform UI (CPUI)
- Added an option to use legacy convergence mode for Recycle blocks (#667)
- Improved CPUI initialization and loading times
- Fixed Dynamics Integrator not working in CPUI
- Fixed Reaktoro Property Package
- Fixed issue with reactor models
- Fixed Heat of Combustion plugin
- Fixed solid density calculation
- Fixed issue #669 (single compound partial sublimation when Tfus > Tsat)
- Fixed issue with rigorous column
- Fixed issue #656 (Crashing when using Simulation setup wizard)
- Fixed issue #657 (Faulty unit for Cp in compound creator wizard)
- Fixed issue #664: memory leak in automation mode
- Fixed issue with property package missing BIPs after calculating a binary envelope (Classic UI)

Version 8.8.0

- New search functionality to Property Table editors (Classic UI)
- New Object List Panel (classic ui)
- New Step feature in Dynamic Mode (Classic UI)
- New Recycle Smoothing Factor to improve convergence
- Implemented item ordering for Flowsheet Property Tables
- New Load Compound JSON files on startup (Classic UI)
- Implemented #630: User selected solver for PFR reactor (DotNumerics)
- Implemented #631: Compound Properties: Add 'Export to JSON' button
- Improved Azeotropic temperature calculation
- Workaround for invalid SSL Certificate at cheric.org
- Fixed Surface Tension calculation
- Fixed issue with Compound Creator Wizard
- Fixed issues with Data regression tool
- Added tabular t-dep data to compound constant properties
- Fixed issue with PFD designer
- Fixed issue #628: Native save dialog for Compound creator
- Fixed issue #649: Water electrolyzer results not updating
- Fixed issue #652: Compound separator generates mass
- Fixed issue #628: Utilities running
- Fixed issue #627: Delete or update user compound

Version 8.7.1

- Enhanced integration with DWSIM Pro
- Fixed issue with Spreadsheet UO
- Fixed/Enhanced Pump Curves subsystem
- Nested Loops Flash fixes
- Fixed Compressor energy stream creator in editor
- Fixed saving DWSIM files with embedded Flowsheet Unit Ops (#617)
- Fixed flowsheet view reduced after deactivating contol panel mode (#607)
- Fixed Extra property values not showing in hover tooltips in DWSIM versions > 8.2.1 (#610)

Version 8.7.0

- Import/Export NRTL/UNIQUAC/Wilson BIPs
- Import/Export PR/SRK/PRSV2/LKP BIPs
- Wilson BIP estimation
- Various Rigorous Column enhancements: new initial estimates selector, enhanced reporting, test convergence tool
- Data Regression Utility: added support for Wilson BIPs
- Data Regression Utility: Export regressed BIPs
- Export imported compounds to JSON (Classic UI)
- Estimate PID Controller parameters (Classic UI)
- Enhanced floating table drawing for big property lists
- Dynamic valve flow calculation now obeys Material Stream's maximum allowable dynamic flow rate
- Toggle show/hide object label
- Fixed Heat Exchanger sizing sample
- Fixed issue with CSTR
- Fixed issue with PRLK enthalpy/entropy calculations (#593)
- Fixed Reporting tool (Classic UI)
- Fixed infinite loop message issue
- Fixed axes in embedded dynamic mode charts
- Fixed phase split heuristics
- Fixed object recalculation not updating editor
- Fixed issue with SimpleLLE flash algorithm
- Fixed issue with single comp PH flash
- Fixed issue with Dynamics Cause and Matrix editor (Classic UI)
- Fixed port/connector positions for ChemSep column
- CAPE-OPEN Property Package Manager fix
- Added compound mass balance check for rigorous columns (#599)
- Fixed Shortcut Column calculation with subcooled and superheated feeds

Version 8.6.8

- Implemented compressor/expander pressure ratio calculation mode (#550)
- Update material stream when changing specs in dynamic mode (#588)
- Fixed issue #566 (Material Streams not updating - Classic UI)
- Fixed issue #567 ("What's New in DWSIM" dialog hides "Update Available" dialog)
- Fixed issues with defining number of stages and initial estimates for rigorous column (#590)
- Fixed issue with dynamic charts
- Fixed issue with compressor and expander performance curves
- Fixed memory leaks in dynamic mode
- Fixed issue with PID Controller
- Fixed issue with ChemSep column

Version 8.6.7

- Fixed issues with CoolProp Incompressible Mixtures (#563)
- Fixed issues with Steam Tables and CoolProp Property Packages
- Fixed an issue with Simultaneous Adjust Solver
- Fixed issues with online compound importing
- Fixed some UI issues
- Removed Online UNIFAC/MODFAC Structure retrieval tool (currently unavailable)

Version 8.6.6

- Added Mach number calculation and reporting for gas flow in pipes
- Added slurry viscosity correction for PFR and Pipe Segment (Yoshida et al, https://www.aidic.it/cet/13/32/349.pdf)
- New Modified Bubble-Point solver for Rigorous Columns
- Kinetic/HetCat/Conversion reactions/reactors now support compounds in solid phase
- Surface Tension is now calculated for all liquid phases
- General fixes to Rigorous Column model and solvers
- Fixed issue with Coolprop Incompressibles PP
- Fixed issues with Pump model
- Fixed Solid Cp calculation
- Other minor fixes and enhancements

Version 8.6.5

- Includes ThermoC Property Package  
- Dynamic Event transitions
- Export PFR Profile to new spreadsheet (Classic UI/CPUI)
- Heat Exchange Profile now available for all Heat Exchanger calculation modes
- Enhanced Heat Exchange Profile Viewer (CPUI)
- New Enthalpy calculation method (Liquid Cp + Excess)
- EOS-based models can now select Experimental Liquid Cp as Enthalpy calculation method
- Changed duration controls for Dynamics Integrator to allow periods higher than 24 hours (Classic UI)
- Fixed issues with Pure Compound Viewer (Classic UI)
- Fixed conversion reactor energy balance (#541)
- Fixed not being able to update kappa parameters for PRSV2 PP
- Fixed Heat Exchanger calculation when specifiyng invalid outlet temperatures
- Fixed Enthalpy calculation from Liquid Cp
- Fixed/added CO2 Normal Boiling Point
- Fixed Excess Enthalpy calculation
- Fixed Pump curves are not being correctly saved (#548)
- Fixed input issue with Pipe Hydraulic Profile on Classic UI (#543)
- German translation fix

Version 8.6.1

- Updated connections for Fuel Cell and Electrolyzer models
- Updated H/S/Cp calculation routine for Raoult's Law Property Package
- Added more Dynamics Integrator events to Script Manager
- Exposing Indicators' monitored values
- Fixed issue with Compound Viewer (Classic UI)
- Fixed some Inspector issues
- Other minor fixes and enhancements

Version 8.6.0

- Redesigned Undo/Redo feature (Classic & Cross-Platform UI)
- Better handling of unknown errors
- New standard volumetric flow units
- Fixed Input and PID controller editors (Classic UI)
- Fixed object deletion in context menu (Classic UI)
- Added fields to setup the equilibrium constant calculation in equilibrium reactions (Cross-Platform UI)
- Fixed Component Properties View: Liquid Phase Missing Property Displays (#521)
- Fixed Chemsep flowsheet mouse hover issue (#520)
- CoolProp fix density for INCOM mixture (#519)
- Bug fixes

Version 8.5.1

- Added messages log feature (Classic UI)
- Added Experimental liquid cp option for PRSV2
- Copy object ID to clipboard (Classic UI)
- Redesigned input box (Classic UI)
- Implemented feature request #502 (Include Stream Description/Comments for HMB)
- Implemented #503 (Mixer in dynamic mode, equalize all option)
- Implemented feature request #500 (Define profile segment from tabular data input Length/Elevation)
- Added t/h and t/min to mass flow unit set (#513)
- Redesigned material stream list (Classic UI)
- Fixed spreadsheet formatting
- Fixed thermo importing
- Fixed floating table formatting for compound amounts
- Fixed energy mixer icon
- Throwing exception for calculated negative flows in compound separator (#496)
- Fixed issue with dynamics integrator (Classic UI)
- Fixed issue #497 (Need clone ForcePhase for ShallowCopy MaterialStream?)
- Fixed deltaT conversion in phase envelope (Classic UI) (#492)
- Floating table adjustments
- Fixed diamter/height units for rig columns
- Fixed viscosity conversion
- Fixed calculation order for single object solver requests
- Fixed updating pump outlet pressure
- Fixed T/P units in column properties report
- Fixed issue #505 (Fix add json-compound)
- Fixed issue #494 ([Linux Cross-Platform UI] Reaction edit exception)
- Fixed issue #491 ([Linux Cross-Platform UI] New reaction buttons placement)
- Fixed issue #480 ([Linux Cross-Platform UI] Mono process use 100% CPU after solver failed to calculate)
- Fixed PR/SRK BIP not updating (#507)
- Other minor fixes and enhancements

Version 8.5.0

- New Object Palette (Classic UI)
- Added Aqueous Electorlytes (Reaktoro) Property Package
- Added Cut/Copy/Paste items to context menus (Classic UI)
- Removed dependency of the additional Python environments package
- Fixed an issue with Gibbs Reactor
- Removed interactive python console (Classic UI)
- Fixed compound search (Classic UI) (#489)
- Fixed Infinity Loop on Calc Critical Point in PR78 (#487)
- Other fixes and enhancements

Version 8.4.7

- Added Feed Recovery as specification for Distillation Columns
- Added context menu in compound selection panel for viewing, exporting and replacing (Classic UI)
- Exposed Tray Spacing for user definition in Rigorous Column
- Implemented Stop button for Cross-Platform UI (feature request #481)
- Fixed loading of additional compounds for Property Packages in CAPE-OPEN/Excel mode
- Improved initialization time
- Other bug fixes

Version 8.4.6

- Fixed memory leaks in Classic UI
- Overall performance and memory consumption improvements

Version 8.4.5

- New Henry's Constants database from https://henrys-law.org/
- Added MaterialStream.SetOverallMolarComposition() function
- Clear log when converging recycles (Classic UI)
- Fixed issue with kinetic and hetcat reaction editors (Classic UI)
- Removed OpenGL renderer (Classic UI)
- Fixed Classic UI freezing during calculations
- Fixed Compressor/Expander curve extrapolation
- Other minor fixes and enhancements

Version 8.4.4

- Implemented Natural Layout feature for PFD
- Display phase total amount in Material Stream editor (Classic UI)
- Implemented #461: warning messages when detecting unexpected phases for Pump, Compressor and Expander
- Fixed and updated Excel Add-In sample
- Fixed issue with macOS/Linux freezing when estimating missing NRTL/UNIQUAC parameters
- Fixed SVLLE flash algorithm error with forced solids
- Fixed #457: SetOverallMassComposition not working
- Fixed SetPropertyVal for Cape-Open UO
- Fixed deadlock on UIThread
- Fixed PFR pressure drop calculation
- Fixed issue with Liquid-Liquid Extraction column
- Fixed missing BIP message for UNIFAC models
- Fixed some property display units for heat exchanger
- Other minor bug fixes and UI adjustments

Version 8.4.3

- Updates to Simulate 365 Login and File Picker
- Nested Loops Flash optimizations
- Exposing Valve's Actual Kv
- Added "Invert Selection" to Edit menu (Classic UI)
- Zoom+Center on mouse double-click
- Fixed object cloning
- Fixed an issue with Air Cooler model (#421)
- Flowsheet zooming/centering fixes
- Fixed issue with Liquid Cp calculation
- Fixed a bug with Valve's Kv/Cv calculation mode
- Other minor fixes

Version 8.4.2

- Updated Cheméo parser to get data from new API endpoint
- Smart Object Solver disabled by default
- Added button to view all log messages
- Japanese translation review
- Fixed Dashboard window size for high-dpi displays
- Fixed System.ValueTuple version mismatch
- Fixed bug with OpenGL renderer (Classic UI)
- Fixed Excel Add-In not working
- Fixed PFR pressure drop calculation when Ntubes > 1 (discussion #445)
- Fixed bug #422: Error in CoolProp PH flash
- Fixed bug #444: Incorrect refrigerant outlet temperature
- Fixed bug #446: Large temperature drop across splitter

Version 8.4.1

- Steam Tables PP now checks for Water exclusivity on Material Streams
- Classic UI layout adjustments
- Fixed an issue with System.Tuple.dll

Version 8.4.0

- Smart Object Solving
- Fail-Safe Equilibrium Calculation method
- Force usage of Henry's constants
- Auto-estimate missing NRTL/UNIQUAC paremeters 
- New Property Package information window (Classic UI)
- Added Critical Point calculation routine for PR78
- More human-readable error messages
- PH Flash optimization
- PR/SRK enhancements
- Fixed issue with Pump's energy stream connection button in editor
- Added FOSSEE Custom Models to object selection palette (Classic UI)
- Fixed issue #425: IronPython - 'import csv' fails in Script Manager
- Fixed issue #429: Dynamic modelling for water heat exchanger does not work
- Fixed issue #432: Adding ExtraProperties to recycle or energy stream crashes flowsheet UI
- Fixed issue with Absorption Column
- Fixed data fetching from Cheméo
- Other minor fixes and enhancements

Version 8.3.5

- Phase Diagram now supports immiscible water (Classic UI)
- Display fractions as percentages in Material Stream editor (Classic UI)
- Updated CoolProp library version to 6.4.3
- Fixed some spreadsheet issues (Classic UI)
- Fixed an issue with Heater and Cooler UOs
- Fixed issue #418: Inert compound that reacts
- Fixed DWSIM requesting unnecessary permission to monitor keyboard input on macOS
- Fixed floating tables not working on macOS
- Fixed issue with saving initial estimates for Rigorous Column
- Fixed compound element parsing from formula
- Fixed water mass balance in immiscible flash
- Other minor bug fixes

Version 8.3.4

- Implemented Feature Request #395: Specification block not updated/solved
- Fixed bug #377: Simulation settings exception
- Fixed bug #407: Binary interaction parameters input not working
- Fixed bug #410: Incompressible fluid PH flash with any PR EOS does not work

Version 8.3.3

- Machine-generated translations for 14 languages (Classic UI)
- Estimation of Rigorous Column diameter and height
- Added SI Engineering units
- Fixed issue with Water Electrolyzer model
- Fixed issue with Compressor and Expander
- Fixed issue with Seawater Property Package
- Other minor fixes and enhancements

Version 8.3.2

- Several Electrolyzer Model enhancements: 
	- Supports Heavy Water + Deuterium
	- Supports User-defined Efficiency
	- Calculation of Thermoneutral and reversible voltages
	- Fixed calculation of outlet temperature/enthalpy
- Enhanced reliability of VLE PV Flash
- Enhanced reliability of Phase Envelope Utility
- Fixed Pump Head calculation
- Fixed Deuterium formation properties
- Other minor bug fixes

Version 8.3.1

- Improved reliability of VLE and VLLE flash algorithms
- Improved calculation speed for Gibbs Reactor model
- Fixed Gibbs Element Matrix editor (CPUI) (#371)
- Automation Interface fixes

Version 8.3.0

- Python Script Controller (Dynamics)
- Implemented Valve Opening/Kv[Cv] relationship types
- Implemented Valve Actuator Delay (Dynamics)
- New Distillation/Absorption Column convergence and property reports
- New PFR Non-Adiabatic Non-Isothermal mode (#167)
- PFR: User-defined linear pressure drop (#168)
- Expose Pump Head as a property 
- Implemented user-defined data table for calculation of Overall HTC in Pipe Segment (#238)
- Persist custom calculation order (#59)
- Changed kij editing table to matrix format (#49)
- Changed recycle mass flow error calculation
- Fixed D2O ideal gas enthalpy/gibbs energy of formation
- Fixed many High-DPI issues (Classic UI)
- Other bug fixes

Version 8.2.1

- Fixed Material Stream issues on Cross-Platform UI
- Fixed Python.NET interpreter on macOS/Linux (set Python library path on General Settings panel)
- Fixed CAPE-OPEN UO selection panel layout (Classic UI)
- Minor fixes

Version 8.2.0

- Updated ChemSep database to v8.32 - new compounds are available
- New Bulk Add Petroleum Fractions Tool (Classic UI)
- Property Package recommendations (beta feature - Classic UI)
- New Flowsheet Theme: Color Icons
- Adjustments to default Flowsheet Theme
- Fixed issue with XML Database loading
- Fixed an issue with Bubble Point Column Solver
- Fixed user-defined Unit Set not being added to list (Classic UI)
- Fixed issues with Heat Exchanger
- Other minor fixes

Version 8.1.1

- Fixed crashes when using certain Property Packages
- Fixed some editors not appearing correctly in High-DPI displays
- Fixed an issue with Air Cooler 2
- Fixed Classic UI not saving simulations on Linux
- Weather Panel is now hidden by default 
- Minor fixes

Version 8.1.0

- Fixed Classic UI crashing on Linux (any)
- Fixed Cross-Platform UI crashing on Ubuntu 18 and newer
- Fixed issues with Gibbs Reactor
- Fixed issues with CoolProp Property Package
- Fixed #359: Latent heat not fully transferred in heat exchanger
- Fixed #362: Mixer not respecting 1st law
- Classic UI layout fixes (Windows)
- Set Flash Calculation type on Simulation Wizard (Classic UI, Windows)
- Fixed PDF/SVG export on High-DPI displays (Cross-Platform UI)
- Fixed issues with Wilson Property Package
- Fixed issues with importing data from online sources (Classic UI)
- Sort material and energy streams list on editors (#358)
- Fixed an issue with Python initialization
- Rigorous Absorber solver improvements
- Fixed a bug with Bubble Point Rigorous Column solver

Version 8.0.4

- Toggle Weather Panel visibility (Classic UI)
- Fixed issues with Rigorous Column
- Fixed an issue with Automation3 interface
- Fixed issue #346 (PEM Fuel Cell)
- Phase Envelope fixes (Classic UI)
- Fixed PEM Fuel Cell editor
- Wind Turbine now uses rotor diameter instead of disk area
- Rolled back compound search behavior (#351)
- Fixed issue #352 (import compound)
- Fix for updating compound data from JSON (#354)
- Fixed pasting data in material stream composition table (#329)
- Fixed simplex penalty value for smoother minimization in conversion reactor (#356)
- Fixed Absorption Column stage naming

Version 8.0.3

- Fixed an issue with FOSSEE flowsheets loading
- Fixed an issue with PV Flash (#333)
- Fixed an issue with compound search on Classic UI (#328)
- Fixed an issue with Master Property Table (#337)
- Fixed an issue with single-compound flash and forced solids

Version 8.0.2

- Added Weather Conditions panel to Cross-Platform UI
- Fixed an issue with PFR
- Fixed an issue with single-compound flash and forced solids
- Disable/Enable automatic update checks

Version 8.0.1/8.0.0

- New Unit Operations:  PEM Fuel Cell, Water Electrolyzer, Wind Turbine, Hydroelectric Turbine,
  Solar Panel, Gibbs Reactor (Reaktoro), Air Cooler, Energy Stream Mixer
- New "Don't Calculate" Phase Equilibria Option
- Import Compounds from thermo/chemicals python packages
- New Quick Create Solid Compound tool
- New Flowsheet Weather component can provide ambient temperature, pressure, wind speed, 
  humidity and solar insulation data for Unit Operations
- Allow user to override flowsheet icons for Component/Compound and Solids Separator
- Pipe Segment UO can now use Flowsheet-provided weather information
- Bug fixes

Version 7.5.7

- Fixed a bug in PFR
- Fixed a bug in Rigorous Column
- Fixed an issue with LLE Diagram

Version 7.5.6

- Implemented #274: Number of Tubes in Tubular Reactors for Correct Pressure Drop Calculation
- Alternate calculation method for Gibbs Reactor
- Fixed #318: Error opening old file in 7.5.5
- Fixed #319: Error loading Compound Creator study
- Fixed #322: Suspicious branch in Valve.vb
- Throw errors (if any) when loading simulations in automation mode
- Fixed issue with Rigorous Column editor
- Fixed ideal gas Cp estimation
- Python Automation fixes

Version 7.5.5

- Dynamic models for Conversion, Equilibrium and Gibbs Reactors (beta)
- New API to run flowsheets in Dynamic Mode
- New calculation routine for Adiabatic Mode in Gibbs Reactor
- Added an option to choose the flow coefficient type in valves (Kv or Cv)
- PFD drawing optimizations
- Phase equilibria calculation enhancements
- Fixed issue with file saving
- Fixed issue with Distillation Column
- Fixed issues with Inspector Reports

Version 7.5.1

- Export Flowsheet drawing to PDF/SVG
- Redesigned search compound experience in Classic UI
- Fixed an issue with liquid phase stability test
- Fixed an issue with the Adjust Control Panel (Classic UI)
- Fixed Dynamic Separator sample
- Fixed crash when trying to load initial estimates for column when there's none (CPUI)
- Updated CPUI editors for Spreadsheet/Flowsheet UO
- Fixed Excel File loading in Files Manager

Version 7.5.0

- Energy Streams are now optional
- New Mass and Energy Balance Summary tool (Classic UI)
- Better Simulate 365 Dashboard integration
- Use Embedded Files in Flowsheet and Spreadsheet Unit Operations (Classic UI)
- Workaround for PH Flash temperature interpolation
- Enhanced Nested Loops VLE/VLLE Flash reliability
- Fixed issue with CoolProp Incompressible Mixtures PP
- Other fixes and enhancements

Version 7.4.0

- Auto-Connect Added Objects
- Fixed issue with Gibbs Reactor
- Fixed Conversion Reactor heat balance
- Fixed issue with temperature calculation in PH Flash
- Other fixes and enhancements

Version 7.3.3

- Fixed issue with Distillation Curves characterization (Cross-Platform UI)
- Set Rigorous Distillation Column pressure profile with top pressure and overall pressure drop
- Fixed issue with setting of compound flows in material streams
- Updated flowsheet object numbering
- Fixed Heat Exchanger calc outlet temperature mode
- Fixed Pipe Segment not being added in Cross-Platform UI

Version 7.3.2

- Fixed Conversion Reactor mass balance
- Reverted regression with overriding object color
- Fixed displaying of some Heat Exchanger properties
- CoolProp Incompressible Mixtures - throw error when Psat = 0
- Fixed object description in floating tables

Version 7.3.1

- Fixed a regression with displaying of error messages (issue 281)
- Fixed a regression with saving of simulations with PR78 Property Package

Version 7.3.0

- New Button Flowsheet Object (Classic UI)
- Disable/Enable Inline Display of Stream Properties
- Fixed Rigorous Column drawing
- Fixed CPUI's Welcome Screen in Dark Mode
- Display filename and line number in error messages
- Fixed PR78 Property Package
- Fixed issue #272: Input changes not passed to Subflowsheet
- Fixed issue #273: Change in temperature not a property in cooler

Version 7.2.0

- HTML Text Flowsheet Object (Classic UI)
- Display Energy Stream values in Flowsheet
- Display variables in plain/html text blocks in the flowsheet
- Fixed issue #265 - subflowsheet initialization
- Fixed kinetic/hetcat reaction conversion factors
- Fixed Python issue on Linux
- Added support for Python 3.10 (#246)
- Fixed issue with Conversion Reactor
- Fixed issue with automation and loading of System.Buffers
- Fixed issue with diffusion coefficients
- Fixed issue #263 - PR interaction parameters
- Fixed issues with Dynamic mode
- Fixed issues with CoolProp Property Packages
- Fixed XML serialization issue
- Fixed issue with UNIQUAC model
- Fixed issue with Single Compound Flash
- Other fixes and enhancements

Version 7.1.2

- Simultaneous Correction solver for Absorption/Extraction Column
- Set Flowsheet Fonts for each Style
- Fixed issue with GERG-2008 Property Package
- Fixed an issue with Conversion Reactor
- Fixed an error in density/compressibility calculation (EOS)
- Fixed custom ordering of Master Property Table objects

Version 7.1.1

- Fixed initialization issue
- Flat color theme by default

Version 7.1.0

- Using mixing rule for rackett liquid density (#232)
- Implemented COSTALD method for liquid density calculations (#231)
- Implemented Total Condenser subcooling in Rigorous Column model
- Fixed Newton-Raphson Rigorous Column solver
- Define global stage efficiency in Rigorous Column model
- Black-and-white flowsheet color theme
- New splash screen
- Globally set size of flowsheet object labels
- Splitter editor fixes (#233)

Version 7.0.1

Author              Description
Daniel Wagner     Fixed bugs in rigorous column editor
Daniel Wagner     Fixed issue 229: Bug in conversion reactor
Daniel Wagner     Worksheets now have 65536 rows
Daniel Wagner     Fixed issue 234: Adjuster error between target and manipulated value
Daniel Wagner     Fixed issue 234: zero-flow errors in some unit operations
Daniel Wagner     Fixed issue 230: bug in compound viewer
Daniel Wagner     Other minor fixes and enhancements

Version 7.0.0

Author              Description
CGC GmbH            Integration with Simulate 365 account + File Picker (Classic UI)
Gregor Reichert     Output Energy Stream - Conversion Reactor 
Daniel Wagner     Added CPUI editor for Property Package Equilibrium settings
Daniel Wagner     Added CPUI Editor for Gibbs Reactor's Element Matrix
Daniel Wagner     Fixed negative experimental thermal conductivity in some compounds
Daniel Wagner     Fixed issue with LK reduced volume calculation
Daniel Wagner     Fixed issue with PR78 model
Gregor Reichert     Fixed Sensitivity Analysis
Daniel Wagner     Fixed a NL PH Flash issue
Daniel Wagner     Fixed Flowsheet Optimizer layout (Classic UI)
Daniel Wagner     Fixed an issue with cubic equation root finding
Daniel Wagner     Other minor fixes and enhancements

Version 6.7.1

Author              Description
Daniel Wagner     Exposing Activity/Fugacity/Diffusion coefficients from compounds in Material Streams
Daniel Wagner     Added Ideal Gas Heat Capacity as a Phase property
Daniel Wagner     Fixed issue 213: Mac OS - Binary Envelope failed
Daniel Wagner     Fixed issue 214: Mac OS - Objects Alignment button get crash
Daniel Wagner     Fixed issue 216: Carbon Combustion sample cannot be calculated
Daniel Wagner     Fixed issue with saturation condition calculations
Daniel Wagner     Fixed duplicate name when adding objects to the flowsheet

Version 6.7.0

Author              Description
Daniel Wagner     IronPython Interactive Console (Classic UI)
Daniel Wagner     Live View for Dynamic Integrator (Classic UI)
Daniel Wagner     Fixed issues with Nested Loops Flash
Daniel Wagner     Fixed Equilibrium Reactor model
Daniel Wagner     Fixed issues with Pipe Hydraulic Profile editor (Classic UI)
Daniel Wagner     Fixed Classic UI HiDPI layout
Daniel Wagner     Fixed issue 207: Mix liquid and liquid1 properties different even for single liquid
Daniel Wagner     Fixed issue 199: Minor issue with Splitter object

Version 6.6.3

Author              Description
Daniel Wagner     Fixed Equilibrium Reactor Adiabatic calculation mode
Daniel Wagner     Fixed some Classic UI issues
Daniel Wagner     Fixed issues with Pipe Segment editor (Classic UI)
Daniel Wagner     Fixed an issue with Material Stream flow specification (#198)
Daniel Wagner     Fixed issues with Petroleum Characterization utilities
Daniel Wagner     Fixed Splitter balance check

Version 6.6.2

Author              Description
Daniel Wagner     DWSIM now uses the System Font in Classic UI
Daniel Wagner     Enhanced stability of Equilibrium Reactor calculation in Adiabatic mode
Daniel Wagner     Fixed a regression in Pipe Segment UO
Daniel Wagner     Fixed Classic UI layout in High-DPI displays
Daniel Wagner     Input Boxes can now be used in Steady-State mode

Version 6.6.1

Author              Description
Daniel Wagner     Updated User's guide
Daniel Wagner     Updated tooltips for Script Editor in Classic UI
Daniel Wagner     Fixed issues with File Provider
Daniel Wagner     Fixed issues with CoolProp Property Package
Daniel Wagner     Improved Pipe Segment UO calculation stability
Daniel Wagner     Steady-State Controller Control Panel is now dockable (Classic UI)
Daniel Wagner     Added energy stream calculation mode to cooler
Daniel Wagner     Added Diameter as an editable property for PFR
Daniel Wagner     Added Flowsheet.ClearLog() function
Daniel Wagner     Implemented Temperature Approach for Equilibrium reactions
Gregor Reichert     Fixed an issue with SVE calculation
Gregor Reichert     Fixed an issue with Solids Separator UO
Daniel Wagner     Other minor fixes and enhancements

Fixed issues from GitHub issue tracker (https://github.com/DanWBR/dwsim6/issues/):

Daniel Wagner     [Enhancement] Format Number in the Reaction Interface Support Engineering Notation #185
Daniel Wagner     [Bug] Script Manager: Click on API Help Button Opens Explorer with Page Not found #178
Daniel Wagner     [Bug] Copy Data to Clipboard in Material Streams Interface #176
Daniel Wagner     [Bug] Kinetic with Python Script the DO RO Shall be Hidden #175
Daniel Wagner     [Bug] Clicking on Open Python Script Editor Several Times Create Several Script Windows #174
Daniel Wagner     [Bug] A Product as a Base Component #172
Daniel Wagner     [Enhancement] Use Rate instead of Velocity in Reaction Interface #171
Daniel Wagner     [Bug] Basis Component in Reaction Interface #170
Daniel Wagner     [Bug] DWSIM Calculate conversions for Products #169

Version 6.6.0

Author              Description
Daniel Wagner     New 'Files' feature enables adding external files to the flowsheet
Daniel Wagner     Updated User Guide
Daniel Wagner     Added Inspector Report for Gibbs/Equilibrium Reactors
Daniel Wagner     Fixed Python Script Import/Export (Classic UI)
Daniel Wagner     Fixed async script execution

Version 6.5.6

Author              Description
Daniel Wagner     Fixed an issue with Compressor/Expander calculation
Daniel Wagner     Fixed an issue with Steam Tables Property Package
Daniel Wagner     Fixed an issue with the Pipe Segment properties editor (Cross-Platform UI)

Version 6.5.5

Author              Description
Daniel Wagner     Python.NET Runtime now works with any Python 3.x distro
Daniel Wagner     Fixed issues with Rigorous Column solvers
Daniel Wagner     Pipe Segment UO's Hydraulic Profile editor now allows pasting from clipboard (Classic UI)
Daniel Wagner     Fixed error on duplicate external components
Daniel Wagner     Fixed an issue with Conversion Reactor UO
Daniel Wagner     Custom Actions for Flowsheet Object's Double Click event
Daniel Wagner     Other minor fixes and enhancements

Version 6.5.4

Author              Description
Daniel Wagner     New Steam Tables (v2) Property Package supporting regions 4 and 5
Daniel/Gregor       Rewritten Flash Algorithm for single compounds
Daniel Wagner     New Interface for External Solvers
Daniel Wagner     Import/Export Scripts in Script Manager (Classic UI)
Daniel Wagner     Python.NET defaults to Python 3.9 on Windows OS
Daniel Wagner     Fixed an issue with the Python Script runner
Daniel Wagner     Fixed an issue with the Compound Creator
Daniel Wagner     Fixed an issue with text-to-number conversion in some regions
Daniel Wagner     Fixed Heat Exchanger Shell-and-Tube editor fouling units
Daniel Wagner     Fixed issues with UNIQUAC model and UNIQUAC/NRTL parameter estimation
Daniel Wagner     Fixed NRTL/UNIQUAC parameter estimation in Data Regression utility
Daniel Wagner     Do not display dynamic properties in steady-state mode
Daniel Wagner     Added Pressure Drops to listed Heat Exchanger properties
Daniel Wagner     Fixed an issue with Pipe Segment's property retrieval
Daniel Wagner     Fixed issue #155: Pressure-Enthalpy Flash with CAPE-OPEN Property Package fail for some Operation Units
Anders Andreasen    Fixed issue #156: Bug in calculation of internal energy and Helmholtz energy
Daniel Wagner     Fixed issue #157: Multivariable Optimization and Sensitivity Analysis cannot use Spreadsheet variables

Version 6.5.3

Author              Description
Daniel Wagner     Fixed crashes when opening the General Settings window (Classic UI)
Daniel Wagner     Added a Numerical Method setting to Equilibrium Calculations
Daniel Wagner     Python Script UO CAPE-OPEN Wrapper now supports Python.NET
Daniel Wagner     Fixed issues with Compound Creator (Classic UI)
Daniel Wagner     Other minor fixes and enhancements

Version 6.5.2

Author              Description
Daniel Wagner     Fixed issues with Python.NET Interpreter, which now defaults to Python 3.8
Daniel Wagner     Fixed issues with Excel Unit Operation
Daniel Wagner     Other bug fixes

Version 6.5.1

Author              Description
Daniel Wagner     New Feature: Add Chart Objects from Charts collection to the Flowsheet
Daniel Wagner     New Feature: Export compound data to Excel file
Daniel Wagner     Updated IronPython+Libs to v2.7.11
Daniel Wagner     Fixed issue 146: Dynamics Manager remove button not working (Cross-Platform UI)
Daniel Wagner     Fixed issue 147: Inspector could not be started (Linux)
Daniel Wagner     Fixed issue 151: Wrong units for formation terms of electrolytes
Daniel Wagner     Fixed issue 152: Error on viewing sub flowsheet
Daniel Wagner     Added an option to define solids handling in the Simulation Wizard (Classic UI)
Daniel Wagner     Fixed issue with Rigorous Column
Daniel Wagner     Fixed solver freezing in certain situations
Daniel Wagner     Fixed a bug with the SRK Adv Property Package

Version 6.5.0

Author              Description
Daniel Wagner     Feature Request 134: Force Material Stream Phase (Vapor, Liquid or Solid)
Daniel Wagner     Feature Request 57: Edit properties of generated petroleum fractions (Classic UI)
Daniel Wagner     Feature Request 63: Display only streams allowable to connect in connections panel (Cross-Platform UI)
Daniel Wagner     Feature Request 65: Insert standard pipe sizes in Pipe Hydraulic Profile editor
Daniel Wagner     Fixed Classic UI unresponsiveness
Daniel Wagner     Fixed issues with Compound Creator (Classic UI)
Daniel Wagner     Fixed loading of external PPs/UOs
Anders Andreasen    Fixed single compound issue with Bulk C7+ characterization utility (Classic UI)
Daniel Wagner     Other bug fixes and enhancements

Version 6.4.9

Author              Description
Daniel Wagner     Implemented feature request #126: Dynamics Integrator pause and resume (Classic UI)
Daniel Wagner     New Separator Filler Utility (Dynamics)
Daniel Wagner     Fixed issue #138: Flowsheet can't be saved
Daniel Wagner     Fixed issue #137: Estimate Pipe Overall Heat Transfer Coefficient
Daniel Wagner     Fixed issue #130: Stream Enthalpy not being correctly set by CAPE-OPEN Property Packages
Daniel Wagner     Fixed issues with Seawater Property Package 
Daniel Wagner     Fixed drawing of some PFD Objects
Daniel Wagner     Fixed Switch logical block behavior
Daniel Wagner     Fixed active/inactive buttons not updating status in object editors (Classic UI)
Daniel Wagner     Fixed Integrator results sheet formatting
Daniel Wagner     Dynamics Volume-Temperature (VT) Flash fixes
Daniel Wagner     Fixed distance units handling (Classic UI)
Daniel Wagner     Fixed Liquid-Liquid Extraction column not working

Version 6.4.8

Author              Description
Daniel Wagner     Updated User's Guide
Daniel Wagner     Added Model Customization samples
Daniel Wagner     Enhanced reliability of the Rigorous Column model solvers
Daniel Wagner     Fixed issue 129 - CAPE-OPEN Thermodynamic Package configure failed
Daniel Wagner     Fixed External Temperature gradient calculation in Pipe Segment model
Daniel Wagner     Added visualization of External Temperature to Pipe Segment model
Daniel Wagner     Fixed infinite loop in Electrolyte Property Packages
Daniel Wagner     Fixed Adjust/Set not being inactive when they should
Daniel Wagner     Fixed a bug with NL VLLE flash algorithm
Daniel Wagner     Other minor fixes and enhancements

Version 6.4.7

Author              Description
Daniel Wagner     New Database with Food Compounds (https://github.com/Spogis/DWSIMFoodProp)
Daniel Wagner     Updated CoolProp libraries to v6.4.1
Daniel Wagner     Grouped all Property Package editors in a single window (Classic UI)
Daniel Wagner     Exposed CSTR's Vapor Residence Time and Headspace
Daniel Wagner     Added an option to import compound data into Compound Creator from JSON files
Daniel Wagner     Fixed issues with some flash algorithms
Daniel Wagner     Fixed issues with Compound Creator
Daniel Wagner     Fixed Gibbs/Equilibrium/Conversion Reactor mixed-phase product enthalpies
Daniel Wagner     Fixed a bug with solids handling
Daniel Wagner     Fixed a bug in Seawater Property Package 
Daniel Wagner     General UI Fixes

Version 6.4.6

Author              Description
Daniel Wagner     Added Wilson Activity Coefficient Property Package
Daniel Wagner     Updated/Redesigned Compound Creator (Classic UI)
Daniel Wagner     Added Compound Creator Wizard to Welcome Screen (Classic UI)
Daniel Wagner     Smoother compound search (Classic UI)
Daniel Wagner     Updated Automation interface
Daniel Wagner     Added PID Controller Setpoint property
Daniel Wagner     Added Dynamic Integrator scripting events
Daniel Wagner     Decreased Auto-Layout distance between objects

Version 6.4.5

Author              Description
Daniel Wagner     Fixed Compressor/Expander power calculation in Polytropic mode
Daniel Wagner     Fixed Gibbs Reactor in Adiabatic mode
Daniel Wagner     Fixed issue with VLLE Flash
Daniel Wagner     Steady-State Adjust/Controller enhancements
Daniel Wagner     Fixed UNIFAC-LL (issue 118)
Daniel Wagner     Fixed Condenser/Reboiler pressure editing (Cross-Platform UI)
Daniel Wagner     Fixed editors not updating after flowsheet solve (Cross-Platform UI)
Daniel Wagner     Fixed issue in Advanced Kinetics expression parser
Daniel Wagner     Fixed Solids Separator energy balance warning

Version 6.4.4

Author              Description
Daniel Wagner     Fixed issue 111 (The field is not updated correctly after the method converges)
Daniel Wagner     Fixed issue 117 (Issue with recycling mixed streams)
Daniel Wagner     Fixed issue with VLE PT Flash
Daniel Wagner     Added an option to use IO Flash in VLE/VLLE equilibrium calculations
Daniel Wagner     Fixed an issue with simultaneous adjust solver
Daniel Wagner     Fixed PFR volume iteration
Daniel Wagner     Fixed flowsheet update after finishing calculation
Daniel Wagner     Fixed units for some properties in Excel Add-In
Daniel Wagner     Fixed conversion from kW to MJ/h
Daniel Wagner     Fixed conversion of BTU/h and MMBTU/h
Daniel Wagner     Fixed issues with Heat Exchanger Shell and Tube calculation mode
Daniel Wagner     Darker green color in digital gauge

Version 6.4.3

Author              Description
Daniel Wagner     Improved stability and reliability of the Distillation/Absorption Column model     
Daniel/Gregor       Improved stability and reliability of equilibrium calculations
Daniel Wagner     Fixed Heat Exchanger Pinch Point calculation mode
Daniel Wagner     Fixed a bug in PFR particle diameter conversion
Anders Andreasen    Partial fix for Reid Vapor Pressure calculation
Alexander Semenyak  Fixed array access in PR78 Property Package
Alexander Semenyak  Fixed comma separator in non-dot decimal separator cultures
Jarl Pedersen       Fixed Globalization issue for parsing Pipes.dat file
Jarl Pedersen       Fixed a bug with Flowsheet Optimization tool (CUI)
Daniel Wagner     Fixed CoolProp compound aliases     
Daniel Wagner     Fixed bugs in GERG2008 Property Package
Daniel Wagner     Refactored PRLK Property Package

Version 6.4.2

Author              Description
Daniel Wagner     Code optimization and calculation speed improvements     
Daniel Wagner     Fixed Heat Exchanger Pinch Point calculation mode
Daniel Wagner     Fixed a bug in Automation Library
Daniel Wagner     Fixed a regression in Property Package cloning
Daniel Wagner     Fixed a bug with Rigorous Column spec unit conversion
Daniel Wagner     Fixed a bug with PFR
Gregor Reichert     Fixed VLLE PS Flash

Version 6.4.1

Author              Description
Daniel Wagner     Fixed issues with Equilibrium and Gibbs Reactors
Daniel Wagner     Fixed issues with Expander, Compressor and Heat Exchanger Models
Daniel Wagner     Fixed memory leaks in Automation mode
Daniel Wagner     Fixed an issue with forced solids
Daniel Wagner     Fixed issues in some CAPE-OPEN interface functions
Daniel Wagner     Fixed Spreadsheet Unit Operation heat balance
Daniel Wagner     Reduced saved file loading time
Gregor Reichert     Fixed solids handling in some equilibrium calculations
John Konecny        Fixed/Refactored Chemeo data importing (issue #97)

Version 6.4.0

Author              Description
Daniel Wagner     Fixed stability and reliability issues with equilibrium calculations
Gregor Reichert     Fixed bugs in Simple LLE and Nested Loops VLLE Flashes
Daniel Wagner     Optimized Automation2 flowsheet loading time
Anders Andreasen    Implemented ANSI/IEC Valve sizing for Steady State with Masoneilan two-phase equation
Daniel Wagner     Fixed some bugs in Heat Exchanger and Pump Unit Ops
Anders Andreasen    Reworked Valve Unit Op to fully IEC 60534 compliant for turbulent flow both choked and no-choked conditions
Anders Andreasen    Updated Petukhov heat transfer coefficient correlation to a version with better capabilities in transition flow (ref. Cengel)
Daniel Wagner     Added outlet Vapor Fraction calculation mode to Heat Exchanger
Danie/Anders        Fixed a bug in Orifice Plate calculation
Alexander Semenyak  Fixed array access in PengRobinson78
Daniel Wagner     Fixed a bug with LLE diagram (Classic UI)
Anders Andreasen    Fixed a bug with Lockhart-Martinelli pressure drop correlation
Anders Andreasen    Bug fix unit conversion SI/Emeprial in Beggs and Brill two-phase friction factor
Anders Andreasen    Fixed a problem with equvalent length from fitting being interger instead of double
Anders Andreasen    Moving pipe fittings into temperature loop to allow pipe flow correlation to be used for dP calc
Anders Andreasen    Changed mix liquid mixing rules for viscosity and thermal conductivity
Anders Andreasen    Added emulsion model, and enabling different viscosity and thermal conductivity for different liquid phase equilibrium reaction basis units
Daniel Wagner     Fixed a bug in PFR model
Daniel Wagner     Fixed conversion of molar concentration PFR/CSTR
Anders Andreasen    Removed JT effect in Pipe Unit Op, and replaced with Emulsion check
Daniel Wagner     Added Reboiled/Refluxed Absorber modes for Rigorous Distillation Column Unit Op
Daniel Wagner     Fixed flowsheet solver stop request
Daniel Wagner     Display currently calculating objects in green color
Daniel Wagner     Fixed conversion from mm to m
Anders Andreasen    Small bug fix in Pc Lee-Kessler, added a Riazi method for Pc/Tc
Anders Andreasen    Changed C7+ Petroleum characterisation to handle a single component
Daniel Wagner     Fixed issue 89: stream report error
Daniel Wagner     Fixed issue 86: SLE-Flash phase composition not 100%
Daniel Wagner     Fixed issue 83: Binary Envelope - Flash algo not suitable for LLE
Daniel Wagner     Fixed issue 82: PVF and TVF flash - no second liquid phase
Daniel Wagner     Fixed issue 64: Rugosity and thermal conductivity not displayed for default materials
Daniel Wagner     Fixed issue 55: Redundant Kij for Water and CO2 
Daniel Wagner     Fixed issue 89: stream report error
Daniel Wagner     Other bug fixes and enhancements

Version 6.3 Update 7

- [CHG] Dynamic Integrator interval can now be set in milisseconds
- [FIX] Bug fixes

Version 6.3 Update 6

- [FIX] Bug fixes

Version 6.3 Update 5

- [CHG] Optimized script execution in advanced kinetic reactors
- [FIX] Fixed convergence issues with Liquid-Liquid Extractor
- [FIX] Fixed convergence issues with Gibbs Reactor
- [FIX] Fixed issues with equilibrium calculations
- [FIX] Layout and other fixes

Version 6.3 Update 4

- [NEW] Advanced Python Script Kinetics
- [CHG] PR/SRK calculation speed improvements
- [CHG] Rigorous Column solver improvements
- [FIX] Thermo subsystem fixes
- [FIX] Bug fixes

Version 6.3 Update 3

- [CHG] Enhanced Automation functionality
- [FIX] Fixed issues with phase equilibria calculation in some specific cases
- [FIX] Other fixes and enhancements

Version 6.3 Update 2

- [CHG] Rigorous Column solver reliability and speed enhancements
- [FIX] Bug fixes

Version 6.3 Update 1

- [NEW] Rigorous Column BP Solver now supports all Condenser/Reboiler specifications
- [FIX] Bug fixes and enhancements

Version 6.3

- [NEW] New Thermodynamics subsystem with Univeral Flash Algorithm used by default
- [FIX] Bug fixes and enhancements

Version 6.2 Update 4

- [NEW] Added an option to disable skipping of equilibrium calculation on some 
		Material Streams (feature introduced in Update 2)
- [CHG] Usage of IPOPT solver in Equilibrium Reactor is optional
- [FIX] Minor bug fixes

Version 6.2 Update 3

- [NEW] Added an editor for custom properties (Classic UI)
- [CHG] Updated Equilibrium Reactor Solver
- [FIX] General bug fixes and enhancements

Version 6.2 Update 2

- [CHG] Enhanced reliability of the Flash Algorithms
- [CHG] Optimized calculation speeds by removing unnecessary Material Stream calculations
- [FIX] General bug fixes and enhancements

Version 6.2 Update 1

- [NEW] Multiphase (SVLLE) Gibbs Minimization Flash Algorithm
- [NEW] Added more units for mass, standard condition volumetric and power/heat flow
- [NEW] Added a new plugin for Heat of Combustion calculation
- [FIX] Bug fixes

Version 6.2

- [NEW] New compounds: Sulfuric Acid, Solid Carbon, Mercury, Casein, Lactose, 
        Methyl Oleate, Myristic Acid, Triolein
- [NEW] GERG-2008 Property Package
- [NEW] PC-SAFT (with Association Support) Property Package
- [NEW] Peng-Robinson (1978) Property Package
- [NEW] Temperature-dependent Interaction Parameter PR78/SRK Property Packages
- [FIX] Fixed Dynamic Mode error messages
- [FIX] Fixed an issue with Gibbs Reactor's element matrix editor
- [FIX] Fixed flowsheet object appearance editor (Classic UI)
- [FIX] Minor bug fixes

Version 6.1 Update 10

- [CHG] Changed minimum required .NET Framework version to v4.6.2 on Windows
- [FIX] Fixed Unit Operations energy balance check
- [FIX] Fixed PFR energy balance
- [FIX] Fixed a regression in Nested Loops VLLE Flash
- [NEW] Added an option to open editors with a double-click (Classic UI)

Version 6.1 Update 8/9

- [FIX] Fixed a regression with Nested Loops VLE Flash Algorithm

Version 6.1 Update 7

- [CHG] Enhanced reliability of Gibbs Reactor solver
- [FIX] Fixed PR/SRK Interaction Parameters missing on first run
- [FIX] Fixed CSTR convergence in some cases
- [FIX] Fixed an issue with the Element Matrix editor on Gibbs Reactor
- [FIX] Fixed reaction editors windows being modal (Classic UI)
- [FIX] Other minor bug fixes

Version 6.1 Update 6

- [NEW] Added an option to initialize Gibbs Reactor solver from a previous valid solution
- [FIX] Mono/Linux bug fixes
- [FIX] Fixed Sensitivity Analysis window on Cross-Platform UI

Version 6.1 Update 5

- [NEW] Added Mean Ionic Activity Coefficient property
- [CHG] Updated valve P2 calculation in kv gas mode
- [CHG] Added valve equations for supercritical expansion
- [FIX] Fixed issues 4, 20, 21, 22, 23, 24, 25, 27, 29, 30 and 31 (https://github.com/DanWBR/dwsim6/issues?q=is%3Aissue+is%3Aclosed)
- [FIX] Rewritten Gibbs reactor solver
- [FIX] Bug fix CAPE-OPEN UO connection update
- [FIX] Fixed null property error when displaying on Property Table
- [FIX] Fixed handling of partial liquefaction
- [FIX] Other minor bug fixes

Version 6.1 Update 4

- [CHG] Updated Python.NET library on Windows to match Python 3.7 runtime
- [FIX] Fixed a bug with master property table
- [FIX] Fixed Excel Add-In
- [FIX] Fixed issues 15, 16, 17 and 19 (https://github.com/DanWBR/dwsim6/issues)

Version 6.1 Update 3

- [NEW] Added an option to force reset of all objects before running a Schedule (Dynamics)
- [NEW] Added an option to close all editors (Cross-Platform UI)
- [CHG] Updated Gibbs/Equilibrium Reactor solvers
- [FIX] Fixed PID Controller not associating with Linked scripts
- [FIX] Other bug fixes

Version 6.1 Update 2

- [NEW] Added Helper Functions to Flowsheet (Scripting)
- [CHG] Updated Spreadsheet property importing/exporting to include units
- [FIX] Bug fixes

Version 6.1 Update 1

- [CHG] Enhanced spreadsheet performance (Cross-Platform UI)
- [FIX] Fixed a bug with the calculation of henry's constant
- [FIX] Fixes for High-DPI displays (Classic UI)
- [FIX] Fixed an issue on saving simulations with Master Property Tables
- [FIX] Other bug fixes

Version 6.1

- [NEW] Added support for the new Neural Network Unit Operation
- [NEW] Flowsheet Auto-Layout function
- [FIX] Bug fixes and enhancements

Version 6.0 Update 7

- [FIX] Bug fixes and adjustments

Version 6.0 Update 6

- [FIX] Fixed a bug in Dynamic Mode
- [FIX] Other fixes and enhancements

Version 6.0 Update 5

- [NEW] Added new Automation interface (Automation2), works on Linux and macOS
- [FIX] Fixed Energy Stream editor (Classic UI)
- [FIX] Fixed a bug with Black Oil Property Package
- [FIX] Fixed Compound Creator's Liquid Cp regression (Classic UI)
- [FIX] Fixed PFD definition on macOS when using CPU Renderer
- [FIX] Other fixes and enhancements

Version 6.0 Update 4

- [FIX] Fixed default units for Cp, Cv and entropy (Excel Add-In)
- [FIX] Fixed Material Stream editor not showing the Dynamic Spec tab (CPUI)
- [FIX] Fixed/Updated some dynamic models

Version 6.0 Update 3

- [NEW] Dynamic Models for CSTR and PFR
- [NEW] Added Search Box to Cross-Platform UI PFD
- [FIX] Bug fixes and enhancements

Version 6.0 Update 2

- [NEW] Dynamic Model for Heat Exchanger
- [NEW] PID Controller Wind-Up Guard
- [NEW] Dynamic Integrator flowsheet charts for Monitored Variables
- [FIX] Bug fixes and enhancements

Version 6.0 Update 1

- [NEW] Added a Real-Time Step Duration property to Dynamic Integrator
- [FIX] Fixed PID Controller Manual Mode
- [FIX] Fixed Material Stream editor issues when in Dynamic Mode
- [FIX] Other fixes

Version 6.0

- [NEW] Dynamic Simulation Mode
- [NEW] New Object Types: PID Controller, Analog/Digital/Level Gauges, Input Box, Switch
- [NEW] New PID Controller Tuning tool
- [NEW] Store/Restore Flowsheet Solutions
- [FIX] Various bug fixes and enhancements

Version 5.8 Update 11

- [FIX] Fixed Compressor/Expander Head calculations
- [FIX] Fixed Compressor/Expander not being added to the flowsheet (Cross-Platform UI)
- [FIX] Fixed display of first and last stage pressures of Absorption Columns

Version 5.8 Update 10

- [NEW] Update compound data from JSON file while importing
- [FIX] Updated SVLLE Flash Algorithm to handle forced solids
- [FIX] Fixes for High DPI displaying (Classic UI)
- [FIX] Raoult's Law/Activity Coeff. Property Packages: added an option to calculate Solid Phase Enthalpy with Solid Cp
- [FIX] General bug fixes and minor enhancements

Version 5.8 Update 9

- [FIX] General bug fixes and minor enhancements

Version 5.8 Update 8

- [NEW] Added a Pervaporation sample
- [NEW] Added support for external Unit Operations
- [NEW] Added the ability to load and use images as flowsheet object icons for Python Script UOs
- [FIX] Fixed some automation bugs
- [FIX] Fixed Pipe insulation calculation
- [FIX] Bug fixes

Version 5.8 Update 7

- [NEW] Added Performance Curves and Polytropic Process support to Compressor and Expander
- [FIX] Fixed single compound phase detection
- [FIX] Other bug fixes

Version 5.8 Update 6

- [NEW] Define the number of grouping rows in Master Property Table
- [FIX] Fixed loading and saving of simulation files in mobile version format (Android/iOS)
- [FIX] Fixed Absorber Column in Liquid-Liquid Extraction mode
- [FIX] Fixed the Filter property editor in Cross-Platform UI
- [FIX] Fixed Filter cake resistance unit conversion
- [FIX] Fixed the Script Editor not working on Classic UI
- [FIX] Other bug fixes

Version 5.8 Update 5

- [NEW] Integrated Solution Inspector with object editors
- [CHG] Enhanced compound selection panel in simulation setup wizard (Classic UI)
- [FIX] Fixed a bug with Master Tables
- [FIX] Fixed Excel Add-In Settings panel not loading correctly
- [FIX] Other bug fixes

Version 5.8 Update 4

- [NEW] Added a Tag property to compounds for custom ordering
- [NEW] Added Compound ordering capability to Cross-Platform UI (CPUI)
- [NEW] Enhanced object connections editor (CPUI)
- [FIX] Fixed Spreadsheet Copy/Paste (CPUI)
- [FIX] Fixed Compressor/Expander calculation mode selection (CPUI)

Version 5.8 Update 3

- [FIX] Fixed Valve Kv calculation mode for Gas/Steam
- [FIX] Fixed loading of properties for some Unit Operations
- [FIX] Other minor bug fixes

Version 5.8 Update 2

- [NEW] Added a new VLLE Flash Algorithm tweak to check for alternative liquid phase compositions on initial VLE PT Flash
- [FIX] Fixed Rigorous Column automatic initialization
- [FIX] Fixed number of trial compositions not changing on Flash Algorithm editor

Version 5.8 Update 1

- [CHG] Changed target .NET Framework version to 4.6.1 on Windows
- [CHG] Reduced the size of the User Guide PDF file
- [FIX] Fixed Automation feature
- [FIX] Fixed some issues with ReoGrid formula parsing
- [FIX] Enhanced reliability of liquid phase instability detection

Version 5.8

- [NEW] Enhanced Spreadsheet Tool
- [NEW] 2D Charts Tool
- [NEW] Enhanced Script Editor (Cross-Platform UI)
- [NEW] Dynamic Script Snippets
- [CHG] Redesigned Cross-Platform UI
- [CHG] Redesigned Property Package editors
- [FIX] Fixed some unit conversions for reaction basis
- [FIX] Fixed text to number conversions
- [FIX] Fixed flowsheet dragging while zoomed
- [FIX] Fixed PFR heat balance
- [FIX] Fixed liquid phase viscosity calculation for supercritical compounds
- [FIX] Fixed Splitter ratios calculation mode
- [FIX] Workaround for math expression parsing errors

Version 5.7 Update 14

- [CHG] Added 'This' to python vars (Python Script UO)
- [FIX] Fixed flowsheet dragging while zoomed
- [FIX] Fixed exception when launching the Script Editor (Classic UI)
- [FIX] Fixed Energy Stream connection editors for Python Script UO (Classic UI)

Version 5.7 Update 13

- [NEW] Insert IronPython Script Snippets (Classic UI)
- [CHG] Restored Reference Object functionality to Controller/Adjust Logical Block
- [CHG] Restored Maximum Iterations editor to Recycle Logical Block
- [CHG] Optimized Isothermal Compressibility calculation
- [FIX] Fixed layout of object editors (Classic UI)
- [FIX] Fixed Thermo library registration
- [FIX] Fixed Molar Volume value in Material Stream report
- [FIX] Minor bug fixes

Version 5.7 Update 12

- [NEW] Added the ability to work with reaction parameters in optimization and sensitivity analysis tools (Classic UI)
- [NEW] Added the ability to load external Property Packages
- [NEW] Added solid fugacity config options to Property Packages
- [FIX] Fixed sorting of optimizer variable properties (Classic UI)
- [FIX] Fixed textbox text color on Property Package config editor (Cross-Platform UI)

Version 5.7 Update 11

- [FIX] Fixed PR/SRK volume translation correction for C1-C10, H2S, N2 and CO2
- [FIX] Restored Eto packages to v2.4.1 for Linux compatibility
- [FIX] Fixed splitter split ratio trackbar precision / added editable textboxes (Classic UI)
- [FIX] Fixed Flowsheet.AddObject() not working in automation mode

Version 5.7 Update 10

- [NEW] Exposing CAPE-OPEN object on CAPE-OPEN Unit Operation ('GetCAPEOPENObject()' method)
- [FIX] Fixed compound grid row double click (Classic UI)
- [FIX] Fixed Valve calculation for Liquid Service Kv mode

Version 5.7 Update 9

- [CHG] Changed Welcome Panel button icons (Classic UI)
- [FIX] Fixed ChemSep column mass/energy balance warnings
- [FIX] Restored GUI language selection (Classic UI)
- [FIX] Compound search textbox crash (Classic UI)
- [FIX] eNRTL fix
- [FIX] Electrolyte Flash warning message
- [FIX] Fixed Advanced Property Package Settings window not on top (Classic UI)
- [FIX] Fixed units mismatch on assay manager (Classic UI)
- [FIX] Other bug fixes

Version 5.7 Update 8

- [NEW] Highlight phase areas in Binary Envelope (Cross-Platform UI)
- [NEW] Context menu for charts (Cross-Platform UI)
- [NEW] Split Material/Energy Streams and Insert Recycles (Classic UI)
- [NEW] Order compound lists (Classic UI)
- [NEW] Exposing petroleum cold flow properties from utility (Classic UI)
- [FIX] Fixed solver infinite loop detection
- [FIX] Other bug fixes

Version 5.7 Update 7

- [NEW] Enhanced Phase/Binary Envelopes in Cross-Platform UI
- [FIX] Fixed compound adding by pressing ENTER on the search box (Classic UI)
- [FIX] Fixed Phase Identification Parameter in Phase Envelope
- [FIX] Fixed merged ToolStrips in Classic UI
- [FIX] Minor bug fixes and tweaks in Cross-Platform UI

Version 5.7 Update 6

- [NEW] IronPython Script Debugging (Classic UI)
- [CHG] Updated components info in about boxes
- [FIX] Fixed Custom Calculation Order not working (Classic UI)
- [FIX] Fixed Pressure units in Valve's Kv calculation mode
- [FIX] Other minor bug fixes

Version 5.7 Update 5

- [NEW] Added Python standard libraries
- [NEW] Enable/disable custom Touch Bar controls on macOS
- [CHG] Electrolyte flash enhancements
- [FIX] Fixed double conversion from string
- [FIX] Fixed remaining CefSharp processes after shutdown (Classic UI)
- [FIX] Fixed compound issues on Classic UI

Version 5.7 Update 4

- [NEW] New syntax-highlighting script editors on macOS and Linux (New UI)
- [FIX] Fixed editors not updating on Compound and Property Package/Flash Algorithm changes (New UI)
- [FIX] SLE-only Fixed binary envelope (Classic UI)

Version 5.7 Update 3

- [NEW] Spreadsheet cell formatting tools (Classic UI)
- [FIX] Fixed importing of property units to spreadsheet (Classic UI)
- [FIX] Fixed pressure drop on Gibbs and Equilibrium reactors

Version 5.7 Update 2

- [NEW] Structured object result reports (New UI)
- [NEW] User-defined expressions for velocity constants (Kinetic Reactions)
- [CHG] Redesigned reaction editors (New UI)
- [CHG] Redesigned flowsheet analysis tools (New UI)
- [FIX] Fixed PFD delayed connector drawing

Version 5.7 Update 1

- [NEW] Exposing additional reactor properties to tables and analysis tools
- [NEW] Shortcut selection buttons on table editors (Classic UI)
- [FIX] Fixed Electrolyte DB
- [FIX] Fixed pressure drop on Gibbs and Equilibrium reactors

Version 5.7

- [NEW] Electrolyte NRTL model for Sour Gas and Amine systems
- [NEW] Copy Flowsheet as image to clipboard
- [NEW] New Web Panel (Classic UI)
- [CHG] Redesigned Welcome Screen (Classic UI)
- [CHG] Redesigned Compound Selector (Classic UI)
- [CHG] Updated ChEDL Compound Database
- [CHG] Restored 'print flowsheet' function (Classic UI)
- [FIX] Fixed object cloning
- [FIX] Fixed SLE flash
- [FIX] Fixed english dynamic viscosity units
- [FIX] Fixed mirrored characters on flowsheet objects
- [FIX] Bug fixes

Version 5.6 Update 12

- [NEW] Change calculation order (Cross-Platform UI)
- [NEW] User-Defined (Python) Flash Algorithm
- [NEW] User can now override Unit Operation and Material Stream calculation routines using Python Scripts

Version 5.6 Update 11

- [NEW] Added the possibility to change the calculation order on Classic UI (hold Ctrl+Alt before solving the flowsheet)
- [NEW] User can now override K-value Fugacity, Enthalpy and Entropy calculations at Property Package level using Python Scripts
- [FIX] Fixed Side Draw flowrate input (Classic UI)

Version 5.6 Update 10

- [FIX] Fixed white background on Dark Mode (macOS)
- [FIX] Immiscible VLLE Flash fixes, restored missing compound selection

Version 5.6 Update 9

- [NEW] New Excel Add-In 'GetPropUnits' function (Windows only)
- [FIX] Fixed calculation of special properties (Excel Add-In)
- [FIX] Fixed Pipe Segment's Outlet P/T spec mode
- [FIX] Fixed some Classic UI issues with High-DPI displays

Version 5.6 Update 8

- [FIX] Fixed Flowsheet Unit Operation block
- [FIX] Fixed Spreadsheet mass/mole fraction writing

Version 5.6 Update 7

- [FIX] Fixed 'Create and Connect' buttons (Classic UI)
- [FIX] Fixed Heater/Cooler report (New UI)

Version 5.6 Update 6

- [NEW] Select Classic UI Flowsheet Renderer (CPU or OpenGL)
- [CHG] Added ChEDL compound database to Classic UI
- [FIX] Fixed Spreadsheet Table crash
- [FIX] Fixes to some flash algorithms
- [FIX] Other fixes and enhancements

Version 5.6 Update 5

- [NEW] Load FOSSEE Flowsheets and Samples from the File menu (Classic UI)
- [NEW] Customize Systems of Units on Simulation Settings Wizard (Classic UI)
- [NEW] Added ChemSep Column directly from the Object Palette
- [FIX] Other bug fixes and enhancements

Version 5.6 Update 4

- [FIX] Fixed loading of FOSSEE Flowsheets

Version 5.6 Update 3

- [FIX] Fixed Flowsheet Zoom
- [FIX] Fixed Petroleum Characterization

Version 5.6 Update 2

- [FIX] Fixed bugs in Gibbs and Equilibrium reactors
- [FIX] Fixed Oveerhead Vapor connection (Rig Dist Column)
- [FIX] Minor bug fixes and enhancements

Version 5.6 Update 1

- [FIX] Fixed bugs in Gibbs and Equilibrium reactors
- [FIX] Fixed Energy Recycle connection
- [FIX] Fixed some drawing issues
- [FIX] Minor bug fixes and enhancements

Version 5.6

- [NEW] Unified Process Flowsheet Drawing interface
- [CHG] Redesigned Distillation/Absorption Column connection editor
- [FIX] Fixed Steam Tables Enthalpy/Entropy calculation near saturation points
- [FIX] Fixed a regression in Nested Loops VLE Flash
- [FIX] Minor bug fixes and enhancements

Version 5.5 Update 1

- [CHG] Updated ChemSep database
- [FIX] Bug fixes

Version 5.5

- [NEW] Added an option to use legacy property editors
- [NEW] Unit tooltips on property editors
- [NEW] Random property units on floating tables
- [NEW] Added more Flash functions to Excel interface
- [NEW] Restored Command Line funcionality
- [CHG] Redesigned Compound Creator
- [FIX] Fixed PFR bugs
- [FIX] Exposing Script UO variables (CPUI)
- [FIX] Fixed reaction editors
- [FIX] Fixed Excel interface
- [FIX] Other bug fixes and enhancements

Version 5.4 Update 3

- [NEW] New Property Packages for CoolProp Incompressible Fluids & Mixtures
- [FIX] Fixed PFR viscosity calculation
- [FIX] Fixed a bug with Tx flashes

Version 5.4 Update 2

- [FIX] Fixed handling of solids by the Gibbs reactor
- [FIX] Other bug fixes

Version 5.4 Update 1

- [FIX] Bug fixes

Version 5.4

- [NEW] A new Global Flash Algorithm supporting one solid, one vapor and two liquid phases, is now the default one
- [NEW] Updated Inspector Window for Classic UI
- [CHG] Enhanced reliability of the liquid phase stability test, removes the need for stability search key compound selection
- [CHG] Missing NRTL/UNIQUAC Interaction Parameters warning only for non-zero compound amounts
- [FIX] Fixed broken sample simulations

Version 5.3

- [NEW] Support for Dark Mode on macOS Mojave (10.14)
- [NEW] Support for High DPI displays on Linux with configurable Scaling Factor (Cross-Platform UI)
- [NEW] Download and Open FOSSEE Flowsheets directly from DWSIM
- [NEW] Configurable warning for missing interaction parameters on Activity Coefficient-based Models
- [NEW] Cross-Platform UI users can now edit initial estimates for Distillation and Absorption Columns
- [CHG] Now requires Mono v5.14 or newer on Linux, which fixes many Classic UI (Windows Forms) glitches on this platform
- [CHG] Prevented unreliable UNIQUAC interaction parameters from loading
- [CHG] Bypass CAPE-OPEN Unit Operations on macOS and Linux
- [FIX] Minor bug fixes

Version 5.2 Update 22

- [CHG] Improved font rendering on Cross-Platform UI PFD Designer
- [FIX] Fixed stoichiometric coefficient input on reaction editor (Classic UI)

Version 5.2 Update 21

- [FIX] Fixed a regression in Heat Exchanger model

Version 5.2 Update 20

- [FIX] Fixed Absorption Column setup on Cross-Platform UI

Version 5.2 Update 19

- [FIX] Fixed a regression in Steam Tables Property Package

Version 5.2 Update 18

- [NEW] Liquid Viscosity Pressure Correction
- [FIX] Fixed Letsou-Stiel Liquid Viscosity estimation

Version 5.2 Update 17

- [CHG] Liquid Density calculation fixes and enhancements
- [FIX] Minor bug fixes

Version 5.2 Update 16

- [NEW] Exposed Gibbs Reactor Numerical Derivative Perturbation
- [FIX] Bug fixes (PFR, Orifice Plate)

Version 5.2 Update 15

- [CHG] Performance updates
- [FIX] Fixed Pipe Segment UO diameter unit conversion
- [FIX] Do not throw CAPE-OPEN UO 'Edit()' not implemented exception

Version 5.2 Update 14

- [NEW] Added support for OpenCL GPU acceleration on macOS and Linux
- [FIX] Fixed Classic UI Material Stream Editor

Version 5.2 Update 13

- [NEW] Enhancements to input textboxes
- [NEW] Enter math expressions on input textboxes

Version 5.2 Update 12

- [NEW] New Kv calculation mode for Valve model
- [NEW] User-Defined Pipe Wall Material
- [NEW] Unit locking mode for Spreadsheet (Classic UI)
- [FIX] Fixed a bug with Pump calculations
- [FIX] Fixed simulation comments not restoring correctly on Classic UI

Version 5.2 Update 11

- [NEW] Heat Exchanger thermal efficiency calculation mode
- [CHG] Material Stream editor updates (Classic UI)
- [FIX] Fixed Flowsheet Object cloning
- [FIX] Fixed Liquid Phase Enthalpy calculation for some Property Packages

Version 5.2 Update 10

- [FIX] Fixed a bug with the CAPE-OPEN UO Socket
- [FIX] Fixed Adjust Control Panel (Classic UI)
- [FIX] Fixed Update reminder

Version 5.2 Update 9

- [NEW] Added Floating Table for compound amounts in Material Streams
- [NEW] Copy data from chart objects
- [FIX] Fixed bugs with the PFR and Equilibrium Reactors
- [FIX] Fixed Adjust Control Panel (Classic UI)

Version 5.2 Update 8

- [NEW] Solution Inspector enhancements
- [FIX] Fixed loading of mobile-saved simulation files
- [FIX] Fixed a bug with the Equilibrium Reactor
- [FIX] Other bug fixes

Version 5.2 Update 7

- [FIX] Fixed a bug with the Compound Creator Wizard
- [FIX] Fixed a bug with the SVLE Flash Algorithm
- [FIX] CAPE-OPEN fixes
- [FIX] Fixed pipe segment diameter quick input (Classic UI)
- [FIX] Other fixes and enhancements

Version 5.2 Update 6

- [NEW] Added Quality Check to Petroleum Characterization Tools
- [CHG] Enhanced Petroleum Characterization Tools on Cross-Platform UI
- [CHG] Removed Update checkboxes on Classic UI
- [FIX] Fixed missing Expander on Cross-Platform UI
- [FIX] Bug fixes

Version 5.2 Update 5

- [NEW] Data copy from property tables to clipboard (Cross-Platform UI)
- [CHG] Limited the maximum mumber of simultaneous opened object editors
- [FIX] Fixed data copy from property table to clipboard (Classic UI)
- [FIX] Bug fixes

Version 5.2 Update 4

- [FIX] Bug fixes

Version 5.2 Update 3

- [FIX] Fixed a bug with the PFR model
- [FIX] Fixed a bug with the Heat Exchanger model
- [FIX] Fixed a bug with the Compound Separator model
- [FIX] Report tool fixes (Classic UI)
- [FIX] Touch Bar fix for macOS < 10.12.2

Version 5.2 Update 2

- [FIX] Fixed Python.NET issues on Linux and macOS
- [FIX] Fixed flowsheet object removal on Cross-Platform UI
- [FIX] Fixed error when connecting rigorous distillation column reboiler duty on Cross-Platform UI
- [FIX] Fixed a bug with the spreadsheet
- [FIX] Restored backup function on Classic UI
- [FIX] Fixed mouse wheel absent bug on Classic UI

Version 5.2 Update 1

- [CHG] Added more Solution Inspector model descriptions (NL SVLE Flash, Conversion Reactor, Compressor, Cooler, Expander, Heater, Mixer and Pump)
- [FIX] Fixed an issue with the Shell-and-Tube Heat Exchanger model
- [FIX] Fixed Material Stream Vapor Fraction editing on the New UI
- [FIX] Fixed an issue with the Nested Loops SVLE (Eutectic) PH/PS Flashes

Version 5.2

- [NEW] Solution Inspector - understand the models behind the simulation
- [NEW] Cross-Platform UI Enhancements: Drag-and-Drop to add Flowsheet Objects, object property editor Panels and much more
- [NEW] Added support for Touch Bar on MacBook Pro 2016/2017 models
- [NEW] Enhanced support for High DPI displays
- [NEW] Added support for neutral compounds on HetCat Reaction Expressions
- [NEW] Exposed Pipe Segment calculation results to analysis tools
- [NEW] Added support for Classic UI on macOS (work in progress)
- [NEW] Added an option to calculate Phase Enthalpies based on liquid phase Cp values on Activity Coefficient-based PPs
- [CHG] Relocated Flowsheet Solver Controls on the Classic UI
- [CHG] Exposing all flash specs on MS editor (Cross-Platform UI)
- [CHG] Updated the target version of .NET Framework to v4.7.1
- [CHG] Removed dependency of Mono Framework from macOS (OS X) version
- [FIX] Rigorous Column fixes for Liquid-Liquid Extraction
- [FIX] PFR zero-flow fix
- [FIX] Fixed Master Property Table not updating upon loading
- [FIX] Fixed Pipe Segment Quantity
- [FIX] Fixed Flash Algorithm temperature limits
- [FIX] Fixed Equilibrium Reactor calculation restart
- [FIX] Fixed issues with Petroleum Characterization Utilities

Version 5.1 Update 12

- [NEW] Exposed CSTR Outlet Temperature for Analysis tools
- [CHG] Updated method for calculating Cp and Cp/T integrals
- [FIX] Fixed ComboBox width for GTK# (Cross-Platform UI)
- [FIX] Fixed System of Units ComboBox selection not working (Classic UI)
- [FIX] Fixed Raoult's Law PP Entropy calculations
- [FIX] Pipe Profile fixes (Classic UI)

Version 5.1 Update 11

- [CHG] UI enhancements
- [FIX] Fixed CSTR and PFR heat balances
- [FIX] Fixed issues with Petroleum Characterization utilities
- [FIX] Fixed Raoult's Law entropy calculations

Version 5.1 Update 10

- [FIX] Fixed HetCat reaction support on CSTR
- [FIX] Fixed reaction temperature limits not working on Equilibrium and CSTR reactors
- [FIX] Fixed loading of duplicate compounds (Classic UI)

Version 5.1 Update 9

- [NEW] Exposed Pipe Segment hydraulic and thermal properties to analysis tools
- [NEW] Exposed Equilibrium Reactor convergence parameters
- [CHG] Classic UI updates
- [FIX] Fixed bugs with Compound Creator Wizard running on the Classic UI
- [FIX] Fixed a bug with the Gibbs Reactor

Version 5.1 Update 8

- [NEW] Added more information about errors listed in the log window (Classic UI)
- [FIX] Rewritten Conversion Reactor model
- [FIX] Fixed CSTR connections (New UI)
- [FIX] CoolProp fixes

Version 5.1 Update 7

- [NEW] Added support for Raspberry Pi 2/3 (Linux 32-bit ARM Platform)
- [NEW] Added a Natural Gas Water Saturator sample
- [CHG] Heat Capacity Difference workaround for SVLE Flash
- [FIX] Ordered list of cape-open objects in selector
- [FIX] Fixed CAPE-OPEN UO validation/calculation
- [FIX] Fixed a bug with valve sizing
- [FIX] Fixed missing temperature fields on Kinetic Reaction editor (New UI)

Version 5.1 Update 6

- [NEW] Added new sample flowsheet: NGPU with Turbo-Expansion (Windows only)
- [NEW] PFD enhancements (New UI): edit flowsheet object appearance, horizontal/vertical flip, rotation and other properties
- [FIX] Bug fixes

Version 5.1 Update 5

- [FIX] Fixed Sour Water model
- [FIX] Fixed a bug with the Flowsheet UO
- [FIX] Fixed molar flow separation spec in Compound Separator
- [FIX] Fixed Material Stream phase property cloning
- [FIX] Fixed section diameter conversion (Pipe Segment, New UI)

Version 5.1 Update 4

- [CHG] Updated CoolProp libraries (Windows/Linux)
- [FIX] Fixed Console Redirection (Classic UI)
- [FIX] Fixed issues with CoolProp Property Package
- [FIX] Fixed issues with CAPE-OPEN Property Package Socket

Version 5.1 Update 3

- [NEW] Exposed Plugins Interface to Cross-Platform UI
- [CHG] New procedure for calculation of dew points in Natural Gas properties Plugin
- [CHG] Improved compound compatibility with CoolProp Property Package
- [CHG] Nested Loops VLE Flash: forcing PH/PS fast mode off with single compound
- [FIX] Fixed issues with Formation Properties in Compound Creation tools
- [FIX] Fixed Flowsheet Solver not handling interconnected energy streams

Version 5.1 Update 2

- [CHG] Updated Compound Creator Wizard to get data from online sources
- [FIX] Exposing missing Rigorous Column specs (New UI)
- [FIX] Minor bug fixes

Version 5.1 Update 1

- [FIX] Fixed issues with the Classic UI on High DPI displays
- [FIX] Fixed inverted Material Stream list (New UI)
- [FIX] Fixed text flowsheet object removal (New UI)

Version 5.1

- [NEW] Simulation Setup Wizard (New UI)
- [NEW] Compound Creator Wizard
- [NEW] Calculated Phase Properties can now be overriden by the user
- [NEW] Force compound to be in the solid phase only
- [NEW] Added full profile information for Pipe Segment result reports (New UI)
- [FIX] Added missing CoolProp and Petalas-Aziz libraries for macOS

Version 5.0 Update 12

- [FIX] Fixed bugs in the SVLE flash algorithm
- [FIX] Fixed bugs in the Pipe Segment model
- [FIX] Fixed bugs in the CSTR editor (Classic UI)

Version 5.0 Update 11

- [NEW] Advanced EOS and ThermoC models are now exposed to Excel and CAPE-OPEN
- [CHG] Excel Add-In enhancements and bug fixes
- [FIX] Minor bug fixes

Version 5.0 Update 10

- [NEW] Added support for ThermoC Property Package
- [FIX] Fixed crashes during loading of simulations on macOS
- [FIX] Fixed Material Stream cloning on new UI
- [FIX] Fixed viscosity calculations for petroleum fractions
- [FIX] Fixed velocity units in Kinetic Reaction editor (new UI)

Version 5.0 Update 9

- [NEW] Anchored Property Lists
- [NEW] Flowsheet Charts
- [CHG] Updated syntax highlighting for macOS/Linux script editors
- [FIX] Minor bug fixes and enhancements

Version 5.0 Update 8

- [NEW] Added IPOPT libraries for macOS
- [NEW] Script Manager: Added an option to run scripts asynchronously
- [NEW] Added dynamic properties to Flowsheet
- [NEW] Redesigned Script Manager (New UI)
- [CHG] Removed FPROPS and PC-SAFT (original) Property Packages
- [FIX] Fixed logical block editors to include name/status (New UI)
- [FIX] Fixed floating property table behavior on macOS (New UI)
- [FIX] Added missing property editors for column solver methods and pump curves (New UI) 
- [FIX] Fixed HetCat reaction rate unit selector (New UI)
- [FIX] Fixed IPOPT link path on Linux
- [FIX] Fixed python script execution on Linux and macOS
- [FIX] Fixed some translation issues

Version 5.0 Update 7

- [FIX] Fixed loading Property Packages from the Advanced EOS Library on the new UI
- [FIX] Fixed missing translations 

Version 5.0 Update 6

- [NEW] Added hardware (OpenGL) renderer option for PFD area (accessible through 'Global Settings' > 'Flowsheet Renderer')
- [NEW] Added Mass/Energy balance check to Unit Operations ('Simulation Setup' > 'Settings')
- [FIX] Fixed new object placement on New UI
- [FIX] Fixed file extension when saving a simulation on the New UI
- [FIX] Fixed Log Window on Classic UI 
- [FIX] Fixed GTK# renderer on macOS
- [FIX] Reworked Sensitivity Analysis results viewing on New UI
- [FIX] Fixed solver selection missing from Rigorous Column editors on New UI

Version 5.0 Update 5

- [FIX] Fixed reporting issues on macOS/Linux
- [FIX] Fixed values of Enthalpy/Entropy/Cp on Material Stream result reports

Version 5.0 Update 4

- [NEW] Exposed Dynamic Properties to Sensitivity Analysis and Optimization utilities
- [NEW] Added ODS/ODT results reporting (New UI)
- [FIX] Load File/Spreadsheet fixes (Classic UI)
- [FIX] Fixed an error with the Sensitivity Analysis utility (New UI)

Version 5.0 Update 2/3

- [CHG] Updated Linux files
- [FIX] Added missing Orifice Plate Editor/Results
- [FIX] Fixed error on spreadsheet value writing
- [FIX] Fixed an issue with the Python Script UO on Linux

Version 5.0 Update 1

- [FIX] Fixed script selection/linking on new UI
- [FIX] Added missing WriteMessage() function to Flowsheet class on new UI
- [FIX] Fixed flowsheet mouse click issues on macOS platform

Version 5.0

- [NEW] New Cross-Platform User Interface (Windows/Linux/macOS)
- [NEW] Simulation Objects now feature Dynamic Properties
- [CHG] Reworked Classic interface to increase PFD area
- [CHG] Native libraries are now included on each platform-specific installer/package
- [FIX] Fixed Equation 107 in compound creator
- [FIX] Fixed ODE solver in PFR code
- [FIX] Various bug fixes and minor enhancements 

Version 4.3 Update 14

- [NEW] Exposed Gibbs Reactor convergence parameters for fine-tuning
- [FIX] Reactor model bug fixes
- [FIX] Fixed Compound Creator JSON export

Version 4.3 Update 13

- [NEW] Added Peng-Robinson interaction parameters for H2O/NH3/CO2/H2S systems
- [CHG] Updated Python Script subsystem
- [FIX] Sour Water model bug fixes
- [FIX] Fixed Equilibrium/Gibbs Reactor adiabatic calculation mode
- [FIX] Compound database loading fix
- [FIX] Fixed PR interaction parameter set retrieval

Version 4.3 Update 12

- [NEW] New samples: Gasoline & Biodiesel Combustion using Cantera
- [NEW] Custom ordering of Master Property Table items
- [NEW] Flowsheet Scripts now support Python.NET
- [FIX] Fixed management of Optimization and Sensitivity Analysis cases
- [FIX] Fixed a bug with the Gibbs Reactor solver 

Version 4.3 Update 11

- [NEW] Python.NET is now an option to parse Python scripts on the Script Unit Operation - enables 
        interoperability between DWSIM and a full Python 2.7 ecosystem
- [NEW] New Sample: Cantera (https://github.com/Cantera/cantera/wiki) Integration with DWSIM
- [FIX] Fixed a bug with the Gibbs Reactor

Version 4.3 Update 10

- [NEW] Quick-edit button for Systems of Units
- [CHG] Changed default number formatting
- [FIX] Flowsheet Solver fixes

Version 4.3 Update 8/9

- [CHG] Enhanced Gibbs Reactor model, now support multiphase chemical equilibrium
- [CHG] Updated Simultaneous Adjust convergence criteria to compare against individual tolerances
- [FIX] Fixed XML serialization of Strings
- [FIX] Fixed Poynting correction for supercritical fluids
- [FIX] Fixed Compound Separator mass balance issues

Version 4.3 Update 7

- [CHG] Redesigned Object Palette
- [FIX] Minor bug fixes

Version 4.3 Update 6

- [NEW] Pump Curve database system
- [FIX] Fixed Flowsheet Object cloning
- [FIX] Fixed issues when adding external compounds to existing simulations
- [FIX] Fixed issues with expression parsing for KDB compounds

Version 4.3 Update 5

- [NEW] Added 'Commercial Copper' to Pipe UO wall material selection
- [CHG] Changed Recycle mass flow error calculation
- [FIX] Fixed heat balance for the Conversion Reactor
- [FIX] Fixed initialization issues on Windows XP
- [FIX] Fixed bugs on CAPE-OPEN UO
- [FIX] Fixed bugs on Flowsheet UO
- [FIX] Fixed a bug on the Flowsheet Solver
- [FIX] Fixed a bug with the Orifice Plate UO Editor

Version 4.3 Update 4

- [NEW] Implemented Poynting Correction Factor for activity coefficient models, enabled by default
- [NEW] More German translations 
- [FIX] Fixed an issue with Nested-Loops immiscible flash algorithm
- [FIX] Fixed flowsheet drag-and-drop when running on Mono
- [FIX] Fixed solver deactivation after an infinite loop error message

Version 4.3 Update 3

- [CHG] Added an option to control decimal separator parsing for spreadsheet formulas
- [FIX] User compound database loading fix
- [FIX] Fixed issues with Material Stream cloning

Version 4.3 Update 2

- [FIX] Fixed an issue with Data Regression utility
- [FIX] Fixed a stack overflow error when cloning a Material Stream

Version 4.3 Update 1

- [FIX] Fixed issues with solid calculations
- [CHG] Moved Compound Data XML Exporting function to Pure Compound Property Viewer

Version 4.3

- [NEW] Import Compound from Online Databases (KDB, Chemeo, DDB GC Structure Info)
- [NEW] Import Compound from ChEDL Thermo Python Library (https://github.com/CalebBell/thermo)
- [NEW] Data Regression Tool: Import VLE data from Online KDB Database
- [NEW] Export/Import Compound data to/from JSON file format
- [NEW] Add objects to the flowsheet from the context menu
- [NEW] Spreadsheet Undo/Redo
- [NEW] Exposing Spec/Adjust properties
- [CHG] Data Regression Tool/Compound Creator UI design enhancements
- [CHG] Changed location of settings file for Mono environments to Documents Folder -> DWSIM Application Data
- [FIX] Fixed Spreadsheet data saving/loading when cell formula contains '>' or '<'
- [FIX] Fixed native libraries extraction procedures
- [FIX] Minor fixes and enhancements

Version 4.2 Update 3

- [NEW] Added support for the Advanced EOS Library
- [FIX] Fixed issue with flowsheet calculation when a Material Stream editor window is opened
- [FIX] Fixed issue when saving a simulation with scripts
- [FIX] Fixed issue with the Heat Exchanger model
- [FIX] Fixed a bug with the Simple LLE flash algorithm
- [CHG] Natural Gas plugin Water Dew Point calculation enhancements

Version 4.2 Update 2

- [FIX] Fixed outlet temperature in Gibbs/Equilibrium reactors
- [FIX] Fixed experimental regression data values not updating when changing units on Compound Creator
- [FIX] Fixed floating table positioning when close to flowsheet borders

Version 4.2 Update 1

- [CHG] Enhancements and fixes for Rigorous Column model solvers
- [FIX] Fixed issues with Material Stream editor

Version 4.2

- [NEW] Added support for COM/.NET Automation
- [NEW] Added string variable support to Script UO
- [CHG] Manage user databases directly from Excel Add-In Options window
- [FIX] Fixed Spreadsheet UO not reading input variables
- [FIX] Fixed issues with Steam Tables Enthalpy/Entropy calculations
- [FIX] Fixed Steam Tables liquid density calculation
- [FIX] Fixed a bug with Heat Exchanger Shell & Tube calculation mode
- [FIX] Other bug fixes and enhancements

Version 4.1 Update 12

- [FIX] Fixed translation issues
- [FIX] Fixed PH/PS Flash calculation through the Excel interface

Version 4.1 Update 11

- [NEW] More German translations
- [CHG] Updated Rigorous Column solvers (NS/IO)
- [FIX] Fixed CSTR editor issue

Version 4.1 Update 10

- [FIX] Implemented data validation for input tables (issue #147 https://sourceforge.net/p/dwsim/tickets/147/)
- [FIX] Fixed unwanted object dragging when 'Close Editors on Deselecting' is enabled (issue #154 https://sourceforge.net/p/dwsim/tickets/154/)
- [FIX] Fixed double array value check on XML serialization (issue #155 https://sourceforge.net/p/dwsim/tickets/155/)

Version 4.1 Update 9

- [FIX] Fixed a bug regression in binary envelope utility
- [FIX] Fixed PH-Flash with Raoult's Law Property Package
- [CHG] CSTR model updates

Version 4.1 Update 8

- [NEW] Updated CSTR model, now supports multiple phases and adds vapor outlet
- [CHG] Update Material Stream flow and composition after removing a compound
- [FIX] Fixed CAPE-OPEN UO Energy Stream connection
- [FIX] Fixed CAPE-OPEN UO error not breaking flowsheet calculation
- [FIX] Fixed CAPE-OPEN UO Array property handling
- [FIX] Fixed data storage for regression case
- [FIX] Fixed display of missing properties on tables
- [FIX] Minor bug fixes and enhancements

Version 4.1 Update 7

- [NEW] Enhanced compatibility with Wine on Linux/macOS
- [CHG] Changed default solver settings
- [CHG] Rewritten Simultaneous Correction and Inside-Out column solvers
- [FIX] Fixed heat balance of Conversion, Equilibrium and Gibbs reactors in Adiabatic Mode
- [FIX] Fixed issues with Flowsheet Block and Rigorous Column editors
- [FIX] Minor bug fixes and enhancements

Version 4.1 Update 6

- [FIX] Added missing libraries

Version 4.1 Update 5

- [FIX] Fixed bugs with Flowsheet and Gas-Liquid Separator Unit Operations

Version 4.1 Update 4

- [FIX] Fixed Recycle convergence issues

Version 4.1 Update 3

- [FIX] Fixed an issue with the Material Stream editor
- [FIX] Fixed CUDA GPU code loading

Version 4.1 Update 2

- [FIX] Fixed Solid Phase Enthalpy/Entropy calculation for some Property Packages
- [CHG] Steam Tables Property Package can now be used in simulations with multiple compounds

Version 4.1 Update 1

- [FIX] Fixed conversion reactor expression parsing

Version 4.1

- [NEW] Added the ability to load, edit and save simulations created on mobile devices (Android/iOS)
- [NEW] Material Stream editor enhancements
- [NEW] Biodiesel production sample
- [NEW] Added more thermophysical phase properties: Isothermal Compressibility, Bulk Modulus, Joule-Thomson Coefficient, Speed of Sound, Internal Energy, Gibbs Free Energy and Helmholtz Free Energy 
- [NEW] New Compound properties menu in Material Stream editor, added Infinite Dilution Diffusivity Coefficients
- [CHG] Updated Molarity/Molality input mechanism
- [FIX] General bug fixes

Version 4.0 Update 35

- [FIX] Fixed bugs with the Rigorous Column model

Version 4.0 Update 34

- [FIX] Fixed PFR & CSTR reaction heats

Version 4.0 Update 33

- [FIX] Fixed bugs with the Compound Creator
- [FIX] Fixed a bug with assignment of formation properties when loading user compounds

Version 4.0 Update 32

- [FIX] FIxed a bug with the Material Stream editor

Version 4.0 Update 31

- [FIX] CAPE-OPEN PP interface workaround for ChemSep
- [FIX] Fixed Sensitivity Analysis variable units
- [CHG] Orifice Plate diameter input

Version 4.0 Update 30

- [NEW] Added the ability to display charts on another window, zoomed copy and save
- [NEW] New Welcome Screen
- [FIX] Fixed PFR/CSTR heat balance
- [FIX] Fixed bugs with the Compound Creator

Version 4.0 Update 28/29

- [FIX] Added missing library from previous update

Version 4.0 Update 27

- [FIX] PFR/CSTR bug fixes
- [FIX] Fixed liquid density for user-defined compounds

Version 4.0 Update 26

- [CHG] CoolProp database compounds can now be used with most Property Packages
- [FIX] Fixed bugs with the CoolProp Property Package
- [FIX] Fixed Shortcut and Rigorous Column connection editors

Version 4.0 Update 25

- [FIX] Fixed bugs with the Steam Tables Property Package

Version 4.0 Update 24

- [FIX] Fixed Splitter Mass/Mole Flow Spec mode

Version 4.0 Update 23

- [FIX] Fixed Steam Tables property calculation on supercritical region
- [FIX] Other minor fixes

Version 4.0 Update 22

- [FIX] Fixed liquid phase reactions on Equilibrium reactors
- [FIX] Fixed Outlet Pressure units on Expander editor

Version 4.0 Update 21

- [FIX] Fixed writing of Spreadsheet Variables to Flowsheet
- [FIX] Fixed some unit conversions
- [CHG] More German translations

Version 4.0 Cumulative Update 2 (Update 20)

Cumulative Update 2 (Update 20) includes all changes from Update 1 to 19 as well as:

- [FIX] Fixed Petroleum Characterization utilities not working
- [CHG] More German translations

Version 4.0 Update 19

- [FIX] Fixed Specification Block calculation
- [FIX] Fixed Solid Phase Enthalpy calculation on Activity Coefficient models

Version 4.0 Update 16/17/18

- [FIX] Fixed PR calculation bug when adding compounds after creating a simulation
- [FIX] Fixed Steam Tables Enthalpy/Entropy calculation
- [FIX] Fixed Rigorous Column Reboiler/Condenser Pressure setting
- [FIX] Fixed Splitter Editor ratio sliders
- [CHG] More German translations

Version 4.0 Update 15

- [FIX] Fixed Steam Tables Property Package
- [FIX] Fixed input for Material Streams connected to Recycles
- [FIX] Fixed Script Editor auto-completion

Version 4.0 Update 14

- [FIX] Fixed number precision loss on saved XML files

Version 4.0 Update 13

- [FIX] Fixed cryptographic issues on FIPS-compliant systems
- [FIX] Fixed opening sample files on read-only installations

Version 4.0 Update 12

- [CHG] Exposing convergence tolerances in Pipe Segment editor

Version 4.0 Update 11

- [FIX] Fixes to Extended UNIQUAC, Sour Water and Steam Tables Property Packages
- [FIX] Fixed Energy Stream connection to Pipe Segment UO

Version 4.0 Cumulative Update 1 (Update 10)

Cumulative Update 1 (Update 10) includes all changes from Update 1 to 9 as well as:

- [FIX] Fixed Electrolyte PVF Flash

Version 4.0 Update 9

- [FIX] Fixed command buttons not showing in Data Regression Utility

Version 4.0 Update 8

- [FIX] Electrolytes subsystem stability and reliability enhancements
- [FIX] Fixed Material Stream, Heater/Cooler, Adjust and Spec editors
- [FIX] Minor bug fixes and enhancements

Version 4.0 Update 7

- [FIX] Fixed Excel Add-In 'CalcProp' function for mass-based properties

Version 4.0 Update 6

- [NEW] Added an option to hide solid phase from list of present phases (for ChemSep compatibility)
- [NEW] Sour Water PP: Added an option to ignore vapor fraction bounds during flash calculations

Version 4.0 Update 5

- [FIX] Fixed missing Compound Spec selection on Rigorous Column editor
- [FIX] Fixed non-converging samples
- [FIX] Fixed bugs in the Flowsheet UO

Version 4.0 Update 4

- [FIX] Fixed loading of DWSIM 3.x simulations with logical blocks
- [FIX] Fixed bugs in the Flowsheet UO

Version 4.0 Update 3

- [FIX] Fixed Gibbs Reactor Editor
- [FIX] Fixed Pipe insulation k calculation
- [FIX] Fixed Material Stream editor composition Clear/Equalize buttons

Version 4.0 Update 2

- [FIX] Fixed issues with Adjust and Simultaneous Adjust Solver

Version 4.0 Update 1

- [FIX] Fixed issues with Adjust and Spec logical blocks

Version 4.0 Release

- [FIX] Fixed enthalpy and entropy calculation for Activity Coefficient-based Property Packages

Version 4.0 Release Candidate 2

- [FIX] Fixed Material Stream flowrate editing
- [FIX] Other bug fixes and enhancements

Version 4.0 Release Candidate 1

- [NEW] Petalas-Aziz pressure drop calculation model now available for Windows 64-bit and Linux 32/64-bit
- [CHG] Updated documentation
- [CHG] Updated CoolProp library to v6.0.0
- [CHG] Pipe Segment editor enhancements
- [FIX] Compound Creator fixes
- [FIX] Other bug fixes and enhancements

Version 4.0 Preview 4

- [FIX] Fixed missing Shortcut Column editor
- [FIX] Restored user-defined interaction parameter funcionality
- [FIX] Fixed ideal gas enthalpy/entropy calculation
- [FIX] Other bug fixes and enhancements

Version 4.0 Preview 3

- [FIX] Bug fixes

Version 4.0 Preview 2

- [NEW] Excel Add-In ribbon menu
- [CHG] Redesigned Material Stream editor
- [FIX] Various bug fixes and enhancements

Version 4.0 Preview 1

- [NEW] Update User Interface
- [NEW] Automatic Updates
- [NEW] Flowsheet utilities: Split/Merge Energy/Material Streams, Resize Objects, Search Bar, Add Rectangle
- [NEW] Export Report data to ODT/ODS files
- [NEW] Heat Exchanger Pinch Point calculation mode
- [NEW] Pipe Segment ambient temperature gradient
- [CHG] Calculation speed improvements up to 50%
- [CHG] Changed Property Tables (Floating, Simple, Master and Linked) behavior
- [CHG] Utilities are now linked to Flowsheet Objects, can be added multiple times and saved/restored with simulation files
- [FIX] Many bug fixes and improvements

Version 3.7 Build 5981

- [FIX] Bug fixes to reactor models
- [FIX] Bug fix for Nested Loops VLLE and Simple LLE flash algorithms
- [FIX] Fixed a bug in the Data Regression utility
- [FIX] Fixed pipe wall conductivity calculation
- [FIX] Fixed CAPE-OPEN Property Package Manager 64-bit registration

Version 3.7 Build 5965

- [NEW] Added more optimizer options to Data Regression utility and Gibbs Minimization flash algorithm
- [NEW] Added the capability of creating electrolyte compounds to the Compound Creator
- [NEW] Added interaction parameter bounds to the Data Regression utility
- [FIX] Several bug fixes and enhancements to PFR and CSTR models
- [FIX] Minor bug fixes

Version 3.7 Build 5952

- [CHG] Changed target .NET Framework to v4.0 for Windows XP compatibility
- [FIX] Bug fixes

Version 3.7 Build 5905

- [NEW] Implemented inline unit conversion for Property Grid
- [NEW] Script Manager comment/uncomment/indent lines
- [CHG] flowsheet object selection rectangle now works from all directions
- [CHG] Updated cut/paste shortcut key behavior
- [FIX] Bug fixes and enhancements

Version 3.7 Build 5901

- [NEW] Added Undo/Redo capability for Flowsheet Objects, Compounds and Property Packages
- [NEW] Added cross-simulation Cut/Copy/Paste capability for Flowsheet Objects
- [NEW] New 'Edit' menu
- [NEW] Added data copy button to Spreadsheet and Material Stream list (Mono only)
- [NEW] Added SRK fugacity GPU calc routine
- [CHG] Minor changes and UI enhancements
- [FIX] Fixed Spreadsheet UO calculation taking too long to finish
- [FIX] Fixed Nested Loops PT/PV Flash mismatch for single-compound mixtures 
- [FIX] General bug fixes

Version 3.6 Build 5884

- [FIX] Performance enhancements and bug fixes

Version 3.6 Build 5876

- [NEW] DWSIM now runs in 64-bit mode on 64-bit operating systems. To run in 32-bit mode, start "DWSIM_32.bat"
- [NEW] New Pipe calculation modes: Specify Outlet Pressure / Outlet Temperature
- [NEW] Sour Water Property Package
- [CHG] Updated User Interface 
- [CHG] Enabled CoolProp Property Package for 64-bit Linux
- [CHG] Changed XML report style
- [FIX] Various stability enhancements and bug fixes when running in Mono mode

Version 3.5 Build 5800

- [NEW] IAPWS-08 Seawater Property Package
- [NEW] Black-Oil Property Package
- [NEW] Debug Object tool
- [CHG] Enhanced Flowsheet Solver to increase calculation speed 
- [CHG] Enhanced Compound Creator to handle black-oil fluids
- [CHG] Updated flowsheet drawing code, added new alignment tools
- [FIX] General bug fixes and optimizations

Version 3.4 Build 5740

- [NEW] Added Spreadsheet Cell support to Sensitivity Analysis and Optimization tools
- [NEW] 'Power' specification added to Pump model
- [NEW] New Heat Exchanger calculation mode
- [NEW] Added API help link to script editors
- [NEW] Added 'Replace' function to compounds added to the simulation
- [NEW] Added an option to backup file when saving an existing simulation
- [CHG] Replaced script editing component to enable code intellisense (Windows only)
- [CHG] Reworked Electrolyte Flash algorithms
- [FIX] Fixed Liquid Phase Enthalpy calculation in Activity Coefficient-based PPs
- [FIX] Fixed Gibbs reactor energy balances
- [FIX] General bug fixes and optimizations

Version 3.4 Build 5644

- [NEW] Added support for Heterogeneous Catalytic reactions
- [NEW] UNIFAC Interaction Parameter Regression Tool
- [NEW] Custom UO input and output variables
- [NEW] Mouse double-click on flowsheet objects opens specific property editor windows
- [NEW] Added three new sample flowsheets
- [CHG] New Welcome Screen
- [CHG] Human-readable error messages
- [CHG] Flowsheet UO compound mapping
- [CHG] Copy and Paste EOS interaction parameters
- [CHG] Minor usability enhancements 
- [CHG] Changed Reid Vapor Pressure calculation procedure
- [FIX] Implemented limiter for parallel conversion reactions
- [FIX] Spec Op general fixes
- [FIX] Fixed conversion and equilibrium reactor expression parsing
- [FIX] General bug fixes

Version 3.4 Build 5608

- [NEW] NIST-MODFAC Property Package (http://trc.nist.gov/TDE/Help/TDE103b/NIST-Modified-UNIFAC-AC-Model.htm)
- [NEW] Model Comparison in Binary Enevelope Utility
- [NEW] New Flowsheet Solver modes
- [NEW] Linked Spreadsheet Table
- [NEW] Activate/Deactivate Flowsheet Objects
- [NEW] Added Input and Output Variables to Custom (Script) Unit Operation
- [NEW] Restore previous successful flowsheet solutions
- [NEW] Equilibrium calculations now use Henry coefficients for non-condensables 
- [NEW] Added support for CAPE-OPEN Array Parameter type
- [NEW] Mouse double-click now opens specific windows for some unit operations
- [NEW] Added window docking menu for Mono compatibility
- [NEW] Export simulation compounds to XML user database
- [CHG] Excel UO is now Spreadsheet UO, supports ODS files and runs on Linux
- [CHG] Speed enhancements for calculations with UNIFAC-type Property Packages
- [CHG] Flash Algorithm "Fast" option is now a Property Package parameter
- [CHG] Changed Flowsheet UO subflowsheet display mode to dockable
- [CHG] Parallelized phase identification routine loop
- [CHG] Optimized Recycle code
- [CHG] Improved Lee-Kesler root finding
- [CHG] New object palette icons
- [FIX] Corrected some Hydrate calculation models
- [FIX] Fixed GPU multithreading lockups
- [FIX] Fixed Sum Rates Absorption Column solver
- [FIX] Minor bug fixes and enhancements

Version 3.3 Build 5517

- [NEW] Flowsheet Unit Operation
- [NEW] Redesigned Nested Loops VLLE flash algorithm
- [NEW] Fast/Rigorous option mode for Nested Loops VLE flash algorithm
- [NEW] Timed simulation script events
- [NEW] Updated Plugin Interface (IUtilityPlugin2), includes new generic function which can be called by scripts
- [CHG] Updated/Redesigned CoolProp interface
- [CHG] Autosave scripts when saving flowsheet
- [FIX] Fixed Heat of Vaporization calculation for ChemSep compounds
- [FIX] Fixed bugs in the PSV sizing utility
- [FIX] General bug fixes and code optimizations

Version 3.3 Build 5488

- [NEW] Excel Unit Operation
- [NEW] IronPython Script Manager
- [CHG] Flowsheet drawing optimizations
- [CHG] Added Energy Stream requirement to Vessel UO to maintain energy balance consistency
- [CHG] Added a confirmation message to add units systems from simulation files
- [CHG] Sensitivity Analysis Utility GUI adjustments
- [CHG] CAPE-OPEN Property Package adjustments
- [CHG] Added Material Stream's phase mole flow information to property grid
- [FIX] (T)xy, (P)xy binary diagram fixes
- [FIX] Fixed Command Line mode issues
- [FIX] Fixed bugs in the Compound Creator utility
- [FIX] Fixed Rigorous Column stage naming
- [FIX] Fixed proxy settings for new version check
- [FIX] Fixed isolated Material Stream not calculating with "Calculate All" command
- [FIX] Minor bug fixes

Version 3.2 Build 5398

- [CHG] Cleaned exception information in log window
- [FIX] General fixes to Parallel CPU/GPU calculations
- [FIX] General fixes and stability enhancements to Three-Phase Flash Algorithms
- [FIX] Fixed Interaction Parameter XML restoring bug
- [FIX] Fixed Recycle Op not updating mole flows
- [FIX] Fixed a report display bug in CAPE-OPEN Unit Operation

Version 3.2 Build 5390

- [NEW] Triangular LLE Phase Envelope Utility
- [NEW] User Compound Database Management Tool
- [NEW] Material Stream data copy through the PFD context menu
- [NEW] Added Enthalpy/Entropy/Cp calculation method selection to Activity Coefficient-based Property Packages.
- [NEW] Added support for hydrate calculations in equilibrium with gas phase only
- [NEW] New interpolation methods for distillation column initial estimates of temperature, vapor and liquid flows
- [CHG] Pure Component Property Viewer improvements
- [CHG] Updates to Help system
- [CHG] Updates to stability tests on three-phase flash algorithms
- [FIX] Implemented missing methods for XML saving/loading of Watch Items
- [FIX] Fixed bugs in the Electrolytes subsystem
- [FIX] Gibbs Reactor model fixes
- [FIX] Minor bug fixes and UI tweaks

Version 3.2 Build 5349

- [NEW] Implemented Pressure-Enthalpy (PH) and Pressure-Vapor Fraction (PVF) Flash algorithms for Electrolyte Property Packages
- [NEW] Added an option to restore Material Streams' default properties
- [NEW] Added "Ignore Vapor Phase" property to the Tank Unit Op
- [CHG] Changed Units System persistence to XML format
- [CHG] Updated calculation procedures in the Petroleum Cold Flow Properties utility
- [CHG] Steam Tables Property Package now works with Water compound from ChemSep database
- [FIX] Restored table property values after loading simulation from a XML file
- [FIX] Minor bug fixes

Version 3.2 Build 5309

- [NEW] Added hydrate calculation method option to the phase envelope utility
- [CHG] Enhanced hydrate models to correctly support hydrocarbons that don't form hydrates

Version 3.2 Build 5296

- [NEW] New Phase Identification Algorithm based on the work of Venkatarathnam et al (http://dx.doi.org/10.1016/j.fluid.2010.12.001)
- [NEW] Enhanced Phase Envelope Utility now shows hydrate curves, dry-basis dew points for wet natural gas mixtures and phase identification boundary for PR and SRK EOS
- [NEW] Added Motor Octane Number (MON) and Methane Number (MN) properties to the Natural Gas Properties Plugin
- [NEW] New flash calculation spec added to the Separator Vessel model
- [CHG] Enhanced NRTL/UNIQUAC interaction parameter estimation procedures, can now estimate temperature dependency
- [CHG] Enhanced flash algorithm initialization
- [CHG] All three-phase flash algorithms now calculate true tree-phase saturation points if required
- [CHG] Enhanced Hydrate Formation Utility with faster calculation times and ChemSep compound support
- [CHG] Updated UNIFAC/MODFAC Property Packages' configuration dialog with UNIFAC group information
- [FIX] Fixed reporting tool
- [FIX] Minor bug fixes

Version 3.1 Build 5265

- [FIX] Mono Runtime compatibility fixes
- [FIX] Fixed Spreadsheet bugs
- [FIX] Fixed bugs in the Adjust and Set logical operations
- [FIX] Fixed PV/TV Flash temperature search limits
- [FIX] Fix for sensitivity analysis window resizing
- [CHG] Updated quickview table
- [CHG] Fixed zoom level of the quickview table at 100%

Version 3.1 Beta Build 5259

- [NEW] Rewritten flowsheet solver for faster calculation times and recycle optimization
- [NEW] CoolProp Property Package (www.coolprop.org)
- [NEW] Simulation Configuration Wizard
- [CHG] Redesigned Online Help file
- [CHG] Activity Coefficient-based Property Packages now default to Ideal Vapor Phase modeling
- [CHG] Properties like Viscosity, Thermal Conductivity and Surface Tension are now calculated using experimental data whenever available
- [CHG] Enhanced Pure Compound Property Viewer to show all T-dependent properties
- [FIX] Restored compatibility with pre-3.0 binary format simulation files (*.dwsim)
- [FIX] Small interface updates and general bug fixes

Version 3.0 Build 5212

- [FIX] Fixed Material Stream molar/volumetric flow input through the Property Grid.

Version 3.0 Build 5209

- [FIX] Fixed ratios not being set in the Splitter Op.
- [FIX] Fixed a small bug in Gibbs PH flash calculation.

Version 3.0 Build 5206

- [NEW] New Nested-Loops Three-Phase algorithm
- [FIX] Various fixes and stability/reliability enhancements for the flash algorithms
- [FIX] Minor bug fixes.

Version 3.0 Build 5162

- [NEW] Added a new "Stream Flow Spec" operation mode for the Splitter Unit Operation.
- [CHG] Workaround for CAPE-OPEN terminate call error during flowsheet close event.
- [CHG] Updated Cudafy.NET.dll to v1.26, compiling CUDA code with CUDA SDK 5.5 and Visual C++ 2012 Express.
- [CHG] Added a trivial solution check to the Gibbs PT flash algorithm.
- [FIX] Fix for zeroing mass and mole flow through CAPE-OPEN calls.
- [FIX] Workaround for Gibbs PT-Flash algorithm reaching maximum number of iterations (happens on single phase regions).
- [FIX] Fixed XML serialization of Double and Single data types.
- [FIX] Minor bug fixes.

Version 3.0 Build 5154

- [NEW] Excel Interface: added functions to return interaction parameters stored in DWSIM for a given binary / model.
- [NEW] Excel Interface: saving and reading settings to/from INI file (stored in "My Documents\DWSIM Application Data") for improved compatibility.
- [FIX] Inside-Out Flash: fixed resulting vapor composition in three-phase PT-Flash.
- [FIX] Separator Vessel: fixed equilibrium calculation for a single compound mixture.
- [FIX] Flowsheet Solver: fixed flash calculation for single compound not following the defined specification.
- [FIX] Fixed equilibrium calculation routines to handle single-compound streams.
- [FIX] Exposing more temperature-dependent pure compound properties through Excel and CAPE-OPEN interfaces.
- [FIX] Excel Interface: Added a function to return compound constants as loaded from the database files.
- [FIX] Fixed a small bug in mixture vapor pressure calculation.
- [FIX] UNIQUAC model: fixed infinite dilution activity coefficient calculation.
- [FIX] Data Regression utility: fixed images not showing in database management buttons.

Version 3.0 Build 5145

- [CHG] Changed the XML simulation loading code, now keeps loading information even if it finds errors in the XML structure
- [CHG] Changed ChemSep database initial directory setting for Excel compatibility if it doesn't find it through registry (defaults to [dwsim]\chemsepdb\chemsep1.xml)
- [FIX] Fixed XML loading of Sensitivity Analysis and Optimization Cases

Version 3.0 Build 5142

- [NEW] User Interaction Parameter database system for NRTL and UNIQUAC models (work in progress)
- [CHG] Compounds and Property Packages can now be added with a double mouse click
- [FIX] Fixed bug #13: Steam Tables - gas-liquid equilibrium in adibatic expander (https://sourceforge.net/apps/mantisbt/dwsim/view.php?id=13)
- [FIX] Fixed bug #14: Sensitivity Analysis Definition cannot be saved (https://sourceforge.net/apps/mantisbt/dwsim/view.php?id=14)
- [FIX] Fixes to the CAPE-OPEN subsystem
- [FIX] Fixed validation of PT-flash calculations
- [FIX] Fixed a bug in the PH-Flash calculation with Gibbs 3P algorithm
- [FIX] Fixed mixing rule for liquid density
- [FIX] Fixed some bugs in the Shortcut Column model
- [FIX] Binary Envelope - y axis scale adjusted to x axis in Txy and Pxy diagrams

Version 3.0 Build 5114

- [NEW] Temperature-dependent VLE/LLE regression for NRTL/UNIQUAC interaction parameters
- [NEW] Excel Interface: created new functions that accept an initial estimate for temperature and pressure calculations
- [CHG] Users can now specify the gibbs energy delta tolerance for validation of flash calculation results
- [CHG] Welcome Screen: Button "Samples" opens a dialog with samples directory selected instead of displaying samples directory.
- [CHG] Enhanced stability of the three-phase Gibbs Minimization flash algorithm
- [FIX] Fixed enthalpy and entropy calculation through Excel interface
- [FIX] Fixed CSTR and Shortcut Column models
- [FIX] Minor bug fixes and enhancements

Version 3.0 Build 5065

- [CHG] Exposed more properties of the Distillation Column to Adjust, Set, Report and Optimization tools
- [FIX] Fixed Excel Thermo Interface
- [FIX] Fixed Material Stream cleaning through CAPE-OPEN commands
- [FIX] Fixed inverted sign on Russell's IO duties results
- [FIX] Fixed stage efficiency on Russell's IO solver
- [FIX] Fixed some Spanish translations
- [FIX] Fixed XML loading of Pipe, Shortcut Column and Master Property Table

Version 3.0 Build 5031

- [NEW] Added Aqueous Electrolyte systems simulation support with two new Property Packages: LIQUAC* and Extended UNIQUAC
- [NEW] New Unit Operations for Solid-handling simulations: Solids Separator and Continuous Cake Filter
- [NEW] Unified code base and single executable for .NET/Mono, compiled for CLR v4.0
- [NEW] New XML simulation file format for full compatibility between platforms (Windows/Linux/OS X)
- [NEW] New Parallel Calculations engine with support for multicore CPUs and CUDA/OpenCL-capable GPUs
- [NEW] 'Copy Data to Clipboard' function added to flowsheet objects
- [CHG] Stability enhancements to Rigorous Column model and Boston-Britt Inside-Out solver
- [CHG] Report tool now fully supports Excel Spreadsheet file format
- [CHG] Added more material stream composition input options: Mass/Mole Flows, Standard Liquid Volumetric Fractions, Molarity and Molality (for electrolyte simulations)
- [CHG] Mixer and Separator Vessel models now supports up to six inlet streams
- [CHG] NRTL/UNIQUAC Interaction Parameters are now temperature-dependent
- [CHG] All Activity coefficient models now include an option to model the Vapor Phase as ideal. The Data Regression utility also supports this option in order to regress data to find the interaction parameters accordingly
- [CHG] The Compound Creator was enhanced to include more Joback/UNIFAC groups, Element information and Solid Phase properties estimation, including temperature-dependent ones (Cp, Density). The regression graphs now show calculated properties for every temperature dependent property
- [CHG] Phase Envelope utilities now have cancellation support
- [CHG] The Binary Envelope Utility was enhanced to show every kind of equilibrium line supported by DWSIM (VLE, LLE, SLE and Critical)
- [CHG] The Data Regression utility now supports fixing some interaction parameters, enhanced LLE /SLE regression
- [CHG] The Pure Compound Property viewer now includes molecular properties, solid properties and property tables
- [FIX] Lots of bug fixes and minor enhancements

Version 2.1 Build 4819

- [FIX] Fixed empty property grid after adding an object to the flowsheet
- [FIX] Fixed Binary Envelope utility "index out of bounds" error
- [FIX] Fixed unit system not being updated after loading from file
- [FIX] Fixed NRTL parameter estimation in the Data Regression utility
- [FIX] Fixed vapor pressure parameters not being saved in the Compound Creator
- [FIX] Fixed NRTL/UNIQUAC parameter estimation between compounds from mixed databases
- [FIX] Fixed boiling point calculation instability in the Binary Envelope utility when two liquid phases are present
- [FIX] Fixed key compound list for instability test not being updated after adding/removing a compound from the simulation
- [FIX] Fixed object property sorting in the Sensitivity Analysis utility
- [FIX] Fixed German translation for the "Advanced" section in Thermodynamics config screen 
- [FIX] Fixed Critical Temperature calculation in the Compound Creator when Normal Boiling Point isn't selected to be calculated by UNIFAC/Joback
- [FIX] Fixed Adjust error function units
- [CHG] Phase Envelope utility is now set to show all calculated points even when an error occurs
- [CHG] Saved file name is now displayed in the window title for Regression and Compound Creator cases

Version 2.1 Build 4768

- [CHG] Various enhancements added to the Binary Envelope utility

Version 2.1 Build 4767

- [CHG] New Material Streams are now added to the flowsheet at standard conditions (25 C, 1 atm, 1 kg/s)
- [CHG] Enhanced stability of the Three-Phase Flash algorithms when there is no vapor phase
- [CHG] More stable NRTL parameter estimation through UNIFAC (fixed alpha12 at 0.3)
- [FIX] Fixed flowsheet page setup for printing
- [FIX] Fixed system of units saving and loading on German and Spanish GUI languages

Version 2.1 Build 4764

- [NEW] Added user-defined units and other minor enhancements/bug fixes to the Compound Creator
- [CHG] Added a message box to warn the user when NRTL/UNIQUAC parameter estimation with UNIFAC method fails
- [CHG] Added an error message to the Information Window when Bubble/Dew point calculation fails
- [FIX] DWSIM now throws an exception when an UNIFAC interaction parameter is not found for a binary during an activity coefficient calculation
- [FIX] Fixed Master Table property list when changing locale setting
- [FIX] Fixed Material Stream's compound mass and mole flow units when displayed on tables and reports

Version 2.1 Build 4762

- [CHG] Added UNIQUAC parameter input to the Compound Creator
- [CHG] Added data point information to the Compound Creator regression viewer
- [FIX] Fixed Compound Creator filename and checkbox persistence

Version 2.1 Build 4761

- [CHG] Enhanced Compound Creator with UNIFAC group pictures and the ability to enter equation coefficients for temperature-dependent properties.
- [CHG] Updated UNIFAC groups and interaction parameters with data from http://www.aim.env.uea.ac.uk/aim/info/UNIFACgroups.html
- [FIX] Fixed CSTR/PFR models

Version 2.1 Build 4753

- [NEW] Added support for multiple dependent variables to the Sensitivity Analysis utility
- [FIX] Fixed Rigorous Column outlet streams' compound mass fractions
- [FIX] Fixed single-compound inlet stream calculation error
- [FIX] Fixed Sensitivity Analysis flowsheet restoring

Version 2.1 Build 4715

- [NEW] Added German translation for the Compound Creator utility (by Gregor Reichert) 
- [FIX] Fixed Material Stream specification not being honored when changing mixture composition
- [FIX] Fixed Pipe Hydraulic Editor form resizing and length/diameter units
- [FIX] Fixed ChemSep database loading in CAPE-OPEN mode
- [FIX] Fixed Data Regression utility
- [FIX] Fixed Heater/Cooler outlet temperature specification bug

Version 2.1 Build 4698

- [CHG] Enhanced Element Matrix and Initial Estimates editor for the Gibbs Reactor

Version 2.1 Build 4680

- [NEW] Save selected object properties to text file
- [FIX] Fixed PFR, CSTR and Pump models
- [FIX] Fixed instability on the Nested Loop PH Flash code
- [FIX] Fixed pressure unit conversion from barg to Pa

Version 2.1 Build 4606

- [CHG] COSMO-SAC database loading is now done only on-demand instead of during startup
- [CHG] Petalas-Aziz pressure drop model now uses a native library by the authors

Version 2.1 Build 4605

- [NEW] Simultaneous Adjust Solver
- [NEW] Added the option to edit pure compound properties through the Pure Compound Properties utility
- [NEW] Added automatic calculation of PR and SRK Peneloux volume translation (shift) coefficients for pseudocomponents
- [FIX] Several fixes to the Pipe Segment (and pressure drop) model, includes Joule-Thomson cooling option
- [FIX] Fixed drawing of Adjust's line connectors

Version 2.1 Build 4602

- [NEW] Added Peneloux volume translation support for PR and SRK Property Packages (Configure Property Package > General Options > Use Peneloux Volume translation => set to 1)
- [FIX] Fixed material stream composition editing in commmand line mode
- [FIX] Fixed ambient temperature not being set on Thermal Profile Editor (Pipe Segment)

Version 2.1 Build 4589

- [CHG] Enabled calculation of material streams when there is no mass/mole flow
- [FIX] Fixed mixer calculation when some of the inlet streams have no flow
- [FIX] Fixed a bug with the mass balance in the gas-liquid separator

Version 2.1 Build 4569

- [NEW] Save/Restore simulation states
- [NEW] Capture flowsheet snapshot and send to clipboard
- [FIX] Fixed command line run mode
- [FIX] Fixed heat capacity coefficients generated by the Compound Creator
- [FIX] Minor fixes to petroleum charact. utilities

Version 2.1 Build 4534

- [FIX] More fixes and enhancements to the flash algorithms
- [FIX] Fixed a bug in the excel interface
- [NEW] Added "reset settings" functionality by pressing Shift during startup

Version 2.1 Build 4526

- [NEW] Added Lua scripting support
- [FIX] Further fixes and improvements to the flash algorithms
- [FIX] Fixed ChemSep column operation with pseudocomponents/hypotheticals
- [FIX] Fixed Zc/Vc calculation for pseudocomponents
- [FIX] Fixed some bugs in the CAPE-OPEN Thermo interfaces

Version 2.1 Build 4513

- [FIX] Enhanced stability and reliability for Pressure-Enthalpy flash calculations

Version 2.1 Build 4503

- [NEW] FPROPS Property Package (needs testing)
- [NEW] Gibbs Minimization (experimental stage!), Hybrid Nested Loops / Inside Out flash algorithms
- [NEW] Added convergence information (error value, time taken and iteration count) for all flash algorithms
- [FIX] Fixed IO PH/PS flash calculation for single compounds
- [FIX] Fixed Property Grid issues with some unit operations and Spanish language
- [FIX] Fixed a bug with energy stream connection to rigorous columns

Version 2.1 Build 4466

- [NEW] Added custom object ordering feature to the Master Table
- [NEW] Added more mole flow units of measure
- [NEW] Added the possibility of removing multiple compounds from the simulation at once
- [FIX] Fixed ChemSep database registry search
- [FIX] Minor translation fixes

Version 2.1 Build 4463

- [FIX] Fixed calculation of ideal gas heat capacity for petroleum fractions

Version 2.1 Build 4452

- [NEW] Petroleum Assay Manager (store/reload/import/export bulk and distillation characterization assay data)
- [NEW] Master Property Table object ordering by property or name ascending/descending
- [NEW] PFD Zoom All, Pan (Shift + Left mouse button)
- [NEW] Area unit conversion to the Heat Exchanger
- [FIX] Fixed PFD Select and Center Object

Version 2.1 Build 4442

- [NEW] Added the Master Property Table to display grouped properties from objects of the same type
- [NEW] Added save-to-image flowsheet feature
- [NEW] Added bubble and dew points to Material Stream's property list
- [NEW] Added the capability of editing custom system of units
- [NEW] Added a keyboard shortcut (Ctrl+E) to edit material stream compositions
- [NEW] Added the capability of showing/hiding table items from the Property Grid

Version 2.1 Build 4438

- [CHG] Changed Adjust variables' units to match the ones in the selected system of units
- [FIX] Fixed custom system of units not being restored
- [FIX] Fixed Adjust behavior
- [FIX] Fixed Flowsheet printing
- [FIX] Fixed Steam Tables Property Package PVF/TVF stream spec calculation
- [FIX] Fixed Bubble and Dew points not being shown if the stream state is single phase
- [FIX] Fixed WFP donation window always showing
- [FIX] Fixed duplicate Separator Op in the Object Palette

Version 2.1 Build 4422

- [CHG] Overall speed and usability improvements
- [NEW] Added drag-and-drop support for opening simulations/cases from Windows Explorer

Version 2.1 Build 4416

- [CHG] Includes ChemSep Lite 6.90, now with 400+ compound database and 40 compounds/300 stages Column model
- [NEW] Includes PRSV2 Property Package with Van Laar-type mixing rule (original PRSV2 w/ Margules MR is now PRSV2-M)
- [FIX] Fixed PRSV2 compresibility factor calculation

Version 2.1 Beta Build 4410

- [NEW] New Property Package: Peng-Robinson-Stryjek-Vera 2 (PRSV2)
- [CHG] Added missing data estimation feature to the Data Regression Utility
- [CHG] Enhanced Unit Set Creator to start with units from the current system

Version 2.1 Beta Build 4407

- [CHG] Added LLE support and initial estimates calculation using UNIFAC structure to the Data Regression Utility
- [CHG] Changed density input from Specific Gravity to API Gravity on the Dist. Curves Characterization Utility
- [FIX] Fixed solver not recalculating outlet streams
- [FIX] Fixed infinite volumetric flow on first Material Stream calculation through the property grid
- [FIX] Fixed Adjust and Spec Ops not showing anything on the property grid

Version 2.1 Beta Build 4404

- [NEW] Added the Compound Creator Utility and corresponding user database structure (work in progress)
- [NEW] Added a Binary Data Regression utility (work in progress)
- [NEW] Added feature request #3409646 (Name given to Material Objects)
- [CHG] Rewritten solver logic increases calculation speed by 35% (Cavett sample)
- [FIX] Fixed Lee-Kesler Cp/Cv calculation
- [FIX] Fixed Distillation Curves Petroleum Characterization Utility
- [FIX] Fixed Pipe Segment model overall HTC calculation and liquid phase volume retrieval
- [FIX] Fixed Spanish translation of the Object Palette
- [FIX] Fixed bug #3409641 (Call to Terminate missing)
- [FIX] Fixed bug #3409637 (Collection Count method called twice in a row)
- [FIX] Fixed bug #3409628 (ICapeUtilities queried twice in a row)

Version 2.0 Build 4258

- [FIX] Fixed unit conversion from lbmol/h to mol/s
- [FIX] Fixed empty compound list array when adding DWSIM Property Packages to CAPE-OPEN simulators
- [FIX] Fixed calculation of activity coefficient when requested from external components (Excel/CAPE-OPEN)
- [FIX] Fixed Energy Stream parameter list (CAPE-OPEN)

Version 2.0 Build 4252

- [FIX] Fixed Compressor and Expander models for single component simulations
- [FIX] Fixed a bug that caused DWSIM to throw an exception on startup related to user-created unit systems
- [FIX] Fixed the negative temperature input bug on Heater and Cooler models

Version 2.0 Build 4251

- [FIX] Fixed the reference state for enthalpy and entropy calculations

Version 2.0 Build 4249

- [NEW] Excel Interface for Equilibrium and Property calculators
- [FIX] Minor bug fixes

Version 2.0 Build 4235

- [FIX] Fixed Shell and Tube Heat Exchanger shell side pressure drop calculation
- [NEW] Added an option to save simulations with password protection
- [CHG] Centralized flowsheet drawing surface (PFD)

Version 2.0 Build 4225

- [FIX] Fixed Shell and Tube Heat Exchanger model
- [FIX] Fixed Units of measure in Watch Panel
- [CHG] Changed update checker to show only a non-obtrusive link on the status panel

Version 2.0 Build 4220

- [NEW] Added a set of shapes for CAPE-OPEN Unit Operations in the flowsheet
- [FIX] Fixed CAPE-OPEN compliancy of the Steam Tables Property Package 
- [FIX] Corrected CAPE-OPEN Report Window behavior

Version 2.0 Build 4207

- [NEW] Support for CAPE-OPEN Unit Operations, Property Packages (1.0/1.1) and Plugins (Flowsheet Monitoring Objects)
- [NEW] DWSIM Property Packages can now be exposed to external CAPE-OPEN compliant simulators
- [NEW] Inside-Out Three-Phase (VLLE) Flash Algorithm
- [NEW] PC-SAFT (without association term) Property Package
- [NEW] UNIFAC Property Package with Liquid-Liquid interaction parameters
- [NEW] Liquid-Liquid Extractor operation mode for the Absorption Column
- [NEW] Three-Phase separation mode for the Vessel
- [NEW] ChemSep database automatic loading
- [NEW] Watch window - allows property monitoring from different objects at the same time
- [NEW] CAPE-OPEN Unit Reports window - view output from CAPE-OPEN Unit Operations
- [NEW] Updated flowsheet drawing theme
- [NEW] 'Send Error Info' button added to the Unhandled Exception window
- [NEW] New version checking tool - informs the user when a new version becomes available and downloads the setup file
- [FIX] General bug fixes and speed improvements

Version 1.8 Build 4101

- [NEW] 'Fouling Factor' calculation mode for the Heat Exchanger Shell and Tube model
- [NEW] Added non-linear solver IPOPT to the Optimizer
- [NEW] DWSIM now reads experimental liquid density and liquid thermal conductivity data for ChemSep components (enabled by default)
- [NEW] Added multiple selection capability to the flowsheet to enable moving multiple objects at once
- [NEW] Added a 'Snap to Grid' capability to the flowsheet for better object alignment
- [CHG] Reactivated flowsheet navigation through the arrow keys
- [CHG] Reactivated the quick connect tool on the flowsheet
- [CHG] General Heat Exchanger model improvements
- [FIX] Fixed 'lbmol/h' unit conversion from SI to English

Version 1.8 Build 4080

- [NEW] Model for rating Shell and Tube Heat Exchangers
- [NEW] Scripting support for pre- and post- Unit Op calculations
- [NEW] Console Output and Calculation Queue windows
- [NEW] "Fast mode" switch for Inside-Out Flash calculations

Version 1.8 Build 3947

- [NEW] Paste from Excel function (Ctrl-V) added to the Composition Editor
- [FIX] Fixed liquid density and water content calculations in Peng-Robinson IWVT Property Package
- [FIX] Fixed Grayson-Streed fugacity calculation
- [CHG] Added ChemSep component support to the COSMO-SAC Property Package

Version 1.8 Build 3938

- [NEW] New converter in Property Grid for some units (temperature, pressure, flow rates, etc.)
- [NEW] New gauge pressure units
- [FIX] Fixed a cut/paste bug in the script editor
- [FIX] Fixed wrong molar/mass fraction values in flowsheet tables

Version 1.8 Build 3922

- [NEW] COSMO-SAC Property Package based on the JCOSMO library (http://code.google.com/p/jcosmo/)
- [CHG] Improvements to the Custom UO script editor
- [CHG] Updated Plugin Interface definition

Version 1.8 Build 3908

- [NEW] Added IronPython, IronRuby, VBScript and JScript scripting support
- [NEW] Added a new Unit Operation: Custom UO, which lets the user run scripts as an unit operation calculation routine

Version 1.7 Build 3875

- [NEW] Added German translation by Rainer Gollnitz

Version 1.7 Build 3868

- [NEW] Interface definition for external plugins
- [FIX] Updated Rigorous Column solvers (Inside-Out and Simultaneous Correction)
- [FIX] Updated Critical Point calculation

Version 1.7 Build 3850

- [NEW] Lee-Kesler-Pl�cker Property Package
- [FIX] Fixed K-value calculation call in the Sum Rates method for solving Absorption Columns
- [FIX] Fixed IO Flash calculation in single phase region
- [FIX] Fixed Critical Point calculation with PR and SRK Equations of State
- [FIX] Fixed portions of GUI language that were not being set on the first run

Version 1.7 Build 3840

- [NEW] Gibbs Reactor model (vapor phase only) with two solving methods: reaction extents and direct minimization
- [NEW] New global settings for Property Packages: Flash Algorithm and Calculate Bubble/Dew points
- [NEW] New approach for equilibrium calculation: Inside-Out by Boston and Britt
- [NEW] Added an option to adjust Rackett Parameters and Acentric Factors to match SG and NBP in Petroleum Characterization Utilities
- [NEW] New Quick Settings toolbar: Unit system and number formatting
- [NEW] New menu gives quick access to DWSIM Tools
- [NEW] Added the option to select stream component amounts as properties to show on PFD tables
- [CHG] Completely rewritten Equilibrium Reactor model
- [CHG] Updated ChemSep database loading code to support ChemSep 6.62
- [FIX] Fixed an exception when working with pump curves to calculate pump power
- [FIX] Fixed a bug in temperature calculation with the Steam Tables Property Package

Version 1.6 Build 3756

- [FIX] Fixed Conversion Reactor calculation with more than one reaction in parallel
- [FIX] Fixed Separator Vessel calculation under normal flash settings (Force PH flash option not selected)

Version 1.6 Build 3752

- [NEW] Component Separator Unit Op
- [NEW] Orifice Plate Unit Op
- [CHG] Improvements to the Optimizer, new solvers, more control options
- [CHG] Changed object insertion method from strip buttons to menu items, enhanced object palette to include new view modes
- [FIX] Fixed problems with rigorous column stream connections
- [FIX] Fixed Pump curves feature
- [FIX] Fixed Shortcut Column unhandled exception

Version 1.6 Build 3676

- [NEW] Added new rigorous column specification options
- [NEW] New rigorous column solving method: Napthali-Sandholm Simultaneous Correction (SC)
- [NEW] Added curves support to the Pump unit op
- [NEW] Added expression support to the Sensitivity Analysis Utility
- [FIX] Corrected Cooler and Heater heat exchanged sign
- [CHG] Various Flowsheet improvements (new font and color scheme, new table style, calculation indicator, improved connector drawing algorithm) 
- [CHG] General improvements to the IO Method, new configuration options
- [CHG] Added co/countercurrent flow direction selector to the Heat Exchanger
- [FIX] Fixed Pipe heat loss calculations
- [FIX] Fixed some translation errors
- [FIX] Removed degree symbol from Celsius temperature unit to improve compatibility with foreign languages
- [FIX] Fixed PH Flash setting in Thermo & Reactions configuration section not being effective
- [FIX] Optimizer/Sensitivity utilites: Fixed Material Streams' molar and volumetric flow changes not being effective
- [FIX] Fixed temperature calculations in the Steam Tables property package
- [FIX] Fixed Heat Exchanger Area calculation
- [FIX] Fixed Pipe properties not showing when the GUI language is set to Spanish

Version 1.6 Build 3618

- [FIX] Fixed control placement in the composition editor when using Spanish locale settings

Version 1.6 Build 3605

- [NEW] Added Spanish GUI translation (many thanks to Abad Lira and Gustavo Le�n!)
- [NEW] Added a Multivariate, Constrained Optimization utility
- [NEW] Added a Sensitivity Analysis utility supporting up to 2 independent variables
- [NEW] Added "command-line run mode" (read the documentation for more details)
- [NEW] Added a 'Write to the Flowsheet' capability to the Spreadsheet
- [NEW] New Property Packages: Chao-Seader, Grayson-Streed, Modified UNIFAC (Dortmund) and Peng-Robinson with support for immiscible water and Volume Translation
- [NEW] New Energy Recycle unit operation
- [NEW] Added more units for the most commom properties (temperature, pressure, etc.)
- [NEW] Added code to display a message when the flash algorithm converges to the trivial solution
- [CHG] Redesigned UNIFAC group structure to include all available groups
- [CHG] Report can now show all properties from all objects in the flowsheet
- [CHG] Removed single-phase and phase change limitations from the Heat Exchanger
- [CHG] Property Package selection interface now groups packages by type
- [CHG] Changed component selection interface
- [FIX] Fixed bugs and made minor changes to the code/interface

Version 1.5 Build 3399

- [FIX] Corrected Ideal Gas Enthalpy/Entropy calculation for CheResources and ChemSep database components
- [FIX] Corrected heat duty sign in Equilibrium Reactor
- [FIX] Corrected a unit inconsistency in Rigorous Column heat balances

Version 1.5 Build 3398

- [NEW] Added component volumetric fraction / volumetric flow information to Material Streams
- [NEW] Added a liquid density calculation mode to EOS-specific Property Packages (EOS or Rackett) - this also affects partial volume calculations
- [FIX] Fixed bug #2750848 (null reference error when adding components)
- [FIX] Fixed the 'Recalculate All' calculator feature - it wasn't recalculating input streams
- [FIX] Fixed some errors in the Equilibrium Reactor calculation routine
- [FIX] Fixed some errors when connecting product streams to Absorbers
- [FIX] Added a default value of zero for the UNIFAC groups in the Component Creator to avoid null reference errors

Version 1.5 Build 3377

- [NEW] New Calculator features: Break Calculation, Recalculate All and Clear Queue List
- [NEW] Added information about calculation time
- [FIX] Fixed positioning of rigorous column connections
- [FIX] Fixed UNIFAC Property Package configuration error
- [FIX] Fixed rigorous column 'Decalculate' routine
- [FIX] Fixed stability curve (phase envelope utility) calculation issues
- [FIX] Rigorous columns are now recalculated when editing properties in modal windows
- [CHG] Changed calculator behavior so the interface is more responsive

Version 1.5 Build 3372

- [FIX] Object selection by mouse dragging now updates the property grid correctly
- [FIX] Changed liquid viscosity mixing rule (for multicomponent systems)
- [FIX] Fixed an error in liquid mixture surface tension calculation
- [FIX] Fixed null Viscosity error in C7+ characterization tool
- [FIX] Fixed PRLK Property Package configuration error
- [FIX] Fixed a bug in rigorous column condenser connections
- [FIX] Fixed n-Butane database parameter (liquid viscosity)
- [FIX] Fixed rigorous column full reflux condenser behavior
- [FIX] Fixed vessel and tank null object reference error
- [CHG] Speed improvements in column IO method

Version 1.5 Build 3353

- [NEW] Redesigned component database system
- [NEW] Added two new Property Packages: NRTL/Peng-Robinson and UNIQUAC/Peng-Robinson
- [NEW] Added support for loading ChemSep(TM) databases
- [NEW] Added a tool to characterize petroleum fractions from ASTM/TBP distillation curves
- [NEW] Added a tool to insert user-defined components
- [NEW] Added the ability to use multiple property packages in a single simulation
- [NEW] Dockable help window with localized tips
- [NEW] New Unit Operations: Distillation Column, Absorption Column, Refluxed Absorber and Reboiled Absorber
- [NEW] Rigorous Column solving methods: Bubble-Point, Sum-Rates and Inside-Out
- [NEW] New utility for calculation of Petroleum Cold Flow Properties
- [CHG] Surface Tension is now correctly listed as a liquid phase property
- [CHG] Redesigned splash and welcome screens
- [FIX] Fixed high-pressure vapor viscosity calculation
- [FIX] Fixed liquid viscosity calculation error when supercritical components are present
- [FIX] Fixed flash calculation vapor fraction initialization error (should avoid some impossible solutions)
- [FIX] Fixed culture-specific error related to Property Package parameter storage
- [FIX] Fixed calculation of vapor phase thermal conductivity
- [FIX] Fixed a bug in internal calculation of heat capacity ratio
- [FIX] Fixed MRU filelist

Version 1.4

- [NEW] Added three new Unit Operations: Shortcut Column, Equilibrium Reactor and a basic Heat Exchanger
- [NEW] Added three new utilities: PSV/Vessel Sizing and Spreadsheet
- [CHG] New drag-and-drop feature for adding objects to flowsheet
- [CHG] Some cosmetic changes in the PFD
- [CHG] The critical point utility was modified to calculate multiple critical points when they exists. It's (still) not perfect, but it should work well in most cases.
- [CHG] Petroleum Characterization Utility: temperature-dependent properties are now calculated only when requested, greatly improving the speed of the pseudocomponent creation process.
- [FIX] Corrected the PFD object numbering bug

Version 1.3

- This version is the first to be released under the GPL v3 license
- Added support for English language and translation. Contact the developer for more information.
- Added reactions (conversion, kinetic, equilibrium) and reactors support (Conversion, PFR, CSTR)
- Removed Krypton Toolkit controls in order to mantain consistency of the GUI as a whole
- English reporting now works correctly

- Corrected many other bugs from the previous version