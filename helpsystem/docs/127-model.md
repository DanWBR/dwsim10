# Model

#### Agitated bed

The agitated bed follows the penetration model of Schlünder and Mollekopf \[1\], in the form given in \[2\]. The heat crosses two resistances in series: the contact between the wall and the first layer of particles, with the coefficient $h_{ws}$, and the penetration into the bed. The agitator mixes the bed after a number of revolutions $N_{mix}$, the mixing number, so the bed stays in contact with the wall for periods of



\[
t_{R}=\frac{N_{mix}}{n}
\]


where $n$ is the speed of the agitator. In each period a drying front enters the bed from the wall. The reduced position of the front, $\xi$, comes from



\[
\xi\sqrt{\pi}\,\exp\left(\xi^{2}\right)\left[1+\left(\frac{h_{ws}}{h_{dry}}-1\right)\mathrm{erf}\left(\xi\right)\right]=\left(\frac{h_{ws}}{h_{dry}}-1\right)\frac{c_{p,s}\left(T_{w}-T_{b}\right)}{X\,\lambda}
\]


where $X$ is the solvent content of the bed, in kg per kg of dry solid, $\lambda$ the heat of vaporization, $c_{p,s}$ the heat capacity of the solid, $T_{w}$ the wall temperature and $T_{b}$ the bed temperature. The coefficients are



\[
h_{sb,dry}=\frac{2}{\sqrt{\pi}}\sqrt{\frac{k\,\rho\,c_{p,s}}{t_{R}}}\qquad\frac{1}{h_{dry}}=\frac{1}{h_{ws}}+\frac{1}{h_{sb,dry}}\qquad\frac{1}{h_{wet}}=\frac{1}{h_{ws}}+\frac{\mathrm{erf}\left(\xi\right)}{h_{sb,dry}}
\]


with $k$ the thermal conductivity and $\rho$ the bulk density of the dry bed. The drying rate per unit of heated area is



\[
N_{v}=\frac{h_{wet}\left(T_{w}-T_{b}\right)\exp\left(-\xi^{2}\right)}{\lambda}
\]


At the end of the period the bed is mixed: its solvent content falls by the amount evaporated, and its temperature rises with the part of the heat that stayed in the dried particles. While the bed holds much liquid, almost all the heat evaporates solvent and the bed stays near the boiling temperature. As it dries, the bed warms toward the wall temperature and the rate falls. The model gives the falling rate without a critical moisture content.

Before the liquid boils, the wet bed is heated from the feed temperature to the boiling temperature with the same two resistances.

#### Bed at rest

In a tray dryer the bed isn’t mixed. A drying front moves from the heated plate into the bed, and the heat crosses the contact resistance and the dried layer behind the front:



\[
N_{v}=\frac{T_{w}-T_{s}}{\lambda\left(\dfrac{1}{h_{ws}}+\dfrac{a}{k}\right)}
\]


where $T_{s}$ is the boiling temperature and $a$ the thickness of the dried layer. The front advances until the solvent left in the bed reaches the target. The depth of the bed comes from the dry solid of the batch, the bulk density and the heated area. In this mode the vapor has the composition of the liquid in the cake.

#### Contact between the wall and the particles

The contact coefficient follows Schlünder \[3\]. It depends on the particle diameter, on the thermal conductivity of the vapor and on the pressure, through the mean free path of the vapor molecules, and it falls as the vacuum gets deeper. A value can be given in its place.

