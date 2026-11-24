# How the solubility is calculated

The solid phase is taken as the pure solute. At saturation the activity of the solute in the liquid equals the ratio between the fugacities of the pure solid and of the pure subcooled liquid, which comes from the fusion data:



<a id="eq:solscreen_sle"></a>

\[
\ln\left(x_{1}\gamma_{1}\right)=-\frac{\Delta H_{fus}}{R\,T}\left(1-\frac{T}{T_{f}}\right)-\frac{\Delta C_{p}}{R}\left(\frac{T-T_{f}}{T}+\ln\frac{T_{f}}{T}\right)\tag{1}
\]


where $x_{1}$ is the mole fraction of the solute in the saturated solution, $\gamma_{1}$ its activity coefficient, $T_{f}$ the melting point, $\Delta H_{fus}$ the enthalpy of fusion at the melting point and $\Delta C_{p}$ the difference between the heat capacities of the liquid and of the solid at the melting point. The last term is left out when the compound does not carry $\Delta C_{p}$.

With an activity coefficient package the tool takes $\gamma_{1}$ from the model. With an equation of state it takes the ratio between the fugacity coefficient of the solute in the mixture and the one of the pure liquid solute. Because $\gamma_{1}$ depends on the composition, the equation is solved for $x_{1}$: the tool scans the composition from infinite dilution upwards and keeps the lowest root, which is the saturated solution on the solvent side even when the model predicts two liquid phases.

With $\gamma_{1}=1$ the equation gives the ideal solubility, which is the same in every solvent. The tool shows it next to the curves, on the mole fraction basis, as a reference.

