# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

RDP Manager is a WPF desktop application for managing Remote Desktop Protocol (RDP) connections on Windows. It organizes computer/server entries into hierarchical groups, manages credentials securely via Windows Credential Manager, and provides system tray integration for quick access.

## Build Commands

### Build the project
```bash
dotnet build RdpManager/RdpManager.sln
```

### Run the application
```bash
dotnet run --project RdpManager/RdpManager/RdpManager.csproj
```

### Create release build
```bash
dotnet build RdpManager/RdpManager.sln --configuration Release
```

## Architecture

The application follows the **MVVM (Model-View-ViewModel)** pattern with a **SQLite persistence layer**:

- **Models** (`Models/`): Data structures including `ComputerEntry`, `Server`, `TreeNode`, `ConnectionHistory`, `Credential`, `RdpSession`, `FileExplorerSession`, and the Kubernetes models (`KubernetesCluster`, `KubernetesPod`, `KubernetesDeployment`, `KubernetesEvent`, `KubernetesResource`)
- **Services** (`Services/`): External integrations — `KubernetesService` orchestrates the `gcloud`/`kubectl` CLIs (per-cluster isolated kubeconfigs, optional proxy handling)
- **Data Layer** (`Data/`):
  - `Database/`: SQLite connection management and schema initialization
  - `Repositories/`: Data access layer (ComputerRepository, ConnectionLogRepository, GroupRepository, PreferencesRepository, TagRepository, FileExplorerSessionRepository, KubernetesClusterRepository)
  - `Models/`: Database-specific models (ComputerRdpSettings, Tag, ConnectionLog, Group)
  - `Migration/`: JSON-to-SQLite migration (JsonMigrator) and schema versioning (SchemaMigrator)
- **Views** (`Views/`): Modern UI (`ModernMainView.xaml`), view-specific components (ActiveSessionsView, ComputersView, ServersView, KubernetesView, FileExplorerView, RecentView, FavoritesView, GamesView), and ViewModel (`MainViewModel.cs`)
- **Controls** (`Controls/`): Custom controls including `RdpSessionControl` for embedded RDP
- **Dialogs** (`Dialogs/`): Dialog windows for add/edit, credentials, and confirmations
- **Windows** (`Windows/`): Additional windows like SettingsWindow, AddEditWindow, and base window classes
- **Controller** (`MainController.cs`): Handles external RDP connections, credential management, and UI automation
- **Helpers** (`Helpers/`): ThemeManager, DragDropHelper, GroupStateManager

### Data Persistence Evolution

The application migrated from JSON files to SQLite:
- **Legacy (JSON)**: `computers.json`, `connectionHistory.json`, `preferences.json` stored in app directory
- **Current (SQLite)**: `rdpmanager.db` in app directory with automatic migration on first run
- **Migration**: On first startup, `JsonMigrator` imports existing JSON data and creates `.json.backup` files
- **Schema Versioning**: `SchemaMigrator` handles database schema updates across versions

### Key Data Flow

1. **Startup** (`App.xaml.cs`):
   - Single instance enforcement via Mutex
   - Database initialization: Creates schema if needed, runs JSON migration, applies schema updates
   - Theme preference loading
   - Window creation (ModernMainView with fallback to legacy MainWindow)

2. **Computer Entries**:
   - Loaded from SQLite via `ComputerRepository.GetAll()`
   - Built into hierarchical tree in `MainViewModel.BuildComputerEntriesTree()`
   - Supports drag-and-drop reordering within groups

3. **Server Entries**:
   - Fetched from API endpoint (configured via `ServerEndpoint` in appSettings.json)
   - Built into tree in `MainViewModel.BuildServerEntriesTree()`
   - Organized by Domain/Application/Site hierarchy

4. **Connection History**:
   - Stored in SQLite via `ConnectionLogRepository`
   - Tracks timestamps, connection duration, and favorites
   - Limited to recent connections, displayed in Recent/Favorites views

5. **Credentials**:
   - Stored/retrieved via Windows Credential Manager
   - Target: "RdpManagerCredentials"
   - Type: Generic, Persistence: LocalComputer

### RDP Connection Process

The application supports two connection modes (configurable via `UseEmbeddedRdp` preference):

**Embedded Mode (Default):**
1. Create `RdpSession` model with server details
2. Add session to `ActiveSessionsView`
3. Initialize `RdpSessionControl` with MSTSCLib or RoyalApps.FreeRdp COM control
4. Load credentials and connect directly
5. Track connection state, duration, and display in session panel
6. Support session pop-out to separate windows via `PopoutWindow`

