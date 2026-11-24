# Solid forms

A compound that crystallizes in more than one form (polymorphs) can carry the list of its forms. The list is entered in the compound editor, in the solid group, in the field **Solid forms (polymorphs)**. Each form is written as its name, melting point in K, enthalpy of fusion in kJ/mol and, optionally, the heat capacity change on fusion in J/(mol.K) and the density in kg/m<sup>3</sup>, separated by semicolons, with a vertical bar between the forms:

    Form I; 441.75; 28.1 | Form II; 429.55; 27.6

The list by itself does not change any calculation. The field **Solid form in use**, right below it, takes the name of one of the forms and copies its data into the melting point, the enthalpy of fusion and the heat capacity change of the compound, which every solid-liquid calculation of the simulation then uses: the flashes with solids, the Crystallizer and this tool.

Each form has its own right-hand side in equation [1 (How the solubility is calculated)](62-how-the-solubility-is-calculated.md#eq:solscreen_sle). At a given temperature the form with the lowest value is the stable one, and it is also the least soluble; a metastable form dissolves more. Two forms are monotropic when the same one is stable up to its melting point, and enantiotropic when the stable form changes at a transition temperature below both melting points.

With a name in **Solid form of the solute** the study is calculated for that form, whichever form the compound has in use. When the solute has forms, the Messages tab shows the stable form at the cold temperature, how many times more soluble the other forms are there, and for each pair whether it is monotropic or enantiotropic, with the transition temperature. The Crystallizer has the same choice in its **Crystal Form** field.

The comparison between forms rests on their fusion data alone. Small errors in the enthalpies of fusion move a transition temperature by tens of kelvin, so a transition calculated here is an indication to be confirmed by experiment. The form in use does not switch by itself when the temperature crosses a transition.

