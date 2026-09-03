using System;
using System.Collections.Generic;
using Timberborn.CoreUI;
using Timberborn.GameSaveRepositorySystem;
using Timberborn.InputSystem;
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

    private void HandleRestartClick(ClickEvent evt)
    {
      if (evt.shiftKey)
      {
        Debug.Log("[SyncModsPro] Restart clicked (skip mod manager).");
        GameRestarter.RequestSkipModManagerRestart();
      }
      else
      {
        Debug.Log("[SyncModsPro] Restart clicked.");
        GameRestarter.RequestStandardRestart();
      }
    }

    private void HandleRestartLoadClick()
    {
      Debug.Log("[SyncModsPro] Restart & Load clicked.");
      GameRestarter.RequestRestartAndLoad(_currentSaveReference);
    }

    private void HandleSaveLabelClick()
    {
      if (_currentSaveReference != null && _currentSaveReference.SettlementReference != null)
      {
        Debug.Log($"[SyncModsPro] Save label clicked: {_currentSaveReference.SettlementReference.SettlementName} - {_currentSaveReference.SaveName}");
        SaveFileUtility.TouchSaveFile(_currentSaveReference);
      }
    }

    private void OnKeyPressed(object sender, KeyPressedEvent e)
    {
      // Timberborn passes displayName.ToUpper(), we normalize it to lower
      string key = e.Key.ToLowerInvariant();

      // We only want to process standard single character key presses (letters/numbers)
      if (key.Length != 1) return;

      char searchChar = key[0];

      foreach (var rowUI in _orderedRows)
      {
        if (rowUI.Root.style.display != DisplayStyle.Flex) continue;

        // Dynamically select the correct property based on the active sort column
        string targetString = rowUI.Data.ModName;
        if (_currentSortColumn == "Id") targetString = rowUI.Data.ModId;
        else if (_currentSortColumn == "VersionFolder") targetString = rowUI.Data.VersionFolder;

        // Check if the first letter matches the pressed key
        if (!string.IsNullOrEmpty(targetString) && char.ToLowerInvariant(targetString[0]) == searchChar)
        {
          ScrollToRowAtTop(rowUI.Root);
          break; // Stop at the very first match we find
        }
      }
    }

    private void ScrollToRowAtTop(VisualElement row)
    {
      if (row.layout.height == 0) return; // Layout hasn't resolved yet

      float targetY = row.layout.y;
      float viewportHeight = _scrollView.layout.height;

      // Set the scroll offset directly to the row's Y position to place it at the top
      float scrollY = targetY;

      // Clamp to ensure we don't scroll past the boundaries if the row is near the very bottom
      float maxScroll = UnityEngine.Mathf.Max(0, _scrollView.contentContainer.layout.height - viewportHeight);
      scrollY = UnityEngine.Mathf.Clamp(scrollY, 0, maxScroll);

      _scrollView.scrollOffset = new Vector2(0, scrollY);
    }
  }
}