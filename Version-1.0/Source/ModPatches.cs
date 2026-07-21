using HarmonyLib;
using System;
using Timberborn.GameSaveRepositorySystem;
using Timberborn.GameSaveRepositorySystemUI;
using Timberborn.Modding;
using Timberborn.SaveMetadataSystem;
using UnityEngine;

namespace Calloatti.SyncModsPro
{
  // ====================================================================
  // PATCH 1: Validate Save (Active)
  // ====================================================================
  [HarmonyPatch(typeof(SaveModsValidator), "ValidateSave")]
  public static class ValidateSavePatch
  {
    [HarmonyPrefix]
    public static bool Prefix(SaveModsValidator __instance, SaveReference saveReference, Action continueCallback)
    {
      if (ModListBox.Instance != null)
      {
        GameSaveDeserializer deserializer = __instance._gameSaveDeserializer;
        SaveMetadataSerializer serializer = __instance._saveMetadataSerializer;

        SaveMetadata metadata = deserializer.ReadFromSaveFile(saveReference, serializer);

        if (ModStarter.Config.GetBool("OnlyOpenOnMismatch"))
        {
          if (ModsAreCompatible(metadata, __instance._modRepository))
          {
            continueCallback();
            return false;
          }
        }

        ModListBox.Instance.Open(metadata, saveReference, continueCallback);
        return false;
      }

      Debug.LogWarning("[SyncModsPro] ModListBox is missing! Falling back to vanilla SaveModsValidator.");
      return true;
    }

    private static bool ModsAreCompatible(SaveMetadata metadata, ModRepository modRepository)
    {
      if (metadata?.Mods != null)
      {
        foreach (var modRef in metadata.Mods)
        {
          if (modRepository.ModIsNotEnabled(modRef.Id) ||
              modRepository.ModIsOnDifferentVersion(modRef.Id, modRef.Version))
            return false;
        }
      }
      return true;
    }
  }

}