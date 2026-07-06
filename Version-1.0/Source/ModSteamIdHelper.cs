using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using Timberborn.Modding;
using UnityEngine;

namespace Calloatti.SyncModsPro
{
  public static class ModSteamIdHelper
  {
    private static readonly Dictionary<string, string> _customSteamIdMapping = new Dictionary<string, string>();
    private static bool _rosettaLoaded = false;

    private static void LoadRosetta()
    {
      try
      {
        string rosettaPath = Path.Combine(ModStarter.ModPath, "rosetta.txt");

        if (File.Exists(rosettaPath))
        {
          string[] lines = File.ReadAllLines(rosettaPath);
          foreach (string line in lines)
          {
            if (string.IsNullOrWhiteSpace(line)) continue;

            string[] parts = line.Split('\t');
            if (parts.Length >= 2)
            {
              string steamId = parts[0].Trim();
              string modId = parts[1].Trim();

              if (ulong.TryParse(steamId, out _))
              {
                _customSteamIdMapping[modId] = steamId;
              }
            }
          }
        }
        else
        {
          Debug.LogWarning($"[Calloatti.SyncModsPro] rosetta.txt not found at: '{rosettaPath}'.");
        }
      }
      catch (System.Exception e)
      {
        Debug.LogWarning($"[Calloatti.SyncModsPro] Exception while loading rosetta.txt: {e.Message}");
      }
    }

    // --- OVERLOAD 1: Physical Mods (Pass 1) ---
    public static string GetSteamId(Mod mod)
    {
      if (mod == null || mod.Manifest == null) return null;
      string modId = mod.Manifest.Id;

      if (!_rosettaLoaded)
      {
        LoadRosetta();
        _rosettaLoaded = true;
      }

      // 1. Check Rosetta Mapping first (Fastest: in-memory lookup)
      if (!string.IsNullOrEmpty(modId) && _customSteamIdMapping.TryGetValue(modId, out string mappedSteamId))
      {
        return mappedSteamId;
      }

      // 2. If it's a native Workshop mod, trust the OriginName (Folder Name)
      if (!mod.ModDirectory.IsUserMod)
      {
        if (ulong.TryParse(mod.ModDirectory.OriginName, out _))
        {
          return mod.ModDirectory.OriginName;
        }
      }

      string originPath = mod.ModDirectory.OriginPath ?? "";
      string versionPath = mod.ModDirectory.Directory?.FullName ?? "";

      // 3. Fallback to reading the workshop_data.json
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
            JToken token = json.GetValue("ItemId", System.StringComparison.OrdinalIgnoreCase);

            if (token != null && token.Type != JTokenType.Null)
            {
              string parsedId = token.ToString();
              if (!string.IsNullOrEmpty(parsedId) && parsedId != "0")
              {
                return parsedId;
              }
            }
          }
          catch (System.Exception e)
          {
            Debug.LogWarning($"[Calloatti.SyncModsPro] Mod '{modId}': Error parsing workshop_data.json at '{wsPath}': {e.Message}");
          }
        }
      }

      return null;
    }

    // --- OVERLOAD 2: Missing Mods (Pass 2) ---
    public static string GetSteamId(string missingModId)
    {
      if (string.IsNullOrEmpty(missingModId)) return null;

      if (!_rosettaLoaded)
      {
        LoadRosetta();
        _rosettaLoaded = true;
      }

      // Missing mods can only be evaluated against our in-memory Rosetta dictionary
      if (_customSteamIdMapping.TryGetValue(missingModId, out string mappedSteamId))
      {
        return mappedSteamId;
      }

      return null;
    }
  }
}