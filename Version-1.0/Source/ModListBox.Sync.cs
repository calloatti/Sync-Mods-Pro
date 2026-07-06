using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Timberborn.CoreUI;
using Timberborn.GameSaveRepositorySystem;
using Timberborn.Modding;
using Timberborn.SaveMetadataSystem;
using UnityEngine;

namespace Calloatti.SyncModsPro
{
  public partial class ModListBox
  {
    private static readonly HashSet<string> ObsoleteModIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
      "calloatti.syncmods",
      "Calloatti.LoadGameModValidator"
    };

    private void ExecuteAutoSync(List<RowData> rowsData)
    {
      // TODO: We will write the logic to snap TargetState to SavedState here next!
      Debug.Log("[SyncModsPro] AutoSync clicked.");
    }

    private void ApplyMatrixAndRestart(List<RowData> rowsData, SaveReference saveReference)
    {
      // TODO: We will write the logic to save TargetState and targetPriority to PlayerPrefs here next!
      Debug.Log("[SyncModsPro] Apply & Restart clicked.");
    }

    private void TouchSaveFile(SaveReference saveReference)
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