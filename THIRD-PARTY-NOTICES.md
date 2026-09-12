# Third-Party Notices

AudioFlow is distributed under the MIT License (see [LICENSE](LICENSE)).
It depends on the following third-party components.

## Runtime dependencies

| Component | Version | License | Project |
| --------- | ------- | ------- | ------- |
| .NET / .NET Runtime | 8.0 | MIT | https://github.com/dotnet/runtime |
| Windows Desktop (WPF) | 8.0 | MIT | https://github.com/dotnet/wpf |
| NAudio.Wasapi | 2.2.1 | MIT | https://github.com/naudio/NAudio |

`NAudio.Wasapi` is used for Windows Core Audio / WASAPI interop
(device enumeration, audio sessions, endpoint metering).

## Development / test dependencies

| Component | Version | License | Project |
| --------- | ------- | ------- | ------- |
| xUnit.net | 2.9.x | Apache-2.0 | https://github.com/xunit/xunit |
| xunit.runner.visualstudio | 2.8.x | Apache-2.0 | https://github.com/xunit/visualstudio.xunit |
| Microsoft.NET.Test.Sdk | 17.x | MIT | https://github.com/microsoft/vstest |

## Notes

- WPF is part of the .NET platform and is licensed under the MIT License.
- No GPL, LGPL or other copyleft dependency is used.
- If you add a dependency, document it here with its license and upstream URL.
