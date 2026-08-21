# Lee-Kesler-Plocker

The Lee-Kesler-Plocker package is a corresponding-states model: the mixture is represented by a simple and a reference fluid, each obeying a Benedict-Webb-Rubin equation, and its properties are interpolated with the mixture acentric factor. Its derivatives are the most involved of the packages, so every ingredient is written out below.

#### Mixture pseudo-critical rules

With the pair quantities



<a id="eq:d-lkp-pair"></a>

\[
v_{cjk}=\tfrac{1}{8}\!\left(V_{c,j}^{1/3}+V_{c,k}^{1/3}\right)^{3},\qquad t_{cjk}=\sqrt{T_{c,j}T_{c,k}}\;k_{jk}
\]


the mixture critical properties are



<a id="eq:d-lkp-mix"></a>

\[
\begin{aligned}w_{m} & =\sum_{i}x_{i}w_{i},\qquad V_{cm}=\sum_{j}\sum_{k}x_{j}x_{k}v_{cjk},\qquad T_{cm}=V_{cm}^{-1/4}\sum_{j}\sum_{k}x_{j}x_{k}\,v_{cjk}^{1/4}\,t_{cjk}\\
P_{cm} & =(0.2905-0.085\,w_{m})\frac{RT_{cm}}{V_{cm}},\qquad z_{cm}=\frac{P_{cm}V_{cm}}{RT_{cm}}
\end{aligned}
\]


#### Compressibility and fugacity coefficient

Each fluid (simple $s$ and reference $h$ , with its own constant set) gives a compressibility from the reduced volume $V_{r}=P_{c}V/(RT_{c})$ , and the mixture interpolates with $w_{h}=0.3978$ :



<a id="eq:d-lkp-bwr"></a>

\[
\begin{aligned}z=\frac{P_{r}V_{r}}{T_{r}} & =1+\frac{B}{V_{r}}+\frac{C}{V_{r}^{2}}+\frac{D}{V_{r}^{5}}+\frac{c_{4}}{T_{r}^{3}V_{r}^{2}}\!\left(\beta+\frac{\gamma}{V_{r}^{2}}\right)e^{-\gamma/V_{r}^{2}}\\
z_{m} & =z^{(s)}+\frac{w_{m}}{w_{h}}\!\left(z^{(h)}-z^{(s)}\right)
\end{aligned}
\]


with $B=b_{1}-b_{2}/T_{r}-b_{3}/T_{r}^{2}-b_{4}/T_{r}^{3}$ , $C=c_{1}-c_{2}/T_{r}+c_{3}/T_{r}^{3}$ , $D=d_{1}+d_{2}/T_{r}$ (the two fluid constant sets are the standard Lee-Kesler table). The component fugacity coefficient is built from the composition derivatives of the mixing rules,



<a id="eq:d-lkp-lnphi"></a>

\[
\ln\varphi_{i}=\ln\varphi_{m}-\frac{\hat{H}}{T}\,S_{i}^{a}+\frac{z_{m}-1}{P_{cm}}\,S_{i}^{b}-\Big(\tfrac{\partial\ln\varphi_{m}}{\partial w_{m}}\Big)S_{i}^{c}
\]


where $\ln\varphi_{m}$ is the mixture (corresponding-states) fugacity coefficient, $\hat{H}=H_{m}^{\mathrm{dep}}M_{m}/(RT_{cm})$ the reduced enthalpy departure, $\partial\ln\varphi_{m}/\partial w_{m}=(\ln\varphi^{(h)}-\ln\varphi^{(s)})/w_{h}$ , and



<a id="eq:d-lkp-S"></a>

\[
S_{i}^{a}=\sum_{j\neq i}x_{j}\,T_{cm}^{(ij)},\qquad S_{i}^{b}=\sum_{j\neq i}x_{j}\,P_{cm}^{(ij)},\qquad S_{i}^{c}=\sum_{j\neq i}x_{j}(w_{j}-w_{i})
\]


The pairwise mixing-rule derivatives are



<a id="eq:d-lkp-pairderiv"></a>

\[
\begin{aligned}V_{cm}^{(ij)} & =2\sum_{l}x_{l}(v_{clj}-v_{cli}),\qquad Z_{cm}^{(ij)}=-0.085\,(w_{j}-w_{i})\\
T_{cm}^{(ij)} & =V_{cm}^{-1/4}\!\left[2\sum_{l}x_{l}\!\left(v_{clj}^{1/4}t_{clj}-v_{cli}^{1/4}t_{cli}\right)-\tfrac{1}{4}V_{cm}^{-3/4}V_{cm}^{(ij)}T_{cm}\right]\\
P_{cm}^{(ij)} & =P_{cm}\!\left(\frac{Z_{cm}^{(ij)}}{z_{cm}}+\frac{T_{cm}^{(ij)}}{T_{cm}}-\frac{V_{cm}^{(ij)}}{V_{cm}}\right)
\end{aligned}
\]


