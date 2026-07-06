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

    private void HandleSaveProfileClick()
    {
      Debug.Log("[SyncModsPro] Save Profile clicked.");
      ModProfileManager.SaveProfile(_currentSaveReference, _modRepository);
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

      ApplyFilters();

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

    private void HandleRestartLoadClick(List<RowData> rowsData)
    {
      ApplyMatrixAndRestart(rowsData, _currentSaveReference);
    }

    // CHANGED: Replaced DialogBox reference with native PanelStack popping logic
    private void HandleLoadGameClick(Action continueCallback)
    {
      if (continueCallback != null)
      {
        _panelStack.Pop(this);
        continueCallback.Invoke();
      }
    }

    private void HandleSaveLabelClick()
    {
      if (_currentSaveReference != null && _currentSaveReference.SettlementReference != null)
      {
        Debug.Log($"[SyncModsPro] Save label clicked: {_currentSaveReference.SettlementReference.SettlementName} - {_currentSaveReference.SaveName}");
        TouchSaveFile(_currentSaveReference);
      }
    }
  }
}