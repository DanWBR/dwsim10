# Limitations

- The equations of state are the cubic ones. The tool does not have association or asphaltene models.

- The equilibrium is between two fluid phases. Water, a second liquid and solids (wax, asphaltenes, hydrates) are outside the calculations.

- The minimum miscibility pressure assumes two fluid phases in every contact. Carbon dioxide with heavy oils at low temperature can form a second liquid, and the result is then not reliable; an irregular curve of the shortest tie line against pressure is the sign of it.

- The fluid sent to the flowsheet does not carry the viscosity model of the study: the flowsheet uses the viscosity methods of its property package.

- The compositional export follows the keywords of Eclipse-type simulators; other formats are not written.

