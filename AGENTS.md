Include ..\AGENTS.md

# Sync Mods Pro — Mod-Specific Agent Instructions

## Identity
- **Assembly:** `syncmodspro`
- **Namespace:** `Calloatti.SyncModsPro`
- **ModId:** `calloatti.syncmodspro`
- **Framework:** Harmony, Bindito DI
- **Publicizer:** includes `Timberborn.CoreUI`, `Timberborn.Modding`, `Timberborn.MainMenuScene`
- **Min Game Version:** 1.0.0.0 — uses `timberborn-decompiled-1.0.*`

## What This Mod Does
Professional version of Sync Mods with enhanced mod management: profile management, dependency resolution, mod list UI with sorting/filtering, workshop ID management, obsolete mod validation, and save file sync.

## Source Architecture (`Version-1.0/Source/`)

| File | Role |
|---|---|
| `ModStarter.cs` | Entry point — `IModStarter` |
| `ModConfigurator.cs` | DI configurator |
| `ModProfileManager.cs` | Profile management |
| `ModSyncEngine.cs` | Core sync engine |
| `ModPatches.cs` | Harmony patches |
| `MainMenuPatch.cs` | Main menu UI patches — injects a Restart button; SHIFT-click skips the mod manager dialog; tooltip via vanilla `ITooltipRegistrar` (`Tooltip.RestartShiftClick` loc key) |
| `ExperimentalDialogPatch.cs` | Bypasses the experimental welcome dialog (configurable via `SkipExperimentalDialog`) |
| `ModListBox.cs` | Mod list box container |
| `ModListBox.Actions.cs` | Mod list actions |
| `ModListBox.Data.cs` | Mod list data handling |
| `ModListBox.Debug.cs` | Debug helpers |
| `ModListBox.RowData.cs` | Row data model |
| `ModListBox.UI.cs` | UI rendering |
| `ModListBox.UI.Links.cs` | Link handling in UI |
| `WorkshopIdManager.cs` | Workshop ID management |
| `WorkshopManager.cs` | Workshop operations |
| `WorkshopViewController.cs` | Workshop UI view controller |
| `DependencyViewController.cs` | Dependency graph display |
| `CustomTooltipManager.cs` | Custom tooltips |
| `ObsoleteModValidator.cs` | Obsolete mod detection |
| `SaveFileUtility.cs` | Save file introspection |
| `GameRestarter.cs` | Game restart utility |
| `Calloatti.Util.cs` | Shared utility helpers |

## Hard Rule
DO NOT EVER TOUCH THE DEPLOY FOLDER.

BUILD DOES EVERYTHING, NEVER EVER MESS WITH THE DEPLOY PROCESS.
