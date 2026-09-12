using System.Windows.Data;
using System.Windows.Markup;

namespace AudioFlow.UI.Localization;

/// <summary>
/// XAML markup extension: <c>Text="{loc:Tr NavDashboard}"</c>.
/// Resolves to a live binding on <see cref="LocalizationSource"/> so the UI
/// updates immediately when the language changes.
/// </summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class TrExtension : MarkupExtension
{
    public TrExtension()
    {
    }

    public TrExtension(string key) => Key = key;

    public string Key { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var binding = new Binding($"[{Key}]")
        {
            Source = LocalizationSource.Instance,
            Mode = BindingMode.OneWay
        };

        return binding.ProvideValue(serviceProvider);
    }
}
