using System;
using Avalonia.Markup.Xaml;

namespace DWSIM.UI.Shared.Avalonia;

public sealed class LocalizeExtension : MarkupExtension
{
    public string Text { get; set; } = "";

    public LocalizeExtension() { }

    public LocalizeExtension(string text) => Text = text;

    public override object ProvideValue(IServiceProvider serviceProvider) => Text.Localize();
}
