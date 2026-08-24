# Thermodynamic Properties

#### Phase Equilibria Calculation

At vapor–liquid equilibrium (VLE), the fugacity of every component is equal in all coexisting phases :



<a id="eq:elv1"></a>

\[
f_{i}^{L}=f_{i}^{V}
\]


The fugacity of a component in a mixture depends on temperature, pressure, and composition. To relate $f_{i}^{V}$ to these variables, the fugacity coefficient is defined as



\[
\phi_{i}=\frac{f_{i}^{V}}{y_{i}P},
\]


which can be calculated from PVT data, commonly obtained from an equation of state. For a mixture of ideal gases, $\phi_{i}=1$ .

The fugacity of the $i$ component in the liquid phase is related to the composition of that phase by the activity coefficient $\gamma_{i}$ , which by itself is related to $x_{i}$ and standard-state fugacity $f_{i}^{0}$ by



\[
\gamma_{i}=\frac{f_{i}^{L}}{x_{i}f_{i}^{0}}.
\]


The standard-state fugacity $f_{i}^{0}$ is the fugacity of pure component $i$ at the system temperature and a reference pressure and composition. In DWSIM, the standard-state fugacity of each component is taken as that of the pure liquid at the system temperature and pressure (Lewis–Randall convention).

If an Equation of State is used to calculate equilibria, fugacity of the $i$ -th component in the liquid phase is calculated by



\[
\phi_{i}=\frac{f_{i}^{L}}{x_{i}P},
\]


with the fugacity coefficient $\phi_{i}$ calculated by the EOS, just like it is for the same component in the vapor phase.

The fugacity coefficient of the $i$ -th component either in the liquid or in the vapor phase is obtained from the same Equation of State through the following expressions


\[
\begin{eqnarray}
RT\ln\phi_{i}^{L} & = & \intop_{V^{L}}^{\infty}\left[\left(\frac{\partial P}{\partial n_{i}}\right)_{T,V,n_{j}}-\frac{RT}{V}\right]dV-RT\ln Z^{L},
\end{eqnarray}
\]


\[
RT\ln\phi_{i}^{V}=\intop_{V^{V}}^{\infty}\left[\left(\frac{\partial P}{\partial n_{i}}\right)_{T,V,n_{j}}-\frac{RT}{V}\right]dV-RT\ln Z^{V},
\]


where the compressibility factor $Z$ is given by



\[
Z^{L}=\frac{PV^{L}}{RT}
\]


\[
Z^{V}=\frac{PV^{V}}{RT}
\]


##### Fugacity Coefficient calculation models

###### *Peng-Robinson Equation of State* {#peng-robinson-equation-of-state .unnumbered}

The Peng–Robinson (PR) equation is a cubic equation of state that relates temperature, pressure, and molar volume for pure components and mixtures. Cubic equations of state are the simplest models capable of representing both liquid and vapor phases simultaneously. The PR EOS is written as


<a id="eq:PR"></a>

\[
P=\frac{RT}{(V-b)}-\frac{a(T)}{V(V+b)+b(V-b)}
\]








where

$P$ pressure

$R$ universal gas constant

$v$ molar volume

$b$ co-volume parameter (related to molecular size)

$a$ attraction parameter (related to intermolecular forces)



For pure substances, the $a$ and $b$ parameters are given by:



\[
a(T)=[1+(0.37464+1.54226\omega-0.26992\omega^{2})(1-T_{r}^{(1/2)})]^{2}0.45724(R^{2}T_{c}^{2})/P_{c}
\]




\[
b=0.07780(RT_{c})/P_{c}
\]








where

$\omega$ acentric factor

$T_{c}$ critical temperature

$P_{c}$ critical pressure

$T_{r}$ reduced temperature, $T/Tc$



