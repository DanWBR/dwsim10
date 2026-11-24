# Recovery Boiler

The black liquor burns with the combustion air: the chlorine leaves as NaCl, the sulfur that doesn’t leave as $\ce{SO2}$ goes to $\ce{Na2S}$ at the reduction efficiency and to $\ce{Na2SO4}$, and the rest of the sodium forms $\ce{Na2CO3}$. The other carbon leaves as $\ce{CO2}$, and the air is the one that gives the $\ce{O2}$ of the flue gas when the air inlet isn’t connected. The heat balance follows the heat loss method of Vakkilainen \[17\] and Tran and Grace \[18\]: reaction heat from the formation enthalpies, sensible heats of the liquor, air and flue gas, the enthalpy of the molten smelt from the NIST-JANAF tables, and the radiation and unaccounted losses as a share of the heat input. The rest raises steam from the feedwater stream (on the steam tables) to the steam conditions, and the block writes the feedwater flow. An optional dissolving tank turns the smelt and a weak wash into green liquor and vent steam.







| Quantity                               | Model | Tran and Grace \[18\] |
|:---------------------------------------|:-----:|:---------------------:|
| Steam at 62 bar, 482 °C, kg/kg solids  | 3.49  |         3.53          |
| Heat to steam, kJ/kg solids            | 9913  |        10,010         |
| Smelt, kg/kg solids                    | 0.444 |         0.456         |
| Heat to form the sulfide, kJ/kg solids | 1230  |         1330          |



Potassium is counted as sodium, and the sootblowing steam, the unburned carbon and the ash recycle aren’t modeled.

