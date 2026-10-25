# Reactions

DWSIM includes support for chemical reactions through the Chemical Reactions Manager. Three types of reactions are available to the user:







Conversion, where you must specify the conversion (%) of the limiting reagent as a function of temperature

Equilibrium, where you must specify the equilibrium constant (K) as a function of temperature, a constant value or calculated from the Gibbs free energy of reaction ( $\Delta G/R$ ). The orders of reaction of the components are obtained from the stoichiometric coefficients.

Kinetic, where you should specify the frequency factor (A) and activation energy (E) for the direct reaction (optionally for the reverse reaction), including the orders of reaction (direct and inverse) of each component.



For each chemical reaction is necessary to specify the stoichiometric coefficients of the compounds and a base compound, which must be a reactant. This base component is used as reference for calculating the heat of reaction.

#### Conversion Reaction

In the conversion reaction it is assumed that the user has information regarding the conversion of one of the reactants as a function of temperature. By knowing the conversion and the stoichiometric coefficients, the quantities of the other components in the reaction can be calculated.

Considering the following chemical reaction:



\[
aA+bB\rightarrow cC,
\]


\
where *a*, *b* and *c* are the stoichiometric coefficients of reactants and product, respectively. *A* is the limiting reactant and *B* is in excess. The amount of each component at the end of the reaction can then be calculated from the following stoichiometric relationships:



\[
\begin{eqnarray}
N_{A} & = & N_{A_{0}}-N_{A_{0}}X_{A}\\
N_{B} & = & N_{B_{0}}-\frac{b}{a}N_{A_{0}}X_{A}\\
N_{C} & = & N_{C_{0}}+\frac{c}{a}(N_{A_{0}}X_{A})
\end{eqnarray}
\]


\
where $N_{A,B,C}$ are the molar amounts of the components at the end of the reaction, $N_{A_{0},B_{0},C_{0}}$ are the molar amount of the components at the start of the reaction and $X_{A}$ is the conversion of the base-reactant *A*.

#### Equilibrium Reaction

In the equilibrium reaction, the quantity of each component at the equilibrium is related to equilibrium constant by the following relationship:



\[
K=\underset{j=1}{\overset{n}{\prod}}(q_{j})^{\nu_{j}},
\]


\
where *K* is the equilibrium constant, *q* is the basis of components (partial pressure in the vapor phase or activity in the liquid phase) $\nu$ is the stoichiometric coefficient of component *j* and *n* is the number of components in the reaction.

The equilibrium constant can be obtained by three different means. One is to consider it a constant, another is considering it as a function of temperature, and finally calculate it automatically from the Gibbs free energy at the temperature of the reaction. The first two methods require user input.

##### Solution method

For each reaction that is occurring in parallel in the system, we can define $\xi$ as the *reaction extent*, so that the molar amount of each component in the equilibrium is obtained by the following relationship:



\[
N_{j}=N_{j_{0}}+\underset{i}{\sum}\nu_{ij}\xi_{i},
\]


\
where $\xi_{i}$ is the coordinate of the reaction *i* and $\nu_{ij}$ is the stoichiometric coefficient of the *j* component at reaction *i*. Defining the molar fraction of the component *i* as $x_{j}=n_{j}/n_{t}$ , where $n_{t}$ is the total number of mols, including inerts, whe have the following expression for each reaction *i*:



\[
f_{i}(\xi)=\underset{i}{\sum}\ln(x_{i})-\ln(K_{i})=0,
\]


\
where the system of equations F can be easily solved by Newton-Raphson’s method .

#### Kinetic Reaction

The kinetic reaction is defined by the parameters of the equation of Arrhenius (frequency factor and activation energy) for both the direct order and for the reverse order. Suppose we have the following kinetic reaction:



\[
aA+bB\rightarrow cC+dD
\]


\
The reaction rate for the *A* component can be defined as



\[
r_{A}=k[A][B]-k'[C][D]
\]


where



\[
\begin{eqnarray}
k & = & A\exp\left(-E/RT\right)\\
k' & = & A'\exp\left(-E'/RT\right)
\end{eqnarray}
\]


The kinetic reactions are used in Plug-Flow Reactors (PFRs) and in Continuous-Stirred Tank Reactors (CSTRs). In them, the relationship between molar concentration and the rate of reaction is given by



\[
F_{A}=F_{A_{0}}+\intop^{V}r_{A}dV,
\]


\
where $F_{A}$ is the molar flow of the *A* component and *V* is the reactor volume.

