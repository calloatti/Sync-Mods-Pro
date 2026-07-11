using System;
using System.Collections.Generic;
using System.IO;
using Steamworks;
using Timberborn.CoreUI;
using Timberborn.Localization;
using Timberborn.PlayerDataSystem;
using UnityEngine;
using UnityEngine.UIElements;

namespace Calloatti.SyncModsPro
{
  public class WorkshopLogEntry
  {
    public string Timestamp { get; set; }
    public string Action { get; set; }
    public string SteamId { get; set; }
    public string ModName { get; set; }
    public string Result { get; set; }
    public bool IsSubscribed { get; set; }
  }

  public class WorkshopManager
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

    private readonly ILoc _loc;
    private readonly DialogBoxShower _dialogBoxShower;
    private readonly string _logFilePath;

    // Caches the known state of items modified during this session to bypass Steam API lag
    private readonly Dictionary<string, bool> _recentStateOverrides = new Dictionary<string, bool>();

    // Fired when Steam confirms an action and writes to the log
    public event Action OnHistoryUpdated;

    public WorkshopManager(ILoc loc, DialogBoxShower dialogBoxShower)
    {
      _loc = loc;
      _dialogBoxShower = dialogBoxShower;

      // Define both paths to handle migration
      string oldLogFilePath = Path.Combine(PlayerDataFileService.PlayerDataDirectory, "SyncModsPro_Workshop.log");
      _logFilePath = Path.Combine(PlayerDataFileService.PlayerDataDirectory, "SyncModsPro.Workshop.log");

      // Backward compatibility check to seamlessly rename the file if it exists on disk
      try
      {
        if (File.Exists(oldLogFilePath) && !File.Exists(_logFilePath))
        {
          File.Move(oldLogFilePath, _logFilePath);
          Debug.Log($"[SyncModsPro] Successfully migrated workshop history file name to: '{_logFilePath}'");
        }
      }
      catch (Exception ex)
      {
        Debug.LogWarning($"[SyncModsPro] Exception during workshop log migration: {ex.Message}");
      }
    }

    private class TrackedWorkshopCall
    {
      public string ModDisplayName;
      public string SteamIdString;
      public bool IsUnsubscribe;
      public CallResult<RemoteStorageSubscribePublishedFileResult_t> SubscribeListener;
      public CallResult<RemoteStorageUnsubscribePublishedFileResult_t> UnsubscribeListener;
    }

    private readonly List<TrackedWorkshopCall> _inFlightWorkshopCalls = new List<TrackedWorkshopCall>();

    public void PromptSubscriptionChange(string steamIdStr, string modDisplayName)
    {
      if (string.IsNullOrEmpty(steamIdStr) || !ulong.TryParse(steamIdStr, out ulong parsedSteamId))
      {
        Debug.LogError($"[SyncModsPro] Cannot process cloud action. Invalid Steam ID: '{steamIdStr}'");
        return;
      }

      PublishedFileId_t fileId = new PublishedFileId_t(parsedSteamId);

      // Check our instant cache first, fallback to Steam API
      bool isSubscribed;
      if (_recentStateOverrides.TryGetValue(steamIdStr, out bool knownState))
      {
        isSubscribed = knownState;
      }
      else
      {
        uint itemStateFlags = SteamUGC.GetItemState(fileId);
        isSubscribed = (itemStateFlags & (uint)EItemState.k_EItemStateSubscribed) != 0;
      }

      string promptLocKey = isSubscribed ? SteamUnsubscribePromptLocKey : SteamSubscribePromptLocKey;
      Label msgLabel = new Label(_loc.T(promptLocKey, modDisplayName));
      msgLabel.AddToClassList("text--default");
      msgLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
      msgLabel.style.whiteSpace = WhiteSpace.Normal;
      msgLabel.style.marginTop = 10;
      msgLabel.style.marginBottom = 10;

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
      TrackedWorkshopCall callRecord = new TrackedWorkshopCall
      {
        ModDisplayName = modDisplayName,
        SteamIdString = steamIdStr,
        IsUnsubscribe = unsubscribe
      };

      if (unsubscribe)
      {
        SteamAPICall_t apiCall = SteamUGC.UnsubscribeItem(fileId);
        callRecord.UnsubscribeListener = CallResult<RemoteStorageUnsubscribePublishedFileResult_t>.Create(OnUnsubscribeResult);
        callRecord.UnsubscribeListener.Set(apiCall);
        _inFlightWorkshopCalls.Add(callRecord);
        Debug.Log($"[SyncModsPro] Tracking Unsubscribe call for '{modDisplayName}' ({steamIdStr})");
      }
      else
      {
        SteamAPICall_t apiCall = SteamUGC.SubscribeItem(fileId);
        callRecord.SubscribeListener = CallResult<RemoteStorageSubscribePublishedFileResult_t>.Create(OnSubscribeResult);
        callRecord.SubscribeListener.Set(apiCall);
        _inFlightWorkshopCalls.Add(callRecord);
        Debug.Log($"[SyncModsPro] Tracking Subscribe call for '{modDisplayName}' ({steamIdStr})");
      }
    }

