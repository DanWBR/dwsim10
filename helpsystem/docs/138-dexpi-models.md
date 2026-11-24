# DEXPI Models

DEXPI is the open exchange format of the process industry for P&IDs and process flow diagrams: a file that carries the equipment, the piping, the instrumentation and the process data as objects, with the drawing beside them, so that one engineering tool reads what another one wrote. A DEXPI file holds one of two models. A process model describes the process the way a simulator does: process steps, streams with their conditions and compositions, energy flows and control. A plant model is the content of a P&ID: equipment with nozzles, piping with its valves and fittings, instruments and control loops.

DWSIM works with both, in both directions, from the CAD Tools menu:

- **Export DEXPI Process Model:** writes the flowsheet as a process model.

- **Export DEXPI P&ID Model:** writes the P&ID of the flowsheet as a plant model.

- **Import Flowsheet from Drawing (PFD, P&ID):** builds a flowsheet from one DEXPI file or from several, and brings an existing flowsheet up to date with a new revision of its model.

The two exports are part of DWSIM and do not need SolidCraft, CADCraft or the AI Assistant. The import reads the files with dwsim-dexpi, a small program installed once, described next.

#### The dwsim-dexpi Program

DEXPI files are read by dwsim-dexpi, a free program distributed on its own. It is built on the pyDEXPI library and is licensed, like that library, under the GNU Affero General Public License; DWSIM only runs it and reads the file it writes. It reads DEXPI 2.0 and 2.0.1 files and the Proteus XML files of DEXPI 1.3, and does not need Python or anything else installed. To install it:

1.  Download the package for your system (Windows, Linux or macOS) from https://github.com/DanWBR/dwsim-dexpi/releases/latest.

2.  Unpack it into the `extenders/dexpi` folder under the folder where DWSIM is installed. The program can also stay in any folder named by the `DWSIM_DEXPI_HOME` environment variable, or in a folder of the `PATH`.

3.  Read the DEXPI file again.

When the program is missing, the import says so, with the download page and the folder to unpack it into, and offers to open the page. Each reading takes a few seconds, most of them for the program to start.

#### Exporting the Flowsheet as a DEXPI Process Model

CAD Tools \> Export DEXPI Process Model asks for a file name and writes the flowsheet as a DEXPI 2.0.1 process model (an .xml file). The flowsheet’s log says how many process steps, streams, energy flows and control loops were written, and what was left out. Solve the flowsheet first: the values written are the ones it holds at that moment. The file holds:

- **Process steps:** each unit operation as the DEXPI process step of its kind (a pump as pumping, a compressor as compressing, a heater, a cooler and a heat exchanger as their heat transfer steps, a column as distilling or absorbing, a reactor as reacting chemicals, a separator vessel as separating by flash, a valve as regulating flow, mixers and splitters, tanks, filters and pipes), named by its tag.

- **Ports:** one for each connected material stream, with its role where the model has one: the vapor and liquid outlets of a vessel, the top and bottom products of a column, the feeds.

- **Feeds and products:** the streams that enter and leave the flowsheet start at a source and end at a sink.

- **Streams:** temperature, pressure and mass flow, each marked as specified (a feed) or calculated, and the composition as mole fractions and as the mass flow of each compound. They are given for the stream as a whole and for each phase present (vapor, liquid, a second liquid and solid), with the flow, density, viscosity, volume flow and specific enthalpy of the phase.

- **Compounds:** listed once, each with its CAS number and, for the compounds of DWSIM’s databases that have them, the IUPAC name and the ChEBI identifier, the two identifiers the standard names a pure compound by. A compound added by the user, without a CAS number, is written by its name only.

- **Equipment data:** what the solved flowsheet has for each item: the pressure rise and head of a pump, the power and efficiency of a compressor or an expander, the duty of a heater or a cooler, the duty and area of a heat exchanger, the pressure drop of a valve, a pipe or an orifice, the volume of a CSTR or a PFR.

- **Rigorous columns:** the trays between the condenser and the reboiler, each feed and side draw on the tray it is on, the reflux ratio and the condenser and reboiler duties. A feed on the condenser or on the reboiler stage is written as a reflux or as a boil-up.

- **Energy streams:** each one as an energy flow with its value, thermal for heat duties and mechanical for the shaft power of pumps, compressors and expanders.

