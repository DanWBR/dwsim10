# Model and data

The model is COSMO-SAC \[1\] with the GMHB1808 parametrization of the Virtual Laboratory for Properties Prediction of UFRGS (LVPP) \[2\]. The surface of each molecule is divided into segments. Two segments in contact have an energy that grows with the mismatch between their charge densities $\sigma_{m}$ and $\sigma_{n}$,



\[
\Delta W_{mn}=\frac{f_{pol}\,\alpha}{2}\left(\sigma_{m}+\sigma_{n}\right)^{2}+c_{HB}\,\max\left(0,\sigma_{acc}-\sigma_{HB}\right)\min\left(0,\sigma_{don}+\sigma_{HB}\right)
\]


where the second term is added when one segment belongs to a hydrogen bond donor and the other to an acceptor. Donors are the hydrogen atoms bonded to oxygen, with one class for water and another for the other compounds. Acceptors are oxygen, nitrogen and fluorine atoms, in four classes: water, atoms bonded to hydrogen, atoms bonded to a single atom (as in ketones and nitriles) and atoms bonded to two or more atoms (as in ethers, esters and tertiary amines). Each pair of classes has its own constant $c_{HB}$. The activity coefficient of a compound is the sum, over its segments, of the difference between the segment activity coefficients in the mixture and in the pure compound, plus a Flory-Huggins term built on the molecular volumes.

The sigma profiles are the ones of the LVPP sigma-profile database, release 20.06, calculated with GAMESS at the HF/TZVP level \[2\]. The database is distributed under the MIT License and holds 1,755 molecules, among them the common solvents, water, light gases, ibuprofen and paracetamol.

The vapor phase, the enthalpies and the other properties are calculated as in the other activity coefficient packages. The excess enthalpy comes from the temperature derivative of the activity coefficients.

