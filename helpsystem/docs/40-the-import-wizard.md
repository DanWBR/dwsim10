# The Import Wizard

The wizard is laid out as the Simulation Wizard: the steps are listed on the left, and Back and Next move between them. Nothing is built until the last step.

#### Simulation file

Choose the file with **Browse**. The wizard shows which simulator and version wrote it and whether it is ready to be read. **Read the file** reads the case and prepares the conversion. The table below it lists what the reader and the converter have to say about the case as a whole: sections that were skipped, compounds without a match, units of measure that were assumed.

The same page shows the model the AI Assistant answers with and a link to its settings. With **Ask the AI Assistant about what could not be converted** checked, the Assistant is asked for the items the importer left open: a compound it could not match, a property method with no package set, the kinetics of a reaction, a unit with no equivalent. Its answers enter the conversion as suggestions and are listed in the table. The option comes checked when the Assistant is set to a model that does not run on the computer; a local model seldom answers these questions well.

#### Units

One row for each unit operation of the source: its name, its type, what it becomes in DWSIM, the state of the conversion and the remarks. Read the remarks of the rows in colour: they say what was assumed and what has to be checked or set by hand after the import.

Clear the box in the **Build** column to leave a unit out. This is useful when a unit is known to be troublesome, or to import a part of a large flowsheet first.

#### Streams

The material streams, where each one comes from and goes to, and its temperature, pressure and molar flow. A feed shows what the source specifies. The other streams show what the source computed, when the file has results.

#### Thermodynamics

The upper table matches the compounds of the source to the compounds of DWSIM. Most are matched by their CAS number or by name. For a compound that was not matched, type the name it has in DWSIM or its CAS number in the row.

The lower table shows the property package chosen for the property method of the source. Pick another package in the list to change it.

#### Build

Choose where the DWSIM simulation is saved and click **Build and solve**. The table shows the result: how many objects were built, whether the flowsheet solves, the errors of the solver, and the temperature and flow of the streams next to the values of the source.

With **Try to repair what does not solve** checked, the importer makes several attempts before it shows the result. It may redraw the flowsheet when the source has no usable drawing, give the solver more time, give a column more iterations, ask the AI Assistant for a repair, and as a last resort leave out the unit that fails. Each attempt is listed, and the best one is kept. These repairs never change process data: the conditions of a feed and the specifications of a unit stay as they are in the source. When the Assistant thinks one of them is wrong, it says so in a proposal that is shown and left for you to apply.

**Finish** closes the wizard and opens the saved simulation.

