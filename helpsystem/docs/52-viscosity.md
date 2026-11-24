# Viscosity

#### Lohrenz-Bray-Clark

The viscosity $\mu$ of a phase is the viscosity $\mu^{*}$ of the dilute gas, from the correlation of Stiel and Thodos with the mixing rule of Herning and Zipperer, plus a term in the reduced density $\rho_{r}=\sum x_{i}v_{ci}/v$:


\[
\left[\left(\mu-\mu^{*}\right)\xi+10^{-4}\right]^{1/4}=a_{1}+a_{2}\rho_{r}+a_{3}\rho_{r}^{2}+a_{4}\rho_{r}^{3}+a_{5}\rho_{r}^{4},
\]


with $\xi=T_{c}^{1/6}M^{-1/2}p_{c}^{-2/3}$ of the mixture and the coefficients 0.1023, 0.023364, 0.058533, $-$<!-- -->0.040758 and 0.0093324. The same expression serves gas and liquid. The result depends on the fourth power of a polynomial in density, so it is sensitive to the critical volumes: the critical volume of the heavy fractions is the natural variable to fit, and a fit keeps it apart from the critical volume the interaction parameters use.

#### Corresponding states

The model of Pedersen and Fredenslund takes the viscosity of methane, the reference fluid, at a corresponding temperature and pressure:


\[
\mu(T,p)=\left(\frac{T_{c,mix}}{T_{c0}}\right)^{-1/6}\left(\frac{p_{c,mix}}{p_{c0}}\right)^{2/3}\left(\frac{M_{mix}}{M_{0}}\right)^{1/2}\frac{\alpha_{mix}}{\alpha_{0}}\,\mu_{0}\!\left(T_{0},p_{0}\right),
\]




\[
T_{0}=T\,\frac{T_{c0}\,\alpha_{0}}{T_{c,mix}\,\alpha_{mix}},\qquad p_{0}=p\,\frac{p_{c0}\,\alpha_{0}}{p_{c,mix}\,\alpha_{mix}},
\]


where the subscript 0 is methane, the pseudocritical properties come from the mixing rules of the authors, and the mixture molar mass weights the heavy components through the mass and number averages $M_{w}$ and $M_{n}$:


\[
M_{mix}=1.304\times10^{-4}\left(M_{w}^{2.303}-M_{n}^{2.303}\right)+M_{n}.
\]


The coupling coefficient is $\alpha=1+7.378\times10^{-3}\rho_{r}^{1.847}M^{0.5173}$, with $\rho_{r}$ the reduced density of methane at the corresponding state. The viscosity of methane is the correlation of Hanley, Haynes and McCarty, with the low temperature term of Pedersen and Fredenslund, and its density the 32-term equation of McCarty. The coefficient $1.304\times10^{-4}$ and the exponent 2.303 are the fitting variables.

The model usually needs less tuning than the Lohrenz-Bray-Clark correlation for oils. For heavy oils the corresponding state of methane falls below its freezing point, and the result is then an extrapolation.

