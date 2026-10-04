# Reports, failure taxonomy and suite hygiene

## Allure

Every test derived from `TafTest` is reported to Allure (the `[AllureNUnit]` attribute is inherited). On top of the
NUnit adapter, the framework adds:
- report steps for every UI action and check;
- report parameters and labels from config: with `Report:Parameters:App`, WPF and WinForms runs of a test appear
  side by side instead of as retries;
- failure evidence (screenshot, UI tree), categories, the environment overview and a performance report.

```bash
allure serve artifacts/allure-results/Taf.Desktop.Examples
```

CI merges the WPF and WinForms results into one report and publishes it to GitHub Pages.

Use the framework's `Step.Run(...)` instead of Allure's `[AllureStep]` or `AllureApi.Step`. With NUnit, those wrap
failures in a `TargetInvocationException`, or report a test with a broken step as passed.

## Failure taxonomy

| Category | Status | Comes from |
|---|---|---|
| Known issues | failed | Tests marked `[KnownIssue("GH-12")]` |
| UI element not found | failed | `ElementNotFoundException`, `WindowNotReadyException`: the UI changed or a locator is outdated |
| Product defects | failed | Any other assertion (`Verify`, FluentAssertions, NUnit), `PotentialDefectException` |
| Test data problems | broken | `TestDataException` |
| Environment / infrastructure | broken | App did not start, UI Automation timeouts, unreachable backend (`EnvironmentException`, `TimeoutException`, `COMException`, ...) |
| Quarantined | skipped | `[Quarantined("2026-11-15", "...")]` |
| Framework problems | broken | `FrameworkException`, null references, invalid casts, ...: misconfiguration or a bug in test code |

NUnit only reports "failed", and Allure.NUnit counts an assertion as a product failure only if NUnit's own `Assert`
raised it. So after each test, the framework records any other assertion failure (from FluentAssertions or the
framework exceptions) and puts the exception type at the top of the trace, where the categories look for it.

## Verify

```csharp
Verify.Equal(cart.Total, "Total: $74.96", "Order total");   // a report step; fails with expected and actual
Verify.That(!details.CanAddToCart, "Add to cart is disabled");
Verify.Softly("Cart lines", () => cart.Lines.Should().HaveCount(2));   // soft in any mode
```

With `Verify:Mode=Soft`, failures are collected, each with a screenshot, and the test fails when its body ends.

## Known issues, quarantine, retries

- **`[KnownIssue("GH-12")]`** keeps the test running. A failure becomes "[Known issue GH-12] ..." with a link and
  is grouped under *Known issues*. A pass logs a warning to remove the attribute.
- **`[Quarantined("2026-11-15", "flaky on CI")]`** skips the test until that date, then it runs again
  automatically. The linter rejects dates more than 90 days ahead.
- **`[InfraRetry(2)]`** retries a test only after an infrastructure failure (app start, UI Automation timeout,
  backend). Assertions are never retried.

## Failure evidence

| | |
|---|---|
| Screenshot on failure | the primary screen, so dialogs and popups are included |
| UI tree on failure | XML of the app's windows (control types, ids, names, values, bounds; passwords left out) |
| Screenshot per soft failure | `Evidence:ScreenshotOnSoftFailure` |
| Screen recording | `Evidence:Video=OnFailure` or `Always` with ffmpeg (`Evidence:FfmpegPath` or PATH) |
| App process | if the app exited or crashed during the test, the report says so |

## Performance timings

The framework collects these timings during the functional tests:
- app start (`ShopDesk.Wpf start`);
- window ready (`CartWindow ready`);
- click-to-next-window transitions (`MainWindow.cartButton -> CartWindow`);
- element lookups, optionally (`Perf:Lookups`).

At the end of the run, the result **Performance timings** in the suite *Framework reports* shows min/avg/p95/max and
marks p95 values above `Perf:Thresholds`. With `Perf:FailOnThreshold=true`, a breach fails the report result. The
same data is in `artifacts/taf-reports/performance.html` and `.json`.

## Metadata linter

`MetadataLintTest` (category `static`) runs in every CI build without an app. It fails when:
- a test has no `[AllureOwner]`, or no `[AllureFeature]`/`[AllureStory]`;
- a window has no class-level `[Locate]` or no `[WindowIdentifier]`;
- a `[Quarantined]` date is invalid or too far ahead, or a `[KnownIssue]` has no id;
- a configuration file contains a secret value.
