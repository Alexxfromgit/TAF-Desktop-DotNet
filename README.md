# desktop-taf

[![CI](https://github.com/Alexxfromgit/TAF-Desktop-DotNet/actions/workflows/ci.yml/badge.svg)](https://github.com/Alexxfromgit/TAF-Desktop-DotNet/actions/workflows/ci.yml)
[![UI tests](https://github.com/Alexxfromgit/TAF-Desktop-DotNet/actions/workflows/ui-tests.yml/badge.svg)](https://github.com/Alexxfromgit/TAF-Desktop-DotNet/actions/workflows/ui-tests.yml)
[![Allure report](https://img.shields.io/badge/report-Allure-orange)](https://alexxfromgit.github.io/TAF-Desktop-DotNet/)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
![.NET 10](https://img.shields.io/badge/.NET-10-informational)

**A ready-to-use .NET framework for Windows desktop UI test automation.** Click **Use this template**, point it at
your app, and you get declarative window objects, smart app lifecycle handling, backend mocking, performance timings
and failure evidence. Results are reported in Allure, and CI runs on GitHub's Windows runners.

It runs on .NET 10, NUnit 5, FlaUI 5 (UI Automation 3), FluentAssertions 7, WireMock.Net and Allure 2. It works with
WPF, WinForms, Win32 and UWP/WinUI apps. The examples run the same tests against a WPF and a WinForms flavour of a
bundled demo app.

```csharp
[Test]
[Category(TestCategories.Smoke)]
[TestUser("standard")]
[LoggedIn]
[CartContains("Canvas Backpack", 2)]
public void PlacedOrderReachesTheBackend()
{
    var confirmation = On<MainWindow>().OpenCart()
        .EnterCustomer("Ada Tester", "1 Test Street")
        .ChooseShipping("Express")
        .PlaceOrder();

    Verify.Equal(confirmation.Message, "Order ORD-1001 placed. Total: $69.97", "Confirmation");
    Backend.SingleRequest("POST", "/api/orders").Json.GetProperty("shipping").GetString().Should().Be("Express");
}
```

Preconditions are declarative, navigation is fluent, and the backend is a mock you control. If a test fails, the
report shows a screenshot and the UI Automation tree; a screen recording is optional.

## Features

| | |
|---|---|
| **Declarative windows** | `[Locate(AutomationId = "CartWindow")]` on the class, `[Locate(...)]` on fields. Components (panels, list rows) are scoped to their root. Mixins come from C# 14 extension properties. Generic Win32 message boxes and file dialogs are included. |
| **Readable failures** | `CartWindow.placeOrder is not visible after 10s [AutomationId=PlaceOrderButton]`: window, field and locator in one line. |
| **Window readiness** | `[WindowIdentifier]` and `[WaitForGone]` (loading indicators): `On<T>()` knows when a window is really ready. Waits are explicit only, with no `Thread.Sleep`. |
| **One API for every UI stack** | `Ui:ClickMode=Auto` uses UI Automation Invoke for WPF and real mouse clicks for WinForms and Win32. A WinForms Invoke that opens a modal dialog would block UI Automation. Combo boxes, menus and popups work the same everywhere. |
| **Smart app lifecycle** | Each test gets the cheapest safe start: restart the app, wipe its data, or keep it running. The app also restarts after a failure or a user change. `[FreshApp]` and `[ResetAppData]` force a clean start. |
| **Declarative preconditions** | `[LoggedIn]` and `[CartContains(...)]` are your own attributes, run before the test body. `[TestUser("standard")]` reads credentials from config and environment variables. |
| **Backend mocking** | The app talks to WireMock.Net in stub-only or reverse-proxy mode. Tests simulate errors, slow responses and outages, and check the requests the app sent. |
| **Performance timings** | App start, window-ready and click-to-next-window times are collected during functional tests. The report shows min/avg/p95/max against thresholds. |
| **Failure evidence** | Each failure gets a screenshot and the UI Automation tree as XML; each soft-assertion failure gets a screenshot. Screen recording with ffmpeg is optional. |
| **Suite hygiene** | Allure categories (*UI element not found*, *Product defects*, *Environment*...), infrastructure-only retries, `[KnownIssue]`, `[Quarantined]`, and a linter that fails on tests without owner or windows without identifier. |

## Quick start

You need Windows 10/11 and the .NET 10 SDK. The demo apps are built along with the tests.

```bash
dotnet build
dotnet test tests/Taf.Desktop.Core.Tests     # framework unit tests, no app needed
dotnet test examples/Taf.Desktop.Examples    # UI tests against the WPF demo app (keep mouse and keyboard idle)
```

To run the same tests against the WinForms flavour, set the environment:

```bash
TAF_ENV=winforms dotnet test examples/Taf.Desktop.Examples
```

Open the report with the Allure CLI (`npm i -g allure-commandline`, needs Java):

```bash
allure serve artifacts/allure-results/Taf.Desktop.Examples
```

| Command / setting | What it does |
|---|---|
| `dotnet test examples/Taf.Desktop.Examples --filter TestCategory=smoke` | Only smoke tests |
| `dotnet test examples/Taf.Desktop.Examples --filter TestCategory=static` | Linter only, no app |
| `TAF__App__Lifecycle=Reuse` | Keep the app running between tests |
| `TAF__Verify__Mode=Soft` | Collect all `Verify` failures of a test instead of stopping at the first |
| `TAF__Evidence__Video=OnFailure` | Record the screen and keep the video of failed tests (needs ffmpeg) |

The rest of the setup, including IDEs, display scaling and ffmpeg, is in [docs/local-setup.md](docs/local-setup.md).

## Project layout

```
src/Taf.Desktop.Core/          framework: config, NUnit integration, app lifecycle, windows, evidence, mocking, reports
tests/Taf.Desktop.Core.Tests/  framework unit tests (fake UI trees, no app)
examples/Taf.Desktop.Examples/ example tests, windows, preconditions and mock backend for the demo app (replace them)
demo/                          ShopDesk demo app: shared core, WPF and WinForms flavours
docs/                          guides
```

## Documentation

- [Adapting the template to your app](docs/adapting-to-your-app.md): start here
- [Local setup](docs/local-setup.md)
- [Windows, components and locators](docs/windows-and-locators.md)
- [App lifecycle, users and preconditions](docs/app-lifecycle.md)
- [Backend mocking](docs/network-mocking.md)
- [Configuration reference](docs/configuration.md)
- [Reports, failure taxonomy and suite hygiene](docs/reports.md)
- [Architecture](docs/architecture.md)

## Contributing and license

Contributions are welcome, see [CONTRIBUTING.md](CONTRIBUTING.md). The project is licensed under [MIT](LICENSE);
see [NOTICE](NOTICE) for the libraries it builds on.
