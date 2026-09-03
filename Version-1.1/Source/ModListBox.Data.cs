using System.Collections.Generic;
using System.Linq;
using Timberborn.Modding;
using Timberborn.SaveMetadataSystem;
using UnityEngine;

namespace Calloatti.SyncModsPro
{
  public partial class ModListBox
  {
    private List<ModRecord> GenerateUnifiedList(SaveMetadata metadata)
    {
      List<ModRecord> entries = new List<ModRecord>();
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

        ExportModObjectDiagnostics(mod);

        ModState currentState = isEnabled ? ModState.Enabled : ModState.Disabled;
        ModState savedState = inSave ? ModState.Enabled : ModState.Disabled;

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

        string computedSteamId = _workshopIdManager.GetSteamId(mod);
        ModSource originValue = mod.ModDirectory.IsUserMod ? ModSource.Local : ModSource.Steam;

        entries.Add(new ModRecord
        {
          UniqueRowKey = mod.ModDirectory.Path,
          ModId = id,
          ModName = mod.Manifest.Name,
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
          DupStatus = 0,
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
            string computedSteamId = _workshopIdManager.GetSteamId(savedMod.Id);
            entries.Add(new ModRecord
            {
              UniqueRowKey = savedMod.Id,
              ModId = savedMod.Id,
              ModName = savedMod.Name,
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

        bool inSave = saveFileInfo.ContainsKey(group.Key);
        string requestedVersion = inSave ? saveFileInfo[group.Key].Version : null;

        if (sortedGroup.Count == 1)
        {
          sortedGroup[0].DupStatus = -1;
        }
        else
        {
          ModRecord master = null;
          if (inSave) master = sortedGroup.FirstOrDefault(e => e.Version == requestedVersion);
          if (master == null) master = sortedGroup.First();

          // Initialize a counter to track the duplicate iteration
          int duplicateCounter = 1;

          foreach (var row in sortedGroup)
          {
            // Append the duplicate number dynamically to the ModName
            row.ModName = $"{row.ModName} ({duplicateCounter})";
            duplicateCounter++;

            if (row == master)
            {
              row.DupStatus = 1;
            }
            else
            {
              row.DupStatus = 0;

              if (row.Source != ModSource.Missing)
              {
                row.TargetState = ModState.Disabled;
              }

              // Corrects the history: The save file only enabled ONE of these duplicates.
              row.SavedState = ModState.Disabled;
            }
          }
        }
      }

      foreach (var modRecord in entries)
      {
        modRecord.UniqueRowKey = string.IsNullOrEmpty(modRecord.DirectoryPath) ? modRecord.ModId : modRecord.DirectoryPath;
        modRecord.UpdateStatus();
      }

      return entries.OrderBy(e => e.ModName)
                    .ThenBy(e => e.Source)
                    .ThenByDescending(e => e.Version)
                    .ToList();
    }
  }
}