- **Control:** each PID controller as an instrumentation system: an activity that measures the pressure, temperature, flow or level of a stream or an item, an activity that controls it, with the setpoint, and the valve it acts on. A gauge is written as a measuring activity.

- **Diagram:** a block with the tag for each item where the flowsheet has it, and the streams drawn between their ports, so that the layout goes with the model.

Adjusts, specifications, Python and MPC controllers and other blocks without material streams are left out, and the log lists them.

#### Exporting the P&ID as a DEXPI Plant Model

CAD Tools \> Export DEXPI P&ID Model writes the P&ID of the flowsheet as a DEXPI 2.0.1 plant model, the form in which P&ID tools exchange their drawings. It says what the P&ID drawing of CAD Tools says, with the same tags, line numbers, nozzle marks and loop numbers. The file holds:

- **Equipment:** each unit operation as the equipment class of its kind, with its tag and one nozzle for each connected stream.

- **Pieces in the lines:** the valves, the mixers and splitters as tees, the relief valves and the orifice plates.

- **Lines:** each material stream as a line with its number, fluid code and nominal size, from the nozzle or piece it leaves to the one it enters. Feeds and products end at off-page connectors.

- **Instruments:** each controller and gauge as an instrument with its letters and loop number, the point it senses (an equipment nozzle or a line) and, for a controller that moves a valve, the actuator and the valve.

- **Design data:** the head, power and flow of a pump, the power of a compressor or a turbine, the duty and area of a heater or an exchanger and the volume of a tank or a vessel, from the solved flowsheet.

- **Diagram:** every item where the flowsheet has it, drawn as a plain outline with its tag, the lines with their numbers and the instrument bubbles with their signal lines.

A stream named after a line number, as the import of a P&ID names it (DN80-MNb-47121-75HB13), keeps that number, with its size, fluid code and piping class, in this model, in the P&ID drawing and in the line list. A flowsheet built from a P&ID therefore goes back out with the line numbers it came with. Energy streams, adjusts and other logical blocks are left out, and the notes in the flowsheet’s log say so. The symbols of the diagram are plain outlines, simpler than the ones of the P&ID drawing.

#### Importing a DEXPI Model

A DEXPI file is opened with CAD Tools \> Import Flowsheet from Drawing (PFD, P&ID), the wizard of the previous section: choose the .xml file on the Drawing page and click Read the drawing. The file is read as it is, so the AI Assistant is not needed and the same file always gives the same reading. The page then shows the model’s diagram, when the file has one, with a dot on each item, and the other four pages work as they do for a drawing: the equipment and the lines can be corrected, the compounds and the property package are chosen on the Process page, and the last page checks, builds, solves and compares.

##### From a process model

- **Equipment:** each process step as the DWSIM object of its kind, with the tag of the step. Sources and sinks become the open ends of the feeds and products. Shut-off and check valves and plain transport steps are passed through, and mixing and splitting steps become mixers and splitters.

- **Streams:** temperature, pressure and mass flow, and the composition, given as the mass flow of each compound, as mass fractions or as mole fractions. Mole fractions are turned into mass flows with the molar weights of the compounds once these are chosen on the Process page. When the model gives the phases of a stream, the share of the vapor goes with it and tells the vapor outlets from the liquid ones.

- **Outlets:** the role of each port (top or bottom product, vapor, liquid) puts the stream on the matching outlet of a vessel or a column.

- **Columns:** a column the model gives the trays of becomes a rigorous column with that number of stages between the condenser and the reboiler and with the reflux ratio of the model. A feed or a side draw whose port belongs to a tray goes on that tray; one the model leaves on the column itself goes on the middle stage, to be moved in the column’s editor.

- **Equipment data:** the duty, temperature, pressure, efficiency, pressure ratio, area, overall coefficient and volume a step gives for itself. It is used where the streams leave the item open: a heater or a cooler without an outlet temperature takes its duty, a heat exchanger without outlet temperatures takes its duty or its area and overall coefficient, a compressor without an outlet pressure takes its pressure ratio. The Check and build page lists each value taken from the model.

- **Control loops:** each instrumentation system that measures a variable and controls it becomes a PID controller that reads the stream or the item measured and moves the valve the controlling activity acts on.

