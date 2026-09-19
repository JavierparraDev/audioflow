using System.Globalization;
using System.Resources;

namespace AudioFlow.UI.Localization;

/// <summary>
/// Runtime localization accessor. Default language is English; the UI ships
/// with several translations (see docs/LOCALIZATION.md). UI text is never
/// hardcoded: everything goes through <see cref="Get"/>.
/// </summary>
public static class Loc
{
    private static readonly ResourceManager Manager =
        new("AudioFlow.UI.Resources.Strings", typeof(Loc).Assembly);

    public static CultureInfo CurrentCulture { get; private set; } = CultureInfo.GetCultureInfo("en");

    /// <summary>Raised after the language changes so bindings can refresh.</summary>
    public static event Action? LanguageChanged;

    public static string Get(string key)
    {
        try
        {
            return Manager.GetString(key, CurrentCulture) ?? key;
        }
        catch
        {
            return key;
        }
    }

    public static string Format(string key, params object[] args)
    {
        var format = Get(key);
        try
        {
            return string.Format(CurrentCulture, format, args);
        }
        catch (FormatException)
        {
            return format;
        }
    }

    public static void SetLanguage(string cultureCode)
    {
        var culture = CultureInfo.GetCultureInfo(cultureCode);
        if (Equals(culture, CurrentCulture))
        {
            return;
        }

        CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        LanguageChanged?.Invoke();
    }
}