For mixtures, Equation[\[eq:PR\]](#eq:PR) is applied with mixture-averaged parameters $a_{m}$ and $b_{m}$ computed from the van der Waals one-fluid mixing rules:



<a id="eq:mixrule1"></a>

\[
a_{m}=\sum_{i}\sum_{j}x_{i}x_{j}\sqrt{(a_{i}a_{j})}(1-k_{ij})
\]




<a id="eq:mixrule2"></a>

\[
b_{m}=\sum_{i}x_{i}b_{i}
\]








where

$x_{i,j}$ molar fraction of the $i$ or $j$ component in the phase (liquid or vapor)

$a_{i,j}$ $i$ or $j$ component $a$ constant

$b_{i,j}$ $i$ or $j$ component $b$ constant

$k_{ij}$ binary interaction parameter for the $i$ – $j$ pair (fitted to experimental VLE data)



> 
>
> | *<span class="image placeholder" original-image-src="dialog-information.png" original-image-title="">image</span>* | <span class="sans-serif">*The binary interaction parameters used by DWSIM are loaded from a databank (file) and can be modified in the Property Package configuration window.*</span> |
> |:---|:--:|

The fugacity coefficient obtained from the Peng–Robinson EOS is given by



\[
\ln\dfrac{f_{i}}{x_{i}P}=\frac{b_{i}}{b_{m}}\left(Z-1\right)-\ln\left(Z-B\right)-\frac{A}{2\sqrt{2}B}\left(\frac{\sum_{k}x_{k}a_{ki}}{a_{m}}-\frac{b_{i}}{b_{m}}\right)\ln\left(\frac{Z+2,414B}{Z-0,414B}\right),
\]


where $Z$ is the phase compressibility factor (liquid or vapor), obtained by solving the cubic polynomial derived from Equation [\[eq:PR\]](#eq:PR),



<a id="eq:PR_Z"></a>

\[
Z^{3}-(1-B)Z^{2}+(A-3B^{2}-2B)Z-(AB-B^{2}-2B)=0,
\]




\[
A=\frac{a_{m}P}{R^{2}T^{2}}
\]




\[
B=\frac{b_{m}P}{RT}
\]




\[
Z=\frac{PV}{RT}
\]


###### *Peng-Robinson (1978) Equation of State* {#peng-robinson-1978-equation-of-state .unnumbered}

The 1978 version of the PR EOS introduces a modification to the $a$ term:

If $\omega_{i}\leq0.491$ :



\[
c_{i}=0.37464+1.5422\omega_{i}-0.26992\omega_{i}^{2}
\]


Else:



\[
c_{i}=0.379642+1.48503\omega_{i}-0.164423\omega{}_{i}^{2}+0.016666\omega_{i}^{3}
\]




\[
a_{i}=[1+c_{i}(1-T_{r}^{(1/2)})]^{2}0.45724(R^{2}T_{c}^{2})/P_{c}
\]


###### *Soave-Redlich-Kwong Equation of State* {#soave-redlich-kwong-equation-of-state .unnumbered}

The Soave-Redlich-Kwong Equation is also a cubic equation of state in volume,



<a id="eq:SRK"></a>

\[
P=\frac{RT}{(V-b)}-\frac{a(T)}{V(V+b)},
\]


The $a$ and $b$ parameters are given by:



\[
a(T)=[1+(0.48+1.574\omega-0.176\omega^{2})(1-T_{r}^{(1/2)})]^{2}0.42747(R^{2}T_{c}^{2})/P_{c}
\]




\[
b=0.08664(RT_{c})/P_{c}
\]


The equations [\[eq:mixrule1\]](#eq:mixrule1) and [\[eq:mixrule2\]](#eq:mixrule2) are used to calculate mixture parameters. Fugacity is calculated by



\[
\ln\dfrac{f_{i}}{x_{i}P}=\frac{b_{i}}{b_{m}}\left(Z-1\right)-\ln\left(Z-B\right)-\frac{A}{B}\left(\frac{\sum_{k}x_{k}a_{ki}}{a_{m}}-\frac{b_{i}}{b_{m}}\right)\ln\left(\frac{Z+B}{Z}\right)
\]


The phase compressibility factor $Z$ is obtained from the equation [\[eq:SRK\]](#eq:SRK),



<a id="eq:SRK_Z"></a>

\[
Z^{3}-Z^{2}+(A-B-B^{2})Z-AB=0,
\]




\[
A=\frac{a_{m}P}{R^{2}T^{2}}
\]




\[
B=\frac{b_{m}P}{RT}
\]




\[
Z=\frac{PV}{RT}
\]


The equations [\[eq:PR_Z\]](#eq:PR_Z) and [\[eq:SRK_Z\]](#eq:SRK_Z), in low temperature and pressure conditions, can provide three roots for $Z$ . In this case, if liquid properties are being calculated, the smallest root is used. If the phase is vapor, the largest root is used. The remaining root has no physical meaning; at high temperatures and pressures (conditions above the pseudocritical point), the equations [\[eq:PR_Z\]](#eq:PR_Z) and [\[eq:SRK_Z\]](#eq:SRK_Z) provides only one real root.

###### *Peng-Robinson with Volume Translation* {#peng-robinson-with-volume-translation .unnumbered}

Volume translation addresses the well-known deficiency of two-parameter cubic equations of state: inaccurate prediction of liquid molar volumes. A component-specific constant is subtracted from the EOS-calculated molar volume:



\[
v=v^{EOS}-c,
\]


where $v=$ corrected molar volume, $v^{EOS}=$ EOS-calculated volume, and $c=$ component-specific constant. This volume shift is equivalent to introducing a third parameter into the EOS, but it has the important property that phase-equilibrium conditions (fugacity equalities) remain unaltered.

It is also shown that multicomponent VLE is unaltered by introducing the volume-shift term *c* as a mole-fraction average,



\[
v_{L}=v_{L}^{EOS}-\sum x_{i}c_{i}
\]


Volume translation can be applied to any two-parameter cubic equation of state, substantially reducing the liquid-density prediction errors inherent in all such models .

###### *Peng-Robinson-Stryjek-Vera* {#peng-robinson-stryjek-vera .unnumbered}







PRSV1



A modification to the attraction term in the Peng-Robinson equation of state published by Stryjek and Vera in 1986 (PRSV) significantly improved the model’s accuracy by introducing an adjustable pure component parameter and by modifying the polynomial fit of the acentric factor.

The modification is:



\[
\kappa=\kappa_{0}+\kappa_{1}\left(1+T_{r}^{0.5}\right)\left(0.7-T_{r}\right)
\]




\[
\kappa_{0}=0.378893+1.4897153\,\omega-0.17131848\,\omega^{2}+0.0196554\,\omega^{3}
\]


where $\kappa_{1}$ is an adjustable pure component parameter. Stryjek and Vera published pure component parameters for many compounds of industrial interest in their original journal article.







PRSV2



A subsequent modification published in 1986 (PRSV2) further improved the model’s accuracy by introducing two additional pure component parameters to the previous attraction term modification.

The modification is:



\[
\kappa=\kappa_{0}+\left[\kappa_{1}+\kappa_{2}\left(\kappa_{3}-T_{r}\right)\left(1-T_{r}^{0}.5\right)\right]\left(1+T_{r}^{0.5}\right)\left(0.7-T_{r}\right)
\]




\[
\kappa_{0}=0.378893+1.4897153\,\omega-0.17131848\,\omega^{2}+0.0196554\,\omega^{3}
\]


where $\kappa_{1}$ , $\kappa_{2}$ , and $\kappa_{3}$ are adjustable pure component parameters.

PRSV2 is particularly advantageous for VLE calculations. While PRSV1 does offer an advantage over the Peng-Robinson model for describing thermodynamic behavior, it is still not accurate enough, in general, for phase equilibrium calculations. The highly non-linear behavior of phase-equilibrium calculation methods tends to amplify what would otherwise be acceptably small errors. It is therefore recommended that PRSV2 be used for equilibrium calculations when applying these models to a design. However, once the equilibrium state has been determined, the phase specific thermodynamic values at equilibrium may be determined by one of several simpler models with a reasonable degree of accuracy.

##### Chao-Seader and Grayson-Streed models

Chao-Seader () and Grayson-Streed () are older, semi-empirical models. The Grayson-Streed correlation is an extension of the Chao-Seader method with special applicability to hydrogen. In DWSIM, only the equilibrium values produced by these correlations are used in the calculations. The Lee-Kesler method is used to determine the enthalpy and entropy of liquid and vapor phases.

####### *Chao Seader* {#chao-seader .unnumbered}

This method is applicable to heavy hydrocarbon systems at pressures below 10 342 kPa (1 500 psia) and temperatures between -18 °C and 260 °C.

####### *Grayson Streed* {#grayson-streed .unnumbered}

Recommended for simulating heavy hydrocarbon systems with a high hydrogen content.

##### Calculation models for the liquid phase activity coefficient

The activity coefficient $\gamma_{i}$ quantifies the deviation of component $i$ in the liquid phase from ideal-solution (Raoult’s law) behavior. In an ideal solution, the enthalpy of mixing is zero and all intermolecular interactions are identical; the activity coefficient equals unity for every component. Non-ideal mixtures require $\gamma_{i}\neq1$ to correct the effective concentration. The activity coefficient is formally defined as



\[
\gamma_{i}=[\frac{\partial(nG^{E}/RT)}{\partial n{}_{i}}]_{P,T,n_{j\neq i}}
\]


where $G^{E}$ represents the excess Gibbs energy of the liquid solution, which is a measure of how far the solution is from ideal behavior. For an ideal solution, $\gamma_{i}$ = 1. Expressions for $G^{E}/RT$ provide values for the activity coefficients.

###### *UNIQUAC and UNIFAC models* {#uniquac-and-unifac-models .unnumbered}

The UNIQUAC (Universal Quasi-Chemical) model expresses the dimensionless excess Gibbs energy $g\equiv G^{E}/RT$ as the sum of two contributions: a combinatorial term $g^{C}$ that accounts for differences in molecular size and shape, and a residual term $g^{R}$ that accounts for energetic interactions between molecules:



\[
g\equiv g^{C}+g^{R}
\]


The $g^{C}$ function contains only pure species parameters, while the $g^{R}$ function incorporates two binary parameters for each pair of molecules. For a multicomponent system,


\[
g^{C}=\sum_{i}x_{i}\ln\phi_{i}/x_{i}+5\sum_{i}q_{i}x_{i}\ln\theta_{i}/\phi_{i}
\]


and



\[
g^{R}=-\sum_{i}q_{i}x_{i}\ln(\sum_{j}\theta_{j}\tau_{j}i)
\]


where



\[
\phi_{i}\equiv(x_{i}r_{i})/(\sum_{j}x_{j}r_{j})
\]


and



\[
\theta_{i}\equiv(x_{i}q_{i})/(\sum_{j}x_{j}q_{j})
\]


The $i$ subscript indicates the species, and $j$ is an index that represents all the species, $i$ included. All sums are over all the species. Note that $\tau_{ij}\neq\tau_{ji}$ . When $i=j$ , $\tau_{ii}=\tau_{jj}=1$ . In these equations, $r_{i}$ (a relative molecular volume) and $q_{i}$ (a relative molecular surface area) are pure species parameters. The influence of temperature in $g$ enters by means of the $\tau_{ij}$ parameters, which are temperature-dependent:



\[
\tau_{ij}=\exp(u_{ij}-u_{jj})/RT
\]


This way, the UNIQUAC parameters are values of $(u_{ij}-u_{jj})$ .

An expression for $\gamma_{i}$ is found through the application of the following relation:



<a id="eq:gamaiuniquac"></a>

\[
\ln\gamma_{i}=\left[\partial(nG^{E}/RT)/\partial n_{i}\right]{}_{(P,T,n_{j\neq i})}
\]


The result is represented by the following equations:



\[
\ln\gamma_{i}=\ln\gamma_{i}^{C}+\ln\gamma_{i}^{R}
\]




<a id="eq:gic"></a>

\[
\ln\gamma_{i}^{C}=1-J_{i}+\ln J_{i}-5q_{i}(1-J_{i}/L_{i}+\ln J_{i}/L_{i})
\]




<a id="eq:ric"></a>

\[
\ln\gamma_{i}^{R}=q_{i}(1-\ln s_{i}-\sum_{j}\theta_{j}\tau_{ij}/s_{j})
\]


where



<a id="eq:ji"></a><a id="eq:li"></a>

\[
\begin{eqnarray}
J_{i}=r_{i}/(\sum_{j}r_{j}x_{j})\\
L=q_{i}/(\sum_{j}q_{j}x_{j})
\end{eqnarray}
\]




\[
s_{i}=\sum_{l}\theta_{l}\tau_{li}
\]


Again the $i$ subscript identify the species, $j$ and $l$ are indexes which represent all the species, including $i$ . all sums are over all the species, and $\tau_{ij}=1$ for $i=j$ . The parameters values $(u_{ij}-u_{jj})$ are found by regression of binary VLE/LLE data.

The UNIFAC method for the estimation of activity coefficients depends on the concept of that a liquid mixture can be considered a solution of its own molecules. These structural units are called subgroups. The greatest advantage of this method is that a relatively small number of subgroups can be combined to form a very large number of molecules.

The activity coefficients do not only depend on the subgroup properties, but also on the interactions between these groups. Similar subgroups are related to a main group, like “CH2”, “OH”, “ACH” etc.; the identification of the main groups are only descriptive. All the subgroups that belongs to the same main group are considered identical with respect to the interaction between groups. Consequently, the parameters which characterize the interactions between the groups are identified by pairs of the main groups.

The UNIFAC method is based on the UNIQUAC equation, where the activity coefficients are given by the equation [\[eq:gamaiuniquac\]](#eq:gamaiuniquac). When applied to a solution of groups, the equations [\[eq:gic\]](#eq:gic) and [\[eq:ric\]](#eq:ric) are written in the form:



\[
\ln\gamma_{i}^{C}=1-J_{i}+\ln J_{i}-5q_{i}(1-J_{i}/L_{i}+\ln J_{i}/L_{i})
\]




\[
\ln\gamma_{i}^{R}=q_{i}(1-\sum_{k}(\theta_{k}\beta_{ik}/s_{k})-e_{ki}ln\beta_{ik}/s_{k})
\]


The parameters $J_{i}$ e $L_{i}$ are still given by eqs. [\[eq:ji2\]](#eq:ji2) and (eq.). Furthermore, the following definitions apply:


\[
r_{i}=\sum_{k}\nu_{k}^{(i)}R_{k}
\]




\[
q_{i}=\sum_{k}\nu_{k}^{(i)}Q_{k}
\]




\[
e_{ki}=(\nu_{k}^{(i)}Q_{k})/q_{i}
\]




\[
\beta_{ik}=\sum_{m}e_{mk}\tau_{mk}
\]




\[
\theta_{k}=(\sum_{i}x_{i}q_{i}e_{ki})/(\sum_{i}x_{j}q_{j})
\]




\[
s_{k}=\sum_{m}\theta_{m}\tau_{mk}
\]




\[
s_{i}=\sum_{l}\theta_{l}\tau_{li}
\]




\[
\tau_{mk}=\exp(-a_{mk})/T
\]


The $i$ subscript identify the species, and $j$ is an index that goes through all the species. The $k$ subscript identify the subgroups, and $m$ is an index that goes through all the subgroups. The parameter $\nu_{k}^{(i)}$ is the number of the $k$ subgroup in a molecule of the $i$ species. The subgroup parameter values $R_{k}$ and $Q_{k}$ and the interaction parameters $-a_{mk}$ are obtained in the literature.

###### ***Modified UNIFAC (Dortmund) model*** {#modified-unifac-dortmund-model .unnumbered}

The UNIFAC model, despite being widely used in various applications, has some limitations which are, in some way, inherent to the model. Some of these limitations are:

1.  UNIFAC is unable to distinguish between some types of isomers.

2.  The $\gamma-\phi$ approach limits the use of UNIFAC for applications under the pressure range of 10-15 atm.

3.  The temperature is limited within the range of approximately 275-425 K.

4.  Non-condensable gases and supercritical components are not included.

5.  Proximity effects are not taken into account.

6.  The parameters of liquid-liquid equilibrium are different from those of vapor-liquid equilibrium.

7.  Polymers are not included.

8.  Electrolytes are not included.

Some of these limitations can be overcome. The insensitivity of some types of isomers can be eliminated through a careful choice of the groups used to represent the molecules. The fact that the parameters for the liquid-liquid equilibrium are different from those for the vapor-liquid equilibrium seems not to have a theoretical solution at this time. One solution is to use both data from both equiibria to determine the parameters as a modified UNIFAC model. The limitations on the pressure and temperature can be overcome if the UNIFAC model is used with equations of state, which carry with them the dependencies of pressure and temperature.

These limitations of the original UNIFAC model have led several authors to propose changes in both combinatorial and the residual parts. To modify the combinatorial part, the basis is the suggestion given by Kikic et al. (1980) in the sense that the Staverman-Guggenheim correction on the original term of Flory-Huggins is very small and can, in most cases, be neglected. As a result, this correction was empirically removed from the UNIFAC model. Among these modifications, the proposed by Gmehling and coworkers \[Weidlich and Gmehling, 1986; Weidlich and Gmehling, 1987; Gmehling et al., 1993\], known as the model UNIFAC-Dortmund, is one of the most promising. In this model, the combinatorial part of the original UNIFAC is replaced by:



\[
\ln\gamma_{i}^{C}=1-J_{i}+\ln J_{i}-5q_{i}(1-J_{i}/L_{i}+\ln J_{i}/L_{i})
\]




<a id="eq:ji2"></a>

\[
\begin{eqnarray}
J_{i}=r_{i}^{3/4}/(\sum_{j}r_{j}^{3/4}x_{j})
\end{eqnarray}
\]


where the remaining quantities is defined the same way as in the original UNIFAC. Thus, the correction in-Staverman Guggenheim is empirically taken from the template. It is important to note that the in the UNIFAC-Dortmund model, the quantities $R_{k}$ and $Q_{k}$ are no longer calculated on the volume and surface area of Van der Waals forces, as proposed by Bondi (1968), but are additional adjustable parameters of the model.

The residual part is still given by the solution for groups, just as in the original UNIFAC, but now the parameters of group interaction are considered temperature dependent, according to:



\[
\tau_{mk}=\exp(-a_{mk}^{(0)}+a_{mk}^{(1)}T+a_{mk}^{(2)}T^{2})/T
\]


These parameters must be estimated from experimental phase equilibrium data. Gmehling et al. (1993) presented an array of parameters for 45 major groups, adjusted using data from the vapor-liquid equilibrium, excess enthalpies, activity coefficients at infinite dilution and liquid-liquid equilibrium. enthalpy and entropy of liquid and vapor.

###### *Modified UNIFAC (NIST) model* {#modified-unifac-nist-model .unnumbered}

This model is similar to the Modified UNIFAC (Dortmund), with new modified UNIFAC parameters reported for 89 main groups and 984 group–group interactions using critically evaluated phase equilibrium data including vapor–liquid equilibrium (VLE), liquid–liquid equilibrium (LLE), solid–liquid equilibrium (SLE), excess enthalpy (HE), infinite dilution activity coefficient (AINF) and excess heat capacity (CPE) data. A new algorithmic framework for quality assessment of phase equilibrium data was applied for qualifying the consistency of data and screening out possible erroneous data. Substantial improvement over previous versions of UNIFAC is observed due to inclusion of experimental data from recent publications and proper weighting based on a quality assessment procedure. The systems requiring further verification of phase equilibrium data were identified where insufficient number of experimental data points is available or where existing data are conflicting.

###### *NRTL model* {#nrtl-model .unnumbered}

Wilson (1964) presented a model relating $g^{E}$ to the molar fraction, based mainly on molecular considerations, using the concept of local composition. Basically, the concept of local composition states that the composition of the system in the vicinity of a given molecule is not equal to the overall composition of the system, because of intermolecular forces.

Wilson’s equation provides a good representation of the Gibbs’ excess free energy for a variety of mixtures, and is particularly useful in solutions of polar compounds or with a tendency to association in apolar solvents, where Van Laar’s equation or Margules’ one are not sufficient. Wilson’s equation has the advantage of being easily extended to multicomponent solutions but has two disadvantages: first, the less important, is that the equations are not applicable to systems where the logarithms of activity coefficients, when plotted as a function of *x*, show a maximum or a minimum. However, these systems are not common. The second, a little more serious, is that the model of Wilson is not able to predict limited miscibility, that is, it is not useful for LLE calculations.

Renon and Prausnitz developed the *NRTL* equation *(Non-Random, Two-Liquid)* based on the concept of local composition but, unlike Wilson’s model, the NRTL model is applicable to systems of partial miscibility. The model equation is:



\[
\ln\gamma_{i}=\frac{\underset{j=1}{\overset{n}{\sum}}\tau_{ji}x_{j}G_{ji}}{\underset{k=1}{\overset{n}{\sum}}x_{k}G_{ki}}+\underset{j=1}{\overset{n}{\sum}}\frac{x_{j}G_{ij}}{\underset{k=1}{\overset{n}{\sum}}x_{k}G_{kj}}\left(\tau_{ij}-\frac{\underset{m=1}{\overset{n}{\sum}}\tau_{mj}x_{m}G_{mj}}{\underset{k=1}{\overset{n}{\sum}}x_{k}G_{kj}}\right),
\]




\[
G_{ij}=exp(-\tau_{ij}\alpha_{ij}),
\]




\[
\tau_{ij}=a_{ij}/RT,
\]


\
where







$\gamma_{i}$ Activity coefficient of component *i*

$x_{i}$ Molar fraction of component *i*

$a_{ij}$ Interaction parameter between *i-j* $(a_{ij}\neq a_{ji})$ (cal/mol)

$T$ Temperature (K)

$\alpha_{ij}$ non-randomness parameter for the *i-j* pair $(\alpha_{ij}=\alpha_{ji})$



The significance of $G_{ij}$ is similar to $\Lambda_{ij}$ from Wilson’s equation, that is, they are characteristic energy parameters of the *ij* interaction. The parameter is related to the non-randomness of the mixture, i.e. that the components in the mixture are not randomly distributed but follow a pattern dictated by the local composition. When it is zero, the mixture is completely random, and the equation is reduced to the two-suffix Margules equation.

For ideal or moderately ideal systems, the NRTL model does not offer much advantage over Van Laar and three-suffix Margules, but for strongly non-ideal systems, this equation can provide a good representation of experimental data, although good quality data is necessary to estimate the three required parameters.

#### Enthalpy, Entropy and Heat Capacities

###### *Peng-Robinson, Soave-Redlich-Kwong* {#peng-robinson-soave-redlich-kwong .unnumbered}

> 
>
> | *<span class="image placeholder" original-image-src="dialog-information.png" original-image-title="">image</span>* | <span class="sans-serif">*$H^{id}$ values are calculated from the ideal gas heat capacity. For mixtures, a molar average is used.*</span> <span class="sans-serif">*The value calculated by the EOS is for the phase, independently of the number of components present in the mixture.*</span> |
> |:---|:--:|

For cubic equations of state, enthalpy, entropy, and heat capacities are computed using *departure functions*, which quantify the difference between the property of the real fluid and that of the corresponding ideal-gas mixture at the same temperature and composition. The departure functions are defined as ,



\[
\frac{H-H^{id}}{RT}=X;\:\frac{S-S^{id}}{R}=Y
\]


values for $X$ and $Y$ are calculated by the PR and SRK EOS, according to the table [18](#tab:Entalpia/Entropia-por-equações):\



<a id="tab:Entalpia/Entropia-por-equações"></a>



|  |  |  |
|:--:|:--:|:--:|
|  | <span class="roman">$\frac{H-H^{id}}{RT}$</span> | $\frac{S-S^{id}}{R}$ |
| PR | $Z-1-\frac{1}{2^{1,5}bRT}\left[a-T\frac{da}{dT}\right]\times$ | $\ln(Z-B)-\ln\frac{P}{P^{0}}-\frac{A}{2^{1,5}bRT}\left[\frac{T}{a}\frac{da}{dT}\right]\times$ |
|  | <span class="roman">$\times\ln\left[\frac{V+2,414b}{V+0,414b}\right]$</span> | $\times\ln\left[\frac{V+2,414b}{V+0,414b}\right]$ |
| SRK | $Z-1-\frac{1}{bRT}\left[a-T\frac{da}{dT}\right]\times$ | $\ln(Z-B)-\ln\frac{P}{P^{0}}-\frac{A}{B}\left[\frac{T}{a}\frac{da}{dT}\right]\times$ |
|  | $\times\ln\left[1+\frac{b}{V}\right]$ | $\times\ln\left[1+\frac{B}{Z}\right]$ |

Enthalpy/Entropy calculation with an EOS



> 
>
> | *<span class="image placeholder" original-image-src="dialog-information.png" original-image-title="">image</span>* | <span class="sans-serif">*In DWSIM, $P_{o}$ = 1 atm.*</span> |
> |:---|:--:|

Heat capacities are obtained directly from the EOS, by using the following thermodynamic relations:



\[
C_{p}-C_{p}^{id}=T\intop_{\infty}^{V}\left(\frac{\partial^{2}P}{\partial T^{2}}\right)dV-\frac{T(\partial P/\partial T)_{V}^{2}}{(\partial P/\partial V)_{T}}-R
\]




\[
C_{p}-C_{v}=-T\frac{\left(\frac{\partial P}{\partial T}\right)_{V}^{2}}{\left(\frac{\partial P}{\partial V}\right)_{T}}
\]


###### *Lee-Kesler* {#lee-kesler .unnumbered}

Enthalpies, entropies and heat capacities are calculated by the Lee-Kesler model through the following equations:



<a id="eq:LKH"></a>

\[
\frac{H-H^{id}}{RT_{c}}=T_{r}\left(Z-1-\frac{b_{2}+2b_{3}/T_{r}+3b_{4}/T_{r}^{2}}{T_{r}V_{r}}-\frac{c_{2}-3c_{3}/T_{r}^{2}}{2T_{r}V_{r}^{2}}+\frac{d_{2}}{5T_{r}V_{r}^{2}}+3E\right)
\]




\[
\frac{S-S^{id}}{R}+\ln\left(\frac{P}{P_{0}}\right)=\ln Z-\frac{b_{2}+b_{3}/T_{r}^{2}+2b_{4}/T_{r}^{3}}{V_{r}}-\frac{c_{1}-2c_{3}/T_{r}^{3}}{2V_{r}^{2}}+\frac{d_{1}}{5V_{r}^{5}}+2E
\]




\[
\frac{C_{v}-C_{v}^{id}}{R}=\frac{2\left(b_{3}+3b_{4}/T_{r}\right)}{T_{r}^{2}V_{r}}-\frac{3c_{3}}{T_{r}^{3}V_{r}^{2}}-6E
\]




\[
\frac{C_{p}-C_{p}^{id}}{R}=\frac{C_{v}-C_{v}^{id}}{R}-1-T_{r}\frac{\left(\frac{\partial P_{r}}{\partial T_{r}^{}}\right)_{V_{r}}^{2}}{\left(\frac{\partial P_{r}}{\partial V_{r}}\right)_{T_{r}}}
\]




\[
E=\frac{c_{4}}{2T_{r}^{3}\gamma}\left[\beta+1-\left(\beta+1+\frac{\gamma}{V_{r}^{2}}\right)\exp\left(-\frac{\gamma}{V_{r}^{2}}\right)\right]
\]


> 
>
> | *<span class="image placeholder" original-image-src="dialog-information.png" original-image-title="">image</span>* | <span class="sans-serif">*An iterative method is required to calculate $V_{r}$ . The user should always watch the values generated by DWSIM in order to detect any issues in the compressibility factors generated by the Lee-Kesler model.*</span> |
> |:---|:--:|



\[
Z=\frac{P_{r}V_{r}}{T_{r}}=1+\frac{B}{V_{r}}+\frac{C}{V_{r}^{2}}+\frac{D}{V_{r}^{5}}+\frac{c_{4}}{T_{r}^{3}V_{r}^{2}}\left(\beta+\frac{\gamma}{V_{r}^{2}}\right)\exp\left(-\frac{\gamma}{V_{r}^{2}}\right)
\]




\[
B=b_{1}-b_{2}/T_{r}-b_{3}/T_{r}^{2}-b_{4}/T_{r}^{3}
\]




\[
C=c_{1}-c_{2}/T_{r}+c_{3}/T_{r}^{3}
\]




\[
D=d_{1}+d_{2}/T_{r}
\]


Each property must be calculated based in two fluids apart from the main one, one simple and other for reference. For example, for the compressibility factor,



\[
Z=Z^{(0)}+\frac{\omega}{\omega^{(r)}}\left(Z^{(r)}-Z^{(0)}\right),
\]


where the $(0)$ superscript refers to the simple fluid while the $(r)$ superscript refers to the reference fluid. This way, property calculation by the Lee-Kesler model should follow the sequence below (enthalpy calculation example):

1.  $V_{r}$ and $Z^{(0)}$ are calculated for the simple fluid at the fluid $T_{r}$ and $P_{r}$ . using the equation [\[eq:LKH\]](#eq:LKH), and with the constants for the simple fluid, as shown in the table [19](#tab:Constantes-para-o), $(H-H^{0})/RT_{c}$ is calculated. This term is $\left[(H-H^{0})/RT_{c}\right]^{(0)}$ . in this calculation, $Z$ in the equation [\[eq:LKH\]](#eq:LKH) is $Z^{(0)}$ .

2.  The step 1 is repeated, using the same $T_{r}$ and $P_{r}$ , but using the constants for the reference fluid as shown in table [19](#tab:Constantes-para-o). With these values, the equation [\[eq:LKH\]](#eq:LKH) allows the calculation of $\left[(H-H^{0})/RT_{c}\right]^{(r)}$ . In this step, $Z$ in the equation [\[eq:LKH\]](#eq:LKH) is $Z^{(r)}$ .

3.  Finally, one determines the residual enthalpy for the fluid of interest by



\[
\begin{eqnarray}
\left[(H-H^{0})/RT_{c}\right] & = & \left[(H-H^{0})/RT_{c}\right]^{(0)}+ \\
 &  & \frac{\omega}{\omega^{(r)}}\left(\left[(H-H^{0})/RT_{c}\right]^{(r)}-\left[(H-H^{0})/RT_{c}\right]^{(0)}\right),
\end{eqnarray}
\]


where $\omega^{(r)}=0,3978$ .\



<a id="tab:Constantes-para-o"></a>



|                       |              |                 |
|:---------------------:|:------------:|:---------------:|
|       Constant        | Simple Fluid | Reference Fluid |
|       $b_{1}$       |  0.1181193   |    0.2026579    |
|       $b_{2}$       |   0.265728   |    0.331511     |
|       $b_{3}$       |   0.154790   |    0.027655     |
|       $b_{4}$       |   0.030323   |    0.203488     |
|       $c_{1}$       |  0.0236744   |    0.0313385    |
|       $c_{2}$       |  0.0186984   |    0.0503618    |
|       $c_{3}$       |     0.0      |    0.016901     |
|       $c_{4}$       |   0.042724   |    0.041577     |
| $d_{1}\times10^{4}$ |   0155488    |     0.48736     |
| $d_{2}\times10^{4}$ |   0.623689   |    0.0740336    |
|       $\beta$       |   0.65392    |      1.226      |
|      $\gamma$       |   0.060167   |     0.03754     |

Constants for the Lee-Kesler model



#### Speed of Sound

The speed of sound in a given phase is calculated by the following equations:



\[
c=\sqrt{\frac{K}{\rho}},
\]








where:

$c$ Speed of sound (m/s)

$K$ Bulk Modulus (Pa)

$\rho$ Phase Density (kg/m³)



#### Joule-Thomson Coefficient

In thermodynamics, the Joule–Thomson effect (also known as the Joule–Kelvin effect, Kelvin–Joule effect, or Joule–Thomson expansion) describes the temperature change of a real gas or liquid when it is forced through a valve or porous plug while kept insulated so that no heat is exchanged with the environment. This procedure is called a throttling process or Joule–Thomson process. At room temperature, all gases except hydrogen, helium and neon cool upon expansion by the Joule–Thomson process. The rate of change of temperature with respect to pressure in a Joule–Thomson process is the Joule–Thomson coefficient.

The Joule-Thomson coefficient for a given phase is calculated by the following definition:



\[
\mu=\left(\frac{\partial T}{\partial P}\right)_{H},
\]


The JT coefficient is calculated rigorously by the PR and SRK equations of state, while the Goldzberg correlation is used for all other models,



\[
\mu=\frac{0.0048823T_{pc}\left(18/T_{pr}^{2}-1\right)}{P_{pc}C_{p}\gamma},
\]


for gases, and



\[
\mu=-\frac{1}{\rho C_{p}},
\]


for liquids.

