namespace DWSIM.UI.Desktop.Avalonia;

public sealed record LanguageChoice(string Code, string DisplayName)
{
    public override string ToString() => DisplayName;
}
