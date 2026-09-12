using System.ComponentModel;

namespace AudioFlow.UI.Localization;

/// <summary>
/// Binding source for XAML: <c>{Binding [Key], Source={x:Static loc:LocalizationSource.Instance}}</c>
/// or, more conveniently, the <see cref="TrExtension"/> markup extension.
/// Raising PropertyChanged with an empty name refreshes every indexer binding.
/// </summary>
public sealed class LocalizationSource : INotifyPropertyChanged
{
    public static LocalizationSource Instance { get; } = new();

    private LocalizationSource() =>
        Loc.LanguageChanged += () => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));

    public string this[string key] => Loc.Get(key);

    public event PropertyChangedEventHandler? PropertyChanged;
}
