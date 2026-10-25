# Overriding Calculated Properties

Introduction Starting from DWSIM Version 5.1, you can override the calculated phase properties through Python scripts. This can be useful if the calculated property is far from the expected value, or if you need to include advanced mixing rules when calculating mixed phase properties.

#### Accessing the feature

####### Classic UI

Click on ’Edit’ menu item \> ’Simulation Settings’ \> ’Basis’ and, on the Added Property Packages section, select an added Property Package, click on ’Settings’ and go to ’Property Overrides’.

####### Cross-Platform UI

Click on ’Edit’ menu item \> ’Simulation Settings’, go to the ’Thermodynamics’ tab, click ’Configure’ on an added Property Package and go to ’Advanced Settings’ \> ’Override Phase Properties’.




![](images/screens64/Propovrr.jpg)



#### Override Script Samples

##### Solid Density

If you have a solid phase in your simulation, try the following:




![](images/screens64/Propovrr2.jpg)



Click ’Save’ to store the override script on the Property Package. Solve the simulation and check the calculated value. This override will work on all Material Streams which are associated with the modified Property Package.




![](images/screens64/Propovrr3.jpg)



#### Water/Hydrocarbon Emulsion Viscosity

In this example, we will use a script to update the viscosity calculation method of the overall liquid phase when there are two liquid phases on the mixture, in order to take into account the emulsion effects in a pipe flow simulation.

We will replace the current method for liquid mixture viscosity (a simple mass fraction weighted average) by this emulsion equation:

$μ=(μ_{w}/δ_{w})[1+1.5μ_{h}δ_{h}(μ_{w}+μ_{h})]$

where $μ$ is viscosity and $δ$ is volumetric fraction. Subscripts $w$ and $h$ refer to water and hydrocarbon phase, respectively.

Open the Property Override editor and enter the following script for the "OverallLiquid/viscosity" property:

    # properties are nullable values, so we need to check if they have a value first.
     
    viscobj = matstr.GetPhase('OverallLiquid').Properties.viscosity
     
    if (viscobj <> None):
     
        currval = viscobj
         
        # write current value to the flowsheet (Pa.s)
        flowsheet.WriteMessage('current liquid mixture viscosity value (Pa.s): ' + str(currval))
         
        # get viscosity of the liquid hydrocarbon phase
        mu_h = matstr.GetPhase('Liquid1').Properties.viscosity
         
        # get viscosity of the liquid water phase
        mu_w = matstr.GetPhase('Liquid2').Properties.viscosity
     
        # get volumetric flow of the liquid hydrocarbon phase
        vflow_h = matstr.GetPhase('Liquid1').Properties.volumetric_flow
     
        # get volumetric flow of the liquid water phase
        vflow_w = matstr.GetPhase('Liquid2').Properties.volumetric_flow
     
        # calculate volumetric fraction of the liquid hydrocarbon phase
        vf_h = matstr.GetPhase('Liquid1').Properties.volumetric_flow / (vflow_h + vflow_w)
     
        # calculate volumetric fraction of the liquid water phase
        vf_w = matstr.GetPhase('Liquid2').Properties.volumetric_flow / (vflow_h + vflow_w)
     
        # update liquid mixture viscosity with the result from emulsion equation (Pa.s)
        propval = (mu_w/vf_w)*(1.0+1.5*mu_h*vf_h/(mu_w+mu_h))
     
        flowsheet.WriteMessage('updated liquid mixture (emulsion) viscosity value (Pa.s): ' + str(propval))
     
    else:
     
        propval = 0.0




![](images/screens64/Propovrr4.jpg)



Run the simulation again and store the results. You can view the effect of the updated viscosity on the pipe pressure profile:




![](images/screens64/Propovrr5.jpg)



