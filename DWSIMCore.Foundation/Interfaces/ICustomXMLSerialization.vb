'DWSIM Interface definition for Custom XML serialization
'Copyright 2012 Daniel Wagner O. de Medeiros


Imports System.Xml.Linq

<Runtime.InteropServices.InterfaceType(Runtime.InteropServices.ComInterfaceType.InterfaceIsIDispatch)>
Public Interface ICustomXMLSerialization

    Function SaveData() As System.Collections.Generic.List(Of System.Xml.Linq.XElement)

    Function LoadData(ByVal data As System.Collections.Generic.List(Of System.Xml.Linq.XElement)) As Boolean

End Interface
