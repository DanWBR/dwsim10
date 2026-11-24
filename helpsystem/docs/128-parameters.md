# Parameters

| Group | Parameters |
|:---|:---|
| Type | bed (agitated or at rest), solvents |
| Dryer and Batch | heated area in contact with the bed, dry solid per batch, loading and unloading time |
| Operation | wall temperature, chamber pressure, target solvent in the product, temperature limit of the product |
| Bed | bulk density and thermal conductivity of the dry bed, heat capacity of the solid, particle diameter |
| Agitation | agitator speed, mixing number (given or from a correlation), dryer diameter |
| Contact | contact coefficient (zero to have it calculated), surface coverage, accommodation coefficient, thermal conductivity and heat capacity of the vapor |

The mixing number belongs to a dryer and its agitator. It can be given, when it was fitted to a drying curve of the equipment, or taken from the power laws of the original work \[1\], which correlate it with the Froude number of the agitator:



\[
N_{mix}=C\,\mathrm{Fr}^{x}\qquad\mathrm{Fr}=\frac{\left(2\pi n\right)^{2}D}{2g}
\]


where $D$ is the diameter of the disc or of the drum.

| Dryer              | $C$ | $x$ |
|:-------------------|:-----:|:-----:|
| Disc dryers        |  25   | 0.20  |
| Rotary drum dryers |  16   | 0.20  |
| Paddle dryers      |   9   | 0.05  |

The values found in that work go from 2 to 25. A constant value of 7 describes its drying rate curves within about 50 %, so the mixing number matters less than the contact coefficient and the solvent content. The default of the block is a given value of 10.