    private void DownloadNow(PublishedFileId_t fileId, string steamIdStr)
    {
      bool downloadTriggered = SteamUGC.DownloadItem(fileId, bHighPriority: true);
      Debug.Log($"[SyncModsPro] Direct Download call processed for item '{steamIdStr}' (Dispatched: {downloadTriggered})");
    }

    private void LogWorkshopAction(string action, string steamId, string modName, string result)
    {
      try
      {
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        string logEntry = $"[{timestamp}] {action} | {steamId} | {modName} | {result}";

        List<string> lines = new List<string>();
        if (File.Exists(_logFilePath))
        {
          lines.AddRange(File.ReadAllLines(_logFilePath));
        }

        lines.Add(logEntry);

        // Enforce the rolling 100-row limit (keeps the bottom/newest 100 lines)
        const int MaxLogEntries = 100;
        if (lines.Count > MaxLogEntries)
        {
          lines.RemoveRange(0, lines.Count - MaxLogEntries);
        }

        File.WriteAllLines(_logFilePath, lines);

        // Notify the view controller that new data is ready to be drawn
        OnHistoryUpdated?.Invoke();
      }
      catch (Exception e)
      {
        Debug.LogWarning($"[SyncModsPro] Failed to write to Workshop log: {e.Message}");
      }
    }

    private void OnSubscribeResult(RemoteStorageSubscribePublishedFileResult_t callback, bool ioFailure)
    {
      TrackedWorkshopCall matchingCall = _inFlightWorkshopCalls.Find(c =>
        !c.IsUnsubscribe && c.SteamIdString == callback.m_nPublishedFileId.m_PublishedFileId.ToString()
      );

      string displayName = matchingCall != null ? matchingCall.ModDisplayName : _loc.T(SteamUnknownModLocKey);
      string steamIdStr = matchingCall != null ? matchingCall.SteamIdString : callback.m_nPublishedFileId.m_PublishedFileId.ToString();

      if (matchingCall != null) _inFlightWorkshopCalls.Remove(matchingCall);

      string title = _loc.T(SteamDialogTitleLocKey);
      string message;
      string logResult;

      if (ioFailure)
      {
        message = _loc.T(SteamSubscribeIoFailureLocKey, displayName, steamIdStr);
        logResult = "IO_FAILURE";
        Debug.LogError($"[SyncModsPro] IO Failure received during subscription to {steamIdStr}");
      }
      else if (callback.m_eResult == EResult.k_EResultOK)
      {
        message = _loc.T(SteamSubscribeSuccessLocKey, displayName);
        logResult = "SUCCESS";
        _recentStateOverrides[steamIdStr] = true; // INSTANT CACHE OVERRIDE
        Debug.Log($"[SyncModsPro] Steam confirm: Subscribed successfully to {steamIdStr}");
        DownloadNow(callback.m_nPublishedFileId, steamIdStr);
      }
      else
      {
        string errorCode = $"{callback.m_eResult} ({(int)callback.m_eResult})";
        message = _loc.T(SteamSubscribeRejectedLocKey, displayName, errorCode);
        logResult = $"FAILED ({errorCode})";
        Debug.LogWarning($"[SyncModsPro] Steam subscription failed with result: {callback.m_eResult}");
      }

      LogWorkshopAction("SUBSCRIBE", steamIdStr, displayName, logResult);
      ShowFeedbackDialog(title, message);
    }