- **Compounds:** by their names. A compound whose name the database does not have is looked up by the other names the model gives for it: its identifier, its IUPAC name, its ChEBI identifier, or a CAS number in its description. So a model that names water only as oxidane or as CHEBI:15377 still finds Water. The Check and build page says which compounds were found that way.

##### From a plant model (a P&ID)

- **Equipment:** the tagged equipment, by its class: pumps, compressors, heat exchangers, vessels, tanks, columns, filters and the others.

- **Valves:** the valves that an actuator moves become control valves, with the tag of their loop, and the safety valves become relief valves, each with its set pressure. Block valves, check valves, reducers and flanges are passed through.

- **Piping:** the lines between the items, each with its line number (size, fluid code, number and piping class). Dead-end branches, such as drains and instrument connections, are left out, and a tee with more than two lines becomes a mixer or a splitter. Off-page connectors are the open ends of the feeds and products.

- **Nozzles:** the height of each nozzle on its equipment, which tells the top from the bottom of a vessel, a tank or a column.

- **Design data:** the design duty and area of a heat exchanger, the design head of a pump and the capacity of a tank, used as the item’s specification. A pump takes the pressure rise of its head on water (1000 kg/m3), to be corrected for the fluid. The design pressure and temperature of each item are kept for the check described below.

- **Control loops:** each loop that controls pressure, temperature, flow or level becomes a PID controller that reads the line, the nozzle or the item its transmitter senses and moves its valve.

A P&ID does not carry temperatures, pressures, flows or compositions, so the flowsheet it gives is the equipment and its connections: enter the feeds and the specifications before solving, or read it together with a process model, as described next. The wizard treats it as it treats a P&ID drawing and leaves out the relief valves and the flare, drain and vent connections.

##### Several files as one plant

Several DEXPI files can be read as one plant: select them together in the file dialog, and they show in the box separated by semicolons. Two combinations are understood:

- **The sheets of a P&ID:** are joined where a line leaves one sheet at an off-page connector and enters another. The connectors are paired by the reference one makes to the other’s number, by a number they share, or by the line number of the lines they end, and only when one pair fits. The picture shows the sheets side by side.

- **A P&ID and a process model of the same plant:** the process model gives its stream conditions, flows and compositions and its equipment data to the lines and items of the P&ID with the same tags, so that the flowsheet has the detail of the P&ID (control valves, control loops, line numbers) and the data of the process model. A stream goes to the line that joins the same two items, through the pieces only the P&ID has. Junctions, which do not have tags on a P&ID, are matched by the items they join. The notes of the last page say how many items and streams were matched.

##### What the last page adds for a model

- **Control loops:** the PID controllers are built inactive. Once the flowsheet is solved, a pressure, temperature or flow loop takes the solved value of what it measures as its setpoint, and each controller takes the opening of its valve as the output it starts from. Set the tuning, and the setpoint of a level loop, before a dynamic run.

- **Design limits:** when a P&ID gives the design pressure and temperature of its equipment, the solved flowsheet is checked against them: the results list each stream on an item that is above its design pressure or temperature, or below its lowest ones. An exchanger is checked against the widest limits of its two sides.

- **Relief valves:** a relief valve comes with its set pressure, shown in the note that leaves it out. Ticked on the Equipment page, it is built as a Relief Valve object with that set pressure, fully open at 10 % above it.

- **Pipe segments:** when the lines give their size in the line number, Pipe segments for the sized lines makes a Pipe Segment of each one, named after the line, a straight run of 10 m with the diameters of its size, to be given its true route by hand or with Import Pipe Segment from Piping Isometric.

#### Updating a Flowsheet from a New Revision

A flowsheet built from a drawing or a DEXPI model can be brought up to date when a new revision arrives. Open the flowsheet, import the new revision with CAD Tools \> Import Flowsheet from Drawing (PFD, P&ID) and, on the Check and build page, tick Update the items with the same tags, add nothing. The option shows when the flowsheet already has items with the tags of the reading, and the button then reads Update the flowsheet.

The update finds each item by its tag, and each stream by its tag or by the two items it joins. The feeds take the temperature, pressure, flow and composition of the new revision, the items take its specifications (outlet temperatures and pressures, split ratios and the data of the equipment), and with Solve and compare with the table ticked the flowsheet is solved and compared with the stream data. The results list how many items and lines were found, the items and lines of the revision that the flowsheet lacks, and the items of the flowsheet that are absent from the revision. Those are left as they are: an update does not add, remove or reconnect anything, so a change in the equipment or in the piping is made by hand or with a fresh import.

