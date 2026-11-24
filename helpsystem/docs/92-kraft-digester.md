# Kraft Digester

Chips, white liquor and an optional dilution liquor pass through the zones of a continuous digester: chip heating with the impregnation alkali, impregnation, heating, cooking, an optional second cooking zone and an optional wash zone. Each zone is a temperature ramp, and a share of the white liquor enters at its start. The block integrates the kinetics in plug flow and writes the pulp and, if its outlet is connected, the black liquor; the pulp then leaves at the outlet consistency. The heat port takes the duty that brings the outlets to the end temperature of the schedule.

**Specification:** the cooking time as given, the cooking time that gives a target kappa number, or the one that gives a target H-factor.

**Delignification:** three phases (Gustafson, through Rantanen et al. \[4\]), with time in minutes and the concentrations in mol/L of free liquor:



\[
\begin{align}
\text{initial:}\quad & k=\exp(17.5-8760/T)\quad\text{down to 82 \% of the wood lignin [2]}\\
\text{bulk:}\quad & k=F_{b}\,k_{150}\,e^{-\frac{E_{a}}{R}\left(\frac{1}{T}-\frac{1}{423.15}\right)}\left(\frac{[\ce{OH-}]}{1.129}\right)^{0.5636}\left(\frac{[\ce{HS-}]}{0.161}\right)^{0.4}\quad\text{down to 3 \%}\\
\text{residual:}\quad & k=2.2\,\exp(19.64-10804/T)\,[\ce{OH-}]^{0.7}
\end{align}
\]


with $k_{150}=2.14\times10^{-2}$ min$^{-1}$, the rate of E. urograndis at 150 °C \[3\], $E_{a}=134$ kJ/mol and $F_{b}=1.0992$. Glucan and xylan dissolve at first order with the rates of Santos et al. \[3\] (0.136 and $0.245\times10^{-2}$ min$^{-1}$ at 150 °C), scaled by factors and alkali exponents fitted on the cooks of Wedin et al. \[5\] and Gomide et al. \[1\]. The dissolved carbohydrates become sodium lactate (2 NaOH per $\ce{C6}$), which puts their alkali consumption at the 0.49 g/g that Kron et al. \[7\] measured. The kappa number adds the hexenuronic acids, 4.1 $\mu$mol/g per percent of hemicelluloses in the pulp \[5\]. Softwood takes factors calibrated on one P. taeda cook \[25\].

**Results:** kappa number (with and without the hexenuronic acids), H-factor, screened yield, lignin and hemicelluloses in the pulp, alkali charges and residual alkali, black liquor solids, heat duty, the heat that brings the chips and liquors to the cooking temperature, reaction heat and pulp production, with the profile of the cook in the notes.







| Case | Model | Measured |
|:---|:--:|:--:|
| Gomide, ten clones (fitted): mean deviation of kappa / yield | -0.70 / -0.33 points | \[1\] |
| Segura AA 17.5 %, 151 °C (not fitted): kappa / yield | 16.3 / 55.1 | 17.7 / 56.1 \[6\] |
| Segura AA 19.0 %, 153 °C (not fitted): kappa / yield | 14.6 / 52.3 | 14.9 / 55.0 \[6\] |
| P. taeda, H-factor 1760 (calibrated): kappa / yield | 30.8 / 47.3 | 31.3 / 47.7 \[25\] |



