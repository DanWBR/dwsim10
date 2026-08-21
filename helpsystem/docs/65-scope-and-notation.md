# Scope and Notation

For a component $i$ the fugacity coefficient in a phase is $\varphi_{i}$ , the liquid activity coefficient is $\gamma_{i}$ , and the equilibrium ratio is $K_{i}=y_{i}/x_{i}$ . Temperature derivatives are taken at constant pressure and composition; composition derivatives at constant temperature and pressure.

Composition derivatives are reported on a mole-number basis, evaluated at a total of one mole. Since the intensive quantities depend on composition only through the mole fractions $x_{j}=n_{j}/\sum_{k}n_{k}$ , a mole-number derivative is recovered from the mole-fraction derivatives by the projection



<a id="eq:d-project"></a>

\[
\frac{\partial\ln\gamma_{i}}{\partial n_{j}}=\frac{\partial\ln\gamma_{i}}{\partial x_{j}}-\sum_{k}x_{k}\frac{\partial\ln\gamma_{i}}{\partial x_{k}}
\]


which automatically enforces the Gibbs-Duhem constraint $\sum_{j}x_{j}\,\partial\ln\varphi_{i}/\partial n_{j}=0$ .

The K-value and its derivatives are



<a id="eq:d-kdef"></a>

\[
K_{i}=\frac{\varphi_{i}^{L}}{\varphi_{i}^{V}}\;\text{(EOS)},\qquad K_{i}=\frac{\gamma_{i}P_{i}^{\mathrm{sat}}}{\varphi_{i}^{V}P}\;\text{(activity)}
\]




<a id="eq:d-kderiv"></a>

\[
\begin{aligned}\frac{\partial K_{i}}{\partial T} & =K_{i}\!\left(\frac{\partial\ln\varphi_{i}^{L}}{\partial T}-\frac{\partial\ln\varphi_{i}^{V}}{\partial T}\right)\\
\frac{\partial K_{i}}{\partial n_{j}} & =K_{i}\,\frac{\partial\ln\varphi_{i}^{L}}{\partial n_{j}}
\end{aligned}
\]


the composition derivative being with respect to liquid mole numbers, for which the vapor fugacity coefficient is independent of the liquid composition. The remaining task, for each model, is the derivative of $\ln\varphi_{i}$ (EOS) or of $\ln\gamma_{i}$ (activity model).

