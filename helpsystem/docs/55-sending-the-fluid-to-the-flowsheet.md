# Sending the fluid to the flowsheet

**Send to flowsheet** writes the fluid of the last run or fit in the open flowsheet:

- the defined components become compounds of the database, and the pseudocomponents become petroleum pseudocompounds, all with the critical properties, acentric factors and molar masses of the fluid;

- a property package of the equation of state of the study, tagged `PVT Analysis`, receives every interaction parameter and the volume translations;

- a material stream tagged `PVT fluid` receives the composition, at the reservoir temperature and the saturation pressure.

Sending again updates the same compounds, package and stream. The flash algorithm of the flowsheet is not changed. Very near the critical point of the fluid the flash of the flowsheet can differ from the one of the tool.

