using System;
using HarmonyLib;
using Timberborn.MainMenuScene;

namespace Calloatti.SyncModsPro
{
  [HarmonyPatch(typeof(WelcomeScreenBox), "Show")]
  public static class ExperimentalDialogSkipPatch
  {
    private const string SkipExperimentalDialogKey = "SkipExperimentalDialog";

    [HarmonyPrefix]
    public static bool Prefix(Action onStart)
    {
      if (ModStarter.Config != null && ModStarter.Config.GetBool(SkipExperimentalDialogKey))
      {
        onStart?.Invoke();
        return false;
      }
      return true;
    }
  }
}
