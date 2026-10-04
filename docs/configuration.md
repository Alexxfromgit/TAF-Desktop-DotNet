# Configuration

## Layers

Later layers win:

| # | Layer | Typical use |
|---|---|---|
| 1 | `taf.defaults.json` (embedded in Taf.Desktop.Core) | Framework defaults (below) |
| 2 | `taf.json` next to the test assembly | App path, users, report parameters |
| 3 | `taf.{env}.json` | Environments: `winforms`, `staging`, ... |
| 4 | Environment variables `TAF__Section__Key` | CI settings, secrets |
| 5 | Test run parameters | `.runsettings` `<TestRunParameters>`, or `dotnet test -- TestRunParameters.Parameter(name="App:Lifecycle", value="Reuse")` |
| 6 | Runtime overrides | Values set during the run, e.g. `Mock:Url` |

The environment comes from the test parameter `env` or the variable `TAF_ENV` (default `default`).

Values can reference other keys: `"Arguments": "--api ${Mock:Url}"`. Two keys are built in:

| Key | Value |
|---|---|
| `Taf:Root` | the repository root |
| `Build:Configuration` | the output folder of the test assembly (`debug` or `release` in the `artifacts/` layout) |

Relative paths (`App:Path`, `App:DataDirectory`, report directories) are resolved against the repository root.

## Secrets

Secrets are read from environment variables only, with `TafConfig.Current.Secret("Users:standard:Password")`, which
reads `TAF__Users__standard__Password`. Loading fails if `taf.json` or `taf.{env}.json` contains a value for a
secret-looking key (password, token, secret, api key, credential). The linter checks the same.

- **Locally:** set the variables in your shell. For a team, use a `.runsettings` file that stays out of git.
- **In CI:** use repository secrets, mapped to `env:`.
- **Examples:** the demo backend is a mock, so `RunSetup` generates the demo users' passwords for each run.
  No password is stored anywhere.

## Reference

| Key | Default | |
|---|---|---|
| `App:Path` | | Executable (relative to the repository root); empty = attach to `App:ProcessName` |
| `App:Arguments` / `App:WorkingDirectory` / `App:Environment:*` | | Process start |
| `App:DataDirectory` | | Deleted by `ResetAppData` |
| `App:ProcessName` | | Attach to a running process instead of starting one |
| `App:StartTimeout` / `App:CloseTimeout` | `30s` / `3s` | Close: main window first, killed after the timeout or at once if a dialog is open |
| `App:Lifecycle` / `App:AfterFailure` | `RestartApp` / `RestartApp` | `ResetAppData`, `RestartApp`, `Reuse` |
| `App:ResetDataOnFirstStart` | `true` | |
| `Ui:ClickMode` | `Auto` | `Auto`, `Invoke`, `Mouse` |
| `Ui:InvokeReturnTimeout` | `1500ms` | Continue when an Invoke opened a modal dialog |
| `Wait:Timeout` / `Wait:Poll` / `Wait:WindowTimeout` | `10s` / `200ms` / `20s` | Explicit waits |
| `Users:{alias}:Username` | | Password: `TAF__Users__{alias}__Password` |
| `Verify:Mode` | `Hard` | `Soft`: collect `Verify` failures of a test |
| `Evidence:ScreenshotOnFailure` / `Evidence:UiTreeOnFailure` / `Evidence:ScreenshotOnSoftFailure` | `true` | |
| `Evidence:Video` | `Off` | `Off`, `OnFailure`, `Always` (needs ffmpeg) |
| `Evidence:FfmpegPath` / `Evidence:VideoDirectory` | PATH / `artifacts/videos` | |
| `Perf:Enabled` / `Perf:Lookups` / `Perf:FailOnThreshold` | `true` / `false` / `false` | |
| `Perf:Thresholds:{timing}` | | e.g. `"MainWindow ready": "2s"`; compared with p95 |
| `Mock:UrlKey` | `Mock:Url` | Key under which `MockBackend` publishes its URL |
| `Report:Parameters:*` / `Report:Labels:*` | | Added to every test in Allure, e.g. `App: WinForms`, `parentSuite: WinForms` |
| `Report:CleanResults` / `Report:Directory` | `true` / `artifacts/taf-reports` | |
| `Lint:RequireOwner` / `Lint:RequireFeature` / `Lint:RequireWindowIdentifier` / `Lint:Secrets` | `true` | |
| `Lint:QuarantineMaxDays` | `90` | |
| `Taf:SecretsGuard:Enabled` | `true` | |
| `Taf:KeepAwake` | `true` | Keep the display on and the PC awake during the run |
| `Taf:RootDirectory` | detected | Repository root override |
