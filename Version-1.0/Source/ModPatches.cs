using HarmonyLib;
using System;
using System.Collections.Generic;
using Timberborn.GameSaveRepositorySystem;
using Timberborn.GameSaveRepositorySystemUI;
using Timberborn.Modding;
using Timberborn.SaveMetadataSystem;
using UnityEngine;

namespace Calloatti.SyncModsPro
{
  [HarmonyPatch]
  public static class ModPatches
  {
    // Changed patch target to ValidateSave so we can capture the SaveReference and the continueCallback
    [HarmonyPatch(typeof(SaveModsValidator), "ValidateSave")]
    [HarmonyPrefix]
    public static bool ValidateSave_Prefix(SaveModsValidator __instance, SaveReference saveReference, Action continueCallback)
    {
      if (ModListBox.Instance != null)
      {
        GameSaveDeserializer deserializer = __instance._gameSaveDeserializer;
        SaveMetadataSerializer serializer = __instance._saveMetadataSerializer;

        SaveMetadata metadata = deserializer.ReadFromSaveFile(saveReference, serializer);

        // Pass the callback directly to the panel so it can resume the native load sequence
        ModListBox.Instance.Open(metadata, saveReference, continueCallback);

        // Return false to block vanilla ONLY when our panel successfully triggers
        return false;
      }

      Debug.LogWarning("[SyncModsPro] ModListBox is missing! Falling back to vanilla SaveModsValidator.");
      return true;
    }

    // --- SHOW SAVED MODS INTERCEPT ---
    [HarmonyPatch(typeof(GameSaveModBox), "Show")]
    [HarmonyPrefix]
    public static bool GameSaveModBox_Show_Prefix(GameSaveModBox __instance, GameSaveItem gameSaveItem)
    {
      if (ModListBox.Instance != null)
      {
        GameSaveDeserializer deserializer = __instance._gameSaveDeserializer;
        SaveMetadataSerializer serializer = __instance._saveMetadataSerializer;

        SaveMetadata metadata = deserializer.ReadFromSaveFile(gameSaveItem.SaveReference, serializer);

        // Pass null for the callback since we are just looking at the mods, not loading the game
        ModListBox.Instance.Open(metadata, gameSaveItem.SaveReference, null);

        // Return false to block vanilla ONLY when our panel successfully triggers
        return false;
      }

      Debug.LogWarning("[SyncModsPro] ModListBox is missing! Falling back to vanilla GameSaveModBox.");
      return true;
    }
  }
}