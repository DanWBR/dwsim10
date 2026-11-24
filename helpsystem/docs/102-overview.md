# Overview

The alumina tools model a Bayer refinery for gibbsitic bauxite with low-temperature digestion, from the bauxite to the calcined alumina and around the liquor loop. They are part of the Patreon edition, for Premium and Premium+ members, in the Classic and in the Cross-Platform interfaces:

- **Bayer Liquors**, a property package for the caustic aluminate liquors, the bauxite minerals, the red mud, the hydrate and the alumina;

- the **Bayer Stream Analysis** utility, which reads a stream in the quantities of a refinery and writes a stream from them;

- eight unit operations: **Bauxite Digester**, **Mud Settler**, **Mud Washer Train**, **Precipitator Train**, **Hydrate Calciner**, **Liquor Evaporator**, **Bayer Causticizer** and the **Oxalate Removal**, which build on the base of the pulp and paper unit operations;

- the *Gibbsitic bauxite alumina refinery* case of the Case Library, which joins them.

The tools cover gibbsitic bauxite and digestion at about 140 to 150 °C. High-temperature digestion of boehmitic and diasporic bauxite, the particle size distribution of the hydrate, grinding and classification aren’t modeled.

#### Liquor conventions {#liquor-conventions-.unnumbered}

The liquor quantities follow the North American convention, all at 25 °C:

- **C (caustic):** NaOH and sodium aluminate, as g/L of $\ce{Na2CO3}$;

- **A (alumina):** the dissolved alumina, as g/L of $\ce{Al2O3}$;

- **S (total soda):** C plus the sodium carbonate, as g/L of $\ce{Na2CO3}$;

- **A/C** and **C/S**, and the molar ratio $\alpha_{c}=0.96197/(A/C)$.

