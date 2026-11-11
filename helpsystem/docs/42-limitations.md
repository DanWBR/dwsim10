# Limitations

- A flowsheet that solves in the source may need work to solve in DWSIM. The two programs differ in their column algorithms, in how they converge recycles and in the parameters of their property packages. Read the errors on the Build page and the remarks on the Units page first.

- Results differ where the property models differ. The comparison on the Build page shows by how much.

- A PRO/II keyword file holds the input of the case and no results, so there is nothing to compare the solution with, and a unit left as a placeholder has no values to pass downstream.

- Hypothetical and petroleum components are created from the properties in the file when molar weight, boiling point and density are all there. Otherwise they have to be created by hand.

- Kinetics are converted for power-law reactions. Other rate expressions are kept as text in the conversion for you to enter in DWSIM.

- Columns with side draws, pumparounds or side strippers are left as placeholders.

- Dynamic data, costing, equipment sizing and the studies of the source (sensitivity, optimization, case studies) are left out.

- The positions of the units on the drawing are kept when the file has them. When it does not, the repair option of the Build page lays the flowsheet out.

