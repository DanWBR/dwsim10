# Thermal Properties

#### Thermal Conductivity

###### *Liquid Phase* {#liquid-phase-2 .unnumbered}

When experimental data is not available, the contribution of each component for the thermal conductivity of the liquid phase is calculated by the *Latini* method *,*



\[
\begin{eqnarray}
\lambda_{i} & = & \frac{A(1-T_{r})^{0.38}}{T_{r}^{1/6}}
\end{eqnarray}
\]




\[
\begin{eqnarray}
A & = & \frac{A^{*}T_{b}^{0.38}}{MM^{\beta}T_{c}^{\gamma}},
\end{eqnarray}
\]


where $A^{*},\:\alpha,\:\beta$ and $\gamma$ depend on the nature of the liquid (Saturated Hydrocarbon, Aromatic, Water, etc). The liquid phase thermal conductivity is calculated from the individual values by the *Li* method ,



\[
\begin{eqnarray}
\lambda_{L} & =\sum\sum\phi_{i}\phi_{j}\lambda_{ij}
\end{eqnarray}
\]




\[
\begin{eqnarray}
\lambda_{ij} & = & 2(\lambda_{i}^{-1}+\lambda_{j}^{-1})^{-1}
\end{eqnarray}
\]




\[
\phi_{i}=\frac{x_{i}V_{c_{i}}}{\sum x_{i}V_{c_{i}}},
\]








where

<span class="roman">$\lambda_{L}$ liquid phase thermal conductivity (W/\[m.K\])</span>



###### *Vapor Phase* {#vapor-phase-2 .unnumbered}

When experimental data is not available, vapor phase thermal conductivity is calculated by the *Ely and Hanley* method ,



\[
\lambda_{V}=\lambda^{*}+\frac{1000\eta^{*}}{MM}1.32\left(C_{v}-\frac{3R}{2}\right),
\]








where

<span class="roman">$\lambda_{V}$ vapor phase thermal conductivity (W/\[m.K\])</span>

<span class="roman">$C_{v}$ constant volume heat capacity (J/\[mol.K\])</span>



$\lambda^{*}$ and $\eta^{*}$ are defined by:



\[
\lambda^{*}=\lambda_{0}H
\]




\[
H=\left(\frac{16.04E-3}{MM/1000}\right)^{1/2}f^{1/2}/h^{2/3}
\]




\[
\lambda_{0}=1944\eta_{0}
\]




\[
f=\frac{T_{0}\theta}{190.4}
\]




\[
h=\frac{V_{c}}{99.2}\phi
\]




\[
\theta=1+(\omega-0.011)(0.56553-0.86276\ln T^{+}-0.69852/T^{+}
\]




\[
\phi=\left[1+(\omega-0.011)(0.38650-1.1617\ln T^{+})\right]0.288/Z_{c}
\]


If $T_{r}\leqslant2,\:T^{+}=T_{r}$ . If $T_{r}>2,\:T^{+}=2$ .



\[
h=\frac{V_{c}}{99.2}\phi
\]




\[
\eta^{*}=\eta_{0}H\frac{MM/1000}{16.04E-3}
\]




\[
\eta_{0}=10^{-7}\sum_{n=1}^{9}C_{n}T_{0}^{(n-4)/3}
\]




\[
T_{0}=T/f
\]

