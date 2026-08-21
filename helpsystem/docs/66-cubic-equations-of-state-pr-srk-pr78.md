# Cubic Equations of State (PR, SRK, PR78)

Peng-Robinson, SRK and Peng-Robinson (1978) share one generalized cubic, with constants $(\epsilon_{1},\epsilon_{2})=(1+\sqrt{2},\,1-\sqrt{2})$ for the Peng-Robinson family and $(1,0)$ for SRK. Write $u=\epsilon_{1}+\epsilon_{2}$ , $w=\epsilon_{1}\epsilon_{2}$ , $\delta=\epsilon_{1}-\epsilon_{2}$ . The fugacity coefficient is



<a id="eq:d-cub-lnphi"></a>

\[
\ln\varphi_{i}=\frac{b_{i}}{b}(Z-1)-\ln(Z-B)-D\,q_{i}\,L
\]


with the shorthands $D=\dfrac{A}{B\delta}$ , $q_{i}=\dfrac{2\bar{a}_{i}}{a}-\dfrac{b_{i}}{b}$ , $L=\ln\dfrac{Z+\epsilon_{1}B}{Z+\epsilon_{2}B}$ , the mixture parameters $a=\sum_{i}\sum_{j}x_{i}x_{j}a_{ij}$ , $b=\sum_{i}x_{i}b_{i}$ , $\bar{a}_{i}=\sum_{j}x_{j}a_{ij}$ , and $A=aP/(RT)^{2}$ , $B=bP/(RT)$ . The compressibility $Z$ is the selected root of



<a id="eq:d-cub-cubic"></a>

\[
\begin{aligned}Z^{3}+c_{2}Z^{2}+c_{1}Z+c_{0} & =0\\
c_{2}=(u-1)B-1,\quad c_{1} & =A+wB^{2}-uB(1+B)\\
c_{0} & =-AB-wB^{2}(1+B)
\end{aligned}
\]


#### Temperature derivative

Temperature enters through the attractive parameter. With the alpha function



<a id="eq:d-cub-a"></a>

\[
\begin{aligned}a_{i} & =\Omega_{a}\frac{R^{2}T_{c,i}^{2}}{P_{c,i}}\alpha_{i},\qquad\alpha_{i}=\left[1+\kappa_{i}\!\left(1-\sqrt{T/T_{c,i}}\right)\right]^{2}\\
a_{ij} & =\sqrt{a_{i}a_{j}}\,(1-k_{ij})
\end{aligned}
\]




<a id="eq:d-cub-dadt"></a>

\[
\begin{aligned}\frac{da_{i}}{dT} & =-\Omega_{a}\frac{R^{2}T_{c,i}^{2}}{P_{c,i}}\frac{\kappa_{i}\sqrt{\alpha_{i}}}{\sqrt{T\,T_{c,i}}}\\
\frac{da_{ij}}{dT} & =\frac{1-k_{ij}}{2\sqrt{a_{i}a_{j}}}\!\left(a_{i}\frac{da_{j}}{dT}+a_{j}\frac{da_{i}}{dT}\right)
\end{aligned}
\]


so that $\partial a/\partial T=\sum_{i}\sum_{j}x_{i}x_{j}\,da_{ij}/dT$ and $\partial\bar{a}_{i}/\partial T=\sum_{j}x_{j}\,da_{ij}/dT$ . The dimensionless groups and the compressibility follow as



<a id="eq:d-cub-dABdt"></a>

\[
\frac{\partial B}{\partial T}=-\frac{B}{T},\qquad\frac{\partial A}{\partial T}=\frac{P}{(RT)^{2}}\!\left(\frac{\partial a}{\partial T}-\frac{2a}{T}\right)
\]




<a id="eq:d-cub-dcdt"></a>

\[
\begin{aligned}\frac{\partial c_{2}}{\partial T} & =(u-1)\frac{\partial B}{\partial T}\\
\frac{\partial c_{1}}{\partial T} & =\frac{\partial A}{\partial T}+\left[2wB-u(1+2B)\right]\frac{\partial B}{\partial T}\\
\frac{\partial c_{0}}{\partial T} & =-B\frac{\partial A}{\partial T}-\left[A+w(2B+3B^{2})\right]\frac{\partial B}{\partial T}
\end{aligned}
\]




<a id="eq:d-cub-dZdt"></a>

\[
\frac{\partial Z}{\partial T}=-\frac{Z^{2}\,\partial c_{2}/\partial T+Z\,\partial c_{1}/\partial T+\partial c_{0}/\partial T}{3Z^{2}+2c_{2}Z+c_{1}}
\]


