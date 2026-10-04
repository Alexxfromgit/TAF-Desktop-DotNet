# Adapting the template to your app

1. **Create your repository** with **Use this template**, then run `dotnet build` and `dotnet test`.
2. **Rename (optional).** Rename the projects and namespaces `Taf.Desktop.*` with your IDE's refactoring, and update
   `TafDesktop.slnx`.
3. **Point it at your app** in `examples/Taf.Desktop.Examples/taf.json`:
   ```json
   "App": {
     "Path": "C:/Program Files/Contoso/Orders/Orders.exe",
     "DataDirectory": "%LOCALAPPDATA%/Contoso/Orders",
     "Arguments": "--environment test"
   }
   ```
   For an app that is started by someone else, use `"ProcessName": "Orders"` instead of `Path`.
4. **Inspect the UI** with FlaUInspect (UIA3) and note AutomationIds and control types. Ask developers to add
   AutomationIds where they are missing: `AutomationProperties.AutomationId` in WPF and WinUI, the control `Name`
   in WinForms.
5. **Model windows.** For each window of your first flow, add a class-level `[Locate]`, a `[WindowIdentifier]`, the
   elements the tests need, and methods that return the next window. See [windows-and-locators.md](windows-and-locators.md).
6. **Users and preconditions.** Add users to `taf.json` and their passwords as environment variables
   (`TAF__Users__alias__Password`). Then write preconditions such as `[LoggedIn]` for the states many tests need.
7. **Backend.** If your app has a configurable backend URL, start a `MockBackend` in `RunSetup` and pass
   `${Mock:Url}` to the app. Otherwise, remove the mock. See [network-mocking.md](network-mocking.md).
8. **Remove the examples.** Delete `demo/`, the ShopDesk windows, tests and backend, and the demo-app references in
   the examples project. Keep `RunSetup` (without the generated demo passwords), `MetadataLintTest` and the
   `ShopDeskTest` base class (rename it).
9. **CI.**
   - `ci.yml` builds and runs the unit tests and the linter on every push.
   - `ui-tests.yml` runs the UI tests on a Windows runner, where your app must be installable or buildable.
   - Enable *Settings > Pages > Source: GitHub Actions* for the report.
