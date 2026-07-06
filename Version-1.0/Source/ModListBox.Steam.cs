using System;
using System.Collections.Generic;
using Steamworks;
using Timberborn.CoreUI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Calloatti.SyncModsPro
{
  public partial class ModListBox
  {
    private const string SteamSubscribePromptLocKey = "Calloatti.SyncModsPro.Steam.SubscribePrompt";
    private const string SteamUnsubscribePromptLocKey = "Calloatti.SyncModsPro.Steam.UnsubscribePrompt";
    private const string SteamUnknownModLocKey = "Calloatti.SyncModsPro.Steam.UnknownMod";
    private const string SteamDialogTitleLocKey = "Calloatti.SyncModsPro.Steam.DialogTitle";
    private const string SteamSubscribeIoFailureLocKey = "Calloatti.SyncModsPro.Steam.SubscribeIoFailure";
    private const string SteamSubscribeSuccessLocKey = "Calloatti.SyncModsPro.Steam.SubscribeSuccess";
    private const string SteamSubscribeRejectedLocKey = "Calloatti.SyncModsPro.Steam.SubscribeRejected";
    private const string SteamUnsubscribeIoFailureLocKey = "Calloatti.SyncModsPro.Steam.UnsubscribeIoFailure";
    private const string SteamUnsubscribeSuccessLocKey = "Calloatti.SyncModsPro.Steam.UnsubscribeSuccess";
    private const string SteamUnsubscribeRejectedLocKey = "Calloatti.SyncModsPro.Steam.UnsubscribeRejected";

    /// <summary>
    /// An internal tracking structure representing a single isolated network call in flight.
    /// </summary>
    private class TrackedWorkshopCall
    {
      public string ModDisplayName;
      public string SteamIdString;
      public bool IsUnsubscribe;

      // Separate references ensure type safety for Steam's native call result layout structures
      public CallResult<RemoteStorageSubscribePublishedFileResult_t> SubscribeListener;
      public CallResult<RemoteStorageUnsubscribePublishedFileResult_t> UnsubscribeListener;
    }

    // Our central tracking array/list holding all active requests currently communicating with Steam
    private readonly List<TrackedWorkshopCall> _inFlightWorkshopCalls = new List<TrackedWorkshopCall>();

    /// <summary>
    /// Entry point for the cloud icon click. Queries current Steam subscription state,
    /// prompts the user with a confirmation dialog box, and dispatches the action.
    /// </summary>
    public void HandleCloudIconClick(string steamIdStr, string modDisplayName)
    {
      if (string.IsNullOrEmpty(steamIdStr) || !ulong.TryParse(steamIdStr, out ulong parsedSteamId))
      {
        UnityEngine.Debug.LogError($"[SyncModsPro] Cannot process cloud action. Invalid Steam ID: '{steamIdStr}'");
        return;
      }

      PublishedFileId_t fileId = new PublishedFileId_t(parsedSteamId);

      // 1. Query Steam directly for the live status bitwise mask
      uint itemStateFlags = SteamUGC.GetItemState(fileId);
      bool isSubscribed = (itemStateFlags & (uint)EItemState.k_EItemStateSubscribed) != 0; //

      string promptLocKey = isSubscribed ? SteamUnsubscribePromptLocKey : SteamSubscribePromptLocKey;
      Label msgLabel = new Label(_loc.T(promptLocKey, modDisplayName));
      msgLabel.AddToClassList("text--default");
      msgLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
      msgLabel.style.whiteSpace = WhiteSpace.Normal;
      msgLabel.style.marginTop = 10;
      msgLabel.style.marginBottom = 10;

      // 3. Spawn the Confirmation Dialog Box using injected game instances
      _dialogBoxShower.Create()
        .AddContent(msgLabel)
        .SetConfirmButton(() =>
        {
          ExecuteSubscriptionChange(fileId, steamIdStr, modDisplayName, unsubscribe: isSubscribed);
        }, _loc.T("Calloatti.SyncModsPro.Button.Confirm"))
        .SetCancelButton(() => { }, _loc.T("Calloatti.SyncModsPro.Button.Cancel"))
        .Show();
    }

    private void ExecuteSubscriptionChange(PublishedFileId_t fileId, string steamIdStr, string modDisplayName, bool unsubscribe)
    {
      // Create our tracked element for this specific call context
      TrackedWorkshopCall callRecord = new TrackedWorkshopCall
      {
        ModDisplayName = modDisplayName,
        SteamIdString = steamIdStr,
        IsUnsubscribe = unsubscribe
      };

      if (unsubscribe)
      {
        SteamAPICall_t apiCall = SteamUGC.UnsubscribeItem(fileId);

        // Bind the call result handler directly to this record element
        callRecord.UnsubscribeListener = CallResult<RemoteStorageUnsubscribePublishedFileResult_t>.Create(OnUnsubscribeResult);
        callRecord.UnsubscribeListener.Set(apiCall);

        // Add to our central tracking list
        _inFlightWorkshopCalls.Add(callRecord);
        UnityEngine.Debug.Log($"[SyncModsPro] Array Entry Added: Tracking Unsubscribe call for '{modDisplayName}' ({steamIdStr})");
      }
      else
      {
        SteamAPICall_t apiCall = SteamUGC.SubscribeItem(fileId);

        // Bind the call result handler directly to this record element
        callRecord.SubscribeListener = CallResult<RemoteStorageSubscribePublishedFileResult_t>.Create(OnSubscribeResult);
        callRecord.SubscribeListener.Set(apiCall);

        // Add to our central tracking list
        _inFlightWorkshopCalls.Add(callRecord);
        UnityEngine.Debug.Log($"[SyncModsPro] Array Entry Added: Tracking Subscribe call for '{modDisplayName}' ({steamIdStr})");
      }
    }

    /// <summary>
    /// Commands the Steam background service API to initiate a high-priority download
    /// sequence for a given file asset, bypassing standard network scheduling rules.
    /// </summary>
    public void DownloadNow(PublishedFileId_t fileId, string steamIdStr)
    {
      // Setting bHighPriority to true pushes the download to the front of Steam's line
      bool downloadTriggered = SteamUGC.DownloadItem(fileId, bHighPriority: true);
      UnityEngine.Debug.Log($"[SyncModsPro] Direct Download call processed for item '{steamIdStr}' (Dispatched: {downloadTriggered})");
    }

    private void OnSubscribeResult(RemoteStorageSubscribePublishedFileResult_t callback, bool ioFailure)
    {
      // Find our matching tracking element inside the array using the returned Steam ID
      TrackedWorkshopCall matchingCall = _inFlightWorkshopCalls.Find(c =>
        !c.IsUnsubscribe && c.SteamIdString == callback.m_nPublishedFileId.m_PublishedFileId.ToString()
      );

      // Fallback fallback safeguards if lookups encounter system discrepancies
      string displayName = matchingCall != null ? matchingCall.ModDisplayName : _loc.T(SteamUnknownModLocKey);
      string steamIdStr = matchingCall != null ? matchingCall.SteamIdString : callback.m_nPublishedFileId.m_PublishedFileId.ToString();

      // Clean up the element from our tracking array as the call is complete
      if (matchingCall != null) _inFlightWorkshopCalls.Remove(matchingCall);

      string title = _loc.T(SteamDialogTitleLocKey);
      string message;

      if (ioFailure)
      {
        message = _loc.T(SteamSubscribeIoFailureLocKey, displayName, steamIdStr);
        UnityEngine.Debug.LogError($"[SyncModsPro] IO Failure received during subscription to {steamIdStr}");
      }
      else if (callback.m_eResult == EResult.k_EResultOK)
      {
        message = _loc.T(SteamSubscribeSuccessLocKey, displayName);
        UnityEngine.Debug.Log($"[SyncModsPro] Steam confirm: Subscribed successfully to {steamIdStr}");

        // Automatically fire off our download wrapper on success
        DownloadNow(callback.m_nPublishedFileId, steamIdStr);
      }
      else
      {
        string errorCode = $"{callback.m_eResult} ({(int)callback.m_eResult})";
        message = _loc.T(SteamSubscribeRejectedLocKey, displayName, errorCode);
        UnityEngine.Debug.LogWarning($"[SyncModsPro] Steam subscription failed with result: {callback.m_eResult}");
      }

      ShowFeedbackDialog(title, message);
    }

    private void OnUnsubscribeResult(RemoteStorageUnsubscribePublishedFileResult_t callback, bool ioFailure)
    {
      // Find our matching tracking element inside the array using the returned Steam ID
      TrackedWorkshopCall matchingCall = _inFlightWorkshopCalls.Find(c =>
        c.IsUnsubscribe && c.SteamIdString == callback.m_nPublishedFileId.m_PublishedFileId.ToString()
      );

      string displayName = matchingCall != null ? matchingCall.ModDisplayName : _loc.T(SteamUnknownModLocKey);
      string steamIdStr = matchingCall != null ? matchingCall.SteamIdString : callback.m_nPublishedFileId.m_PublishedFileId.ToString();

      // Clean up the element from our tracking array as the call is complete
      if (matchingCall != null) _inFlightWorkshopCalls.Remove(matchingCall);

      string title = _loc.T(SteamDialogTitleLocKey);
      string message;

      if (ioFailure)
      {
        message = _loc.T(SteamUnsubscribeIoFailureLocKey, displayName, steamIdStr);
        UnityEngine.Debug.LogError($"[SyncModsPro] IO Failure received during unsubscription from {steamIdStr}");
      }
      else if (callback.m_eResult == EResult.k_EResultOK)
      {
        message = _loc.T(SteamUnsubscribeSuccessLocKey, displayName);
        UnityEngine.Debug.Log($"[SyncModsPro] Steam confirm: Unsubscribed successfully from {steamIdStr}");
      }
      else
      {
        string errorCode = $"{callback.m_eResult} ({(int)callback.m_eResult})";
        message = _loc.T(SteamUnsubscribeRejectedLocKey, displayName, errorCode);
        UnityEngine.Debug.LogWarning($"[SyncModsPro] Steam unsubscription failed with result: {callback.m_eResult}");
      }

      ShowFeedbackDialog(title, message);
    }

    private void ShowFeedbackDialog(string title, string message)
    {
      VisualElement root = new VisualElement();
      root.style.width = 400;

      Label msgLabel = new Label(message);
      msgLabel.AddToClassList("text--default");
      msgLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
      msgLabel.style.whiteSpace = WhiteSpace.Normal;
      msgLabel.style.marginTop = 15;
      msgLabel.style.marginBottom = 15;
      root.Add(msgLabel);

      _dialogBoxShower.Create()
        .AddContent(root)
        .SetConfirmButton(() => { }, _loc.T("Calloatti.SyncModsPro.Button.Confirm"))
        .Show();
    }
  }
}
