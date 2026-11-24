# CAPE-OPEN Subsystem

#### Introduction

CAPE-OPEN (Computer-Aided Process Engineering – Open) is an international set of interface standards for interoperability between process modeling software components. Built on COM (and historically CORBA) middleware, CAPE-OPEN enables thermodynamic property packages, unit-operation models, and flowsheet monitoring objects from different vendors to be combined in a single simulation. The standards are open, platform-independent, and available free of charge.

DWSIM supports a number of CAPE-OPEN features, including:

####### Property Packages (Thermo Specs 1.0 and 1.1) {#property-packages-thermo-specs-1.0-and-1.1}

You can use external CAPE-OPEN thermodynamic equilibrium and property calculators as Property Packages in DWSIM. Integration is done transparently. You’ll only have to map the external property package components and phases to the ones in the internal DWSIM databases.




![](images/screens64/Snap0038.png)



####### Unit Operations

CAPE-OPEN Unit Operations can be added to DWSIM flowsheets and connected to/from energy and material streams just as normal DWSIM Unit Operations. DWSIM also implements the CAPE-OPEN Reaction interfaces so you can use your CAPE-OPEN Reactor Model together with DWSIM and manage your reactions using the Reactions Manager as usual.




![](images/screens64/Snap0042.png)



####### Flowsheet Monitoring Objects

DWSIM supports CAPE-OPEN Flowsheet Monitoring Objects (FMOs), such as the WAR Add-in created by William Barrett, USEPA:




![](images/screens64/Snap0043.png)



CAPE-OPEN Property Packages, CAPE-OPEN Unit Operations, DWSIM Property Packages and DWSIM Unit Operations can work together on any possible combination. For instance, you can use a DWSIM Property Package as the thermodynamics provider for a CAPE-OPEN Unit Operation in the same way you can use a CAPE-OPEN Property Package as the thermodynamics provider for a DWSIM Unit Operation.

#### Using external components

##### Property Packages

To use external CAPE-OPEN Property Packages in DWSIM, add a Property Package of the “CAPE-OPEN” type to the flowsheet:




![](images/screens64/Snap0051.png)



After that, click on “Configure” to setup your property package. On the window that appears, select a Thermo Server or Property Package Manager, depending on the CAPE version you chose. After selecting the server, a list of available Property Packages for that server/provider should be available for selection on the PP combo box:




![](images/screens64/Snap0052.png)



After selecting your Property Package, you can edit it by clicking on the “Edit” button. If the package was just selected or you’ve done changes to its compounds or phases, you MUST map compounds and phases to DWSIM equivalents on the “Compound/Component Mapping” and “Phase Mapping” tabs:




![](images/screens64/Snap0053.png)






![](images/screens64/Snap0054.png)



**Attention:** If your PP lists “Overall” or “Mixture” as a phase, you should not associate it with any of DWSIM phases. If a DWSIM phase doesn’t exist in the PP, select “Disable” as its label - DO NOT leave it blank! DWSIM and the CAPE-OPEN PP should have exactly the same number of compounds, even if some of them aren’t used by DWSIM or by the Property Package. After the compound and phase mapping steps, click “OK” and you’re ready to go.

##### Unit Operations

To add CAPE-OPEN Unit Operations to DWSIM flowsheets, drag and drop the “CAPEOPEN Unit Operation icon to the flowsheet. A selection window will appear where you should choose which Operation will be added:




![](images/screens64/Snap0048.png)



After the Unit is added, it works the same way as a DWSIM Unit Operation. You can edit its connections to Material and Energy Streams, parameters and access its ‘Edit’ window through the Property Editor.

**Attention:** The “Property Package” setting for the CAPE-OPEN Unit Operation has no effect. It accesses the property packages linked to Material Streams to do calculations. It is recommended that all streams connected to a CAPE-OPEN UO have the same associated Property Package to ensure consistency of the results obtained after flowsheet calculation.

##### Flowsheet Monitoring Objects

CAPE-OPEN Add-ins can be accessed through the “Plugins” menu item:




![](images/screens64/Snap0050.png)



#### Other features

##### Using DWSIM as a CAPE-OPEN Property Package Manager (Thermo 1.1) {#using-dwsim-as-a-cape-open-property-package-manager-thermo-1.1}

If you registered DWSIM types during the installation process, DWSIM will expose its Property Packages to CAPE-OPEN compliant simulators through Thermo 1.1 Interfaces (if your simulator is only Thermo 1.0 compliant, DWSIM Thermo Server will not be selectable). The configuration window allows you to add/remove compounds, configure flash settings, set the GUI language and edit model parameters and binary interaction parameters (BIPs). You can download an example of DWSIM Property Packages used as property and equilibrium calculators in COCO/COFE on SourceForge.




![](images/screens64/Snap0045.png)



##### Using the Script Unit Operation in CAPE-OPEN compliant simulators

If you registered DWSIM types during the installation process, the Custom Unit Operation will be exposed to CAPE-OPEN compliant simulators as “IronPython Script Unit Operation”. You can download an example of the Script UO being used to model a simple membrane separation process in COCO/COFE on SourceForge.




![](images/screens64/Snap0056.png)






![](images/screens64/Snap0044.png)



