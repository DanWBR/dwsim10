# Model

#### Freezing

The product of a vial is taken as one lump that exchanges heat with the shelf, with the freezing heat transfer coefficient and the outer area of the vial. The shelf goes from the feed temperature to the freezing shelf temperature at the ramp rate and holds for the hold time. The product cools as a liquid down to the nucleation temperature, where the supercooling is released as ice. It then freezes at the freezing temperature and cools as a solid. The block warns when the product is not completely frozen at the end of the step.

#### Primary drying

The ice sublimes at a front that moves from the top of the cake to the bottom of the vial. At each moment the heat that reaches the front equals the heat taken by the vapor that leaves it \[1\]:



\[
\frac{T_{sh}-T_{sub}}{\dfrac{1}{K_{v}A_{v}}+\dfrac{L_{f}}{k_{ice}A_{p}}}=\Delta H_{s}\,\frac{A_{p}\left(P_{ice}\left(T_{sub}\right)-P_{ch}\right)}{R_{p}}
\]


where $T_{sh}$ is the shelf temperature, $T_{sub}$ the temperature of the front, $A_{v}$ and $A_{p}$ the outer and inner areas of the vial, $L_{f}$ the thickness of the frozen layer still in the vial, $k_{ice}$ the thermal conductivity of ice, $\Delta H_{s}$ the heat of sublimation, $P_{ice}$ the vapor pressure of ice and $P_{ch}$ the chamber pressure. The vial heat transfer coefficient depends on the chamber pressure and the resistance of the dried layer on its thickness $L_{d}$:



\[
K_{v}=K_{C}+\frac{K_{P}\,P_{ch}}{1+K_{D}\,P_{ch}}\qquad R_{p}=R_{0}+\frac{A_{1}\,L_{d}}{1+A_{2}\,L_{d}}
\]


The equation is solved for $T_{sub}$ and the front advances with the sublimation rate until the ice is gone. The temperature at the bottom of the vial, the warmest point of the product, is compared with the collapse temperature of the formulation. The vapor pressure and the heat of sublimation of ice come from the correlations of Murphy and Koop \[2\].

The thickness of the frozen layer comes from the volumes of the ice and of the solutes in the vial, with the density of the feed from the property package.

#### Secondary drying

The water that stays bound to the solid after primary drying leaves by desorption. The product follows the shelf temperature, and the moisture $w$, in kg of water per kg of dry solid, falls at a rate proportional to its distance from the equilibrium moisture:



\[
\frac{dw}{dt}=-k\left(T\right)\left(w-w_{eq}\right)\qquad k\left(T\right)=k_{ref}\exp\left[-\frac{E_{a}}{R}\left(\frac{1}{T}-\frac{1}{T_{ref}}\right)\right]
\]


The step ends when the moisture reaches the target.

