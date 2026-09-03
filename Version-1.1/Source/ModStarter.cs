using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Calloatti.Config;
using HarmonyLib;
using Timberborn.ModManagerScene;
using Timberborn.PlayerDataSystem;
using UnityEngine;

namespace Calloatti.SyncModsPro
{
  public class ModStarter : IModStarter
  {
    public static SimpleConfig Config { get; private set; }

    public static string ModPath { get; private set; }

    // Reusing a single HttpClient instance is best practice in C#
    private static readonly HttpClient _httpClient = new HttpClient();

    public void StartMod(IModEnvironment modEnvironment)
    {
      // SAVE THE PATH HERE:
      ModPath = modEnvironment.ModPath;

      Config = new SimpleConfig(modEnvironment.ModPath);
      new Harmony("calloatti.syncmodspro").PatchAll();

      // Dispatch the download to a background thread so the game initialization doesn't freeze
      Task.Run(() => DownloadFileAsync());
    }

    private async Task DownloadFileAsync()
    {
      try
      {
        // Define your target URL
        string targetUrl = "https://raw.githubusercontent.com/calloatti/rosetta_steam/refs/heads/main/data/rosetta.txt";

        // Save directly to the player data folder as requested
        string targetSavePath = Path.Combine(PlayerDataFileService.PlayerDataDirectory, "SyncModsPro.Rosetta.txt");

        // Check if the file exists and is less than 8 hours old
        if (File.Exists(targetSavePath))
        {
          TimeSpan fileAge = DateTime.Now - File.GetLastWriteTime(targetSavePath);
          if (fileAge.TotalHours < 8)
          {
            Debug.Log($"[SyncModsPro] Local file is only {fileAge.TotalHours:F1} hours old (less than 8). Skipping download.");
            return;
          }
        }

        Debug.Log($"[SyncModsPro] Starting background download from {targetUrl}...");

        // Download the file asynchronously
        byte[] fileBytes = await _httpClient.GetByteArrayAsync(targetUrl);

        // Write the data to disk
        File.WriteAllBytes(targetSavePath, fileBytes);

        Debug.Log($"[SyncModsPro] Download complete. File saved to {targetSavePath}");
      }
      catch (Exception ex)
      {
        // Catch network errors silently without breaking the game thread
        Debug.LogError($"[SyncModsPro] Background download failed: {ex.Message}");
      }
    }
  }
}