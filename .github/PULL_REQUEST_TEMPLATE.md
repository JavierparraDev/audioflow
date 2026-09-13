# Pull request

## What does this PR do?

<!-- A clear description of the change and the problem it solves. -->

Closes #

## Type of change

- [ ] Bug fix (non-breaking change that fixes an issue)
- [ ] New feature (non-breaking change that adds functionality)
- [ ] Breaking change (fix or feature that changes existing behavior)
- [ ] Documentation
- [ ] Refactor / performance
- [ ] Build / CI / tooling

## How has this been tested?

<!-- Describe the tests you ran and the hardware/OS used. -->

- OS / build:
- Audio devices:
- Commands or steps:

```powershell
dotnet test tests/AudioFlow.Tests
```

## Checklist

- [ ] I have read [CONTRIBUTING.md](../CONTRIBUTING.md).
- [ ] My code follows the style of the surrounding code.
- [ ] UI strings are localized in both `Strings.resx` and `Strings.es.resx`.
- [ ] I added or updated tests for the behavior change.
- [ ] `dotnet build AudioFlow.sln -c Release` succeeds with no warnings.
- [ ] All tests pass.
- [ ] AudioFlow still leaves no rules, logs or registry changes behind when closed.
- [ ] I have updated the documentation (README / docs) if needed.

## Screenshots (for UI changes)

<!-- Before / after if applicable. -->
