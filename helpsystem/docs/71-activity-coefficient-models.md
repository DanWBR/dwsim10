# Activity-Coefficient Models

For an activity-coefficient model the liquid logarithmic fugacity coefficient and its derivatives are



<a id="eq:d-act-split"></a>

\[
\begin{aligned}\ln\varphi_{i}^{L} & =\ln\gamma_{i}+\ln\frac{P_{i}^{\mathrm{sat}}}{P}+\ln\mathrm{Poy}_{i}\\
\frac{\partial\ln\varphi_{i}^{L}}{\partial T} & =\frac{\partial\ln\gamma_{i}}{\partial T}+\frac{d\ln P_{i}^{\mathrm{sat}}}{dT},\qquad\frac{\partial\ln\varphi_{i}^{L}}{\partial n_{j}}=\frac{\partial\ln\gamma_{i}}{\partial n_{j}}
\end{aligned}
\]


with the vapor-pressure term taken from the compound correlation. The activity-coefficient derivatives follow. Composition derivatives are given with respect to $x_{k}$ and are converted to the mole-number basis by the projection of the Scope and Notation section.

#### NRTL

Writing the NRTL activity coefficient with the column sums



<a id="eq:d-nrtl-lng"></a>

\[
\begin{aligned}\ln\gamma_{i} & =\frac{S_{1i}}{S_{2i}}+\sum_{j}\frac{x_{j}G_{ij}}{D_{j}}\!\left(\tau_{ij}-E_{j}\right)\\
S_{1i} & =\sum_{j}x_{j}\tau_{ji}G_{ji},\quad S_{2i}=\sum_{j}x_{j}G_{ji},\quad D_{j}=\sum_{k}x_{k}G_{kj}\\
N_{j} & =\sum_{m}x_{m}\tau_{mj}G_{mj},\quad E_{j}=N_{j}/D_{j}
\end{aligned}
\]




<a id="eq:d-nrtl-tau"></a>

\[
\begin{aligned}\tau_{ij} & =\frac{A_{ij}+B_{ij}T+C_{ij}T^{2}}{RT},\qquad G_{ij}=e^{-\alpha_{ij}\tau_{ij}}\\
\frac{\partial\tau_{ij}}{\partial T} & =\frac{B_{ij}+2C_{ij}T}{RT}-\frac{A_{ij}+B_{ij}T+C_{ij}T^{2}}{RT^{2}}\\
\frac{\partial G_{ij}}{\partial T} & =-\alpha_{ij}G_{ij}\frac{\partial\tau_{ij}}{\partial T}
\end{aligned}
\]


The temperature derivative (composition fixed) is



<a id="eq:d-nrtl-dt"></a>

\[
\begin{aligned}\frac{\partial\ln\gamma_{i}}{\partial T} & =\frac{S_{1i}'S_{2i}-S_{1i}S_{2i}'}{S_{2i}^{2}}\\
 & \quad{}+\sum_{j}x_{j}\!\left[\frac{G_{ij}'D_{j}-G_{ij}D_{j}'}{D_{j}^{2}}(\tau_{ij}-E_{j})+\frac{G_{ij}}{D_{j}}(\tau_{ij}'-E_{j}')\right]
\end{aligned}
\]


where a prime denotes $\partial/\partial T$ , $S_{1i}'=\sum_{j}x_{j}(\tau_{ji}'G_{ji}+\tau_{ji}G_{ji}')$ , $S_{2i}'=\sum_{j}x_{j}G_{ji}'$ , $D_{j}'=\sum_{k}x_{k}G_{kj}'$ , $N_{j}'=\sum_{m}x_{m}(\tau_{mj}'G_{mj}+\tau_{mj}G_{mj}')$ and $E_{j}'=(N_{j}'D_{j}-N_{j}D_{j}')/D_{j}^{2}$ . The composition derivative (temperature fixed, so $\tau$ and $G$ constant) is



<a id="eq:d-nrtl-dx"></a>

\[
\begin{aligned}\frac{\partial\ln\gamma_{i}}{\partial x_{k}} & =\frac{G_{ki}}{S_{2i}}\!\left(\tau_{ki}-\frac{S_{1i}}{S_{2i}}\right)+\frac{G_{ik}}{D_{k}}(\tau_{ik}-E_{k})\\
 & \quad{}-\sum_{j}x_{j}\frac{G_{ij}G_{kj}}{D_{j}^{2}}\!\left[(\tau_{ij}-E_{j})+(\tau_{kj}-E_{j})\right]
\end{aligned}
\]


#### UNIQUAC

UNIQUAC splits into a combinatorial part, which depends on composition only through $S_{r}=\sum_{k}r_{k}x_{k}$ and $S_{q}=\sum_{k}q_{k}x_{k}$ , and a residual part built from $\tau_{ji}=e^{-a_{ji}/T}$ (with $z=10$ and $l_{i}=\tfrac{z}{2}(r_{i}-q_{i})-(r_{i}-1)$ ):



