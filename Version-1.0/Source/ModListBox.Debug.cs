using System;
using System.IO;
using System.Text;
using UnityEngine;
using Timberborn.Modding;

namespace Calloatti.SyncModsPro
{
  public partial class ModListBox
  {
    // --- DEBUG CONFIGURATION FLAG ---
    private static readonly bool _debugMode = true;

    private void ExportModObjectDiagnostics(Mod mod)
    {
      // Instantly abort execution if the debug flag is turned off
      if (!_debugMode) return;
      if (mod == null || mod.Manifest == null) return;

      try
      {
        // Targets the application LocalLow storage folder where Player.log sits
        string logDirectory = Path.Combine(Application.persistentDataPath, "Logs");
        if (!Directory.Exists(logDirectory))
        {
          Directory.CreateDirectory(logDirectory);
        }

        // FIX: Rebuilt file name generation to match your exact structural requirements string
        string fileName = mod.Manifest.Id + "." + mod.ModDirectory.OriginName + "." + mod.ModDirectory.Directory.Name + ".txt";
        string logPath = Path.Combine(logDirectory, fileName);

        StringBuilder report = new StringBuilder();

        report.AppendLine("======================================================================");
        report.AppendLine($"MOD OBJECT DIAGNOSTICS DUMP - GENERATED: {DateTime.Now}");
        report.AppendLine("======================================================================");
        report.AppendLine();

        // 1. Root Mod Object Elements
        report.AppendLine("[ROOT OBJECT PROPERTIES]");
        report.AppendLine($"mod.DisplayName:  {mod.DisplayName}");
        report.AppendLine($"mod.IsEnabled:  {mod.IsEnabled}");
        report.AppendLine();

        // 2. ModManifest Properties
        report.AppendLine("[MANIFEST DATA]");
        report.AppendLine($"mod.Manifest.Id:  {mod.Manifest.Id}");
        report.AppendLine($"mod.Manifest.Name:  {mod.Manifest.Name}");
        report.AppendLine($"mod.Manifest.Description:  {mod.Manifest.Description}");
        report.AppendLine($"mod.Manifest.Version.Full:  {mod.Manifest.Version.Full}");
        report.AppendLine($"mod.Manifest.Version.Formatted:  {mod.Manifest.Version.Formatted}");
        report.AppendLine($"mod.Manifest.MinimumGameVersion.Full:  {mod.Manifest.MinimumGameVersion.Full}");
        report.AppendLine($"mod.Manifest.MinimumGameVersion.Formatted:  {mod.Manifest.MinimumGameVersion.Formatted}");
        report.AppendLine($"mod.Manifest.MinimumGameVersion.IsDevelopmentVersion:  {mod.Manifest.MinimumGameVersion.IsDevelopmentVersion}");
        report.AppendLine();

        // Required Dependency Elements Loop (All sub-properties exposed)
        report.AppendLine("[REQUIRED MOD DEPENDENCIES]");
        if (mod.Manifest.RequiredMods.Length > 0)
        {
          for (int i = 0; i < mod.Manifest.RequiredMods.Length; i++)
          {
            var required = mod.Manifest.RequiredMods[i];
            report.AppendLine($"mod.Manifest.RequiredMods[{i}].Id:  {required.Id}");
            report.AppendLine($"mod.Manifest.RequiredMods[{i}].MinimumVersion.Full:  {required.MinimumVersion.Full}");
            report.AppendLine($"mod.Manifest.RequiredMods[{i}].MinimumVersion.Formatted:  {required.MinimumVersion.Formatted}");
            report.AppendLine($"mod.Manifest.RequiredMods[{i}].MinimumVersion.IsDevelopmentVersion:  {required.MinimumVersion.IsDevelopmentVersion}");
          }
        }
        else
        {
          report.AppendLine("mod.Manifest.RequiredMods.Length:  0");
        }
        report.AppendLine();

        // Optional Dependency Elements Loop (All sub-properties exposed)
        report.AppendLine("[OPTIONAL MOD DEPENDENCIES]");
        if (mod.Manifest.OptionalMods.Length > 0)
        {
          for (int i = 0; i < mod.Manifest.OptionalMods.Length; i++)
          {
            var optional = mod.Manifest.OptionalMods[i];
            report.AppendLine($"mod.Manifest.OptionalMods[{i}].Id:  {optional.Id}");
            report.AppendLine($"mod.Manifest.OptionalMods[{i}].MinimumVersion.Full:  {optional.MinimumVersion.Full}");
            report.AppendLine($"mod.Manifest.OptionalMods[{i}].MinimumVersion.Formatted:  {optional.MinimumVersion.Formatted}");
            report.AppendLine($"mod.Manifest.OptionalMods[{i}].MinimumVersion.IsDevelopmentVersion:  {optional.MinimumVersion.IsDevelopmentVersion}");
          }
        }
        else
        {
          report.AppendLine("mod.Manifest.OptionalMods.Length:  0");
        }
        report.AppendLine();

        // 3. ModDirectory Structural Elements & System IO Properties
        if (mod.ModDirectory.Directory != null)
        {
          report.AppendLine("[DIRECTORY FILE-SYSTEM DATA]");
          report.AppendLine($"mod.ModDirectory.Path:  {mod.ModDirectory.Path}");
          report.AppendLine($"mod.ModDirectory.OriginPath:  {mod.ModDirectory.OriginPath}");
          report.AppendLine($"mod.ModDirectory.OriginName:  {mod.ModDirectory.OriginName}");
          report.AppendLine($"mod.ModDirectory.DisplaySource:  {mod.ModDirectory.DisplaySource}");
          report.AppendLine($"mod.ModDirectory.IsUserMod:  {mod.ModDirectory.IsUserMod}");
          report.AppendLine($"mod.ModDirectory.GameVersion.Full:  {mod.ModDirectory.GameVersion.Full}");
          report.AppendLine($"mod.ModDirectory.GameVersion.Formatted:  {mod.ModDirectory.GameVersion.Formatted}");
          report.AppendLine($"mod.ModDirectory.GameVersion.IsDevelopmentVersion:  {mod.ModDirectory.GameVersion.IsDevelopmentVersion}");

          // Underlying DirectoryInfo core properties
          report.AppendLine($"mod.ModDirectory.Directory.Name:  {mod.ModDirectory.Directory.Name}");
          report.AppendLine($"mod.ModDirectory.Directory.FullName:  {mod.ModDirectory.Directory.FullName}");
          report.AppendLine($"mod.ModDirectory.Directory.Exists:  {mod.ModDirectory.Directory.Exists}");
          report.AppendLine($"mod.ModDirectory.Directory.CreationTime:  {mod.ModDirectory.Directory.CreationTime}");
          report.AppendLine($"mod.ModDirectory.Directory.LastWriteTime:  {mod.ModDirectory.Directory.LastWriteTime}");
        }

        File.WriteAllText(logPath, report.ToString());
        Debug.Log($"[SyncModsPro] Diagnostic diagnostics log generated successfully at: {logPath}");
      }
      catch (Exception ex)
      {
        Debug.LogError($"[SyncModsPro] Failed to output active structural metadata logs on local disk: {ex.Message}");
      }
    }
  }
}