# Pulp Stream Analysis

**Utilities \> Pulp Stream Analysis** lists the streams of the flowsheet that use the Kraft Liquors package and shows, for each one:

- the **moisture**, the **consistency** (wood and pulp solids) and the **lime solids**;

- the **dry solids** of the liquor and its density;

- the **alkali** in g/L as $\ce{Na2O}$: TTA (NaOH + $\ce{Na2S}$ + $\ce{Na2CO3}$), AA (NaOH + $\ce{Na2S}$), EA (NaOH + $\ce{Na2S}$/2) and each salt;

- the **sulfidity** on AA and on TTA, the **causticity** NaOH/(NaOH + $\ce{Na2CO3}$) and the **reduction** $\ce{Na2S}$/($\ce{Na2S}$ + $\ce{Na2SO4}$);

- the organics and the **higher heating value** of the dry solids;

- the boiling point and the boiling point rise at the stream pressure;

- the composition of the wood or pulp solids and the **kappa number** from their lignin (0.15 % lignin per kappa unit).

The same window writes five kinds of stream from the quantities of the mill: wood chips, pulp suspension, white liquor, green liquor and black liquor. The presets come from published data (eucalyptus and pine chips, brown stock, white and green liquor of Sanchez \[20\], weak and strong black liquors of Persson and Energiforsk \[22\]), and the window asks the user to replace them with the data of the mill. The package also writes these quantities into the stream as properties named *Pulp: ...*, which adjusts, specifications and the spreadsheet can read.

