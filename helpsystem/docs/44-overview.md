# Overview

The Reservoir Coupling tool runs a reservoir simulation together with the gathering network and the process plant of a DWSIM flowsheet. The reservoir is solved by OPM Flow, the open source black-oil simulator of the Open Porous Media initiative, from an ordinary reservoir model file, which reservoir engineers call a deck (`.DATA` file). The surface is the open flowsheet: a Pipe Network that gathers the wells, followed by whatever equipment the simulation has.

The two models exchange information once per report step of the reservoir model:

1.  The reservoir runs one report step with a tubing head pressure (THP) limit on each producer and returns the oil, gas and water rates of each well.

2.  The rates become a compositional stream at each wellhead, the flowsheet is solved, and the Pipe Network returns the pressure each wellhead needs to deliver its production.

3.  That pressure, under-relaxed, is the THP limit of the well in the next report step.

4.  Values read from the solved flowsheet can also be sent to the reservoir for the next step: an injection rate, a production limit of a group, or the lift gas available to the wells.

The exchange is explicit: each side sees the other with a delay of one report step. Short report steps and the relaxation factor keep the two consistent.

The tool is part of the Patreon edition, for Premium+ members. It is the item **Reservoir Coupling (OPM Flow)** of the **Tools** menu of the flowsheet, in the Classic and in the Cross-Platform interfaces.

#### What is needed

A Linux environment for OPM Flow:  
on Windows the reservoir simulator runs inside the Windows Subsystem for Linux (WSL), which has to be installed with a distribution such as Ubuntu. On Linux it runs directly. OPM Flow does not have a package for macOS.

Python 3 in that environment:  
with the `venv` module (`sudo apt install python3 python3-venv` on Ubuntu).

OPM Flow:  
installed by the tool itself, from the Python Package Index, into a Python environment of its own (about 300 MB). OPM Flow is free software under the GPL and is not distributed with DWSIM.

A reservoir model:  
in which every producer to be coupled has a THP limit and a lift table (`VFPPROD`) assigned in `WCONPROD`.

A flowsheet:  
with a Pipe Network that has one Source block per producer, each attached to a material stream, and two material streams that give the composition of the stock tank oil and of the separator gas.

