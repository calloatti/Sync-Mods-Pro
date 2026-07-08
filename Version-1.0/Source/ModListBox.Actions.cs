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
    private bool _isStrictOn = true;

    private void HandleSaveProfileClick()
    {
      Debug.Log("[SyncModsPro] Save Profile clicked. Delegating to ModProfileManager.");
      ModProfileManager.SaveProfile(_currentSaveReference, _modRepository, _dialogBoxShower, _loc);
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

          // TargetState alone handles the user plan without corrupting status facts
          if (data.CurrentState == ModState.Enabled && data.SavedState == ModState.Disabled && data.DupStatus != 0)
          {
            data.TargetState = _isStrictOn ? ModState.Disabled : ModState.Enabled;
            RepaintRow(rowUI);
          }
        }
      }

      ApplyFilters();
      UpdateEnabledStats();

      Debug.Log($"[SyncModsPro] Strict mode toggled: {_isStrictOn}");
    }

    private void HandleSyncClick(List<ModRecord> modTable)
    {
      Debug.Log("[SyncModsPro] Sync clicked. Applying list changes to PlayerPrefs...");

      SaveFileUtility.TouchSaveFile(_currentSaveReference);

      bool success = ModSyncEngine.ApplyModChanges(modTable);

      if (_dialogBoxShower != null && _loc != null)
      {
        if (success)
        {
          var builder = _dialogBoxShower.Create()
            .SetMessage(_loc.T("Calloatti.SyncModsPro.SyncDialogboxText"))
            .SetConfirmButton(() => { }, _loc.T("Calloatti.SyncModsPro.Button.OK"));
          builder.Show();
        }
        else
        {
          var builder = _dialogBoxShower.Create()
            .SetMessage("An error occurred during synchronization. Check logs for details.")
            .SetConfirmButton(() => { }, _loc.T("Calloatti.SyncModsPro.Button.OK"));
          builder.Show();
        }
      }
    }

    private void HandleRestartClick()
    {
      Debug.Log("[SyncModsPro] Restart clicked.");
      ModRestarter.RequestStandardRestart();
    }

    private void HandleRestartLoadClick()
    {
      Debug.Log("[SyncModsPro] Restart & Load clicked.");
      ModRestarter.RequestRestartAndLoad(_currentSaveReference);
    }

    private void HandleSaveLabelClick()
    {
      if (_currentSaveReference != null && _currentSaveReference.SettlementReference != null)
      {
        Debug.Log($"[SyncModsPro] Save label clicked: {_currentSaveReference.SettlementReference.SettlementName} - {_currentSaveReference.SaveName}");
        SaveFileUtility.TouchSaveFile(_currentSaveReference);
      }
    }
  }
}