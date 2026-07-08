using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Timberborn.CoreUI;
using Timberborn.GameSaveRepositorySystem;
using Timberborn.Localization;
using Timberborn.Modding;
using UnityEngine;
using UnityEngine.UIElements;

namespace Calloatti.SyncModsPro
{
  public static class ModProfileManager
  {
    public static void SaveProfile(SaveReference saveReference, ModRepository modRepository, DialogBoxShower dialogBoxShower, ILoc loc)
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

      if (dialogBoxShower == null || loc == null)
      {
        Debug.LogError("[SyncModsPro] DialogBoxShower or ILoc is null. Aborting profile save.");
        return;
      }

      string defaultName = $"Profile {DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss")}";

      VisualElement root = new VisualElement();
      root.style.width = 400;

      Label promptLabel = new Label("Save Profile Name:");
      promptLabel.AddToClassList("text--default");
      promptLabel.style.marginBottom = 5;
      root.Add(promptLabel);

      NineSliceTextField nameInput = new NineSliceTextField();
      nameInput.AddToClassList("text-field");
      nameInput.AddToClassList("box__input");
      nameInput.value = defaultName;
      root.Add(nameInput);

      Toggle emptyModsToggle = new Toggle();
      emptyModsToggle.AddToClassList("game-toggle");
      emptyModsToggle.text = "Empty mods list";
      emptyModsToggle.SetValueWithoutNotify(false);
      emptyModsToggle.style.marginTop = 15;
      emptyModsToggle.style.marginBottom = 5;
      root.Add(emptyModsToggle);

      dialogBoxShower.Create()
        .AddContent(root)
        .SetConfirmButton(() =>
        {
          string finalName = string.IsNullOrWhiteSpace(nameInput.value) ? defaultName : nameInput.value;
          ExecuteSave(saveReference, modRepository, dialogBoxShower, loc, finalName, emptyModsToggle.value);
        }, loc.T("Calloatti.SyncModsPro.Button.OK"))
        .SetCancelButton(() => { }, loc.T("Calloatti.SyncModsPro.Button.Cancel"))
        .Show();
    }

    private static void ExecuteSave(SaveReference saveReference, ModRepository modRepository, DialogBoxShower dialogBoxShower, ILoc loc, string customFileName, bool emptyModsList)
    {
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

        // 1b. Dynamically regenerate save_metadata.json inside the source folder matching exactly how game sorts enabled mods
        string jsonPath = Path.Combine(sourceDataPath, "save_metadata.json");

        object generatedMods = emptyModsList
          ? (object)new object[0]
          : (object)modRepository.EnabledMods.Select(m => new
          {
            Id = m.Manifest.Id,
            Name = m.Manifest.Name,
            Version = m.Manifest.Version.Full
          }).ToList();

        var profileMetadataObject = new
        {
          Timestamp = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"),
          Cycle = 1,
          Day = 1,
          Mods = generatedMods
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
        string fileName = $"{customFileName}.timber";
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

      // --- POST-SAVE POPUP EXECUTION ---
      string message = success
        ? loc.T("Calloatti.SyncModsPro.Profile.SaveSuccess")
        : loc.T("Calloatti.SyncModsPro.Profile.SaveFailure");

      dialogBoxShower.Create()
        .SetMessage(message)
        .SetConfirmButton(() => { }, loc.T("Calloatti.SyncModsPro.Button.OK"))
        .Show();
    }
  }
}