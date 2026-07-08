using HarmonyLib;
using System;
using Timberborn.GameSaveRepositorySystem;
using Timberborn.GameSaveRepositorySystemUI;
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

        // Pass the callback directly to the panel so it can resume the native load sequence
        ModListBox.Instance.Open(metadata, saveReference, continueCallback);

        // Return false to block vanilla ONLY when our panel successfully triggers
        return false;
      }

      Debug.LogWarning("[SyncModsPro] ModListBox is missing! Falling back to vanilla SaveModsValidator.");
      return true;
    }
  }

  // ====================================================================
  // PATCH 2: Show Saved Mods (Excluded via Prepare)
  // ====================================================================
  [HarmonyPatch(typeof(GameSaveModBox), "Show")]
  public static class GameSaveModBoxPatch
  {
    // Harmony will run this and skip ONLY this class
    static bool Prepare()
    {
      return false;
    }

    [HarmonyPrefix]
    public static bool Prefix(GameSaveModBox __instance, GameSaveItem gameSaveItem)
    {
      if (ModListBox.Instance != null)
      {
        GameSaveDeserializer deserializer = __instance._gameSaveDeserializer;
        SaveMetadataSerializer serializer = __instance._saveMetadataSerializer;

        SaveMetadata metadata = deserializer.ReadFromSaveFile(gameSaveItem.SaveReference, serializer);

        // Pass null for the callback since we are just looking at the mods, not loading the game
        ModListBox.Instance.Open(metadata, gameSaveItem.SaveReference, null);

        return false;
      }

      Debug.LogWarning("[SyncModsPro] ModListBox is missing! Falling back to vanilla GameSaveModBox.");
      return true;
    }
  }
}