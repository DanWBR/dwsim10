using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using DWSIM.UI.Shared.Avalonia;
using NUnit.Framework;

namespace DWSIM.UI.Shared.Avalonia.Tests;

[TestFixture, NonParallelizable]
public class LocalizationTests
{
    private CultureInfo _culture = null!;
    private CultureInfo? _defaultUICulture;

    [SetUp]
    public void SaveCulture()
    {
        _culture = CultureInfo.CurrentUICulture;
        _defaultUICulture = CultureInfo.DefaultThreadCurrentUICulture;
        Localization.SetCulture("en");
    }

    [TearDown]
    public void RestoreCulture()
    {
        Localization.SetCulture(_culture.Name);
        CultureInfo.DefaultThreadCurrentUICulture = _defaultUICulture;
    }

    [Test]
    public void ChineseDictionaryIsEmbeddedInTheMainAssembly()
    {
        var assembly = typeof(Localization).Assembly;
        var name = assembly.GetManifestResourceNames().Single(n => n.EndsWith("Localization.zh-CN.json", StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(name)!;
        using var document = JsonDocument.Parse(stream);
        Assert.That(document.RootElement.GetProperty("Temperature").GetString(), Is.EqualTo("温度"));
        Assert.That(document.RootElement.EnumerateObject().All(p => !string.IsNullOrEmpty(p.Value.GetString())), Is.True);
    }

    [Test]
    public void SwitchingBackToEnglishDoesNotRetainChinese()
    {
        Assert.That("Temperature".Localize(), Is.EqualTo("Temperature"));
        Localization.SetCulture("zh-CN");
        Assert.That("Temperature".Localize(), Is.EqualTo("温度"));
        Localization.SetCulture("en");
        Assert.That("Temperature".Localize(), Is.EqualTo("Temperature"));
    }

    [Test]
    public void MissingTranslationReturnsTheOriginalText()
    {
        Localization.SetCulture("zh-CN");
        const string unknown = "an unregistered user-defined compound";
        Assert.That(unknown.Localize(), Is.EqualTo(unknown));
        Assert.That("".Localize(), Is.Empty);
        Assert.That(((string)null!).Localize(), Is.Null);
    }

    [Test]
    public void ChangingUILanguageDoesNotChangeNumericCulture()
    {
        var before = CultureInfo.CurrentCulture;
        var sample = 1234.5.ToString(before);
        Localization.SetCulture("zh-CN");
        Assert.That(CultureInfo.CurrentCulture, Is.EqualTo(before));
        Assert.That(1234.5.ToString(CultureInfo.CurrentCulture), Is.EqualTo(sample));
    }

    [Test]
    public void InvalidCultureDoesNotDiscardTheActiveDictionary()
    {
        Localization.SetCulture("zh-CN");
        Localization.SetCulture("not_a_valid_culture_!@#");
        Assert.That("Pressure".Localize(), Is.EqualTo("压力"));
    }

    [Test]
    public void RegisteredFallbackRetainsItsExistingPrecedence()
    {
        var key = "language-test-" + Guid.NewGuid();
        Localization.Register(key, "registered value");
        Localization.SetCulture("zh-CN");
        Assert.That(key.Localize(), Is.EqualTo("registered value"));
        Localization.SetCulture("en");
        Assert.That(key.Localize(), Is.EqualTo("registered value"));
    }

    [Test]
    public void MarkupExtensionUsesTheSelectedLanguage()
    {
        Localization.SetCulture("zh-CN");
        Assert.That(new LocalizeExtension("Temperature").ProvideValue(null!), Is.EqualTo("温度"));
        Localization.SetCulture("en");
        Assert.That(new LocalizeExtension("Temperature").ProvideValue(null!), Is.EqualTo("Temperature"));
    }
}
