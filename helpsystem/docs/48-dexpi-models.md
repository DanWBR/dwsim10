# DEXPI Models

DEXPI is the open exchange format of the process industry for P&IDs and process flow diagrams: a file that carries the equipment, the piping, the instrumentation and the process data as objects, with the drawing beside them, so that one engineering tool reads what another one wrote. DWSIM writes the flowsheet as a DEXPI model and builds a flowsheet from one. A DEXPI file holds one of two models: a process model, which describes the process the way a simulator does (process steps, streams with their conditions and compositions), or a plant model, the content of a P&ID (equipment with nozzles, piping with its valves and fittings, instruments and control loops).

#### Exporting the Flowsheet as a DEXPI Process Model

CAD Tools \> Export DEXPI Process Model asks for a file name and writes the flowsheet as a DEXPI 2.0.1 process model (an .xml file). It needs neither SolidCraft nor CADCraft. The flowsheet’s log says how many process steps, streams and compounds were written, and what was left out. The file holds:

- **Process steps:** each unit operation as the DEXPI process step of its kind (a pump as pumping, a compressor as compressing, a heater, a cooler and a heat exchanger as their heat transfer steps, a column as distilling or absorbing, a reactor as reacting chemicals, a separator vessel as separating by flash, a valve as regulating flow, mixers and splitters, tanks, filters and pipes), named by its tag.

- **Ports:** one for each connected material stream, with its role where the model has one: the vapor and liquid outlets of a vessel, the top and bottom products of a column, the feeds.

- **Feeds and products:** the streams that enter and leave the flowsheet start at a source and end at a sink.

- **Streams:** temperature, pressure and mass flow, and the composition as mole fractions and as the mass flow of each compound. The compounds of the simulation are listed once, with their CAS numbers.

- **Equipment data:** what the solved flowsheet has for each item: the pressure rise and head of a pump, the power and efficiency of a compressor or an expander, the duty of a heater or a cooler, the duty and area of a heat exchanger, the reflux ratio and the condenser and reboiler duties of a rigorous column, the pressure drop of a valve, a pipe or an orifice, the volume of a CSTR or a PFR.

- **Diagram:** a block with the tag for each item where the flowsheet has it, and the streams drawn between their ports, so that the layout goes with the model.

Solve the flowsheet before exporting: the stream values and the equipment data are the ones the flowsheet holds at that moment. Energy streams are left out (the duties are on the equipment), and so are controllers, adjusts, specifications and other blocks without material streams; the log lists them.

#### Importing a DEXPI Model

A DEXPI file is opened with CAD Tools \> Import Flowsheet from Drawing (PFD, P&ID), the wizard of the previous section: choose the .xml file on the Drawing page and click Read the drawing. DEXPI 2.0 and 2.0.1 files and the Proteus XML files of DEXPI 1.3 are read. The file is read as it is, so the AI Assistant is not needed and the same file always gives the same reading. The page then shows the model’s diagram, when the file has one, with a dot on each item, and the other four pages work as they do for a drawing.

The file is read by dwsim-dexpi, a free program distributed on its own (it is built on the pyDEXPI library, under the GNU Affero General Public License). To install it:

1.  Download the package for your system (Windows, Linux or macOS) from https://github.com/DanWBR/dwsim-dexpi/releases/latest.

2.  Unpack it into the extenders/dexpi folder under the folder where DWSIM is installed. The program can also stay in any folder named by the DWSIM_DEXPI_HOME environment variable, or in a folder of the PATH.

3.  Read the DEXPI file again.

When the program is missing, the wizard says so, with the download page and the folder to unpack it into. The first reading after the program starts takes a few seconds longer.

From a process model the reading takes:

- **Equipment:** each process step as the DWSIM object of its kind, with the tag of the step. Sources and sinks become the open ends of the feeds and products. Shut-off and check valves and plain transport steps are passed through, and mixing and splitting steps become mixers and splitters.

- **Streams:** temperature, pressure and mass flow, and the composition, given as the mass flow of each compound, as mass fractions or as mole fractions. Mole fractions are turned into mass flows with the molar weights of the compounds once these are chosen on the Process page.

- **Outlets:** the role of each port (top or bottom product, vapor, liquid) puts the stream on the matching outlet of a vessel or a column.

- **Equipment data:** the duty, temperature, pressure, efficiency, pressure ratio, reflux ratio, area, overall coefficient and volume a step gives for itself, and the trays of a column. They are used where the streams leave the item open: a heater or a cooler without an outlet temperature takes its duty, a heat exchanger without outlet temperatures takes its duty or its area and overall coefficient, a compressor without an outlet pressure takes its pressure ratio. The Check and build page lists each value taken from the model.

From a plant model (a P&ID) the reading takes:

- **Equipment:** the tagged equipment, by its class: pumps, compressors, heat exchangers, vessels, tanks, columns, filters and the others.

- **Valves:** the valves that an actuator moves become control valves, with the tag of their loop, and the safety valves become relief valves. Block valves, check valves, reducers and flanges are passed through.

- **Piping:** the lines between the items, each with its line number (size, fluid code, number and piping class). Dead-end branches, such as drains and instrument connections, are left out, and a tee with more than two lines becomes a mixer or a splitter. Off-page connectors are the open ends of the feeds and products.

- **Nozzles:** the height of each nozzle on its equipment, which tells the top from the bottom of a vessel, a tank or a column.

- **Control loops:** each loop that controls pressure, temperature, flow or level becomes a PID controller that reads the stream or the vessel it measures and moves its valve. The controllers are built inactive: enter the setpoint and the tuning before a dynamic run.

A P&ID carries no temperatures, pressures, flows or compositions, so the flowsheet it gives is the equipment and its connections: enter the feeds and the specifications before solving. The wizard treats it as it treats a P&ID drawing, leaving out the relief valves and the flare, drain and vent connections.

An export followed by an import brings the flowsheet back: the Cavett’s Problem sample, written as a DEXPI process model and read again, gives the same vessels, valves, compressors and mixers with the same streams, the three recycles are found again, and the solved streams match the exported values. Recycle blocks are written as plain transport steps and found again by the import as loops.

#### From Scripts

CadExport.ExportDexpi(flowsheet, file) writes the DEXPI process model, and the ImportFromDrawing function of the CAD imports takes a DEXPI .xml file where it takes a drawing or a saved reading. The dwsim-dexpi program runs on its own too: dwsim-dexpi model.xml -o model.reading.json writes the reading as a JSON file, which the wizard opens with Open a saved reading.

#### Limitations

- **Property package and reactions:** a DEXPI model has neither. The import asks for the compounds and the property package on the Process page, and reactors are built without reactions.

- **Compound names:** the compounds of a process model are matched to the database by name. A model that names its compounds with letters or internal codes needs them chosen by hand.

- **Incomplete models:** many DEXPI process models carry conditions for some of the streams only. The checks list the feeds that lack temperature, pressure or flow.

- **Equipment data:** the head of a pump is read but not used, and the data of rigorous columns and of items the model gives no class for is left for the user to enter.

- **Control loops:** loops are read from P&ID models only, with one measurement and one valve each.

- **Export:** the overall composition of each stream is written, without the separate phases, and the compounds carry their names and CAS numbers, without the ChEBI and IUPAC identifiers of the standard. The two sides of a heat exchanger are written as plain inlets and outlets.

