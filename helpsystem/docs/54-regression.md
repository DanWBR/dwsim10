# Regression

#### Objective

The fit minimizes the sum of the squares of the weighted deviations,


\[
F=\sum_{k}\left[w_{k}\,\frac{y_{k}^{\mathrm{calc}}-y_{k}^{\mathrm{meas}}}{y_{\max}}\right]^{2},
\]


where $y_{\max}$ is the largest measured value of the column of the point. Dividing by the largest value of the column keeps the points near zero (the first liquid below a dew point, the last gas in solution) from dominating. A measured value that the simulation cannot produce counts as a deviation of 100 %. A weight of zero shows the quantity in the results without fitting it.

A new experiment starts with weight 5 on the saturation pressure and 3 on the densities. The solution gas-oil ratio, Bo and Bt of a differential liberation depend on the residual oil of the last stage, which the laboratory measures with the least accuracy; when the report allows it, fit the oil volume and the gas removed, referred to the bubble point, and set those three to zero.

#### Variables

Methane interaction parameters (multiplier):  
moves the saturation pressure most.

Volume translation of the heavy fractions (multiplier):  
moves the liquid densities without changing the phase equilibrium.

Tc, pc and acentric factor of the heavy fractions (multipliers):  
change the equilibrium and the volumes together.

Shape of the heavy end distribution:  
redistributes the heavy end between light and heavy fractions. It is often what a gas condensate needs to match its liquid dropout.

Molar mass of the heavy end (multiplier):  
the measured molar mass of a C7+ can be off by several percent, and the limits of this variable should reflect that.

Viscosity (LBC): critical volume of the heavy fractions (multiplier):  
the usual variable to match measured viscosities with the Lohrenz-Bray-Clark correlation. It changes the viscosity alone.

Viscosity (LBC): coefficients 2 to 5 (multipliers):  
the coefficients of the polynomial of the correlation. They are strongly correlated with each other and with the critical volume: use one or two.

Viscosity (corresponding states): molar mass coefficient and exponent (multipliers):  
the two parameters of the mixture molar mass of the Pedersen model.

Measured viscosities enter the objective only when a variable of the viscosity model is being fitted, so that they do not pull the phase behaviour. When only viscosity variables are fitted, nothing else enters.

The minimization is a Levenberg-Marquardt method on the variables scaled by their limits. The suggested steps fit the saturation pressure with the methane interaction multiplier, then the densities with the volume translation, then every quantity with those two and Tc, and last the viscosities with the critical volume of the heavy fractions (Lohrenz-Bray-Clark) or the molar mass coefficient (corresponding states).

#### What the fit reports

After a fit the Summary tab lists the sensitivity of the data to each variable and the alerts:

- a variable that ended at one of its limits;

- a variable the data do not respond to;

- two variables with a correlation above 0.95 in absolute value: the data fix a combination of the two, and one of them should stay out of the fit.

Fit as few variables as the data ask for. With the volume translation and the critical pressure both free, for example, density data seldom separate the two.

