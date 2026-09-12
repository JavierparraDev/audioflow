using AudioFlow.Models;
using AudioFlow.UI.Localization;

namespace AudioFlow.UI.ViewModels;

/// <summary>An output device shown in a combo box.</summary>
public sealed class DeviceOption
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public bool IsDefault { get; init; }

    public string DisplayName => IsDefault ? $"{Name}  ({Loc.Get("DeviceDefault")})" : Name;

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
    public string? PathHash { get; init; }

    private string? _processName;
    public string? ProcessName
    {
        get => _processName;
        set
        {
            if (SetProperty(ref _processName, value))
            {
                OnPropertyChanged(nameof(Subtitle));
            }
        }
    }

    private string? _deviceName;
    public string? DeviceName
    {
        get => _deviceName;
        set
        {
            if (SetProperty(ref _deviceName, value))
            {
                OnPropertyChanged(nameof(Subtitle));
            }
        }
    }

    private bool _isOnSpeakers;
    public bool IsOnSpeakers
    {
        get => _isOnSpeakers;
        set => SetProperty(ref _isOnSpeakers, value);
    }

    private string? _targetDeviceName;
    public string? TargetDeviceName
    {
        get => _targetDeviceName;
        set => SetProperty(ref _targetDeviceName, value);
    }

    private string? _ruleLabel;
    public string? RuleLabel
    {
        get => _ruleLabel;
        set => SetProperty(ref _ruleLabel, value);
    }

    public string Subtitle =>
        string.IsNullOrWhiteSpace(DeviceName) ? ProcessName ?? Key : $"{ProcessName ?? Key}  ·  {DeviceName}";
}

/// <summary>An application explicitly allowed to use the speakers.</summary>
public sealed class SpeakerAppItem : ObservableObject
{
    public required string Key { get; init; }
    public required string Name { get; init; }
    public string? PathHash { get; init; }
}

/// <summary>Technical session information shown only in Diagnostics.</summary>
public sealed class SessionDiagnostic
{
    public required string Process { get; init; }
    public required uint Pid { get; init; }
    public string? Aumid { get; init; }
    public string? SessionId { get; init; }
    public string? Endpoint { get; init; }
    public string? Device { get; init; }
    public string? State { get; init; }
    public string? Volume { get; init; }
    public string? Peak { get; init; }
    public string? Routing { get; init; }
    public string? Verified { get; init; }
}

/// <summary>A selectable UI language.</summary>
public sealed class LanguageOption
{
    public required string Code { get; init; }
    public required string Label { get; init; }

    public override string ToString() => Label;
}
