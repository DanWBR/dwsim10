# Activity model

The excess Gibbs energy of the liquid is the S-UNIFAC model of Spiliotis and Tassios \[1\] on the water, ethanol and sugars, plus Margules terms fitted for cane solutions:



\[
\frac{G^{E}}{RT}=x_{U}\,\frac{G^{E}_{\text{S-UNIFAC}}}{RT}\Big|_{x'}+\sum_{\text{pairs }i,j}x_{i}x_{j}\left(A_{ji}x_{i}+A_{ij}x_{j}\right)
\]


S-UNIFAC is UNIFAC-LLE with two new main groups: CHOH$_\text{sugar}$, with the subgroups CH$_{2}$OH, CHOH$_\text{axial}$, CHOH$_\text{equatorial}$ and the COH subgroups of the ketoses, and CH-O-CH for the glycosidic bond of the disaccharides. It is applied to the water, ethanol and sugars renormalized among themselves ($x'$), and $x_{U}$ is their total mole fraction. The ash and the organic non-sugars enter through the Margules terms alone. Each pair parameter changes with temperature as



\[
A=a+b\,\tau,\qquad\tau=\frac{1000}{T}-\frac{1000}{348.15}
\]








| Pair $i$/$j$ | $a_{ij}$ | $b_{ij}$ | $a_{ji}$ | $b_{ji}$ |
|:---|:--:|:--:|:--:|:--:|
| Water / sucrose | $-2.7108$ | $-4.1875$ | $1.0568$ | $0.1547$ |
| Water / ash, water / organic non-sugars | $-19.5941$ | $-9.4816$ | $-1.7995$ | 0 |
| Sucrose / ash, sucrose / organic non-sugars | $-32.3125$ | $-21.937$ | $-3.5614$ | 0 |



The water-sucrose terms were fitted to the solubility of sucrose in water from 0 to 100 °C \[2\], to the boiling point elevation of pure sucrose syrups at pan conditions (65 to 85 °C and 3 to 4.5 kg of sucrose per kg of water) \[3\] and to the water activity of dilute solutions \[4\]. The impurity terms were fitted to the solubility coefficient of Australian cane molasses (the SC-1 correlation of Miller and Beath, as given by Broadfoot and Fraga \[6\]) and to the boiling point elevation of cane syrups of purity 60 to 90 \[3\]. The impurity is split as the ash and the organic non-sugars of the cane, and the reducing sugars as half glucose and half fructose.

#### Crystallization {#crystallization-.unnumbered}

Sucrose crystallizes when its activity in the liquid reaches the one of the solid,



\[
\ln\left(x\gamma\right)=-\frac{\Delta H_{f}}{RT}\left(1-\frac{T}{T_{f}}\right)-\frac{\Delta c_{p}}{R}\left(\frac{T-T_{f}}{T}+\ln\frac{T_{f}}{T}\right)
\]


with $T_{f}=459.15$ K, $\Delta H_{f}=57$ kJ/mol and $\Delta c_{p}=200.1$ J/(mol K), the values of the S-UNIFAC model. The default flash handles the solid, the liquid and the vapor together: a heater at the boiling temperature of a pan gives the massecuite with its crystals, its mother liquor and the vapor that leaves it.