#### Temperature derivative

Since $T_{r}=T/T_{cm}$ and $P_{r}=P/P_{cm}$ is constant in $T$ , each fluid needs the Benedict-Webb-Rubin derivatives at fixed $P_{r}$ . Writing the residual $\mathcal{R}=P_{r}V_{r}/T_{r}-1-B/V_{r}-C/V_{r}^{2}-D/V_{r}^{5}-\phi_{4}=0$ with $\phi_{4}=(c_{4}/T_{r}^{3}V_{r}^{2})(\beta+\gamma/V_{r}^{2})e^{-\gamma/V_{r}^{2}}$ , implicit differentiation gives



<a id="eq:d-lkp-dVr"></a>

\[
\begin{aligned}\frac{dV_{r}}{dT_{r}} & =-\frac{\partial\mathcal{R}/\partial T_{r}}{\partial\mathcal{R}/\partial V_{r}},\qquad\frac{dz}{dT_{r}}=\frac{P_{r}}{T_{r}}\frac{dV_{r}}{dT_{r}}-\frac{P_{r}V_{r}}{T_{r}^{2}}\\
\frac{\partial\mathcal{R}}{\partial V_{r}} & =\frac{P_{r}}{T_{r}}+\frac{B}{V_{r}^{2}}+\frac{2C}{V_{r}^{3}}+\frac{5D}{V_{r}^{6}}-\frac{\partial\phi_{4}}{\partial V_{r}}\\
\frac{\partial\mathcal{R}}{\partial T_{r}} & =-\frac{P_{r}V_{r}}{T_{r}^{2}}-\frac{B'}{V_{r}}-\frac{C'}{V_{r}^{2}}-\frac{D'}{V_{r}^{5}}+\frac{3\phi_{4}}{T_{r}}
\end{aligned}
\]


with $B'=b_{2}/T_{r}^{2}+2b_{3}/T_{r}^{3}+3b_{4}/T_{r}^{4}$ , $C'=c_{2}/T_{r}^{2}-3c_{3}/T_{r}^{4}$ , $D'=-d_{2}/T_{r}^{2}$ . The single-fluid fugacity coefficient and its temperature derivative are



<a id="eq:d-lkp-dlnfug"></a>

\[
\begin{aligned}\ln\varphi & =z-1-\ln z+\frac{B}{V_{r}}+\frac{C}{2V_{r}^{2}}+\frac{D}{5V_{r}^{5}}+E\\
\frac{d\ln\varphi}{dT_{r}} & =\frac{dz}{dT_{r}}\!\left(1-\frac{1}{z}\right)+\Big(\frac{B'}{V_{r}}-\frac{B}{V_{r}^{2}}\frac{dV_{r}}{dT_{r}}\Big)+\Big(\frac{C'}{2V_{r}^{2}}-\frac{C}{V_{r}^{3}}\frac{dV_{r}}{dT_{r}}\Big)\\
 & \quad{}+\Big(\frac{D'}{5V_{r}^{5}}-\frac{D}{V_{r}^{6}}\frac{dV_{r}}{dT_{r}}\Big)+\frac{dE}{dT_{r}}
\end{aligned}
\]


where $E=\dfrac{c_{4}}{2T_{r}^{3}\gamma}\big[\beta+1-(\beta+1+\gamma/V_{r}^{2})e^{-\gamma/V_{r}^{2}}\big]$ . The mixture derivatives interpolate the two fluids, $dz_{m}/dT_{r}=dz^{(s)}/dT_{r}+(w_{m}/w_{h})(dz^{(h)}/dT_{r}-dz^{(s)}/dT_{r})$ and likewise for $d\ln\varphi_{m}/dT_{r}$ , and the component derivative assembles as



<a id="eq:d-lkp-dT"></a>

\[
\frac{\partial\ln\varphi_{i}}{\partial T}=\frac{1}{T_{cm}}\frac{d\ln\varphi_{m}}{dT_{r}}+S_{i}^{a}\!\left(\frac{\hat{H}}{T^{2}}-\frac{1}{T}\frac{d\hat{H}}{dT}\right)+\frac{S_{i}^{b}}{P_{cm}T_{cm}}\frac{dz_{m}}{dT_{r}}-\frac{S_{i}^{c}}{T_{cm}}\frac{d}{dT_{r}}\!\Big(\tfrac{\partial\ln\varphi_{m}}{\partial w_{m}}\Big)
\]


The reduced enthalpy-departure derivative is the only non-analytical ingredient and is taken by a tight central finite difference. The composition derivative differentiates the same fugacity expression through the pseudo-critical mixing rules; because those rules enter both directly and through the corresponding-states functions, a few mixture terms are likewise taken by finite differences, so the package uses a hybrid composition derivative.

