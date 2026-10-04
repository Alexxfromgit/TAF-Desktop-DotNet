# App lifecycle, users and preconditions

## Start modes

Before each test, `DesktopTest` decides how to start:

| Mode | What happens |
|---|---|
| `ResetAppData` | Close the app, delete `App:DataDirectory` (settings, remembered user, caches), start it again |
| `RestartApp` | Close the app and start it again: back to the start window, data kept |
| `Reuse` | Keep the app; the test continues where the previous one stopped |

Desktop apps usually start in about a second, so the default is `RestartApp` (`App:Lifecycle`). This is the cheapest
mode that keeps tests independent.

## Decision rules

In order of precedence:

1. The **first test of the run** gets `ResetAppData` (`App:ResetDataOnFirstStart`), so nothing is left over from
   earlier runs.
2. If the **app is not running** because it crashed or was closed, it gets `RestartApp`.
3. After a **failed test** comes `App:AfterFailure` (default `RestartApp`), because a failure may leave the app in an
   unknown state.
4. **`[FreshApp]`** gets `RestartApp`, even in `Reuse` mode.
5. **`[ResetAppData]`**, or **another `[TestUser]`** than the previous test, gets `ResetAppData`.
6. Otherwise `App:Lifecycle` applies.

The decision is a report step ("Prepare the app: RestartApp"). When the app is closed (before a restart and at the end of
the run), its main window is asked to close first, and the app is killed after `App:CloseTimeout`. If a dialog is
still open because a test stopped halfway, the app is killed right away.

## Starting and attaching

```json
"App": {
  "Path": "artifacts/bin/ShopDesk.Wpf/${Build:Configuration}/ShopDesk.Wpf.exe",
  "DataDirectory": "${Taf:Root}/artifacts/app-data/shopdesk",
  "Arguments": "--api ${Mock:Url} --data-dir \"${App:DataDirectory}\"",
  "Environment": { "SHOPDESK_THEME": "light" }
}
```

- To test an app that is started by someone else (an installer, a launcher), leave `App:Path` empty and set
  `App:ProcessName`. The framework then attaches to the process and never closes it.
- If the app shows no window within `App:StartTimeout`, the test fails with an `EnvironmentException`. The report
  groups it under *Environment / infrastructure*.

## Test users

```json
"Users": { "standard": { "Username": "standard_user" } }
```

The password comes from the environment variable `TAF__Users__standard__Password`, never from a file.
`[TestUser("standard")]` on a test or class makes `User` available in tests and preconditions.

## Preconditions

```csharp
public sealed class LoggedInAttribute : PreconditionAttribute
{
    public override string Description => "signed in";

    public override void Establish(PreconditionContext context) => context.On<LoginWindow>().SignInAs(context.User);
}

[Test, TestUser("standard"), LoggedIn, CartContains("Canvas Backpack", 2, Order = 1)]
public void PlacedOrderReachesTheBackend() { ... }
```

- Preconditions run after the app started and before the test body.
- They run in `Order`, class attributes first, each as a report step ("Precondition: signed in").
- If one fails, the test is reported as broken, with a screenshot and the UI tree of the failed set-up.
