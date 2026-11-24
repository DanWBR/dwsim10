# Notes and limitations

- The whole result scales with the melting point and the enthalpy of fusion of the solute. The Messages tab shows the values used; check them against a measurement when the compound comes from a database.

- The solid is the pure solute in one crystal form, the one selected or the one in use in the compound. Solvates, hydrates, salts and solid solutions are not handled.

- The solvents do not freeze in the study.

- NRTL and UNIQUAC treat a pair without interaction parameters as ideal. The ranking flags those rows, because their order then only reflects the molar mass of the solvent.

- UNIFAC and Modified UNIFAC need the structure groups of the solute. A compound without groups cannot be used with them.

- The mass per volume basis needs the liquid density of the saturated solution from the property package and stays empty when the package does not return it.

