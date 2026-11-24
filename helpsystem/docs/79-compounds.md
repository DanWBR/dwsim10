# Compounds

The package recognizes the compounds of a sugar solution by their CAS numbers (or their names) and gives each one a role:

- **Water** and **ethanol**: solvents.

- **Sucrose**: the solute that crystallizes.

- **Glucose** and **fructose**: the reducing sugars. They stay dissolved at every concentration, as they do in a raw sugar factory.

- **Cane ash** and **cane organic non-sugars**: the dissolved impurities, non-volatile, from the additional compounds folder.

- **Sugarcane fibre** and **cane mud solids**: insoluble solids from the additional compounds folder. The package keeps them in the solid phase.

Other compounds (air, combustion gases) take an activity coefficient of one and stay in the vapor, so the flue gas of a bagasse boiler or the air of a dryer can share the flowsheet with the juice.

The four pseudo-compounds of the cane are in the additional compounds folder (*Sugarcane fibre*, *Cane ash*, *Cane organic non-sugars* and *Cane mud solids*). The fibre is C$_{6}$H$_{8.94}$O$_{4.32}$, with a formation enthalpy taken from the gross calorific value of dry bagasse \[5\], so it can burn in a reactor. The ash has the molar mass of potassium chloride, the organic non-sugars the formula of aconitic acid, and the mud solids are quartz.

