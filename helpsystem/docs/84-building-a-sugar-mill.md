# Building a sugar mill

The *Raw sugar mill* case of the Case Library builds a mill of 100 t/h of cane with these blocks:

- **Extraction:** a component separator that sends the fibre and part of the juice to the bagasse.

- **Evaporator effect:** a cooler that condenses the heating vapor to saturated liquid, with its duty sent through an energy stream to a Falling Film Evaporator in Energy Stream mode. The juice enters the evaporator at the pressure of the effect, so the evaporator also flashes it.

- **Vacuum pan:** a heater at the boiling temperature of the pan, followed by a vessel that takes the vapor away from the massecuite.

- **Centrifugal:** a solids separator; its liquid separation efficiency is the share of mother liquor that leaves with the molasses.

- **Cooling crystallizer:** a cooler.

Recycles that carry sugar crystals (B and C sugars returned as seed) converge better with a mass flow tolerance of 0.1 % of the recycled flow and Wegstein acceleration, starting from an estimate close to the answer.

