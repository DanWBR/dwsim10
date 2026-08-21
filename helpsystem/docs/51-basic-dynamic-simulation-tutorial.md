# Basic Dynamic Simulation Tutorial

#### Introduction

In this tutorial, we will learn how to do a dynamic simulation of a water storage tank, adding a PID Controller to keep the liquid level inside the tank around a desired value.

#### DWSIM Model (Classic UI)

Create and configure a new simulation. Add Water as the only compound and use the Steam Tables Property Package.

##### Model Building

1.  Build your model as in the following picture:

    


![Water Tank model](images/screens60/dynmodel1.png)

*Water Tank model*



2.  Enable/Activate **Dynamic Mode**.

3.  Set the inlet stream Pressure to 130000 Pa and Mass Flow to 10 kg/s. Set its Dynamic Spec to ’Flow’.

    


![Inlet stream properties.](images/screens60/dynmodel2.png)

*Inlet stream properties.*



4.  Set V-01 calculation type to Liquid Kv, Kvmax to 100, check the Opening vs Kv/Kvmax box and set the valve opening to 50%.

5.  Set T-01 volume to 2 m3 and available liquid height to 2 m. Set the "Reset Content" property to 1.

6.  Set V-02 calculation type to Liquid Kv, Kvmax to 400, check the Opening vs Kv/Kvmax box and set the valve opening to 50%.

7.  Set the outlet stream Pressure to 101325 Pa. Set its Dynamic Spec to ’Pressure’.

With the above settings, the flow rate of water entering the tank will be fixed at 10 kg/s. The dynamic model for the Tank considers the liquid height contribution (static pressure) for the pressure of the tank’s outlet stream. Since V-02’s outlet stream pressure is fixed, the actual outlet flow will be calculated by the valve using the current opening and the connected stream pressures. As a result, the liquid level inside the tank will vary according to the difference between inlet and outlet flow rates.

##### Dynamic Simulation

1.  Add a Level Gauge and associate it with the Tank’s Liquid Level property. Set its maximum value to 3 m.

2.  Save the current flowsheet state as **NewState1**.

3.  Go to the **Dynamics Manager** and create a new Integrator (Int1) with Integration Step equal to 5 seconds and Duration equal to 10 minutes. Add the Tank’s liquid level and the openings of the two valves as monitored variables for this integrator.

4.  Create a new Schedule (Sch1) and associate the previously created Integrator and Flowsheet State with it.

5.  Open the Integrator Controls Panel and run the dynamic simulation. The liquid level on the tank should stabilize around 0.33 meters after 7 minutes.

6.  On the Integrator Controls Panel, click on the **View Results** button. On the created Spreadsheet, select the A and B columns and create a new chart from the selection. View the generated chart.

    


![Liquid Level versus time.](images/screens60/dynmodel3.png)

*Liquid Level versus time.*



##### Adding a PID Controller

1.  Add a PID Controller to the flowsheet and set the V-02’s opening as the manipulated variable and the Tank’s liquid level as the controlled one. The set-point should be equal to 1 (m). Set Kp to 100 and enable Reverse Acting.

2.  Add a new Chart to the flowsheet and associate it with the PID’s History item.

3.  Open the Integrator Controls Panel and run the dynamic simulation. The liquid level on the tank should stabilize around 1.2 meters after 3 minutes.

4.  Now set the controller’s Ki to 10 and rerun the dynamic simulation. The liquid level on the tank should stabilize around 0.95 meters.

    


![Liquid Level versus time with controller engaged.](images/screens60/dynmodel4.png)

*Liquid Level versus time with controller engaged.*



##### PID Controller Tuning

1.  Open the PID Controller Tuning Tool, select the controller added to the simulation and run the tuning.

    


![PID Controller Tuning Tool.](images/screens60/dynmodel5.png)

*PID Controller Tuning Tool.*



2.  Open the Integrator Controls Panel and run the dynamic simulation. The liquid level on the tank should stabilize around 1 meter after 5 minutes. Notice that there is still a very high overshoot on the liquid level, even after the PID tuning. Perhaps you can try tuning it again with different initial values for Kp, Ki and Kd and/or increase the number of optimizer runs.

    


![Liquid Level versus time with controller engaged.](images/screens60/dynmodel6.png)

*Liquid Level versus time with controller engaged.*



#### Real-Time Mode

1.  Change the flowsheet to **Control Panel Mode**. It should become dark and read-only, i.e. you cannot drag and/or add new objects.

2.  Run the dynamic simulation in real-time mode by clicking on the ’clock’ button on the Integrator Controls Panel.

3.  After some time, click on the PID Controller and set the SP value to 1.7 (m). Watch how the system reacts to this change.

    


![Control Panel mode with changes to PID parameters.](images/screens60/dynmodel7.png)

*Control Panel mode with changes to PID parameters.*



4.  Remember that, after each integrator run, you can click on **View Results** and inspect the values of the monitored variables on that ru

