# Changelog

All notable changes to this project are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project uses [Semantic Versioning](https://semver.org/).

## [Unreleased]

## [1.0.0] - 2026-10-04

### Added
- Framework for Windows desktop UI tests on .NET 10, NUnit 5, FlaUI 5 (UIA3), FluentAssertions 7, WireMock.Net and
  Allure 2, with central package management and the `artifacts/` output layout.
- Window model: `[Locate]` windows, components, lists and elements, `[WindowIdentifier]` and `[WaitForGone]`
  readiness, mixins through C# 14 extension properties, message box and file dialog helpers. Lazily re-located
  elements with explicit waits and readable failures. Combo boxes, lists and menus (including WinForms popup menus)
  work through one API. Everything runs on an `IUiNode` abstraction that is unit tested with fake trees.
- Click strategy per UI stack (`Ui:ClickMode=Auto`): Invoke for WPF/XAML, mouse clicks for WinForms/Win32, whose
  Invoke blocks while a modal dialog is open. Typing waits for keyboard focus.
- App lifecycle: start or attach, `ResetAppData` / `RestartApp` / `Reuse` chosen by a policy (first start, crash,
  previous failure, user change), plus `[FreshApp]` and `[ResetAppData]`.
- `[TestUser]` with passwords from environment variables, and declarative `PreconditionAttribute`s.
- `MockBackend` on WireMock.Net (stub-only and reverse proxy) with request verification.
- Failure evidence (screenshot, UI tree, optional ffmpeg screen recording) and performance timings (app start,
  window ready, transitions) with thresholds.
- Shared foundation:
  - layered configuration (`taf.json`, `taf.{env}.json`, `TAF__` environment variables, run parameters) with a
    secrets guard;
  - failure taxonomy with Allure categories, and assertion failures of any library reported as failed;
  - `Verify` (hard and soft), `Step`, `[KnownIssue]`, `[Quarantined]`, `[InfraRetry]`, a metadata linter.
- ShopDesk demo app in WPF and WinForms flavours with identical AutomationIds, and example tests for sign-in,
  catalog, cart and checkout, menus and dialogs.
- CI: build, unit tests and linter on every push; UI tests for both flavours on Windows runners, with the merged
  Allure report published to GitHub Pages.

[Unreleased]: https://github.com/Alexxfromgit/TAF-Desktop-DotNet/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/Alexxfromgit/TAF-Desktop-DotNet/releases/tag/v1.0.0
