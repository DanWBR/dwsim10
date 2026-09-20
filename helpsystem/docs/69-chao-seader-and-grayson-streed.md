# Chao-Seader and Grayson-Streed

These two are semi-empirical correlations rather than equations of state, and both are explicit in temperature: on the liquid side there is no cubic root to track and no iteration to unwind, which makes their derivatives the most direct in this appendix. The liquid fugacity coefficient is a product of a pure-component term and a regular-solution activity coefficient,



<a id="eq:d-cs-phi"></a>

\[
\varphi_{i}^{L}=\nu_{i}\,\gamma_{i},\qquad\log_{10}\nu_{i}=\log_{10}\nu_{i}^{(0)}+\omega_{i}^{\mathrm{CS}}\log_{10}\nu_{i}^{(1)}
\]


where $\log_{10}\nu_{i}^{(0)}$ is a polynomial in the reduced temperature $T_{r,i}$ with pressure-dependent tail terms, $\log_{10}\nu_{i}^{(1)}$ is a second polynomial in $T_{r,i}$ alone, and $\omega_{i}^{\mathrm{CS}}$ is the Chao-Seader acentric factor. That last quantity is tabulated separately from the ordinary acentric factor and the two must not be interchanged: substituting one for the other leaves the fugacity coefficients themselves untouched and puts a systematic error of tens of percent on the liquid derivative alone, which is exactly the kind of defect a verification against finite differences is there to catch. Differentiating term by term and converting from the decimal logarithm,



<a id="eq:d-cs-dnudt"></a>

\[
\frac{\partial\ln\nu_{i}}{\partial T}=\frac{\ln10}{T_{c,i}}\left(\frac{d\log_{10}\nu_{i}^{(0)}}{dT_{r,i}}+\omega_{i}^{\mathrm{CS}}\frac{d\log_{10}\nu_{i}^{(1)}}{dT_{r,i}}\right)
\]


The activity coefficient is the regular-solution expression $\ln\gamma_{i}=V_{i}\left(\delta_{i}-\bar{\delta}\right)^{2}/(RT)$ , with $\bar{\delta}=\sum_{j}x_{j}V_{j}\delta_{j}\big/\sum_{j}x_{j}V_{j}$ . The molar volumes and solubility parameters are taken as constants, so the whole of the temperature dependence sits in the $1/T$ and



<a id="eq:d-cs-dgammadt"></a>

\[
\frac{\partial\ln\gamma_{i}}{\partial T}=-\frac{\ln\gamma_{i}}{T}
\]


The vapour phase of both correlations is Redlich-Kwong, and two properties of that equation do most of the work. First, $a_{i}\propto T^{-1/2}$ while $b_{i}$ is constant, so the mixture $a_{m}$ inherits the same power and



<a id="eq:d-cs-dabdt"></a>

\[
\frac{\partial A}{\partial T}=-\frac{5A}{2T},\qquad\frac{\partial B}{\partial T}=-\frac{B}{T}
\]


Second, the ratio $a_{i}/a_{m}$ appearing under the square root of the fugacity expression has no temperature dependence at all, because the $T^{-1/2}$ cancels exactly between numerator and denominator. Writing the fugacity term that carries it as $t_{i}=(A/B)\left(b_{i}/b_{m}-2\sqrt{a_{i}/a_{m}}\right)$ , the bracket is pure composition and only $A/B$ moves, which gives $\partial t_{i}/\partial T=-3t_{i}/(2T)$ in one step. The compressibility derivative follows from implicit differentiation of the cubic exactly as in the earlier sections, taken on the same root the fugacity routine selected.

Only temperature derivatives are provided for these two packages; the composition derivative falls back to finite differences.

