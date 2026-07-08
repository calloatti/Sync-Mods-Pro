using System;
using System.Collections.Generic;
using System.Linq;
using Timberborn.Modding;
using UnityEngine;

namespace Calloatti.SyncModsPro
{
  public static class ModSyncEngine
  {
    private static readonly HashSet<string> ObsoleteModIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
      "calloatti.syncmods",
      "Calloatti.LoadGameModValidator"
    };

    // Using a List here preserves the exact descending order we want for priority assignments
    private static readonly List<string> EssentialModIds = new List<string>
    {
      "Harmony",
      "calloatti.syncmodspro"
    };

    private static readonly List<string> PriorityModIds = new List<string>
    {
      "MoreModLogs"
    };

    public static bool ApplyModChanges(List<ModRecord> modTable)
    {
      try
      {

        // Using OrdinalIgnoreCase ensures we don't trip over case sensitivity mismatches
        HashSet<string> processedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Filter out missing native references to avoid null errors
        var validRows = modTable.Where(r => r.NativeModReference != null).ToList();

        // ====================================================================
        // PASS 1: SET ALL ENABLED STATES
        // ====================================================================
        foreach (var row in validRows)
        {
          bool enableMod = row.TargetState == ModState.Enabled;

          // Enforce rigid overrides for special mod categories
          if (ObsoleteModIds.Contains(row.ModId))
          {
            enableMod = false;
          }
          if (EssentialModIds.Any(id => string.Equals(id, row.ModId, StringComparison.OrdinalIgnoreCase)) ||
              PriorityModIds.Any(id => string.Equals(id, row.ModId, StringComparison.OrdinalIgnoreCase)))
          {
            enableMod = true;
          }

          ModPlayerPrefsHelper.ToggleMod(enableMod, row.NativeModReference);
        }

        // ====================================================================
        // PASS 2: ASSIGN ALL PRIORITIES
        // ====================================================================
        int currentPriority = 3000000;

        // --- Essential System Mods ---
        foreach (var essentialId in EssentialModIds)
        {
          var row = validRows.FirstOrDefault(r => string.Equals(r.ModId, essentialId, StringComparison.OrdinalIgnoreCase));
          if (row != null && !processedIds.Contains(row.ModId))
          {
            ModPlayerPrefsHelper.SetModPriority(row.NativeModReference, currentPriority);
            row.TargetPriority = currentPriority;
            processedIds.Add(row.ModId);
            currentPriority -= 10000;
          }
        }

        // --- Priority Mods ---
        foreach (var priorityId in PriorityModIds)
        {
          var row = validRows.FirstOrDefault(r => string.Equals(r.ModId, priorityId, StringComparison.OrdinalIgnoreCase));
          if (row != null && !processedIds.Contains(row.ModId))
          {
            ModPlayerPrefsHelper.SetModPriority(row.NativeModReference, currentPriority);
            row.TargetPriority = currentPriority;
            processedIds.Add(row.ModId);
            currentPriority -= 10000;
          }
        }

        // --- Saved Game Mods ---
        var savedGameMods = validRows
          .Where(r => r.TargetState == ModState.Enabled && r.SavedState == ModState.Enabled && !ObsoleteModIds.Contains(r.ModId) && !processedIds.Contains(r.ModId))
          .OrderBy(r => r.SavedLoadOrder)
          .ToList();

        foreach (var row in savedGameMods)
        {
          ModPlayerPrefsHelper.SetModPriority(row.NativeModReference, currentPriority);
          row.TargetPriority = currentPriority;
          processedIds.Add(row.ModId);
          currentPriority -= 10000;
        }

        // --- Extra Enabled Mods (Start: 2,000,000 | Step: -10,000) ---
        var extraEnabledMods = validRows
          .Where(r => r.TargetState == ModState.Enabled && !ObsoleteModIds.Contains(r.ModId) && !processedIds.Contains(r.ModId))
          .OrderByDescending(r => r.CurrentPriority)
          .ToList();

        currentPriority = 2000000;
        foreach (var row in extraEnabledMods)
        {
          ModPlayerPrefsHelper.SetModPriority(row.NativeModReference, currentPriority);
          row.TargetPriority = currentPriority;
          processedIds.Add(row.ModId);
          currentPriority -= 10000;
        }

        // --- Disabled Mods (Start: 1,000,000 | Step: -10,000) ---
        var disabledMods = validRows
          .Where(r => !processedIds.Contains(r.ModId))
          .OrderByDescending(r => r.CurrentPriority)
          .ToList();

        currentPriority = 1000000;
        foreach (var row in disabledMods)
        {
          ModPlayerPrefsHelper.SetModPriority(row.NativeModReference, currentPriority);
          row.TargetPriority = currentPriority;
          processedIds.Add(row.ModId);
          currentPriority -= 10000;
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