# Electrolyte Activity Models

The electrolyte models add an electrostatic contribution and, in use, are coupled to a chemical-speciation equilibrium that is itself solved by Newton iteration. Analytical activity-coefficient derivatives serve two consumers. The first is that speciation solver, which receives an exact Jacobian instead of a finite-difference rebuild of the whole model at every iteration. The second is the property package itself: the derivatives of the activity coefficients are assembled into derivatives of the K-values, which is what the rigorous column solver needs, and are described in the last subsection below.

#### Extended UNIQUAC {#extended-uniquac}

The activity coefficient is the UNIQUAC combinatorial and residual terms (as above) plus a Debye-Huckel electrostatic term. The residual uses $\psi_{ji}=\exp\!\big[-(u_{ji}-u_{ii})/T\big]$ with $u_{ij}(T)=u_{ij}^{0}+u_{ij}^{T}(T-298.15)$ , so, writing $\Delta u_{ji}=u_{ji}-u_{ii}$ and $\Delta u_{ji}^{T}=u_{ji}^{T}-u_{ii}^{T}$ ,



<a id="eq:d-exu-dpsidt"></a>

\[
\frac{\partial\psi_{ji}}{\partial T}=\psi_{ji}\!\left(-\frac{\Delta u_{ji}^{T}}{T}+\frac{\Delta u_{ji}}{T^{2}}\right)
\]


