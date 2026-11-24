# What to expect

The table compares the solubility of ibuprofen at 282 K predicted by the package with the measurements and with the predictions of three other models reported in the same article \[3\], as mole fractions.







| Solvent             | Measured | COSMO-SAC | COSMO-RS(Ol) | UNIFAC | Mod. UNIFAC |
|:--------------------|:--------:|:---------:|:------------:|:------:|:-----------:|
| 3-Methyl-2-butanone |  0.148   |   0.258   |    0.231     | 0.161  |    0.127    |
| 2-Butanone          |  0.148   |   0.280   |    0.234     | 0.161  |    0.125    |
| Isopropyl acetate   |  0.126   |   0.249   |    0.206     | 0.134  |    0.091    |
| 2-Propanol          |  0.122   |   0.291   |    0.169     | 0.082  |    0.055    |
| Acetonitrile        |  0.017   |   0.158   |    0.072     | 0.032  |    0.023    |
| n-Heptane           |  0.010   |   0.002   |    0.004     | 0.023  |    0.014    |



The package behaves like the other model of the same family: it ranks the oxygenated solvents above acetonitrile and n-heptane, as measured, and it overestimates the solubility of the acid in the polar solvents by a factor of about two, and by more in acetonitrile. For paracetamol at 298.15 K it gives 0.038 in ethanol and 0.063 in acetone, against 0.057 and 0.033 measured \[3\]. Where the groups exist, UNIFAC is often closer; the value of COSMO-SAC is in the molecules and solvents that the group methods do not cover.

For vapor-liquid equilibrium of ordinary mixtures the package is close to a fitted model. The bubble points of ethanol and water at 1 atm come out within 1.7 K of the ones of NRTL with its fitted parameters.

