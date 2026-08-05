Public Class ResMan

    Public Shared _ResourceManager As System.Resources.ResourceManager
    Public Shared Culture As Globalization.CultureInfo

    Shared Sub WriteToConsole(text As String, level As Integer)
        'If level > DebugLevel Then Console.WriteLine(text)
    End Sub


    Public Shared Function GetLocalString(ByVal text As String) As String

        If Culture Is Nothing Then Culture = New Globalization.CultureInfo("en-US")

        If _ResourceManager Is Nothing Then

            'loads the resource manager
            _ResourceManager = New System.Resources.ResourceManager("DWSIM.UnitOperations.Strings", System.Reflection.Assembly.GetExecutingAssembly())

            End If

            If text <> "" Then

            Dim retstr As String = _ResourceManager.GetString(text, Culture)
            If retstr Is Nothing Then Return text Else Return retstr

        Else

            Return ""

        End If

    End Function


End Class