The full temperature derivative of the fugacity coefficient is then



<a id="eq:d-cub-dlnphidt"></a>

\[
\begin{aligned}\frac{\partial\ln\varphi_{i}}{\partial T} & =\frac{b_{i}}{b}\frac{\partial Z}{\partial T}-\frac{\partial Z/\partial T-\partial B/\partial T}{Z-B}\\
 & \quad{}-\frac{\partial D}{\partial T}q_{i}L-D\frac{\partial q_{i}}{\partial T}L-Dq_{i}\frac{\partial L}{\partial T}
\end{aligned}
\]


with the building-block derivatives



<a id="eq:d-cub-dtblocks"></a>

\[
\begin{aligned}\frac{\partial D}{\partial T} & =D\!\left(\frac{1}{A}\frac{\partial A}{\partial T}-\frac{1}{B}\frac{\partial B}{\partial T}\right),\qquad\frac{\partial q_{i}}{\partial T}=\frac{2}{a}\!\left(\frac{\partial\bar{a}_{i}}{\partial T}-\frac{\bar{a}_{i}}{a}\frac{\partial a}{\partial T}\right)\\
\frac{\partial L}{\partial T} & =\frac{\partial Z/\partial T+\epsilon_{1}\partial B/\partial T}{Z+\epsilon_{1}B}-\frac{\partial Z/\partial T+\epsilon_{2}\partial B/\partial T}{Z+\epsilon_{2}B}
\end{aligned}
\]


#### Composition derivative

On the one-mole basis the partial-molar (Michelsen-Mollerup) derivatives of the mixture parameters are



<a id="eq:d-cub-dan"></a>

\[
\begin{aligned}\frac{\partial b}{\partial n_{j}} & =b_{j}-b,\quad\frac{\partial a}{\partial n_{j}}=2\bar{a}_{j}-2a,\quad\frac{\partial\bar{a}_{i}}{\partial n_{j}}=a_{ij}-\bar{a}_{i}\\
\frac{\partial A}{\partial n_{j}} & =\frac{A}{a}\frac{\partial a}{\partial n_{j}},\qquad\frac{\partial B}{\partial n_{j}}=\frac{B}{b}\frac{\partial b}{\partial n_{j}}
\end{aligned}
\]


The cubic-coefficient and compressibility derivatives have the same form as the temperature case, with the temperature derivative replaced by the mole-number derivative throughout. The full composition derivative of the fugacity coefficient is



<a id="eq:d-cub-dlnphidn"></a>

\[
\begin{aligned}\frac{\partial\ln\varphi_{i}}{\partial n_{j}} & =\frac{b_{i}}{b}\!\left(\frac{\partial Z}{\partial n_{j}}-\frac{Z-1}{b}\frac{\partial b}{\partial n_{j}}\right)-\frac{\partial Z/\partial n_{j}-\partial B/\partial n_{j}}{Z-B}\\
 & \quad{}-\frac{\partial D}{\partial n_{j}}q_{i}L-D\frac{\partial q_{i}}{\partial n_{j}}L-Dq_{i}\frac{\partial L}{\partial n_{j}}
\end{aligned}
\]


with



<a id="eq:d-cub-dnblk"></a>

\[
\begin{aligned}\frac{\partial D}{\partial n_{j}} & =D\!\left(\frac{1}{A}\frac{\partial A}{\partial n_{j}}-\frac{1}{B}\frac{\partial B}{\partial n_{j}}\right)\\
\frac{\partial q_{i}}{\partial n_{j}} & =\frac{2}{a}\!\left(\frac{\partial\bar{a}_{i}}{\partial n_{j}}-\frac{\bar{a}_{i}}{a}\frac{\partial a}{\partial n_{j}}\right)+\frac{b_{i}}{b^{2}}\frac{\partial b}{\partial n_{j}}\\
\frac{\partial L}{\partial n_{j}} & =\frac{\partial Z/\partial n_{j}+\epsilon_{1}\partial B/\partial n_{j}}{Z+\epsilon_{1}B}-\frac{\partial Z/\partial n_{j}+\epsilon_{2}\partial B/\partial n_{j}}{Z+\epsilon_{2}B}
\end{aligned}
\]


A useful check is the Maxwell symmetry $\partial\ln\varphi_{i}/\partial n_{j}=\partial\ln\varphi_{j}/\partial n_{i}$ .

