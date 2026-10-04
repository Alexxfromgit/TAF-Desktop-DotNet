# Local setup

## Requirements

| | |
|---|---|
| Windows 10 or 11 | UI Automation is part of Windows |
| .NET 10 SDK | `dotnet --list-sdks`; `global.json` accepts any 10.0.1xx or later |
| An IDE (optional) | JetBrains Rider or Visual Studio 2022+; open `TafDesktop.slnx` |
| Allure CLI (optional) | `npm i -g allure-commandline` (needs Java 8+) or `scoop install allure` |
| ffmpeg (optional) | Only for screen recordings: `winget install Gyan.FFmpeg` |

## Build and run

```bash
dotnet build                                    # framework, demo apps and tests (output in artifacts/)
dotnet test tests/Taf.Desktop.Core.Tests        # framework unit tests, no app
dotnet test examples/Taf.Desktop.Examples       # UI tests, WPF demo app
TAF_ENV=winforms dotnet test examples/Taf.Desktop.Examples
```

In PowerShell, set the environment with `$env:TAF_ENV = "winforms"`. The examples need no passwords: their mock
backend accepts the passwords that `RunSetup` generates for each run.

## While UI tests run

- **Keep mouse and keyboard idle.** WPF elements are clicked through UI Automation, but WinForms and Win32 elements
  get real mouse clicks, and typing goes to the focused window. Before a click, the framework checks that the click point
  belongs to the app. If another window covers it, the test fails with *another window covers it* (an environment
  problem) rather than clicking into that window.
- **Don't lock the screen.** UI Automation needs an unlocked, interactive desktop. During a run the framework keeps
  the display on (`Taf:KeepAwake`), but a lock from a policy, or one that started before the run, still blocks all
  input. Remote desktop sessions work
  while they are open; a minimised RDP window stops rendering.
- **Display scaling.** Any scaling works, because element positions come from UI Automation. Screenshots use the
  primary screen.
- **Run sequentially.** Desktop UI tests share one screen, so don't enable NUnit parallelism for them. The
  framework unit tests are independent.

## Inspecting an app

To find AutomationIds and control types, use [FlaUInspect](https://github.com/FlaUI/FlaUInspect) (choose UIA3) or
Accessibility Insights for Windows. When a test fails, its report has a **UI tree** attachment that shows exactly
what the framework could see at that moment.

## Reports

```bash
allure serve artifacts/allure-results/Taf.Desktop.Examples
```

The framework's own reports, such as the performance timings, are in `artifacts/taf-reports/`.
