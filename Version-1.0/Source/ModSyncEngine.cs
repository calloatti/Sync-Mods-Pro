using System;
using System.Collections.Generic;
using System.Linq;
using Timberborn.Modding;
using UnityEngine;

namespace Calloatti.SyncModsPro
{
  public static class ModSyncEngine
  {
    private static readonly List<string> ForceDisabledModIds = new List<string>
    {
      "calloatti.syncmods",
      "Calloatti.LoadGameModValidator"
    };

    private static readonly List<string> ForceEnabledModIds = new List<string>
    {
      "Harmony",
      "calloatti.syncmodspro"
    };

    private static readonly List<string> ForceTopModIds = new List<string>
    {
      "Harmony",
      "MoreModLogs"
    };

    public static bool ApplyModChanges(List<ModRecord> modTable)
    {
      try
      {
        // Filter out missing native references to avoid null errors
        var validRows = modTable.Where(r => r.NativeModReference != null).ToList();

        // ====================================================================
        // PASS 1: SET ALL ENABLED STATES
        // ====================================================================
        foreach (var row in validRows)
        {
          // Safety check: Do not attempt to alter state for missing mods
          if (row.TargetState == ModState.Missing)
          {
            continue;
          }

          row.SyncState = row.TargetState;

          // Enforce rigid overrides directly on the row's SyncState
          if (ForceDisabledModIds.Any(id => string.Equals(id, row.ModId, StringComparison.OrdinalIgnoreCase)))
            {
            row.SyncState = ModState.Disabled;
          }
          if (ForceEnabledModIds.Any(id => string.Equals(id, row.ModId, StringComparison.OrdinalIgnoreCase)))
          {
            row.SyncState = ModState.Enabled;
          }

          ModPlayerPrefsHelper.ToggleMod(row.SyncState == ModState.Enabled, row.NativeModReference);
        }

        // ====================================================================
        // PASS 2: ASSIGN ALL PRIORITIES
        // ====================================================================
        int currentPriority = 3000000;

        // --- Saved Game Mods ---
        var savedGameMods = validRows
          .Where(r => r.SyncState == ModState.Enabled && r.SavedState == ModState.Enabled)
          .OrderBy(r => r.SavedLoadOrder)
          .ToList();

        foreach (var row in savedGameMods)
        {
          ModPlayerPrefsHelper.SetModPriority(row.NativeModReference, currentPriority);
          row.TargetPriority = currentPriority;
          currentPriority -= 10000;
        }

        // --- Extra Enabled Mods (Start: 2,000,000 | Step: -10,000) ---
        var extraEnabledMods = validRows
          .Where(r => r.SyncState == ModState.Enabled && r.SavedState != ModState.Enabled)
          .OrderByDescending(r => r.CurrentPriority)
          .ToList();

        currentPriority = 2000000;
        foreach (var row in extraEnabledMods)
        {
          ModPlayerPrefsHelper.SetModPriority(row.NativeModReference, currentPriority);
          row.TargetPriority = currentPriority;
          currentPriority -= 10000;
        }

        // --- Disabled Mods (Start: 1,000,000 | Step: -10,000) ---
        var disabledMods = validRows
          .Where(r => r.SyncState == ModState.Disabled)
          .OrderByDescending(r => r.CurrentPriority)
          .ToList();

        currentPriority = 1000000;
        foreach (var row in disabledMods)
        {
          ModPlayerPrefsHelper.SetModPriority(row.NativeModReference, currentPriority);
          row.TargetPriority = currentPriority;
          currentPriority -= 10000;
        }

        // --- Priority Mods Override (Start: 4,000,000 | Step: -10,000) ---
        currentPriority = 4000000;
        foreach (var priorityId in ForceTopModIds)
        {
          var row = validRows.FirstOrDefault(r => string.Equals(r.ModId, priorityId, StringComparison.OrdinalIgnoreCase));
          if (row != null)
          {
            ModPlayerPrefsHelper.SetModPriority(row.NativeModReference, currentPriority);
            row.TargetPriority = currentPriority;
            currentPriority -= 10000;
          }
        }

        PlayerPrefs.Save();
        Debug.Log("[SyncModsPro] Mod configurations successfully written to PlayerPrefs.");
        return true;
      }
      catch (Exception ex)
      {
        Debug.LogError($"[SyncModsPro] CRITICAL ERROR during Sync apply: {ex}");
        return false;
      }
    }
  }
}