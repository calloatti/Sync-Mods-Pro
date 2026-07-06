using Calloatti.Config;
using HarmonyLib;
using Timberborn.ModManagerScene;

namespace Calloatti.SyncModsPro
{
  public class ModStarter : IModStarter
  {
    public static SimpleConfig Config { get; private set; }

    public static string ModPath { get; private set; }

    public void StartMod(IModEnvironment modEnvironment)
    {
      // SAVE THE PATH HERE:
      ModPath = modEnvironment.ModPath;

      Config = new SimpleConfig(modEnvironment.ModPath);
      new Harmony("calloatti.SyncModsPro").PatchAll();
    }
  }
}