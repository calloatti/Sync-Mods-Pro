using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using Timberborn.Modding;
using UnityEngine;

namespace Calloatti.SyncModsPro
{
  public static class ModSteamIdHelper
  {
    // --- OVERLOAD 1: Physical Mods (Pass 1) ---
    public static string GetSteamId(Mod mod)
    {
      if (mod == null || mod.Manifest == null) return null;
      string modId = mod.Manifest.Id;

      // If it's a native Workshop mod, trust the OriginName (Folder Name)
      if (!mod.ModDirectory.IsUserMod)
      {
        if (ulong.TryParse(mod.ModDirectory.OriginName, out _))
        {
          return mod.ModDirectory.OriginName;
        }
      }

      string originPath = mod.ModDirectory.OriginPath ?? "";
      string versionPath = mod.ModDirectory.Path ?? "";

      // Fallback to reading the workshop_data.json
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

      // Check Rosetta using the new compatibility logic
      if (!string.IsNullOrEmpty(modId))
      {
        return RosettaDatabase.GetMostCompatibleSteamId(modId);
      }

      return null;
    }

    // --- OVERLOAD 2: Missing Mods (Pass 2) ---
    public static string GetSteamId(string missingModId)
    {
      if (string.IsNullOrEmpty(missingModId)) return null;

      // Missing mods can only be evaluated against our in-memory Rosetta database
      return RosettaDatabase.GetMostCompatibleSteamId(missingModId);
    }
  }
}