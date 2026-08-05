Imports DWSIM.Interfaces

<System.Serializable>
Public Class PropertyPackageMethods

    Implements Interfaces.IPropertyPackageMethods

    ''' <summary>
    ''' Gets or sets the method used to calculate vapor-phase fugacity coefficients.
    ''' </summary>
    Public Property Vapor_Fugacity As String = "" Implements IPropertyPackageMethods.Vapor_Fugacity

    ''' <summary>
    ''' Gets or sets the method used to calculate vapor-phase enthalpy, entropy, and heat capacities (Cp/Cv).
    ''' </summary>
    Public Property Vapor_Enthalpy_Entropy_CpCv As String = "" Implements IPropertyPackageMethods.Vapor_Enthalpy_Entropy_CpCv

    ''' <summary>
    ''' Gets or sets the method used to calculate vapor-phase thermal conductivity (default: Experimental / Ely-Hanley).
    ''' </summary>
    Public Property Vapor_Thermal_Conductivity As String = "Experimental / Ely-Hanley" Implements IPropertyPackageMethods.Vapor_Thermal_Conductivity

    ''' <summary>
    ''' Gets or sets the method used to calculate vapor-phase dynamic viscosity (default: Experimental / Lucas / Jossi-Stiel-Thodos).
    ''' </summary>
    Public Property Vapor_Viscosity As String = "Experimental / Lucas / Jossi-Stiel-Thodos" Implements IPropertyPackageMethods.Vapor_Viscosity

    ''' <summary>
    ''' Gets or sets the method used to calculate vapor-phase density.
    ''' </summary>
    Public Property Vapor_Density As String = "" Implements IPropertyPackageMethods.Vapor_Density

    ''' <summary>
    ''' Gets or sets the method used to calculate liquid-phase fugacity coefficients.
    ''' </summary>
    Public Property Liquid_Fugacity As String = "" Implements IPropertyPackageMethods.Liquid_Fugacity

    Public Property Liquid_Enthalpy_Entropy_CpCv As String = "" Implements IPropertyPackageMethods.Liquid_Enthalpy_Entropy_CpCv

    Public Property Liquid_ThermalConductivity As String = "Experimental / Latini" Implements IPropertyPackageMethods.Liquid_ThermalConductivity

    Public Property Liquid_Viscosity As String = "Experimental / Letsou-Stiel" Implements IPropertyPackageMethods.Liquid_Viscosity

    Public Property Liquid_Density As String = "Experimental / Rackett / COSTALD" Implements IPropertyPackageMethods.Liquid_Density

    Public Property SurfaceTension As String = "Experimental / Brock-Bird" Implements IPropertyPackageMethods.SurfaceTension

    Public Property Solid_Density As String = "Experimental Data / User-Defined" Implements IPropertyPackageMethods.Solid_Density

    Public Property Solid_Enthalpy_Entropy_CpCv As String = "Experimental Solid Cp / From Liquid Phase Enthalpy + Enthalpy of Fusion" Implements IPropertyPackageMethods.Solid_Enthalpy_Entropy_CpCv

End Class