which feeds the UNIQUAC-residual temperature derivative given above. The Debye-Huckel term is linear in the parameter $A(T)$ , so $\ln\gamma_{i}^{\mathrm{DH}}=A(T)f_{i}(I_{x})$ and $\partial\ln\gamma_{i}^{\mathrm{DH}}/\partial T=(A'(T)/A(T))\ln\gamma_{i}^{\mathrm{DH}}$ . Here $I_{x}=\tfrac{1}{2}\sum_{k}m_{k}z_{k}^{2}$ is the molality-based ionic strength, $m_{k}=x_{k}/(x_{w}M_{w})$ , and, with $s=\sqrt{I_{x}}$ , $b=1.5$ , $\beta=1+bs$ , the water and ion parts are $f_{w}=\tfrac{2M_{w}}{b^{3}}(1+bs-1/\beta-2\ln\beta)$ and $f_{i}=-z_{i}^{2}s/\beta$ . The composition derivative enters through $s$ :



<a id="eq:d-exu-dx"></a>

\[
\begin{aligned}\frac{\partial\ln\gamma_{w}^{\mathrm{DH}}}{\partial x_{k}} & =\frac{2AM_{w}s^{2}}{\beta^{2}}\frac{\partial s}{\partial x_{k}},\qquad\frac{\partial\ln\gamma_{i}^{\mathrm{DH}}}{\partial x_{k}}=-\frac{Az_{i}^{2}}{\beta^{2}}\frac{\partial s}{\partial x_{k}}\\
\frac{\partial s}{\partial x_{k}} & =\frac{1}{2s}\frac{\partial I_{x}}{\partial x_{k}},\qquad\frac{\partial I_{x}}{\partial x_{k}}=\frac{z_{k}^{2}}{2M_{w}x_{w}}-\frac{I_{x}\delta_{wk}}{x_{w}}
\end{aligned}
\]


The infinite-dilution ion reference is constant in composition; its residual part is temperature-dependent, and its temperature derivative is subtracted for ions.

#### Electrolyte NRTL (eNRTL) {#electrolyte-nrtl-enrtl}

The eNRTL activity coefficient is a Pitzer-Debye-Huckel long-range term plus a local-composition short-range term. Its interaction parameters are temperature-independent, so the only temperature dependence is the Debye-Huckel constant $A_{\varphi}(T)\propto T^{-3/2}$ . Since the long-range term is linear in $A_{\varphi}$ , the temperature derivative is exact:



<a id="eq:d-enr-dt"></a>

\[
\frac{\partial\ln\gamma_{i}}{\partial T}=-\frac{3}{2T}\,\ln\gamma_{i}^{\mathrm{LR}}
\]


The long-range composition derivative is closed-form through the mole-fraction ionic strength $I_{x}=\tfrac{1}{2}\sum_{k}x_{k}z_{k}^{2}$ (so $\partial I_{x}/\partial x_{k}=\tfrac{1}{2}z_{k}^{2}$ ): with $s=\sqrt{I_{x}}$ , $\rho=14.9$ , $\beta=1+\rho s$ and prefactor $P=(1000/M_{s})^{1/2}A_{\varphi}$ , the ion and solvent derivatives are



<a id="eq:d-enr-lr"></a>

\[
\begin{aligned}\frac{\partial\ln\gamma_{i}^{\mathrm{LR}}}{\partial x_{k}} & =-P\!\left[\frac{2z_{i}^{2}}{\beta}+\frac{(z_{i}^{2}-6I_{x})\beta-(z_{i}^{2}s-2I_{x}s)\rho}{\beta^{2}}\right]\!\frac{\partial s}{\partial x_{k}}\\
\frac{\partial\ln\gamma_{m}^{\mathrm{LR}}}{\partial x_{k}} & =(1000/M_{s})^{1/2}\,2A_{\varphi}M_{s}\frac{s^{2}}{\beta^{2}}\frac{\partial s}{\partial x_{k}}
\end{aligned}
\]


The multi-pair short-range term is an explicit function of composition built from sums, products, quotients and exponentials only; its composition derivative is obtained exactly, to machine precision, by forward-mode automatic differentiation of the same expression, avoiding an error-prone hand derivation.

#### From the activity coefficients to the K-values

The two subsections above give $\partial\ln\gamma_{i}/\partial T$ and $\partial\ln\gamma_{i}/\partial n_{j}$ for the models themselves. Reaching the K-values requires the expression the package actually evaluates around them, which is piecewise: an aqueous electrolyte carries ionic species, solutes above their critical temperature, and ordinary condensable components, and each takes a different form. Writing $m_{i}$ for the molality of species $i$ and $w$ for the solvent mass per mole of mixture,



<a id="eq:d-elec-phi"></a>

\[
\varphi_{i}^{L}=\begin{cases}
m_{i}\,\gamma_{i} & \text{ions and salts}\\
K_{H,i}(T)/P & \text{supercritical solutes}\\
\gamma_{i}\,P_{i}^{\mathrm{sat}}(T)/P & \text{condensable components}
\end{cases}
\]


Each branch is differentiated on its own terms. The molality carries no temperature, so for an ion $\partial\ln\varphi_{i}/\partial T=\partial\ln\gamma_{i}/\partial T$ . For a condensable component the vapour pressure contributes as well, giving $\partial\ln\gamma_{i}/\partial T+\partial\ln P_{i}^{\mathrm{sat}}/\partial T$ . The supercritical branch has no composition dependence at all. Carbon dioxide and hydrogen sulfide are the exception worth naming: their Henry constants are given by explicit correlations of the form $\exp\!\left(a-b/T-c\ln T+eT\right)$ , whose logarithmic derivative $b/T^{2}-c/T+e$ is exact. The remaining Henry constants and the vapour pressures come from any of a dozen per-compound correlations and are differenced centrally in temperature; each is a single smooth function of one variable, so this is effectively exact and is not the same thing as differencing a whole K-value.

The composition derivative follows the same split. Only the ionic branch carries composition outside the activity coefficient, through the molality $m_{i}=x_{i}/w$ with $w=\sum_{k\in\mathrm{solv}}x_{k}M_{k}$ . On the total-moles basis the solvers use, the two unit terms of $\partial\ln x_{i}/\partial n_{j}$ and $\partial\ln w/\partial n_{j}$ cancel and leave



<a id="eq:d-elec-dlnm"></a>

\[
\frac{\partial\ln m_{i}}{\partial n_{j}}=\frac{\delta_{ij}}{x_{i}}-\begin{cases}
M_{j}/w & j\in\mathrm{solv}\\
0 & \text{otherwise}
\end{cases}
\]


The vapour phase is ideal by default, in which case its fugacity coefficients are constants and drop out; with the real-gas option it is Peng-Robinson and reuses the closed form of the first section. The K-value derivatives then follow from the general relations of the Scope and Notation section.

One practical point applies to any activity model reached this way. The derivative and the value must be evaluated at the same composition. That sounds trivial, and is the single most common way for an otherwise correct closed form to go wrong: a model that renormalises the composition it is handed, and is called from the middle of a derivative routine that had already read the unnormalised one, will return a value on one basis and a derivative on another. The discrepancy is small enough to survive a casual check and large enough to spoil a Newton direction.

