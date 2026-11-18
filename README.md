# Deep Dive

Deep Dive is a Windows desktop app for saving groups of applications, folders, and websites as workspaces and opening them together.

## Run in Visual Studio

1. Open `DeepDive.sln` in Visual Studio 2026 with the **.NET desktop development** workload and the .NET 10 SDK.
2. Select **Deep Dive Demo** next to the Run button and press **F5** to try the app before importing a database. Demo changes last for the current session.
3. Select **New workspace**, give it a name, and add actions. Each action has a type, a target, and an enabled setting. Save the workspace and select **Launch** to see the result for each enabled action.

You can reopen the editor from the results window to repair an action. A missing file or folder affects only that action. For an app, folder, or website, a successful result means the launch request was sent to Windows.

## Use an imported database

Import the supplied database into your current Windows account's `(localdb)\MSSQLLocalDB` instance using SQL Server tooling. Use the database name `DeepDive`. If your import has another name, set the `DEEPDIVE_DB_NAME` environment variable and restart Visual Studio before running.

The **Deep Dive** launch profile connects using Windows authentication. The project expects the imported schema to contain `dbo.Workspaces`, `dbo.WorkspaceActions`, `dbo.ActionTypes`, and `dbo.LaunchResults` with the columns from the project ERD. The imported `ActionTypes` must contain rows whose `TypeCode` values are `APP`, `FOLDER`, and `WEBSITE`. The app uses their actual database IDs.

Each Windows account imports into its own LocalDB instance. The app opens the existing database and uses it to save workspaces and launch results. If the database is unavailable or the expected schema is missing, the app explains the problem and opens in demo mode.

## Project structure

- `Models` defines the workspace, action, action type, and launch result.
- `Data` handles the imported database and provides in-memory storage for demo mode.
- `Services` validates and saves workspace drafts and launches enabled actions.
- `UI` contains the dashboard, workspace and action editors, and results window. The forms build their layouts in C#.

## Week 1 planning

- Design phase documentation
- UI design sketches
