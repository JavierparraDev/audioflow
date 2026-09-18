# Localization

AudioFlow ships with **English (default)**, **Spanish** and **Turkish**.

## Resources

| File | Language |
| ---- | -------- |
| `src/AudioFlow.UI/Resources/Strings.resx` | English (neutral, default) |
| `src/AudioFlow.UI/Resources/Strings.es.resx` | Spanish |
| `src/AudioFlow.UI/Resources/Strings.tr.resx` | Turkish |

Resource base name: `AudioFlow.UI.Resources.Strings`.

## How it works

- `Localization/Loc.cs` wraps a `ResourceManager`, exposes `Get`, `Format` and
  `SetLanguage`, and raises `LanguageChanged`.
- `Localization/LocalizationSource.cs` is an `INotifyPropertyChanged` indexer used
  as a binding source. Raising `PropertyChanged` with an empty name refreshes all
  localized bindings.
- `Localization/TrExtension.cs` is the XAML markup extension:
  `Text="{loc:Tr NavDashboard}"`.

## Rules

1. **Never hardcode user-visible text.** Add a key to `Strings.resx` and every
   translated `Strings.<culture>.resx` (including the language labels such as
   `LanguageTurkish`), then use `{loc:Tr Key}` in XAML or `Loc.Get("Key")` in code.
2. Keep terminology consistent:
   - Speakers / Parlantes
   - Headphones / Audífonos
   - Applications / Aplicaciones
   - Default / Predeterminado
   - Audio Lock / Bloqueo de audio
   - Start / Iniciar, Stop / Detener
   - Settings / Configuración
   - Output device / Dispositivo de salida
3. The default language is English. The language selector lives in
   **Settings > Language** and applies immediately.

## Adding a language

1. Copy `Strings.resx` to `Strings.<culture>.resx` (e.g. `Strings.fr.resx`).
2. Translate the values.
3. Add an entry to `MainViewModel.Languages`.
