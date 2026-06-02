# Focus-Tracking-Timer

Windows 10/11 environment local timer app that tracks the currently focused program, groups programs into projects, and provides stored usage statistics.

## Tech Stack

- `.NET 10`
- `WPF`
- `xUnit`

## Repository Structure

```text
src/
  FocusTrackingTimer.App        WPF app
  FocusTrackingTimer.Core       domain and core logic
tests/
  FocusTrackingTimer.Core.Tests core logic tests
```

## Development

```bash
dotnet restore
dotnet build FocusTrackingTimer.sln
dotnet test FocusTrackingTimer.sln
dotnet run --project src/FocusTrackingTimer.App/FocusTrackingTimer.App.csproj
```

## Collaboration Baseline

- SDK version is pinned in `global.json`.
- Shared build rules live in `Directory.Build.props`.
- Package versions are centrally managed in `Directory.Packages.props`.
- VS Code users can run `build`, `test`, and `run-app` from `.vscode/tasks.json`.
