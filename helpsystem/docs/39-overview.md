# Overview

The simulation importer reads a case made in another process simulator and builds the same flowsheet in DWSIM: the compounds, the property package, the streams with their conditions, the unit operations with their specifications and the connections between them. It then solves the flowsheet and, when the source file carries results, compares the two solutions stream by stream.

The importer is part of the Patreon edition and is open to every membership level. It is the item **Import files from other process simulators** of the **File** menu, in the Classic and in the Cross-Platform interfaces. In the Cross-Platform interface the menu belongs to the simulation window, so the item shows once a flowsheet is open.

The importer reads the files themselves. It was built without the programming interface (API) of any of the other programs, and it needs none of them installed on the computer. The names of the simulators are trademarks of their owners.

#### Files that can be imported







| **Simulator** | **File** | **How to get it** |
|:---|:---|:---|
| Aspen Plus | `.bkp` | In Aspen Plus, save the case as a backup file. The document format (`.apw`) is binary and has to be exported as a backup first. |
| Aspen HYSYS | `.xml` | In HYSYS, save the case as XML. The case format (`.hsc`) is binary and has to be saved as XML first. |
| AVEVA Process Simulation | `.simx` | The simulation file as it is. |
| COCO (COFE) | `.fsd` | The flowsheet document as it is. |
| PRO/II | `.prz`, `.inp` | The archive of the case, which carries its keyword file, or the keyword file exported from PRO/II. |



The importer tells the format from the content of the file, so a renamed file is still recognized. When a file is of a known program and cannot be read as it is, the first page of the wizard says how to export it.

#### What a conversion gives

A converted case is an ordinary DWSIM simulation, saved as a `.dwxmz` file. Nothing is changed in the source file. Every decision of the conversion is shown before anything is built, with one of four states:

Converted:  
the unit has a direct equivalent in DWSIM and takes the specification of the source as it is.

Approximate:  
the unit was converted with a difference that the remarks explain, for instance a modified equation of state replaced by the standard one, or a specification taken from the solved streams of the source.

Suggested:  
the choice came from the AI Assistant and is to be reviewed.

Not converted:  
DWSIM has no equivalent set for the unit, or its specification could not be translated. A placeholder takes its place.

A unit that is left as a placeholder is not calculated. The streams that leave it keep the values the source file has for them, so that the rest of the flowsheet can still be built and solved.

