# Extractive Distillation

#### Introduction

This simulation is an example of Pressure Swing Azeotropic Distillation. Test case taken from COCO Simulator ([link](http://www.cocosimulator.org/down.php?dl=CScasebook_MA.fsd)) (original author: Harry Kooijman - [www.chemsep.org](http://www.chemsep.org)). Adapted from Luyben et al., Ind. Eng. Chem. Res. (2008) 47 pp. 2696-2707.

#### Background

Methanol and acetone form a minimum temperature azeotrope but the composition of this azeotrope is sensitive to the pressure. We can make use of this to separate the two components into pure products by operating two columns at different pressures.




![Process Flowsheet.](images/screens58/tut2/Pressure_Swing_MA_iecr47p2696.png)

*Process Flowsheet.*



#### DWSIM Model (Classic UI)

1.  Create a New Steady State Simulation. Close the Simulation Wizard.

    > 
    >
    > | *<span class="image placeholder" original-image-src="dialog-information.png" original-image-title="">image</span>* | <span class="sans-serif">*Remember to* **Save <span class="sans-serif"></span>your simulation at the end of each step.**</span> |
    > |:---|:--:|

2.  Go to **Edit** \> **Simulation Settings** \> **Compounds**, and select Methanol and Acetone to add these compounds to the simulation.

    


![Compound Selection](images/screens58/tut2/tut2-1.png)

*Compound Selection*



3.  Go to **Thermodynamics** tab, select **NRTL** on the Available Property Packages section and click **Add**. Add a second copy of it and rename it to **NRTL (Inside-Out)** on the Added Property Packages grid, then click **Configure** on it and, on the **Equilibrium Calculation Settings** tab, set **Numerical Method** to **Inside-Out**. The flash algorithm is a setting of the property package, so a second copy is what allows one part of the flowsheet to use a different one.

    


![Property Package Selection](images/screens58/tut2/tut2-30.png)

*Property Package Selection*



4.  Check if the NRTL Interaction Parameters are all set (click on **Configure** on the Added Property Packages section).

    


![NRTL Interaction Parameters for Methanol/Acetone](images/screens58/tut2/tut2-3.png)

*NRTL Interaction Parameters for Methanol/Acetone*



5.  Go to the **System of Units** tab and create a new System of Units, with the following setup:

    


![New System of Units](images/screens58/tut2/tut2-4.png)

*New System of Units*



6.  After creating this Units Set, select it on the System of Units combobox.

7.  Add the objects to the flowsheet (streams, pump, valve, recycle and distillation columns) as depicted on the following figure, renaming them as required. You’ll setup the connections between them later on this tutorial.

    


![Process Flowsheet Diagram](images/screens58/tut2/tut2-24.png)

*Process Flowsheet Diagram*



8.  Disable automatic calculation of the flowsheet.

    


![Enable/Disable Flowsheet Calculator/Solver](images/screens58/tut2/tut2-23.png)

*Enable/Disable Flowsheet Calculator/Solver*



9.  Setup the columns and their connections as follows:

    1.  Methanol Column:

        


![Methanol Column configuration](images/screens58/tut2/tut2-25.png)

*Methanol Column configuration*



    2.  Acetone Column:

        


![Acetone Column configuration](images/screens58/tut2/tut2-33.png)

*Acetone Column configuration*



10. Enter initial estimates for the temperature profile of the Acetone Column, and check the **T** checkbox so DWSIM can use them. Insert only the boundary values (condenser and reboiler) and click on **Interpolate** to calculate the inner stage values.

    


![Acetone Column initial estimates for temperature profile](images/screens58/tut2/tut2-31.png)

*Acetone Column initial estimates for temperature profile*



11. After the columns are correctly configured and connected to their associated streams, setup the pump, valve and recycle connections using their Editor Panels.

12. Setup the pump and valve properties as follows:

    


![Pump and Valve properties](images/screens58/tut2/tut2-21.png)

*Pump and Valve properties*



13. Configure the Methanol Inlet Stream as follows:

    


![Methanol Inlet Stream configuration](images/screens58/tut2/tut2-35.png)

*Methanol Inlet Stream configuration*



14. Configure the Methanol Recycle Stream (initial estimates for the recycle stream) as follows:

    


![Methanol Recycle Stream configuration](images/screens58/tut2/tut2-17.png)

*Methanol Recycle Stream configuration*



15. Assign the **NRTL (Inside-Out)** property package to the following streams, on the **Property Package Settings** section of each one’s editor: **MSTR-001**, **Methanol Product**, **Acetone Product** and **Recycle (3)**. This will prevent PH Flash errors from happening during the flowsheet calculation.

    


![Assigning the Inside-Out property package to the streams](images/screens58/tut2/tut2-34.png)

*Assigning the Inside-Out property package to the streams*



16. Re-enable the solver (press F6) and calculate the flowsheet (press F5). Wait for the recycle to converge.

17. After the flowsheet solves, insert a new Property Table:

    


![Inserting a Property Table](images/screens58/tut2/tut2-28.png)

*Inserting a Property Table*



18. Double-click on the inserted table, search for the column energy streams and select Energy Flow for all of them, so these values can be shown on the Property Table.

    


![Setting up a Property Table](images/screens58/tut2/tut2-36.png)

*Setting up a Property Table*



19. Compare the results obtained with the duties specified in the original problem.

    


![Final results](images/screens58/tut2/tut2-29.png)

*Final results*



#### DWSIM Model (Cross-Platform UI)

1.  Create a New Simulation. Close the Simulation Wizard.

    > 
    >
    > | *<span class="image placeholder" original-image-src="dialog-information.png" original-image-title="">image</span>* | <span class="sans-serif">*Remember to* **Save <span class="sans-serif"></span>your simulation at the end of each step.**</span> |
    > |:---|:--:|

2.  Go to **Edit \> General Settings**, **Flowsheet** and disable the **Call Solver on Editor Property Update** option to avoid unnecessary calculations during the model building.

3.  Go to **Edit \> Simulation Settings \> Compounds**, and select Methanol and Acetone to add these compounds to the simulation.

    


![Compound Selection](images/screens58/tut2cp/tut2cp-3.png)

*Compound Selection*



4.  Go to the **Edit \> Simulation Settings \> Thermodynamics** tab and add a copy of the **NRTL** Property Package. Add a second copy of it, rename it to **NRTL (Inside-Out)**, click **Configure** on it and, on the **Equilibrium Calculations** tab, set **Numerical Method** to **Inside-Out**. The flash algorithm is a setting of the property package, so a second copy is what allows one part of the flowsheet to use a different one.

    


![Property Package Selection](images/screens58/tut2cp/tut2cp-4.png)

*Property Package Selection*



5.  Check if the NRTL Interaction Parameters are all set (click on **Edit** on the Added Property Packages section).

    


![NRTL Interaction Parameters for Methanol/Acetone](images/screens58/tut2cp/tut2cp-19.png)

*NRTL Interaction Parameters for Methanol/Acetone*



6.  Go to **Tools \> Systems of Units** and create a new System of Units, with the following setup:

    


![New System of Units](images/screens58/tut2cp/tut2cp-2.png)

*New System of Units*



7.  After creating this Units Set, select it on the System of Units combobox.

8.  Add the objects to the flowsheet (streams, pump, valve, recycle and distillation columns) as depicted on the following figure, renaming them as required. You’ll setup the connections between them later on this tutorial.

    


![Process Flowsheet Diagram](images/screens58/tut2/tut2-24.png)

*Process Flowsheet Diagram*



9.  Setup the columns and their connections as follows:

    1.  Methanol Column (52 stages):

        


![Methanol Column configuration](images/screens58/tut2cp/tut2cp-9.png)

*Methanol Column configuration*



    2.  Acetone Column (61 stages):

        


![Acetone Column configuration](images/screens58/tut2cp/tut2cp-12.png)

*Acetone Column configuration*



10. Enter initial estimates for the temperature profile of the Acetone Column, and check the **Override Temperature Estimates** checkbox so DWSIM can use them. Values must be tab separated, inserted as in the picture below:

    


![Acetone Column initial estimates for temperature profile](images/screens58/tut2cp/tut2cp-15.png)

*Acetone Column initial estimates for temperature profile*



11. After the columns are correctly configured and connected to their associated streams, setup the pump, valve and recycle connections using their Editor Panels.

12. Setup the pump and valve properties as follows:

    


![Pump and Valve properties](images/screens58/tut2cp/tut2cp-7.png)

*Pump and Valve properties*



13. Configure the Methanol Inlet Stream as follows:

    


![Methanol Inlet Stream configuration](images/screens58/tut2cp/tut2cp-5.png)

*Methanol Inlet Stream configuration*



14. Configure the Methanol Recycle Stream (initial estimates for the recycle stream) as follows:

    


![Methanol Recycle Stream configuration](images/screens58/tut2cp/tut2cp-6.png)

*Methanol Recycle Stream configuration*



15. Assign the **NRTL (Inside-Out)** property package to the following streams, on the **Properties** tab of the Editor panel of each one: **MSTR-001**, **Methanol Product**, **Acetone Product** and **Recycle (3)**. This will prevent PH Flash errors from happening during the flowsheet calculation.

    


![Assigning the Inside-Out property package to the streams](images/screens58/tut2cp/tut2cp-18.png)

*Assigning the Inside-Out property package to the streams*



16. Calculate the flowsheet (press F5). Wait for the recycle to converge.

17. After the flowsheet solves, insert a new Property Table:

    


![Inserting a Property Table](images/screens58/tut2cp/tut2cp-20.png)

*Inserting a Property Table*



18. Double-click on the inserted table, search for the column energy streams and select Energy Flow for all of them, so these values can be shown on the Property Table.

    


![Setting up a Property Table](images/screens58/tut2cp/tut2cp-21.png)

*Setting up a Property Table*



19. Compare the results obtained with the duties specified in the original problem.

    


![Final results](images/screens58/tut2/tut2-29.png)

*Final results*



