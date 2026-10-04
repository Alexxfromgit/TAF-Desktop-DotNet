# Contributing

Thanks for helping! Bug reports, docs fixes and features are all welcome.

## Build

```bash
dotnet build
dotnet test tests/Taf.Desktop.Core.Tests        # framework unit tests
dotnet test examples/Taf.Desktop.Examples       # UI tests against the demo app (Windows desktop, keep mouse idle)
```

You need the .NET 10 SDK on Windows 10/11.

## Guidelines

- **Framework code goes into `src/Taf.Desktop.Core`** and must not know about the demo app. Every behaviour change
  needs a unit test in `tests/Taf.Desktop.Core.Tests` that runs without an app; use `FakeNode` trees for the
  window model.
- Behaviour that only a real app shows (focus, modal dialogs, popups) needs an example test against both demo app
  flavours, and the same AutomationIds in WPF and WinForms.
- Prefer configuration keys with sensible defaults over new mandatory setup. Document new keys in
  `docs/configuration.md` and `taf.defaults.json`.
- Never commit secrets, internal hostnames or real personal data.
- Commit messages: imperative mood, short subject line (`Add tab control support`).

## Pull requests

1. For larger changes, open an issue first so we can agree on the approach.
2. Keep PRs focused and update `CHANGELOG.md` under *Unreleased*.
3. Make sure `dotnet test` is green.

By contributing you agree that your contributions are licensed under the MIT License.