**External Mode:**
When connecting via `MainController.ConnectToMachine()`:
1. Load credentials from Windows Credential Manager
2. Generate temporary RDP file with connection settings
3. Launch `mstsc.exe` with the RDP file
4. Start UI Automation thread to auto-fill password in the Windows Security dialog

### System Tray Integration

- Initialized in `ModernMainView.InitializeSystemTray()`
- Application minimizes to tray instead of closing
- Double-click tray icon to restore window
- Right-click context menu with Show/Exit options
- Icon loaded from `icon.ico` in app directory

## Framework Support

The project targets **.NET 10.0 only** (`net10.0-windows`) for simplified maintenance and access to modern C# features including nullable reference types.

## Configuration

### Server Discovery Endpoint
The **Servers** tab is optional and populates from an HTTP API that returns JSON matching `ServerResponse` (a `servers` array of `Server` objects). The endpoint URL is stored in the `Preferences` table (key `ServerEndpoint`) and edited via the Settings UI; `MainViewModel.ServerEndpoint` reads/writes it. The compile-time default (`DefaultServerEndpoint` in `MainViewModel.cs`) is an empty string — when empty, `LoadServersAsync()` short-circuits and makes no network call, leaving the Servers tab empty.

### appSettings.json
An empty `appSettings.json` ships next to the executable as a placeholder. It is **not** read at runtime — there is no configuration-binding code. Do not document it as a live config source unless you also wire up reading it.

### Data Storage
- **Database**: `rdpmanager.db` (SQLite) - All persistent data
- **Legacy Files** (auto-migrated on first run):
  - `computers.json` → backed up as `computers.json.backup`
  - `connectionHistory.json` → backed up as `connectionHistory.json.backup`
  - `preferences.json` → backed up as `preferences.json.backup`

## Key Dependencies

- **Newtonsoft.Json**: JSON serialization for API responses and legacy migration
- **MaterialDesignThemes**: UI theming framework for modern WPF interface
- **System.Data.SQLite.Core**: SQLite database engine
- **MSTSCLib (COM)**: Remote Desktop ActiveX control (legacy embedded RDP)
- **RoyalApps.Community.FreeRdp.WinForms**: FreeRDP control for embedded RDP sessions
- **Interop.UIAutomationClient**: UI Automation for external RDP login automation
- **System.Windows.Forms**: System tray icon and Windows Forms integration for RDP control hosting

## Important Implementation Details

### Database Schema

Key tables managed by repositories:
- **Computers**: Computer entries with GroupPath, IsFavorite, SortOrder
- **ComputerRdpSettings**: Per-computer RDP settings (screen mode, color depth, audio, etc.)
- **Tags**: Tag definitions
- **ComputerTags**: Many-to-many relationship between computers and tags
- **Groups**: Group metadata (expanded state, sort order)
- **ConnectionLogs**: Connection history with timestamps and error codes
- **Preferences**: Key-value preference storage
- **FileExplorerSessions**: Saved file-explorer session states
- **KubernetesClusters**: Registered GKE cluster definitions

Indices optimize queries on MachineName, IsFavorite, GroupPath, and ConnectionLogs timestamp.

### TreeNode Structure
`TreeNode` (in `Models/TreeNode.cs`) is used for hierarchical display:
- Can represent either a group (has `Children`) or a leaf node (has `ComputerEntry` or `Server`)
- Properties: `Name`, `IsExpanded`, `IsVisible`, `IsFavorite`, `Children`, `ComputerEntry`, `Server`
- Used for Computers, Servers, Recent, and Favorites tree views

### Credential Storage
Credentials are stored securely using Windows Credential Manager via the `Credential` class:
- Target: "RdpManagerCredentials"
- Type: `CredentialType.Generic`
- Persistence: `PersistanceType.LocalComputer`
- Accessed via `MainController.LoadCredentials()`

### Server Filtering
Server entries support dynamic filtering via `MainViewModel.FilterText`:
- Filters recursively through the tree structure
- Matches against all Server properties (ServerName, Domain, Application, Site, etc.) using reflection
- Auto-expands matching branches

### Connection History & Favorites
- Managed by `ConnectionLogRepository`
- Tracks connection timestamp, duration, and error codes
- Favorites flag stored on ComputerEntry and in ConnectionLogs
- Display format: `FriendlyName [Group] (MachineName.Domain)`