#### Checks with the Samples

The exports and the import were checked against each other with the samples that come with DWSIM and with the reference P&ID of the DEXPI specification:

- **Cavett’s Problem, process model:** exported and imported back, it gives the same vessels, valves, compressors and mixers with the same streams, the three recycles are found again as loops, and the solved streams match the exported values. Recycle blocks are written as plain transport steps.

- **Cavett’s Problem, P&ID and process model together:** the two exported files, read as one plant, give back its 12 items and 17 streams with their data, and the rebuilt flowsheet solves to the exported values.

- **Update:** with the feed raised by 10 % in the exported model, the update found all 12 items and 17 streams, did not add anything, and after the solve every stream flow was 10 % higher.

- **Columns:** the two columns of the Extractive Distillation sample come back as rigorous columns with their 18 and 38 trays, their reflux ratios and their feeds on the stages they had. The rebuilt flowsheet still needs the estimates of its recycle to solve, which a model does not carry.

- **Control:** the two controllers of the Separator Pressure and Level Control sample come back on their valves, the pressure loop with the solved pressure as its setpoint.

- **Reference P&ID:** imported and exported again, it kept the line numbers of all the lines that were built, and its two control loops became controllers on their valves.

#### DEXPI Files and the AI Assistant

The AI Assistant can work on a DEXPI file next to the simulation through the server that comes with pyDEXPI (the Model Context Protocol): it loads the file, looks items up by tag, follows the piping and lists the equipment, the lines and the instruments, so that a question such as which lines reach a tank, or how the design pressures of a P&ID compare with the pressures of the flowsheet, can be asked in the chat. pyDEXPI is installed on its own (`pip install "pydexpi[mcp]"`, with Python 3.12 or newer) and added to the `mcp_servers.json` file of the assistant as a server whose command is `pydexpi-mcp`.

#### From Scripts

The same tools run from scripts, with the flowsheet of the Automation API or the `Inner` flowsheet of the FluentAPI:

- **CadExport.ExportDexpi(flowsheet, file):** writes the process model.

- **CadExport.ExportDexpiPid(flowsheet, file):** writes the P&ID model.

- **ImportFromDrawing(flowsheet, file, ...):** of the CAD imports takes a DEXPI .xml file where it takes a drawing or a saved reading; several files go in one text, separated by semicolons, and `pipeSegments: true` makes the pipe segments.

- **UpdateFromDrawing(flowsheet, file, ...):** brings the flowsheet up to date from a new revision.

The dwsim-dexpi program runs on its own too: `dwsim-dexpi model.xml -o model.reading.json` writes the reading as a JSON file, which the wizard opens with Open a saved reading, and several files can be given at once. When a model gives its values for several cases (a design case, a summer and a winter case), the value without a case is read, else the first one; `--case NAME` reads the values of that case, and the notes of the reading list the cases the model has.

#### Limitations

- **Property package and reactions:** a DEXPI model does not have either. The import asks for the compounds and the property package on the Process page, and reactors are built without reactions.

- **Compound names:** a model that names its compounds with letters or internal codes, and does not give a CAS number or another identifier, needs them chosen by hand.

- **Incomplete models:** many DEXPI process models carry conditions for some of the streams only. The checks list the feeds that lack temperature, pressure or flow.

- **Recycles and estimates:** a model does not carry the estimates a recycle or a column starts from, so a flowsheet with recycles between columns may need them entered before it solves.

- **Pumps:** the head of a pump becomes a pressure rise with the density of water.

- **Control loops:** a loop is read with one measurement and one valve and comes inactive; a level loop keeps the default setpoint, since a steady state does not hold liquid in a vessel. Controllers other than PID are left out of the export.

- **Relief valves:** a relief valve is built on its own; it is not connected to a flare network.

- **Heat exchangers:** the two sides are written as plain inlets and outlets, and found again on import by their temperatures.

- **Cases:** the import wizard reads one case; another case is read with the dwsim-dexpi program and opened as a saved reading.

- **P&ID export:** the symbols are plain outlines, and the insulation mark of a line number is written as the insulation type of the line.

