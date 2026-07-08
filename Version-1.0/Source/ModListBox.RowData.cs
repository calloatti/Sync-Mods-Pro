using System.Collections.Generic;
using Timberborn.Modding;

namespace Calloatti.SyncModsPro
{
  public enum ModStatus
  {
    NotApplicable,
    Match,
    Version,
    Disabled,
    Missing,
    New,
    Duplicate
  }

  public enum ModSource
  {
    Local = 0,
    Steam = 1,
    Missing = 99
  }

  public enum ModState
  {
    Enabled,
    Disabled,
    Missing
  }

  public class RowData
  {
    public string DisplayName { get; set; }
    public string ModId { get; set; }
    public string VersionFolder { get; set; }
    public string MinimumGameVersion { get; set; }
    public string Version { get; set; }
    public string SavedVersion { get; set; } = "";

    // --- RESTORED PROPERTIES FOR UI LINKS ---
    public string Url { get; set; }
    public string DirectoryPath { get; set; }

    public ModState CurrentState { get; set; }
    public ModState SavedState { get; set; }
    public ModState TargetState { get; set; }

    public ModStatus Status { get; set; }

    public ModSource Source { get; set; }
    public string SteamId { get; set; }
    public int DupStatus { get; set; } = -1;
    public string UniqueRowKey { get; set; }

    // --- RESTORED PRIORITY MATRIX ---
    /// <summary>
    /// The exact index (1, 2, 3...) this mod appeared in the save file. Default to -1 if not in save.
    /// Applied to ALL physical copies of this ModId.
    /// </summary>
    public int SavedLoadOrder { get; set; }

    /// <summary>
    /// The raw integer priority currently saved in Timberborn's PlayerPrefs.
    /// </summary>
    public int CurrentPriority { get; set; }

    /// <summary>
    /// The calculated priority we will write to PlayerPrefs on Apply & Restart.
    /// </summary>
    public int TargetPriority { get; set; }

    // --- RESTORED MANIFEST METADATA ---
    public string Description { get; set; }
    public IEnumerable<VersionedMod> RequiredMods { get; set; }
    public IEnumerable<VersionedMod> OptionalMods { get; set; }

    // --- RESTORED PHYSICAL DIRECTORY DATA ---
    public string DisplaySource { get; set; }
    public string OriginName { get; set; }
    public bool IsUserMod { get; set; }
    public string TargetGameVersion { get; set; }

    // --- RESTORED UTILITY COLUMNS ---
    /// <summary>
    /// Direct reference to the game's native Mod object. Null if the mod is missing.
    /// </summary>
    public Mod NativeModReference { get; set; }

    // Static color definitions mapping exactly to the ModStatus enum values (0 to 6)
    public static readonly UnityEngine.Color[] StatusColors = new UnityEngine.Color[]
    {
      new UnityEngine.Color(0.4f, 0.4f, 0.4f), // 0: NotApplicable (Gray)
      new UnityEngine.Color(0.2f, 0.8f, 0.2f), // 1: Match (Green)
      new UnityEngine.Color(0.9f, 0.9f, 0.2f), // 2: Version (Yellow)
      new UnityEngine.Color(0.9f, 0.5f, 0.1f), // 3: Disabled (Orange)
      new UnityEngine.Color(0.9f, 0.2f, 0.2f), // 4: Missing (Red)
      new UnityEngine.Color(0.7f, 0.4f, 0.9f), // 5: New (Purple)
      new UnityEngine.Color(0.4f, 0.4f, 0.4f)  // 6: Duplicate (Gray)
    };

    /// <summary>
    /// Fetches the configured status color for this row entry.
    /// </summary>
    public UnityEngine.Color GetStatusColor()
    {
      int index = (int)Status;
      if (index >= 0 && index < StatusColors.Length)
      {
        return StatusColors[index];
      }
      return new UnityEngine.Color(0.9f, 0.9f, 0.9f); // Fallback to normal text color
    }

    public void UpdateStatus()
    {
      // 1. Structural Traps (Duplicates and Missing files override everything)
      if (DupStatus == 0)
      {
        //Status = ModStatus.Duplicate;
        //return;
      }

      if (Source == ModSource.Missing)
      {
        Status = ModStatus.Missing;
        return;
      }

      // 2. State Discrepancies (Active current game profile vs. Save data)
      if (SavedState == ModState.Disabled && CurrentState == ModState.Enabled)
      {
        Status = ModStatus.New; // Active now, but wasn't in the save
        return;
      }

      if (SavedState == ModState.Enabled && CurrentState == ModState.Disabled)
      {
        Status = ModStatus.Disabled; // Needed by save, but currently turned off in-game
        return;
      }

      // 3. Version Discrepancies (Only evaluated if both profiles are actively running it)
      if (SavedState == ModState.Enabled && CurrentState == ModState.Enabled && Version != SavedVersion)
      {
        //Status = ModStatus.Version; // Dynamic version mismatch
        //return;
      }

      // 4. Perfect Sync Catch-Alls
      if (SavedState == CurrentState)
      {
        Status = ModStatus.Match; // Cleanly disabled on both sides (N/A)
        return;
      }

      // If it survives all rules, it's a perfect match
      //Status = ModStatus.Match;
    }
  }
}