### Embedded RDP Sessions
The application supports embedding RDP sessions:
- **RdpSession** (`Models/RdpSession.cs`): Tracks session state, connection time, and duration
- **RdpSessionControl** (`Controls/RdpSessionControl.cs`): WPF UserControl hosting RDP COM object via WindowsFormsHost
- **ActiveSessionsView** (`Views/ActiveSessionsView.xaml`): Session panel UI showing active connections with status indicators
- **PopoutWindow** (`Windows/PopoutWindow.xaml`): Allows popping out a session to a separate window
- **Connection States**: Disconnected, Connecting, Connected, Reconnecting, Failed
- **User Preference**: `MainViewModel.UseEmbeddedRdp` (default: true) stored in Preferences table

### Kubernetes Integration (optional)
The **Kubernetes** tab manages Google Kubernetes Engine (GKE) clusters by shelling out to the `gcloud` and `kubectl` CLIs (they must be installed and on `PATH`):
- **KubernetesService** (`Services/KubernetesService.cs`): Resolves CLI tool paths (handles `gcloud` being a `.cmd` on Windows), authenticates clusters via `gcloud container clusters get-credentials`, and runs `kubectl` queries. Each cluster uses an **isolated kubeconfig** under `%APPDATA%/RdpManager/kubeconfigs` so `~/.kube/config` is never modified. Supports an optional per-cluster HTTP(S) proxy and clearing proxy env vars before auth.
- **KubernetesCluster** (`Models/KubernetesCluster.cs`): Cluster definition (project ID, cluster name, region, default namespace, optional proxy). Persisted via `KubernetesClusterRepository`. `ContextName` derives the `gke_<project>_<region>_<cluster>` kube context.
- **KubernetesView** (`Views/KubernetesView.xaml`): Cluster list, authentication, and pod/deployment/event browsing. Add/edit clusters via `AddEditKubernetesClusterDialog`.
- This feature is optional; the tab is always present but unused if no clusters are configured.

### Games (hidden easter egg)
`GamesView` (Snake) is not a normal navigation section. It is added dynamically with `SectionIndex == 99` when triggered from `ComputersView` (`GamesActivated` event → `ModernMainView.ShowGamesTab()`), and removed when closed.

### Drag and Drop
- Implemented via `DragDropHelper` in Helpers folder
- Supports reordering computers within groups
- Visual feedback using `InsertionAdorner`
- Updates `SortOrder` field in database

### Theme Management
- `ThemeManager` singleton manages Light/Dark theme switching
- Theme preference stored in Preferences table
- Loaded on startup in `App.LoadThemePreference()`
- Uses MaterialDesignThemes for consistent styling

## Common Tasks

### Adding a new computer entry property
1. Update `Models/ComputerEntry.cs` to add the property
2. Update database schema in `Data/Database/DatabaseInitializer.cs` (add column to Computers table)
3. Update `Data/Repositories/ComputerRepository.cs` to include in queries and mappings
4. Update `Dialogs/AddEditComputerDialog.xaml` to add UI field
5. Update `Dialogs/AddEditComputerDialog.xaml.cs` to bind the property
6. Create schema migration in `Data/Migration/SchemaMigrator.cs` to add column to existing databases

### Modifying embedded RDP connection settings
Edit `Controls/RdpSessionControl.cs` in the `Connect()` method to change RDP control parameters (color depth, smart sizing, audio redirection, etc.)

### Modifying external RDP connection settings
Edit `MainController.CreateRdpFile()` to change RDP file parameters (screen resolution, session settings, etc.)

### Changing server API endpoint
The endpoint is stored in the `Preferences` table (key `ServerEndpoint`) and exposed via `MainViewModel.ServerEndpoint`. It defaults to an empty string (`DefaultServerEndpoint` constant in `MainViewModel.cs`), which disables server fetching. Set it through the Settings UI at runtime, or change the `DefaultServerEndpoint` constant to ship a built-in default.

### Adding a new tab/view
1. Create the view XAML and code-behind in `Views/`
2. Add `NavigationItem` to `MainViewModel.InitializeNavigation()`
3. Add case handler in `ModernMainView.xaml.cs` `NavigationListBox_SelectionChanged`
4. Update the tab content area in `ModernMainView.xaml`

### Creating a database migration
1. Update schema version in `SchemaMigrator.cs`
2. Add migration logic in `ApplyMigration()` method
3. Test migration path from previous version
4. Ensure rollback safety (migrations should be additive when possible)

### Debugging RDP connections
- Embedded mode: Check `RdpSession.ConnectionState` and `LastError` properties
- External mode: UI Automation logs are suppressed (commented out MessageBox calls in `MainController.AutomateRdpLogin()`)
- Connection logs are stored in ConnectionLogs table with error codes
