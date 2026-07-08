using System.Collections.Generic;
using Timberborn.Modding;

namespace Calloatti.SyncModsPro
{
  public enum ModStatus
  {
    Match,
    Version,
    Disabled,
    Missing,
    New
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

  public class ModRecord
  {
    public string ModName { get; set; }
    public string ModId { get; set; }
    public string VersionFolder { get; set; }
    public string MinimumGameVersion { get; set; }
    public string Version { get; set; }
    public string SavedVersion { get; set; } = "";

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

    public int SavedLoadOrder { get; set; }
    public int CurrentPriority { get; set; }
    public int TargetPriority { get; set; }

    public string Description { get; set; }
    public IEnumerable<VersionedMod> RequiredMods { get; set; }
    public IEnumerable<VersionedMod> OptionalMods { get; set; }

    public string DisplaySource { get; set; }
    public string OriginName { get; set; }
    public bool IsUserMod { get; set; }
    public string TargetGameVersion { get; set; }

    public Mod NativeModReference { get; set; }

    // Tracks the ModIds of all mods keeping this dependency enabled
    public HashSet<string> AutoEnabledBy { get; set; } = new HashSet<string>();

    public static readonly UnityEngine.Color[] StatusColors = new UnityEngine.Color[]
    {
    new UnityEngine.Color(0.2f, 0.8f, 0.2f), // 0: Match (Green)
      new UnityEngine.Color(0.9f, 0.9f, 0.2f), // 1: Version (Yellow)
      new UnityEngine.Color(0.9f, 0.5f, 0.1f), // 2: Disabled (Orange)
      new UnityEngine.Color(0.9f, 0.2f, 0.2f), // 3: Missing (Red)
      new UnityEngine.Color(0.7f, 0.4f, 0.9f)  // 4: New (Purple)
		};

    public UnityEngine.Color GetStatusColor()
    {
      int index = (int)Status;
      if (index >= 0 && index < StatusColors.Length)
      {
        return StatusColors[index];
      }
      return new UnityEngine.Color(0.9f, 0.9f, 0.9f);
    }

    public void UpdateStatus()
    {
      if (Source == ModSource.Missing)
      {
        Status = ModStatus.Missing;
        return;
      }

      if (SavedState == ModState.Disabled && CurrentState == ModState.Enabled)
      {
        Status = ModStatus.New;
        return;
      }

      if (SavedState == ModState.Enabled && CurrentState == ModState.Disabled)
      {
        Status = ModStatus.Disabled;
        return;
      }

      if (SavedState == ModState.Enabled && CurrentState == ModState.Enabled && Version != SavedVersion)
      {
        //Status = ModStatus.Version; 
        //return;
      }

      if (SavedState == CurrentState)
      {
        Status = ModStatus.Match;
        return;
      }

      Status = ModStatus.Match;
    }
  }
}