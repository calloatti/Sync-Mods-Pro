using System.Collections.Generic;
using System.Linq;
using Timberborn.Modding;
using Timberborn.SaveMetadataSystem;
using UnityEngine;

namespace Calloatti.SyncModsPro
{
  public partial class ModListBox
  {
    private List<RowData> GenerateUnifiedList(SaveMetadata metadata)
    {
      List<RowData> entries = new List<RowData>();
      List<Mod> allMods = _modRepository.Mods.ToList();
      HashSet<string> enabledModPaths = new HashSet<string>(_modRepository.EnabledMods.Select(m => m.ModDirectory.Path));

      Dictionary<string, (int Index, string Version)> saveFileInfo = new Dictionary<string, (int Index, string Version)>();
      if (metadata != null && metadata.Mods != null)
      {
        int index = 0;
        foreach (var sm in metadata.Mods)
        {
          saveFileInfo[sm.Id] = (index, sm.Version);
          index++;
        }
      }

      HashSet<string> foundSavedModIds = new HashSet<string>();

      // --- PASS 1: Physical Mods ---
      foreach (var mod in allMods)
      {
        string id = mod.Manifest.Id;
        bool isEnabled = enabledModPaths.Contains(mod.ModDirectory.Path);
        bool inSave = saveFileInfo.ContainsKey(id);

        if (inSave) foundSavedModIds.Add(id);

        // Batch Diagnostics Output
        ExportModObjectDiagnostics(mod);

        ModState currentState = isEnabled ? ModState.Enabled : ModState.Disabled;
        ModState savedState = inSave ? ModState.Enabled : ModState.Disabled;

        // --- UPDATED LOGIC ---
        // If it's in the save, it defaults to Enabled.
        // If it's not in the save, and strict mode is ON, force it to Disabled.
        // Otherwise, keep whatever its current state is.
        ModState targetState = inSave ? ModState.Enabled : (_isStrictOn ? ModState.Disabled : currentState);

        string computedVersionFolder;
        if (mod.ModDirectory.OriginName == mod.ModDirectory.Directory.Name)
        {
          computedVersionFolder = mod.ModDirectory.Directory.Name;
        }
        else
        {
          computedVersionFolder = $"{mod.ModDirectory.OriginName}\\{mod.ModDirectory.Directory.Name}";
        }

        string computedSteamId = ModSteamIdHelper.GetSteamId(mod);
        ModSource originValue = mod.ModDirectory.IsUserMod ? ModSource.Local : ModSource.Steam;

        entries.Add(new RowData
        {
          UniqueRowKey = mod.ModDirectory.Path,
          ModId = id,
          DisplayName = mod.DisplayName,
          Source = originValue,
          Version = mod.Manifest.Version.ToString(),
          SavedVersion = inSave ? saveFileInfo[id].Version : "-",
          SavedLoadOrder = inSave ? saveFileInfo[id].Index : -1,
          CurrentPriority = ModPlayerPrefsHelper.GetModPriority(mod),
          TargetPriority = 0,
          CurrentState = currentState,
          SavedState = savedState,
          TargetState = targetState,
          Description = mod.Manifest.Description,
          MinimumGameVersion = mod.Manifest.MinimumGameVersion.ToString(),
          RequiredMods = mod.Manifest.RequiredMods,
          OptionalMods = mod.Manifest.OptionalMods,
          DirectoryPath = mod.ModDirectory.Path,
          DisplaySource = mod.ModDirectory.DisplaySource,
          OriginName = mod.ModDirectory.OriginName,
          VersionFolder = computedVersionFolder,
          IsUserMod = mod.ModDirectory.IsUserMod,
          TargetGameVersion = mod.ModDirectory.GameVersion.ToString(),
          DupStatus = 0, // Managed by the duplicate pass below
          Url = string.IsNullOrEmpty(computedSteamId) ? null : $"https://steamcommunity.com/sharedfiles/filedetails/?id={computedSteamId}",
          NativeModReference = mod,
          SteamId = computedSteamId
        });
      }

      // --- PASS 2: Missing Mods ---
      if (metadata != null && metadata.Mods != null)
      {
        foreach (var savedMod in metadata.Mods)
        {
          if (!foundSavedModIds.Contains(savedMod.Id))
          {
            string computedSteamId = ModSteamIdHelper.GetSteamId(savedMod.Id);
            entries.Add(new RowData
            {
              UniqueRowKey = savedMod.Id,
              ModId = savedMod.Id,
              DisplayName = savedMod.Name,
              Source = ModSource.Missing,
              Version = "-",
              SavedVersion = savedMod.Version,
              SavedLoadOrder = saveFileInfo[savedMod.Id].Index,
              CurrentPriority = -1,
              TargetPriority = -1,
              CurrentState = ModState.Missing,
              SavedState = ModState.Enabled,
              TargetState = ModState.Disabled,
              Description = "This mod is missing from your hard drive.",
              MinimumGameVersion = "-",
              RequiredMods = new List<VersionedMod>(),
              OptionalMods = new List<VersionedMod>(),
              DirectoryPath = "",
              DisplaySource = "Missing",
              OriginName = "",
              VersionFolder = "-",
              IsUserMod = false,
              TargetGameVersion = "-",
              DupStatus = -1,
              Url = string.IsNullOrEmpty(computedSteamId) ? null : $"https://steamcommunity.com/sharedfiles/filedetails/?id={computedSteamId}",
              NativeModReference = null,
              SteamId = computedSteamId
            });
          }
        }
      }

      // --- DUPLICATE PRIORITY EVALUATION ---
      foreach (var group in entries.GroupBy(e => e.ModId))
      {
        var sortedGroup = group.OrderBy(e => e.Source)
                               .ThenByDescending(e => e.Version)
                               .ToList();

        if (sortedGroup.Count == 1)
        {
          sortedGroup[0].DupStatus = -1; // No duplicates exist for this item
        }
        else
        {
          for (int i = 0; i < sortedGroup.Count; i++)
          {
            if (i == 0)
            {
              sortedGroup[i].DupStatus = 1; // Prioritized active master item
            }
            else
            {
              sortedGroup[i].DupStatus = 0; // Inactive layout duplicate

              if (sortedGroup[i].Source != ModSource.Missing)
              {
                sortedGroup[i].TargetState = ModState.Disabled;
              }
            }
          }
        }
      }

      // Generate Unique Keys and initialize the Status Matrix immediately for UI updates
      foreach (var rowData in entries)
      {
        rowData.UniqueRowKey = string.IsNullOrEmpty(rowData.DirectoryPath) ? rowData.ModId : rowData.DirectoryPath;
        rowData.UpdateStatus();
      }

      return entries.OrderBy(e => e.DisplayName)
                    .ThenBy(e => e.Source)
                    .ThenByDescending(e => e.Version)
                    .ToList();
    }
  }
}