using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using Timberborn.Modding;
using Timberborn.PlayerDataSystem;
using Timberborn.SingletonSystem;
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

  public class WorkshopIdManager : ILoadableSingleton
  {
    private readonly List<RosettaEntry> _entries = new List<RosettaEntry>();

    public void Load()
    {
      _entries.Clear();

      try
      {
        string defaultRosettaPath = Path.Combine(ModStarter.ModPath, "rosetta.txt");
        string rosettaPath = Path.Combine(PlayerDataFileService.PlayerDataDirectory, "SyncModsPro.Rosetta.txt");

        if (!File.Exists(rosettaPath))
        {
          if (File.Exists(defaultRosettaPath))
          {
            File.Copy(defaultRosettaPath, rosettaPath);
          }
        }

        if (File.Exists(rosettaPath))
        {
          string[] lines = File.ReadAllLines(rosettaPath);

          for (int i = 1; i < lines.Length; i++)
          {
            string line = lines[i];
            if (string.IsNullOrWhiteSpace(line)) continue;

            string[] parts = line.Split('\t');

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
        }
        else
        {
          Debug.LogWarning($"[SyncModsPro] rosetta.txt not found at: '{rosettaPath}'.");
        }
      }
      catch (Exception e)
      {
        Debug.LogWarning($"[SyncModsPro] Exception while loading rosetta.txt: {e.Message}");
      }
    }

    public IReadOnlyList<RosettaEntry> GetAllEntries()
    {
      return _entries.AsReadOnly();
    }

    public string GetSteamId(Mod mod)
    {
      if (mod == null || mod.Manifest == null) return null;
      string modId = mod.Manifest.Id;

      // 1. Check if the folder name itself is the Steam ID
      if (!mod.ModDirectory.IsUserMod)
      {
        if (ulong.TryParse(mod.ModDirectory.OriginName, out _))
        {
          return mod.ModDirectory.OriginName;
        }
      }

      string originPath = mod.ModDirectory.OriginPath ?? "";
      string versionPath = mod.ModDirectory.Path ?? "";

      List<string> potentialWorkshopPaths = new List<string>();
      if (!string.IsNullOrEmpty(originPath)) potentialWorkshopPaths.Add(Path.Combine(originPath, "workshop_data.json"));
      if (!string.IsNullOrEmpty(versionPath)) potentialWorkshopPaths.Add(Path.Combine(versionPath, "workshop_data.json"));

      // 2. Check local Steam workshop_data.json files
      foreach (string wsPath in potentialWorkshopPaths)
      {
        if (File.Exists(wsPath))
        {
          try
          {
            string content = File.ReadAllText(wsPath);
            JObject json = JObject.Parse(content);
            JToken token = json.GetValue("ItemId", StringComparison.OrdinalIgnoreCase);

            if (token != null && token.Type != JTokenType.Null)
            {
              string parsedId = token.ToString();
              if (!string.IsNullOrEmpty(parsedId) && parsedId != "0")
              {
                return parsedId;
              }
            }
          }
          catch (Exception e)
          {
            Debug.LogWarning($"[SyncModsPro] Mod '{modId}': Error parsing workshop_data.json at '{wsPath}': {e.Message}");
          }
        }
      }

      // 3. Fallback to Rosetta if all local physical checks fail
      if (!string.IsNullOrEmpty(modId))
      {
        return GetSteamIdFromRosetta(modId);
      }

      return null;
    }

    public string GetSteamId(string missingModId)
    {
      // Missing mods have no physical files, so they immediately use Rosetta
      if (string.IsNullOrEmpty(missingModId)) return null;
      return GetSteamIdFromRosetta(missingModId);
    }

    public bool IsDuplicate(string modId)
    {
      if (string.IsNullOrEmpty(modId)) return false;

      var uniqueIds = _entries
          .Where(e => e.Id.Equals(modId, StringComparison.OrdinalIgnoreCase))
          .Select(e => e.PublishedFileID)
          .Distinct()
          .ToList();

      return uniqueIds.Count > 1;
    }

    private string GetSteamIdFromRosetta(string modId)
    {
      if (IsDuplicate(modId))
      {
        Debug.LogWarning($"[SyncModsPro] Warning: Auto sub/unsub disabled for '{modId}' due to multiple SteamIDs existing for this ModId.");
        return null;
      }

      var entry = _entries.FirstOrDefault(e => e.Id.Equals(modId, StringComparison.OrdinalIgnoreCase));
      return entry?.PublishedFileID;
    }
  }
}