    private void OnUnsubscribeResult(RemoteStorageUnsubscribePublishedFileResult_t callback, bool ioFailure)
    {
      TrackedWorkshopCall matchingCall = _inFlightWorkshopCalls.Find(c =>
        c.IsUnsubscribe && c.SteamIdString == callback.m_nPublishedFileId.m_PublishedFileId.ToString()
      );

      string displayName = matchingCall != null ? matchingCall.ModDisplayName : _loc.T(SteamUnknownModLocKey);
      string steamIdStr = matchingCall != null ? matchingCall.SteamIdString : callback.m_nPublishedFileId.m_PublishedFileId.ToString();

      if (matchingCall != null) _inFlightWorkshopCalls.Remove(matchingCall);

      string title = _loc.T(SteamDialogTitleLocKey);
      string message;
      string logResult;

      if (ioFailure)
      {
        message = _loc.T(SteamUnsubscribeIoFailureLocKey, displayName, steamIdStr);
        logResult = "IO_FAILURE";
        Debug.LogError($"[SyncModsPro] IO Failure received during unsubscription from {steamIdStr}");
      }
      else if (callback.m_eResult == EResult.k_EResultOK)
      {
        message = _loc.T(SteamUnsubscribeSuccessLocKey, displayName);
        logResult = "SUCCESS";
        _recentStateOverrides[steamIdStr] = false; // INSTANT CACHE OVERRIDE
        Debug.Log($"[SyncModsPro] Steam confirm: Unsubscribed successfully from {steamIdStr}");
      }
      else
      {
        string errorCode = $"{callback.m_eResult} ({(int)callback.m_eResult})";
        message = _loc.T(SteamUnsubscribeRejectedLocKey, displayName, errorCode);
        logResult = $"FAILED ({errorCode})";
        Debug.LogWarning($"[SyncModsPro] Steam unsubscription failed with result: {callback.m_eResult}");
      }

      LogWorkshopAction("UNSUBSCRIBE", steamIdStr, displayName, logResult);
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

    public List<WorkshopLogEntry> GetLogEntries()
    {
      List<WorkshopLogEntry> entries = new List<WorkshopLogEntry>();
      if (!File.Exists(_logFilePath)) return entries;

      try
      {
        string[] lines = File.ReadAllLines(_logFilePath);

        for (int i = lines.Length - 1; i >= 0; i--)
        {
          string line = lines[i];
          if (string.IsNullOrWhiteSpace(line)) continue;

          int bracketEnd = line.IndexOf("] ");
          if (bracketEnd > 0)
          {
            string time = line.Substring(1, bracketEnd - 1);
            string remainder = line.Substring(bracketEnd + 2);
            string[] parts = remainder.Split(new string[] { " | " }, StringSplitOptions.None);

            if (parts.Length >= 4)
            {
              string steamIdStr = parts[1].Trim();
              bool isSubscribed = false;

              // Check our instant cache first, fallback to Steam API
              if (_recentStateOverrides.TryGetValue(steamIdStr, out bool knownState))
              {
                isSubscribed = knownState;
              }
              else if (ulong.TryParse(steamIdStr, out ulong parsedId))
              {
                uint itemStateFlags = SteamUGC.GetItemState(new PublishedFileId_t(parsedId));
                isSubscribed = (itemStateFlags & (uint)EItemState.k_EItemStateSubscribed) != 0;
              }

              entries.Add(new WorkshopLogEntry
              {
                Timestamp = time,
                Action = parts[0].Trim(),
                SteamId = steamIdStr,
                ModName = parts[2].Trim(),
                Result = parts[3].Trim(),
                IsSubscribed = isSubscribed
              });
            }
          }
        }
      }
      catch (Exception e)
      {
        Debug.LogWarning($"[SyncModsPro] Error reading Workshop log: {e.Message}");
      }

      return entries;
    }
  }
}