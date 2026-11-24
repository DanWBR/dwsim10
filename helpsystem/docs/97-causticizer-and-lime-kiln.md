# Causticizer and Lime Kiln

**Causticizer:** the slaker, the causticizers and the white liquor clarifier in one block. The lime slakes, and $\ce{Ca(OH)2 + Na2CO3 -> CaCO3 +
2NaOH}$ goes up to the equilibrium causticity less an approach (4.5 points by default). The equilibrium is Lindberg and Ulmgren \[21\], $\log K_{e}=2.95-0.62\,C_{Na}^{0.5}$ with $C_{Na}$ in mol/L, by default, or Theliander \[21\]. The block takes the lime stream as it is, or writes the lime that reaches the target with an excess, and sends the solids to the lime mud at its solids content. For the recausticizing example of Sanchez \[20\] it gives 257.5 kg of NaOH as $\ce{Na2O}$ and a causticity of 80.7 %, against 258 kg and 80.9 %.

**Lime Kiln:** the $\ce{CaCO3}$ calcines down to the residual $\ce{CaCO3}$ of the lime, $\ce{Ca(OH)2}$ dehydrates, and the sodium of the mud liquor leaves as $\ce{Na2CO3}$ and $\ce{Na2SO4}$. Any fuel with C, H, O, N and S burns at the $\ce{O2}$ of the flue gas, and the block writes the fuel flow that closes the heat balance (calcination, mud drying, flue gas and lime sensible heats and the shell loss). The calcination heat is 3.18 GJ/t of CaO, against 3.33 in the example of Adams \[20\].

