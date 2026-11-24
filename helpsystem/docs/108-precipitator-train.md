# Precipitator Train

The pregnant liquor and the seed slurry pass through stirred tanks in series. In each tank, gibbsite deposits on the seed at the growth rate of White and Bateman \[16\], as SysCAD implements it:



\[
G=k_{G}\exp(-E/RT)\,C^{-0.5}\left(\frac{A-A^{*}}{C}\right)^{2}\quad\mu\text{m/h}
\]


with $k_{G}=2.5\times10^{12}$ (the SysCAD plant default) and $E/R=8500$ K. The hydrate deposits at $\rho G$ per m<sup>2</sup> of seed surface ($\rho=2420$ kg/m<sup>3</sup>), and the surface is the specific surface times the hydrate held in the tank. The specific surface either stays at the seed value or follows the particle number of equal spheres, which falls by agglomeration with the kernel $K\,G/(D_{i}+D_{j})$ of Livk and Ilievski. The tanks follow a temperature profile, with the cooling duty on the heat port, or run adiabatic. The particle size distribution and nucleation aren’t modeled.







| Check | Model | Published |
|:---|:--:|:--:|
| Batch with fine seed, 74 °C (US 4305913): A/C at 8 / 16 h | 0.500 / 0.459 | about 0.475 |
| BayeX, 18 tanks at 65 °C \[18\]: rms of the A/C profile | 0.013 | (one $k_{G}$ fitted) |
| BayeX: productivity, g/L | 81.1 | 78.1 \[18\] |
| BayeX: gain of 24 $\times$ 4000 over 12 $\times$ 8000 m<sup>3</sup>, g/L | 0.76 | 0.8 \[18\] |



The plant growth constant fits the batch, while the BayeX model needs a constant 2.5 times higher: the growth constant is a tuning parameter of each refinery.

