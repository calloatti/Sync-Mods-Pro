# SyncModsPro - Architectural Refactor & Feature Roadmap

## 1. UI Architecture Shift: Moving to `IPanelController`
**Current State:** The UI is currently built using Timberborn's `DialogBoxShower`, which forces a strict modal window layout and restricts our ability to customize the window frame or add detached UI elements.
**New Architecture:** We are transitioning `ModListBox` to implement `IPanelController`. 
* **Trigger:** We will use `_panelStack.HideAndPushOverlay(this)` (or similar `PanelStack` methods) instead of spawning a dialog box.
* **The Transparent Wrapper:** The root `VisualElement` will be completely transparent with a `flex-direction: column` layout.
* **The Split Layout:** Inside the transparent root, we will manually create two separate visual containers:
    1.  `_mainWindow`: A large container holding the active view (using the native Timberborn nine-slice wood backgrounds).
    2.  `_bottomDock`: A smaller, detached container sitting below the main window, acting as a persistent navigation/tool hub.

## 2. View Switching Logic (The "Tab" System)
Instead of spawning multiple dialogs or destroying UI elements, we will use a "Hide and Seek" view-switching pattern.
* All three major views (Main Matrix, Steam History, Dependency Audit) are generated when the panel opens.
* They are wrapped in their own container `VisualElement`s (`_mainView`, `_historyView`, `_dependencyView`).
* The `_bottomDock` contains navigation buttons.
* Clicking a navigation button simply flips `style.display = DisplayStyle.None` for all inactive views and `DisplayStyle.Flex` for the active view. This preserves all unsaved state (like checkboxes the user just clicked) instantly and without performance overhead.

## 3. Feature: Steam Workshop History
**Goal:** Persist a history of Subscribe/Unsubscribe actions across game restarts so users can track changes and revert mistakes.
* **Data Model:** ```csharp
    public enum SteamActionType { Subscribed, Unsubscribed }
    public class SteamActionRecord {
      public string SteamId { get; set; }
      public string DisplayName { get; set; }
      public SteamActionType ActionTaken { get; set; }
      public long TimestampTicks { get; set; }
    }
    ```
* **Persistence:** Use `Newtonsoft.Json` to serialize a `List<SteamActionRecord>` to a `.json` file stored in `Application.persistentDataPath`.
* **Interception:** In `ModListBox.Steam.cs`, intercept successful Steam callbacks (`OnSubscribeResult` / `OnUnsubscribeResult`) to append the action to the JSON file. If an inverse action already exists (e.g., subscribing to a mod just unsubscribed from), remove the old entry to prevent clutter.
* **UI (The History View):** A scrollable list showing recent actions. Each row contains a "Revert" button that dispatches the inverse Steam API call and deletes the record.

## 4. Feature: Dependency Audit
**Goal:** Provide a flat, easy-to-read list of all required dependencies to help users troubleshoot "Dependency Hell".
* **Data Generation:** Run a background pass over the generated `RowData` list. Parse `Manifest.RequiredMods` and `OptionalMods`.
* **Data Model:** Create a grouped mapping that links a requested Dependency ID to:
    1.  Its current installation status (Match `[E]`, Disabled `[D]`, or Missing `[M]`).
    2.  A list of "Requested By" parent mods.
* **UI (The Audit View):** A 3-column table:
    * **Column 1:** Dependency Name / ID.
    * **Column 2:** Target Status (using existing color-coding logic, highlighting Missing dependencies in red).
    * **Column 3:** Requested By (Comma-separated list of parent mod names).
* **Scope Note:** The audit should dynamically reflect the *Target State* of mods in the main UI, so if a user toggles a mod on in the Main View, its dependencies immediately show up in the Audit View, even before hitting "Apply & Restart".

## 5. Development Steps (Next Session)
1.  **Refactor ModListBox.cs:** Change from `ILoadableSingleton` to `IPanelController, ILoadableSingleton`. Rewrite `ShowDialog` into the `Open()` / `GetPanel()` interface methods.
2.  **Build the Dock:** Implement the transparent root, the main window styling, and the bottom dock styling.
3.  **Wire the Views:** Create the empty container frames for the 3 views and the dock buttons to toggle their display states.
4.  **Implement Dependency Data:** Write the parsing logic to group dependencies and populate the Audit UI.
5.  **Implement Steam Persistence:** Write the JSON read/write logic and wire it to the Steam callback success blocks.