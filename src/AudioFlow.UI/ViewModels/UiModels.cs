using AudioFlow.Models;

namespace AudioFlow.UI.ViewModels;

/// <summary>An output device shown in a combo box.</summary>
public sealed class DeviceOption
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public bool IsDefault { get; init; }

    public string DisplayName => IsDefault ? $"{Name}  (predeterminado)" : Name;

    public override string ToString() => DisplayName;

    public static DeviceOption From(AudioDevice device) => new()
    {
        Id = device.Id,
        Name = device.FriendlyName,
        IsDefault = device.IsDefault
    };
}

/// <summary>A detected application shown in the "detected" list.</summary>
public sealed class DetectedAppItem : ObservableObject
{
    public required string Key { get; init; }
    public required string Name { get; init; }
    public string? ProcessName { get; init; }
    public string? DeviceName { get; init; }

    private bool _isOnSpeakers;
    public bool IsOnSpeakers
    {
        get => _isOnSpeakers;
        set => SetProperty(ref _isOnSpeakers, value);
    }

    public string Subtitle =>
        string.IsNullOrWhiteSpace(DeviceName) ? ProcessName ?? Key : $"{ProcessName ?? Key}  ·  {DeviceName}";
}

/// <summary>An application explicitly allowed to use the speakers.</summary>
public sealed class SpeakerAppItem : ObservableObject
{
    public required string Key { get; init; }
    public required string Name { get; init; }
}
