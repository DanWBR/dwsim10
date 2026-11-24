# Fitting NRTL to the measured points

A predictive model seldom gives the solubility of a new molecule within the accuracy a crystallization design needs. When a few solubilities were measured, the NRTL model can be fitted to them and then used in the flowsheet.

With an NRTL property package selected and the measured solubilities typed, **Fit NRTL to measured points** adjusts the two interaction energies of the solute with each solvent that has measured points. The Messages tab shows, for each pair, the fitted energies, the ones the package had, the deviation before and after, and the solubility of the model at each measured temperature next to the measured value. A solvent with a single point gets one value for both energies. The non-randomness factor keeps the value the pair already has, or 0.3.

The fit is made on a private copy of the package, so nothing changes in the flowsheet at this point. **Store in package** writes the fitted pairs to the property package of the flowsheet, replacing the parameters those pairs had. Press **Run** afterwards to see the new curves, and save the flowsheet to keep the parameters.

A few rules of thumb:

- Two energies fitted to two or three points reproduce those points and say little about what lies between and beyond them. Five or more points over the temperature range of the process give a curve that can be trusted inside that range.

- The fit follows the measured temperatures. Above the highest one the fitted curve can rise faster than the real one; check it before using the model to size a dissolution step.

- The fit takes the fusion data of the solute, or of the solid form selected, as given. An error in the enthalpy of fusion is absorbed by the interaction energies inside the fitted range and shows up outside it.

- Only the pair of the solute with each solvent is fitted. The pairs between solvents keep the parameters of the package.

