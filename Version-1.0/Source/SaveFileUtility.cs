using System;
using System.IO;
using Timberborn.GameSaveRepositorySystem;
using UnityEngine;

namespace Calloatti.SyncModsPro
{
  public static class SaveFileUtility
  {
    public static void TouchSaveFile(SaveReference saveReference)
    {
      if (saveReference == null || saveReference.SettlementReference == null) return;

      try
      {
        string saveDir = saveReference.SettlementReference.SaveDirectory;
        string settlementName = saveReference.SettlementReference.SettlementName;
        string saveName = $"{saveReference.SaveName}.timber";
        string filePath = Path.Combine(saveDir, settlementName, saveName);

        if (File.Exists(filePath))
        {
          File.SetLastWriteTime(filePath, DateTime.Now);
        }
      }
      catch (Exception ex)
      {
        Debug.LogWarning($"[SyncModsPro] Failed to update save file timestamp: {ex.Message}");
      }
    }
  }
}