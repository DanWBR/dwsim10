# Kraft Liquors property package

The package is built on the activity coefficient base of DWSIM, like Sugar Solutions. The dissolved solids of a liquor are lumped by their dry solids content.

#### Roles of the compounds {#roles-of-the-compounds-.unnumbered}

- **Water**.

- **Dissolved solids:** the salts of the electrolyte database, the *Pulp* and *Sugar* pseudo-compounds, and any compound that boils above 800 K. They don’t precipitate, so they stay in the liquor or in the molten smelt.

- **Solids:** wood, pulp and lime, and the calcium salts of the lime mud. They are forced solids.

- **Volatile compounds:** everything else (methanol, the reduced sulfur gases, air and flue gas), with NRTL among themselves and with water.

#### Water activity and boiling point rise {#water-activity-and-boiling-point-rise-.unnumbered}

The dry solids content $S$ is the mass of the dissolved solids over the water and dissolved solids of the liquid. The activity of water is the one that puts the normal boiling point of the liquor at the atmospheric boiling point rise of the correlation, and it moves to other temperatures with the partial excess enthalpy of water (Gibbs-Helmholtz):



\[
\ln a_{w}(T)=\ln\frac{101325}{P^{sat}_{w}(T_{b})}+\frac{\bar{h}^{E}_{w}}{R}\left(\frac{1}{T}-\frac{1}{T_{b}}\right),\qquad T_{b}=373.124+\Delta T_{b}(S)
\]


Two boiling point rise correlations are available. **Eucalyptus**, the default, is the correlation of Ferreira et al. \[9\] for E. globulus liquor up to 72 % solids, with $X=S/(1-S)$:



\[
\Delta T_{b}=7.454\,X\quad(S<0.43),\qquad\Delta T_{b}=3.231+3.148\,X\quad(S\geq0.43)
\]


**Generic** is $\Delta T_{b}=BPR_{50}\cdot9.1\,S/(8.1-7.1\,S)$ \[10\], with $BPR_{50}=7.5$ K by default.

#### Enthalpy and other properties {#enthalpy-and-other-properties-.unnumbered}

- **Heat capacity and enthalpy:** the form of the IR3 report of Fricke et al. \[8\], fitted to the 32 enthalpies of its Table 2 (10 to 80 % solids, 45 to 120 °C, rms 0.52 kJ/kg), with $t$ in °C and $x$ the solids mass fraction:


\[
c_{p}=c_{p,w}(1-x)+(1.5420+0.009850\,t)\,x+(2.5611-0.047927\,t)(1-x)\,x^{2.5638}
\]


  The heat of dilution to infinite dilution at 80 °C is $Q(x)=222.23\,x-84.29\,x^{2}+43.34\,x^{3}$ kJ/kg of solids, and the excess enthalpy of the liquor is $x\,(Q(x)-Q(1))$ at 80 °C plus the excess heat capacity integrated from 80 °C.

- **Density:** $\rho_{25}=997+649\,S$ (Frederick) up to 60 % solids, and a second segment fitted to Karlsson et al. \[11\] above it, with the temperature factor of Frederick.

- **Viscosity:** $\ln(\mu/\mu_{w})=(6.6359\,S-14.8325\,S^{2}+25.5113\,S^{3})\left(1+1.3625\,(373.15/T-1)\right)$, fitted to Karlsson et al. \[11\] and Clay \[10\]. Black liquor viscosity varies widely from mill to mill, so the package has a viscosity factor to match a measured liquor.

- **Thermal conductivity and surface tension:** Clay \[10\] and Ferreira et al.

The package settings (boiling point rise correlation, $BPR_{50}$ and the viscosity factor) are edited in the Pulp Stream Analysis window.

#### What to expect {#what-to-expect-2-.unnumbered}







| Quantity | Package | Reference |
|:---|:--:|:--:|
| Boiling point rise at 1 atm, 15 to 72 % solids | within 0.02 K | \[9\] |
| Boiling point rise, generic table | within 0.04 K | \[10\] |
| Density, 44 to 85 % solids, 110 to 175 °C | within 1.4 % | \[11\] |
| Heat capacity at 50 and 65 % solids, 104 °C | within 1.3 and 3.2 % | IFRF |
| Enthalpy rise from 80 to 100 °C, 20 to 80 % solids | within 0.4 kJ/kg | \[8\] |



