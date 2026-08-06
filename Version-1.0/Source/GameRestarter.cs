using System;
using System.Diagnostics;
using System.IO;
using Timberborn.GameSaveRepositorySystem;
using UnityEngine;

namespace Calloatti.SyncModsPro
{
  public static class GameRestarter
  {
    private static readonly string LogFilePath = Path.Combine(Application.persistentDataPath, "SyncModsPro.log");
    private const string LogPrefix = "[SyncModsPro]";

    /// <summary>
    /// Appends directly to a dedicated log file to ensure messages flush to disk before Application.Quit().
    /// </summary>
    private static void LogDirect(string message)
    {
      try
      {
        File.AppendAllText(LogFilePath, $"{LogPrefix} [{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
      }
      catch { }

      // Also log to Unity standard log for active runtime tracking
      UnityEngine.Debug.Log($"{LogPrefix} {message}");
    }

    /// <summary>
    /// Executes a clean standard restart of the application with no extra CLI arguments.
    /// </summary>
    public static void RequestStandardRestart()
    {
      ExecuteRestartSequence(new string[0]);
    }

    /// <summary>
    /// Restarts the application, skipping the mod manager (bypasses mod validation/load screen).
    /// </summary>
    public static void RequestSkipModManagerRestart()
    {
      ExecuteRestartSequence(new[] { "-skipModManager" });
    }

    /// <summary>
    /// Constructs target launch parameters and flags to skip managers and load a save automatically on boot.
    /// </summary>
    public static void RequestRestartAndLoad(SaveReference saveReference)
    {
      if (saveReference == null)
      {
        LogDirect("ERROR: Cannot perform Restart + Load. SaveReference is null.");
        return;
      }

      string saveName = saveReference.SaveName;
      string settlementName = saveReference.SettlementReference?.SettlementName ?? "";

      string[] extraArgs = new[]
      {
        "-skipModManager",
        "-settlementName", settlementName,
        "-saveName", saveName
      };

      ExecuteRestartSequence(extraArgs);
    }

    private static void ExecuteRestartSequence(string[] args)
    {
      // Reset the file on sequence initialization
      try { if (File.Exists(LogFilePath)) File.Delete(LogFilePath); } catch { }

      LogDirect("Restart sequence initiated.");

      string rootPath = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
      string exePath;
      int currentPid = Process.GetCurrentProcess().Id;
      LogDirect($"Current Process ID: {currentPid}");

      ProcessStartInfo psi = new ProcessStartInfo
      {
        UseShellExecute = true
      };

      if (Application.platform == RuntimePlatform.WindowsPlayer)
      {
        exePath = Path.Combine(rootPath, "Timberborn.exe");
        string argString = string.Join(" ", Array.ConvertAll(args, a => $"\"{a}\""));

        string psCommand = $"Wait-Process -Id {currentPid} -ErrorAction SilentlyContinue; & '{exePath}' {argString}";
        LogDirect($"Encoding PowerShell payload to preserve formatting: {psCommand}");

        // Convert command to Base64 (UTF-16LE) to safely bypass all Windows quote parsing rules
        byte[] bytes = System.Text.Encoding.Unicode.GetBytes(psCommand);
        string encodedCommand = Convert.ToBase64String(bytes);

        psi.FileName = "powershell.exe";
        psi.Arguments = $"-NoProfile -WindowStyle Hidden -EncodedCommand {encodedCommand}";
        psi.RedirectStandardInput = false;
        psi.UseShellExecute = true; // Forces OS-level execution context to fix MyDocuments
        psi.WindowStyle = ProcessWindowStyle.Hidden;
        psi.WorkingDirectory = rootPath; // Locks the environment to the game root

        try
        {
          Process.Start(psi);
          LogDirect("PowerShell background process successfully deployed.");
        }
        catch (Exception ex)
        {
          LogDirect($"PowerShell Process Error: {ex.Message}");
        }
      }
      else // Linux or macOS Player Execution Environment
      {
        if (Application.platform == RuntimePlatform.OSXPlayer)
        {
          exePath = Path.Combine(rootPath, "Timberborn.app/Contents/MacOS/Timberborn");
        }
        else
        {
          exePath = Path.Combine(rootPath, "Timberborn.x86_64");
        }

        string unixArgs = string.Join(" ", Array.ConvertAll(args, a => $"\"{a}\""));
        string shCommand = $"while kill -0 {currentPid} 2>/dev/null; do sleep 1; done; nohup \"{exePath}\" {unixArgs} > /dev/null 2>&1 &";
        LogDirect($"Piping Bash payload: {shCommand}");

        psi.FileName = "/bin/bash";
        psi.Arguments = $"-c \"{shCommand}\"";

        try
        {
          Process.Start(psi);
          LogDirect("Bash script loop successfully detached.");
        }
        catch (Exception ex)
        {
          LogDirect($"Bash Process Error: {ex.Message}");
        }
      }

      // Terminate application immediately to release runtime system locks
      try
      {
        LogDirect("Calling Application.Quit(). Clearing process memory bounds...");
        Application.Quit();
      }
      catch (Exception ex)
      {
        LogDirect($"Error during Application.Quit execution loop: {ex.Message}");
      }
    }
  }
}