<a id="eq:d-uniq-lng"></a>

\[
\begin{aligned}\ln\gamma_{i}^{C} & =\ln\frac{r_{i}}{S_{r}}+\frac{z}{2}q_{i}\ln\frac{q_{i}S_{r}}{r_{i}S_{q}}+l_{i}-\frac{r_{i}}{S_{r}}\sum_{j}x_{j}l_{j}\\
\ln\gamma_{i}^{R} & =q_{i}\!\left[1-\ln S_{i}-\sum_{j}\frac{\theta_{j}\tau_{ij}}{S_{j}}\right]
\end{aligned}
\]


with $\theta_{i}=q_{i}x_{i}/S_{q}$ and $S_{i}=\sum_{j}\theta_{j}\tau_{ji}$ . Only the residual part depends on temperature, through $\partial\tau_{ji}/\partial T=\tau_{ji}\,a_{ji}/T^{2}$ , giving



<a id="eq:d-uniq-dt"></a>

\[
\begin{aligned}\frac{\partial\ln\gamma_{i}^{R}}{\partial T} & =q_{i}\!\left[-\frac{S_{i}'}{S_{i}}-\sum_{j}\theta_{j}\frac{\tau_{ij}'S_{j}-\tau_{ij}S_{j}'}{S_{j}^{2}}\right],\qquad S_{i}'=\sum_{j}\theta_{j}\frac{\partial\tau_{ji}}{\partial T}\end{aligned}
\]


The composition derivatives are



<a id="eq:d-uniq-dxC"></a>

\[
\begin{aligned}\frac{\partial\ln\gamma_{i}^{C}}{\partial x_{k}} & =-\frac{r_{k}}{S_{r}}+\frac{z}{2}q_{i}\!\left(\frac{r_{k}}{S_{r}}-\frac{q_{k}}{S_{q}}\right)+\frac{r_{i}r_{k}}{S_{r}^{2}}\sum_{j}x_{j}l_{j}-\frac{r_{i}}{S_{r}}l_{k}\end{aligned}
\]




<a id="eq:d-uniq-dxR"></a>

\[
\begin{aligned}\frac{\partial\ln\gamma_{i}^{R}}{\partial x_{k}} & =-\frac{q_{i}}{S_{i}}\frac{\partial S_{i}}{\partial x_{k}}-q_{i}\sum_{j}\tau_{ij}\frac{(\partial\theta_{j}/\partial x_{k})S_{j}-\theta_{j}(\partial S_{j}/\partial x_{k})}{S_{j}^{2}}\\
\frac{\partial\theta_{j}}{\partial x_{k}} & =\frac{q_{j}\delta_{jk}-\theta_{j}q_{k}}{S_{q}},\qquad\frac{\partial S_{i}}{\partial x_{k}}=\sum_{j}\frac{\partial\theta_{j}}{\partial x_{k}}\tau_{ji}
\end{aligned}
\]


#### Wilson

With $s_{i}=\sum_{j}x_{j}\Lambda_{ij}$ and $\Lambda_{ij}=(V_{j}/V_{i})\,e^{-a_{ij}/(RT)}$ (molar volumes at a fixed reference temperature, so $\partial\Lambda_{ij}/\partial T=\Lambda_{ij}\,a_{ij}/(RT^{2})$ ):



<a id="eq:d-wil-lng"></a>

\[
\ln\gamma_{i}=1-\ln s_{i}-\sum_{k}\frac{x_{k}\Lambda_{ki}}{s_{k}}
\]




<a id="eq:d-wil-dt"></a>

\[
\begin{aligned}\frac{\partial\ln\gamma_{i}}{\partial T} & =-\frac{s_{i}'}{s_{i}}-\sum_{k}x_{k}\frac{\Lambda_{ki}'s_{k}-\Lambda_{ki}s_{k}'}{s_{k}^{2}},\qquad s_{i}'=\sum_{j}x_{j}\frac{\partial\Lambda_{ij}}{\partial T}\end{aligned}
\]




<a id="eq:d-wil-dx"></a>

\[
\frac{\partial\ln\gamma_{i}}{\partial x_{m}}=-\frac{\Lambda_{im}}{s_{i}}-\frac{\Lambda_{mi}}{s_{m}}+\sum_{k}x_{k}\frac{\Lambda_{ki}\Lambda_{km}}{s_{k}^{2}}
\]


#### Group-Contribution Models (UNIFAC, Modified UNIFAC)

UNIFAC is a combinatorial plus a residual (solution-of-groups) part, $\ln\gamma_{i}=\ln\gamma_{i}^{C}+\ln\gamma_{i}^{R}$ . With $J_{i}=r_{i}/\sum_{k}x_{k}r_{k}$ and $L_{i}=q_{i}/\sum_{k}x_{k}q_{k}$ the combinatorial part is



<a id="eq:d-uni-comb"></a>

\[
\ln\gamma_{i}^{C}=1-J_{i}+\ln J_{i}-5q_{i}\!\left(1-\frac{J_{i}}{L_{i}}+\ln\frac{J_{i}}{L_{i}}\right)
\]


The residual part uses the group surface fraction $\Theta_{m}=\Big(\sum_{i}x_{i}q_{i}\nu_{m}^{(i)}\Big)/\sum_{k}x_{k}q_{k}$ , the per-component group sum $\beta_{im}=\sum_{k}\nu_{k}^{(i)}\tau_{km}$ and $s_{m}=\sum_{l}\Theta_{l}\tau_{lm}$ , where $\nu_{k}^{(i)}$ is the number of groups of type $k$ in molecule $i$ :



<a id="eq:d-uni-res"></a>

\[
\ln\gamma_{i}^{R}=q_{i}\!\left[1-\sum_{m}\!\left(\frac{\Theta_{m}\beta_{im}}{s_{m}}-\nu_{m}^{(i)}\ln\frac{\beta_{im}}{s_{m}}\right)\right]
\]


Only $\tau_{km}$ depends on temperature. For the original UNIFAC $\tau_{km}=e^{-a_{km}/T}$ and $d\tau_{km}/dT=-\tau_{km}\ln\tau_{km}/T$ ; the combinatorial part is temperature-independent. With $\beta_{im}'=\sum_{k}\nu_{k}^{(i)}\,d\tau_{km}/dT$ and $s_{m}'=\sum_{l}\Theta_{l}\,d\tau_{lm}/dT$ ,



<a id="eq:d-uni-dt"></a>

\[
\frac{\partial\ln\gamma_{i}^{R}}{\partial T}=-q_{i}\sum_{m}\!\left[\Theta_{m}\frac{\beta_{im}'s_{m}-\beta_{im}s_{m}'}{s_{m}^{2}}-\nu_{m}^{(i)}\!\left(\frac{\beta_{im}'}{\beta_{im}}-\frac{s_{m}'}{s_{m}}\right)\right]
\]


For the composition derivative the combinatorial ratios and the group fraction vary through



<a id="eq:d-uni-dxfrac"></a>

\[
\begin{aligned}\frac{\partial J_{i}}{\partial x_{p}} & =-\frac{r_{i}r_{p}}{\left(\sum_{k}x_{k}r_{k}\right)^{2}},\qquad\frac{\partial L_{i}}{\partial x_{p}}=-\frac{q_{i}q_{p}}{\left(\sum_{k}x_{k}q_{k}\right)^{2}}\\
\frac{\partial\Theta_{m}}{\partial x_{p}} & =\frac{q_{p}\left(\nu_{m}^{(p)}-\Theta_{m}\right)}{\sum_{k}x_{k}q_{k}},\qquad\frac{\partial s_{m}}{\partial x_{p}}=\sum_{l}\frac{\partial\Theta_{l}}{\partial x_{p}}\tau_{lm}
\end{aligned}
\]


( $\beta_{im}$ is composition-independent). Writing $R_{i}=J_{i}/L_{i}$ with $\partial R_{i}/\partial x_{p}=\big[(\partial J_{i}/\partial x_{p})L_{i}-J_{i}(\partial L_{i}/\partial x_{p})\big]/L_{i}^{2}$ , the combinatorial and residual composition derivatives are



<a id="eq:d-uni-dx"></a>

\[
\begin{aligned}\frac{\partial\ln\gamma_{i}^{C}}{\partial x_{p}} & =-\frac{\partial J_{i}}{\partial x_{p}}+\frac{1}{J_{i}}\frac{\partial J_{i}}{\partial x_{p}}-5q_{i}\!\left(-\frac{\partial R_{i}}{\partial x_{p}}+\frac{1}{R_{i}}\frac{\partial R_{i}}{\partial x_{p}}\right)\\
\frac{\partial\ln\gamma_{i}^{R}}{\partial x_{p}} & =-q_{i}\sum_{m}\!\left[\frac{\partial\Theta_{m}}{\partial x_{p}}\frac{\beta_{im}}{s_{m}}-\Theta_{m}\frac{\beta_{im}}{s_{m}^{2}}\frac{\partial s_{m}}{\partial x_{p}}+\nu_{m}^{(i)}\frac{1}{s_{m}}\frac{\partial s_{m}}{\partial x_{p}}\right]
\end{aligned}
\]


The two parts are summed and projected onto the mole-number basis. For Modified UNIFAC (Dortmund and NIST) the combinatorial volume term uses $r_{i}^{3/4}$ in $J_{i}$ , and the group interaction is temperature-dependent, $\Psi_{km}=\exp\!\big[-(a_{km}+b_{km}T+c_{km}T^{2})/T\big]$ , with



<a id="eq:d-mod-dpsidt"></a>

\[
\frac{d\ln\Psi_{km}}{dT}=\frac{a_{km}-c_{km}T^{2}}{T^{2}}
\]


everything else being identical.

