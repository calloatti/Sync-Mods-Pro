using System;
using System.Collections.Generic;
using Timberborn.CoreUI;
using Timberborn.GameSaveRepositorySystem;
using UnityEngine;
using UnityEngine.UIElements;

namespace Calloatti.SyncModsPro
{
  public partial class ModListBox
  {
    private bool _isStrictOn = false;

    private void HandleSaveProfileClick(SaveReference saveReference)
    {
      Debug.Log("[SyncModsPro] Save Profile clicked.");
      ModProfileManager.SaveProfile(saveReference, _modRepository);
    }

    private void HandleStrictFlipClick(Button strictButton)
    {
      _isStrictOn = !_isStrictOn;
      string locKey = _isStrictOn ? "Calloatti.SyncModsPro.Button.StrictFlipOn" : "Calloatti.SyncModsPro.Button.StrictFlipOff";
      strictButton.text = _loc.T(locKey);

      foreach (var duplicateList in _duplicateGroups.Values)
      {
        foreach (var rowUI in duplicateList)
        {
          var data = rowUI.Data;

          if (data.CurrentState == ModState.Enabled && data.SavedState == ModState.Disabled && data.DupStatus != 0)
          {
            data.TargetState = _isStrictOn ? ModState.Disabled : ModState.Enabled;
            data.UpdateStatus(); // Sync Enum logic
            RepaintRow(rowUI);
          }
        }
      }

      Debug.Log($"[SyncModsPro] Strict mode toggled: {_isStrictOn}");
    }

    private void HandleSyncClick(List<RowData> rowsData)
    {
      ExecuteAutoSync(rowsData);
    }

    private void HandleRestartClick()
    {
      Debug.Log("[SyncModsPro] Restart clicked.");
      ModExecutionController.RequestStandardRestart();
    }

    private void HandleRestartLoadClick(List<RowData> rowsData, SaveReference saveReference)
    {
      ApplyMatrixAndRestart(rowsData, saveReference);
    }

    private void HandleLoadGameClick(DialogBox dialogBox, Action continueCallback)
    {
      if (continueCallback != null)
      {
        dialogBox.OnUICancelled();
        continueCallback.Invoke();
      }
    }
  }
}