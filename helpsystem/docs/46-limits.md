# Limits

- The reservoir model is black-oil. The composition of the well stream at the surface follows from the oil, gas and water rates and from the two fixed compositions.

- The coupling is explicit, with a delay of one report step.

- Producers on THP control with a lift table are coupled. The optimization of gas lift of the reservoir simulator cannot be driven from the flowsheet; the lift gas return uses fixed shares.

- A run solves the flowsheet once per report step, so the time of a run is set by the size of the network: seconds per step for a few wells, minutes for tens of wells.

