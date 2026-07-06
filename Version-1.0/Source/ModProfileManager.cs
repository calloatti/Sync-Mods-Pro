using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using Timberborn.CoreUI;
using Timberborn.GameSaveRepositorySystem;
using Timberborn.Localization;
using Timberborn.Modding;
using UnityEngine;

namespace Calloatti.SyncModsPro
{
  public static class ModProfileManager
  {
    public static void SaveProfile(SaveReference saveReference, ModRepository modRepository)
    {
      if (saveReference == null || saveReference.SettlementReference == null)
      {
        Debug.LogError("[SyncModsPro] SaveReference context is missing. Aborting profile save.");
        return;
      }

      if (modRepository == null)
      {
        Debug.LogError("[SyncModsPro] ModRepository system context is null. Aborting profile save.");
        return;
      }

      bool success = false;

      try
      {
        // 1. Locate the source Data folder within the mod directory
        string modPath = ModStarter.ModPath;
        if (string.IsNullOrEmpty(modPath))
        {
          Debug.LogError("[SyncModsPro] ModPath is uninitialized. Cannot locate source profile data.");
          return;
        }

        string sourceDataPath = Path.Combine(modPath, "Data");
        if (!Directory.Exists(sourceDataPath))
        {
          Debug.LogError($"[SyncModsPro] Source data directory missing at: {sourceDataPath}");
          return;
        }

        // 1b. Dynamically regenerate save_metadata.json inside the source folder matching exactly how game sorts enabled mods [cite: 1]
        string jsonPath = Path.Combine(sourceDataPath, "save_metadata.json");

        var profileMetadataObject = new
        {
          Timestamp = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"),
          Cycle = 1,
          Day = 1,
          Mods = modRepository.EnabledMods.Select(m => new
          {
            Id = m.Manifest.Id,
            Name = m.Manifest.Name,
            Version = m.Manifest.Version.Full
          }).ToList()
        };

        string generatedJsonString = Newtonsoft.Json.JsonConvert.SerializeObject(profileMetadataObject);
        File.WriteAllText(jsonPath, generatedJsonString);

        // 2. Resolve target path using the save directory from saveReference (Matches ModSyncEngine approach)
        string baseSaveDirectory = saveReference.SettlementReference.SaveDirectory;
        string targetDirectory = Path.Combine(baseSaveDirectory, "Mod Profiles");

        if (!Directory.Exists(targetDirectory))
        {
          Directory.CreateDirectory(targetDirectory);
        }

        // 3. Generate timestamped .timber file
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
        string fileName = $"Profile {timestamp}.timber";
        string destinationZipPath = Path.Combine(targetDirectory, fileName);

        // Guard against duplicate clicks within the same second causing ZipFile IOExceptions
        if (File.Exists(destinationZipPath))
        {
          File.Delete(destinationZipPath);
        }

        Debug.Log($"[SyncModsPro] Archiving mod profile into: {destinationZipPath}");

        // 4. Compress data into target archive location
        ZipFile.CreateFromDirectory(sourceDataPath, destinationZipPath);

        Debug.Log("[SyncModsPro] Mod profile successfully created.");
        success = true;
      }
      catch (Exception ex)
      {
        Debug.LogError($"[SyncModsPro] Failure during profile serialization loop: {ex.Message}");
      }

      // --- POST-SAVE POPUP EXECUTION (Exact replication of ModSyncEngine reflection pattern) ---
      if (ModListBox.Instance != null)
      {
        FieldInfo dialogShowerField = typeof(ModListBox).GetField("_dialogBoxShower", BindingFlags.NonPublic | BindingFlags.Instance);
        FieldInfo locField = typeof(ModListBox).GetField("_loc", BindingFlags.NonPublic | BindingFlags.Instance);

        DialogBoxShower dialogBoxShower = (DialogBoxShower)dialogShowerField?.GetValue(ModListBox.Instance);
        ILoc loc = (ILoc)locField?.GetValue(ModListBox.Instance);

        if (dialogBoxShower != null && loc != null)
        {
          string message = success
            ? loc.T("Calloatti.SyncModsPro.Profile.SaveSuccess")
            : loc.T("Calloatti.SyncModsPro.Profile.SaveFailure");

          var builder = dialogBoxShower.Create()
            .SetMessage(message)
            .SetConfirmButton(() => { }, loc.T("Calloatti.SyncModsPro.Button.OK"));

          builder.Show();
        }
      }
    }
  }
}
