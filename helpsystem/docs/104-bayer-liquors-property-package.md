# Bayer Liquors property package

The package is built on the activity coefficient base of DWSIM. It reads the liquor as water and dissolved salts (NaOH, sodium aluminate, $\ce{Na2CO3}$, $\ce{Na2SO4}$, NaCl, sodium oxalate and the organic salts), and the minerals, the hydrate and the alumina as solids.

#### Properties {#properties-.unnumbered}

- **Density:** water plus the apparent molar volumes of NaOH and sodium aluminate (Young’s rule), fitted to the data of Königsberger et al. \[5\] and Hnědkovský et al. \[6\]. The other sodium salts take the volume of NaOH per mol of sodium.

- **Heat capacity:** the correlation of Langa \[3\], through SysCAD.

- **Heats of solution:** the NaOH table of NBS \[4\] against the water to salt ratio, and the heats of solution of the other salts. The heat of solution of $\ce{NaAlO2}$ in dilute NaOH agrees with Coulier and Tremaine \[7\] within 3 kJ/mol.

- **Boiling point elevation and water activity:** Dewey \[3\], through SysCAD, on the molality of the salts per kg of free water. A liquor at T boils under the saturation pressure of water at T minus the boiling point elevation.

- **Gibbsite solubility:** the equilibrium alumina $A^{*}$ of Rosenberg and Healy \[8\], with the effect of the carbonate, chloride, sulfate and organic carbon. Misra \[9\] is available as a check.

- **Oxalate solubility:** Beckham and Grocott \[10\], through SysCAD.

- **Viscosity:** the EMA correlation with the Alexandrov ratio for the caustic molality, and a user factor.

#### What to expect {#what-to-expect-3-.unnumbered}







| Check | Result | Source |
|:---|:--:|:--:|
| Density, 124 points | 2.4 kg/m<sup>3</sup> rms | \[5\] |
| Density up to 573 K | 4.7 kg/m<sup>3</sup> rms | \[6\] |
| Heat capacity, five liquors | within 0.001 kJ/(kg K) | \[3\] |
| Gibbsite solubility, model column of Table 6 | within 0.4 g/L | \[8\] |
| Gibbsite solubility, 13 measured liquors | 1.0 g/L rms (paper: 1.2) | \[8\] |
| Boiling point elevation of the flash, 0.2 to 5 bar | within 0.02 K of Dewey | \[3\] |



