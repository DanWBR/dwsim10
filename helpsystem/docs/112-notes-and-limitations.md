# Notes and limitations

- The digester doesn’t model the organics of the bauxite or the high-temperature digestion of boehmite.

- Reversion in the settler and the washers and the soda adsorbed on the mud aren’t modeled.

- The precipitation carries a lumped seed surface without a particle size distribution, and nucleation isn’t modeled. Late in a batch, the agglomeration kernel over-agglomerates.

- The package keeps sodium oxalate as a dissolved compound, so the oxalate cake shows it in its liquor.

- The Bayer causticization equilibrium (the highest C/S that lime can reach) isn’t modeled; the lime efficiency and the target C/S are inputs.

- DWSIM conversion reactors with activity coefficient packages, Bayer Liquors included, put liquid water at zero enthalpy at 25 °C, so a reaction that forms liquid water there misses 44 kJ per mol of water. The Bayer blocks correct it themselves.

