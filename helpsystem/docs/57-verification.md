# Verification

The calculations were checked against published data and against another PVT program, on the oil and the gas condensate of the example PVT reports of the monograph of Whitson and Brulé:

- the flash of the tool agrees with an independent implementation of the Peng-Robinson equation on 60 mixtures of an oil with an injection solvent, including points near the critical point;

- the split of the heavy end and the Kesler-Lee properties reproduce the tables of the monograph;

- with the same components and parameters, the bubble point, the differential liberation and the separator tests agree with the other program within 0.02 %, and the constant volume depletion within 0.006 %;

- the oil, with the heavy end in five pseudocomponents and three fitted variables, has its bubble point 0.8 % low, the oil density of the differential liberation within 0.8 %, the oil volume within 1.5 % and the total gas-oil ratio of the separator tests within 3 %;

- the gas condensate, with four fitted variables, has its dew point within 0.1 %, the gas Z factor within 0.9 % and the liquid dropout within 7 % of the largest measured value;

- a flowsheet with the fitted oil and DWSIM’s own separators gives the gas-oil ratios the tool computes for the same separator test;

- the viscosity of methane reproduces the tables of Hanley, Haynes and McCarty within 1 % for the gas, the dense fluid and the liquid;

- the measured oil viscosity of the same oil (18 points from 1 to 345 bar) is reproduced within 3.1 % of the largest value by the Lohrenz-Bray-Clark correlation without tuning, 2.4 % with the critical volume fitted and 1.5 % with two coefficients as well; the corresponding states model gives 1.6 % without tuning and 1.5 % with its coefficient fitted;

- the minimum miscibility pressure agrees within 2.6 % with seven published values for ternary to thirteen-component systems with carbon dioxide, methane and mixed gases, and within 4.7 % with an eighth; a ninth case, whose source does not print three interaction parameters, was not reproduced (13 % high).

