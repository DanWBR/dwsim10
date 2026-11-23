# Desktop UI localization

## Selecting a language

Open **Edit > Preferences > Interface > Language**, choose **简体中文 (zh-CN)**,
accept the settings, and restart DWSIM. Choose **English (en)** and restart to
return to English. The settings store the culture code, not its display name.
No extension, launcher script, environment variable, or translation service is
required.

中文：打开 **编辑 > 首选项 > 界面 > 语言**，选择 **简体中文 (zh-CN)**，
保存设置后重新启动。选择 **English (en)** 并重启可切回英文。

## Current scope

The Simplified Chinese dictionary covers common welcome-screen text, main and
flowsheet menus, preferences, simulation setup, dock titles, and shared editor
labels. This is an initial, partial desktop translation. Newly added tools,
some equipment editors, canvas-drawn text, and engine diagnostics can still be
English. Compound names, property-package identifiers, units, user input, and
simulation values retain their original values.

Language changes take effect when controls are next constructed. Restarting is
required so existing windows consistently use the selected language.

## Translation resources

Resources are embedded in `DWSIM.UI.Shared.Avalonia` under
`Localization/zh-CN.json`, with an explicit invariant resource name. They do not
depend on satellite-language publish settings or external files.

Use `"Source text".Localize()` for a display caption in C#. In AXAML, declare
`xmlns:loc="using:DWSIM.UI.Shared.Avalonia"` and use
`Text="{loc:Localize 'Source text'}"`. A missing translation returns the source
text. Use format placeholders for variable text, rather than adding a key for
each possible number or composition. Keep placeholder indices consistent in
the translation.

Only localize presentation strings. Do not localize control names, dock IDs,
compound/model keys, editable values, or dropdown items that are stored as
simulation data. Localized menu headers retain their original lookup keys in
`LocalizationMenu.SourceHeader` so extensions can still locate their menus.

## Tests

Run `dotnet test tests/DWSIM.UI.Shared.Avalonia.Tests` for embedded-resource,
fallback, language-switching, markup-extension, and numeric-culture checks.
Build the desktop application to compile the localized AXAML files.
