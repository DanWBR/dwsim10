# Peng-Robinson-Stryjek-Vera 2 (PRSV2 and PRSV2-VL)

PRSV2 keeps the Peng-Robinson assembly but makes the alpha parameter temperature-dependent:



<a id="eq:d-prsv2-kappa"></a>

\[
\begin{aligned}\kappa_{i} & =\kappa_{0,i}+\left[\kappa_{1,i}+\kappa_{2,i}\!\left(\kappa_{3,i}-T_{r,i}\right)\!\left(1-\sqrt{T_{r,i}}\right)\right]\\
 & \quad{}\times\left(1+\sqrt{T_{r,i}}\right)\!\left(0.7-T_{r,i}\right)
\end{aligned}
\]


with $T_{r,i}=T/T_{c,i}$ and $\kappa_{0,i}=0.378893+1.4897153\,\omega_{i}-0.17131848\,\omega_{i}^{2}+0.0196554\,\omega_{i}^{3}$ . Because $\kappa_{i}$ depends on temperature, the alpha derivative gains an extra term



<a id="eq:d-prsv2-dadt"></a>

\[
\frac{da_{i}}{dT}=\Omega_{a}\frac{R^{2}T_{c,i}^{2}}{P_{c,i}}\,2\sqrt{\alpha_{i}}\left[\frac{d\kappa_{i}}{dT}\!\left(1-\sqrt{T_{r,i}}\right)-\frac{\kappa_{i}}{2\sqrt{T\,T_{c,i}}}\right]
\]


where $d\kappa_{i}/dT=(1/T_{c,i})\,d\kappa_{i}/dT_{r,i}$ is obtained by differentiating the expression for $\kappa_{i}$ above with respect to $T_{r,i}$ . Everything else follows the Peng-Robinson assembly of the previous section. The composition derivative additionally differentiates the composition-dependent Stryjek-Vera mixing rule.

The PRSV2-VL variant replaces that mixing rule with the van Laar form



<a id="eq:d-prsv2vl-aij"></a>

\[
a_{ij}=\sqrt{a_{i}a_{j}}\left(1-\frac{k_{ij}k_{ji}}{x_{i}k_{ij}+x_{j}k_{ji}}\right)
\]


which carries two interaction matrices and depends on composition, but not on temperature. That single observation is what makes the temperature derivative tractable: the parenthesis is a constant with respect to $T$ , so every $a_{ij}$ and every sum built from it keeps its composition factor unchanged and differentiates through the pure-component $a_{i}$ alone,



<a id="eq:d-prsv2vl-daijdt"></a>

\[
\frac{\partial a_{ij}}{\partial T}=\frac{a_{ij}}{2}\left(\frac{1}{a_{i}}\frac{da_{i}}{dT}+\frac{1}{a_{j}}\frac{da_{j}}{dT}\right)
\]


with $da_{i}/dT$ from equation [\[eq:d-prsv2-dadt\]](#eq:d-prsv2-dadt). Only the temperature derivative is available in closed form for this variant; the composition derivative falls back to finite differences. Two guards keep the analytical result faithful to the function it differentiates: the routine declines to answer, and the caller reverts to finite differences, whenever the root-checking step has displaced the pressure, because that branch adds a term the derivative was not taken of; and it reproduces the same numerical guard the fugacity routine applies to its correction sum, so that the two can never end up differentiating different expressions.

