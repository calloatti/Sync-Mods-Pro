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
        string rosettaPath = Path.Combine(PlayerDataFileService.PlayerDataDirectory, "SyncModsPro_Rosetta.txt");

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
          Debug.LogWarning($"[Calloatti.SyncModsPro] rosetta.txt not found at: '{rosettaPath}'.");
        }
      }
      catch (Exception e)
      {
        Debug.LogWarning($"[Calloatti.SyncModsPro] Exception while loading rosetta.txt: {e.Message}");
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
            Debug.LogWarning($"[Calloatti.SyncModsPro] Mod '{modId}': Error parsing workshop_data.json at '{wsPath}': {e.Message}");
          }
        }
      }

      if (!string.IsNullOrEmpty(modId))
      {
        return GetMostCompatibleSteamId(modId);
      }

      return null;
    }

    public string GetSteamId(string missingModId)
    {
      if (string.IsNullOrEmpty(missingModId)) return null;
      return GetMostCompatibleSteamId(missingModId);
    }

    private string GetMostCompatibleSteamId(string modId)
    {
      var allEntriesForMod = _entries
          .Where(e => e.Id.Equals(modId, StringComparison.OrdinalIgnoreCase))
          .ToList();

      if (allEntriesForMod.Count == 0) return null;

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
              return false;
            }
          })
          .ToList();

      List<RosettaEntry> targetList = compatibleEntries.Count > 0 ? compatibleEntries : allEntriesForMod;

      targetList.Sort((a, b) =>
      {
        try
        {
          var verA = Timberborn.Versioning.Version.Create(a.MinimumGameVersion);
          var verB = Timberborn.Versioning.Version.Create(b.MinimumGameVersion);

          bool aGteB = verA.IsEqualOrHigherThan(verB);
          bool bGteA = verB.IsEqualOrHigherThan(verA);

          if (aGteB && bGteA) return 0;
          if (aGteB) return -1;
          return 1;
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