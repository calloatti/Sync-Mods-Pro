using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Timberborn.Modding;
using UnityEngine;

namespace Calloatti.SyncModsPro
{
  public class RosettaEntry
  {
    public string PublishedFileID { get; set; }
    public string Id { get; set; }
    public string DirectoryName { get; set; }
    public string Name { get; set; }
    public string Title { get; set; }
    public string Version { get; set; }
    public string MinimumGameVersion { get; set; }
    public string RequiredModsId { get; set; }
    public string RequiredModsMinimumVersion { get; set; }
    public string OptionalModsId { get; set; }
    public string OptionalModsMinimumVersion { get; set; }
  }

  public static class RosettaDatabase
  {
    private static readonly List<RosettaEntry> _entries = new List<RosettaEntry>();
    private static bool _isLoaded = false;

    private static void EnsureLoaded()
    {
      if (_isLoaded) return;

      _entries.Clear();

      try
      {
        string rosettaPath = Path.Combine(ModStarter.ModPath, "rosetta.txt");

        if (File.Exists(rosettaPath))
        {
          string[] lines = File.ReadAllLines(rosettaPath);

          // Skip the first line (header)
          for (int i = 1; i < lines.Length; i++)
          {
            string line = lines[i];
            if (string.IsNullOrWhiteSpace(line)) continue;

            string[] parts = line.Split('\t');

            // Map parts to the entry, checking length to avoid IndexOutOfRangeException 
            // if trailing tabs are missing on empty columns.
            RosettaEntry entry = new RosettaEntry
            {
              PublishedFileID = parts.Length > 0 ? parts[0].Trim() : "",
              Id = parts.Length > 1 ? parts[1].Trim() : "",
              DirectoryName = parts.Length > 2 ? parts[2].Trim() : "",
              Name = parts.Length > 3 ? parts[3].Trim() : "",
              Title = parts.Length > 4 ? parts[4].Trim() : "",
              Version = parts.Length > 5 ? parts[5].Trim() : "",
              MinimumGameVersion = parts.Length > 6 ? parts[6].Trim() : "",
              RequiredModsId = parts.Length > 7 ? parts[7].Trim() : "",
              RequiredModsMinimumVersion = parts.Length > 8 ? parts[8].Trim() : "",
              OptionalModsId = parts.Length > 9 ? parts[9].Trim() : "",
              OptionalModsMinimumVersion = parts.Length > 10 ? parts[10].Trim() : ""
            };

            _entries.Add(entry);
          }
          _isLoaded = true;
        }
        else
        {
          Debug.LogWarning($"[Calloatti.SyncModsPro] rosetta.txt not found at: '{rosettaPath}'.");
        }
      }
      catch (Exception e)
      {
        Debug.LogWarning($"[Calloatti.SyncModsPro] Exception while loading rosetta.txt: {e.Message}");
      }
    }

    /// <summary>
    /// Returns all parsed entries allowing you to run custom LINQ queries against the Rosetta data.
    /// </summary>
    public static IReadOnlyList<RosettaEntry> GetAllEntries()
    {
      EnsureLoaded();
      return _entries.AsReadOnly();
    }

    /// <summary>
    /// Returns the Steam ID (PublishedFileID) of the mod that is most compatible with the current game version.
    /// If no strictly compatible version is found, falls back to returning the Steam ID of the highest version available.
    /// </summary>
    public static string GetMostCompatibleSteamId(string modId)
    {
      EnsureLoaded();

      var allEntriesForMod = _entries
          .Where(e => e.Id.Equals(modId, StringComparison.OrdinalIgnoreCase))
          .ToList();

      if (allEntriesForMod.Count == 0)
      {
        return null; // Mod doesn't exist in Rosetta
      }

      var currentGameVersion = Timberborn.Versioning.GameVersions.CurrentVersion;

      var compatibleEntries = allEntriesForMod
          .Where(e =>
          {
            if (string.IsNullOrEmpty(e.MinimumGameVersion)) return false;
            try
            {
              var minVer = Timberborn.Versioning.Version.Create(e.MinimumGameVersion);
              return currentGameVersion.IsEqualOrHigherThan(minVer);
            }
            catch
            {
              // Modder provided a malformed MinimumGameVersion string
              return false;
            }
          })
          .ToList();

      // If we found compatible versions, sort those. Otherwise, sort all entries as a fallback.
      List<RosettaEntry> targetList = compatibleEntries.Count > 0 ? compatibleEntries : allEntriesForMod;

      // Sort descending: highest MinimumGameVersion first
      targetList.Sort((a, b) =>
      {
        try
        {
          var verA = Timberborn.Versioning.Version.Create(a.MinimumGameVersion);
          var verB = Timberborn.Versioning.Version.Create(b.MinimumGameVersion);

          bool aGteB = verA.IsEqualOrHigherThan(verB);
          bool bGteA = verB.IsEqualOrHigherThan(verA);

          if (aGteB && bGteA) return 0;
          if (aGteB) return -1; // a > b, so a comes first
          return 1;             // b > a, so b comes first
        }
        catch
        {
          return 0;
        }
      });

      return targetList[0].PublishedFileID;
    }
  }
}