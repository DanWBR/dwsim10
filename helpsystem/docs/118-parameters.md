# Parameters

| Group | Parameters |
|:---|:---|
| Vials and Batch | outer and inner diameters of the vial, fill volume, vials per batch, loading and unloading time |
| Formulation | density of the solutes, freezing and nucleation temperatures, collapse temperature, bound water after primary drying, target and equilibrium moisture |
| Freezing | shelf temperature, ramp rate, hold time, heat transfer coefficient |
| Primary Drying | shelf temperature, ramp rate, chamber pressure, hold after the ice is gone, $K_{C}$, $K_{P}$, $K_{D}$, $R_{0}$, $A_{1}$, $A_{2}$, thermal conductivity of ice, equipment capability |
| Secondary Drying | shelf temperature, ramp rate, chamber pressure, desorption rate constant with its reference temperature, activation energy and heat of desorption |

The vial heat transfer parameters belong to a vial in a given freeze dryer, and the resistance parameters to a formulation at a given concentration. Both are measured, and published values exist for common vials and excipients. The default values are those of a 6R vial and of a solution with 5 % of mannitol \[1\]. The defaults of the freezing and secondary drying steps are typical values and should be replaced by data of the product.

The equipment capability is the largest sublimation rate that the chamber and the condenser handle, as a straight line of the chamber pressure. The block warns when the peak rate of the batch passes it. With both coefficients at zero the check is skipped.

