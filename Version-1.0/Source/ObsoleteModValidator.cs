using System;
using System.Linq;
using Bindito.Core;
using Timberborn.CoreUI;
using Timberborn.Localization;
using Timberborn.Modding;
using Timberborn.SingletonSystem;
using UnityEngine; // Required for PlayerPrefs.Save()

namespace Calloatti.SyncModsPro
{
  /// <summary>
  /// Checks for obsolete mods upon reaching the Main Menu and warns the user to uninstall them.
  /// </summary>
  public class ObsoleteModValidator : ILoadableSingleton
  {
    private ModRepository _modRepository;
    private DialogBoxShower _dialogBoxShower;
    private ILoc _loc;

    // This static flag persists across Main Menu reloads to ensure single execution per session
    private static bool _hasRun = false;

    // The precise IDs of the mods we want to eradicate
    private readonly string[] _obsoleteIds = new[]
    {
      "calloatti.syncmods",
      "Calloatti.LoadGameModValidator"
    };

    public ObsoleteModValidator(ModRepository modRepository, DialogBoxShower dialogBoxShower, ILoc loc)
    {
      _modRepository = modRepository;
      _dialogBoxShower = dialogBoxShower;
      _loc = loc;
    }

    public void Load()
    {
      // If it has already run this session, safely abort and clean up references immediately
      if (_hasRun)
      {
        return;
      }

      _hasRun = true;

      // Only search mods that are currently ACTIVE/ENABLED in the game
      var foundObsoleteMods = _modRepository.EnabledMods
        .Where(m => _obsoleteIds.Contains(m.Manifest.Id, StringComparer.OrdinalIgnoreCase))
        .ToList();

      if (foundObsoleteMods.Any())
      {
        // Forcefully disable the obsolete mods immediately
        foreach (var mod in foundObsoleteMods)
        {
          ModPlayerPrefsHelper.ToggleMod(false, mod);
        }

        // Push the changes to disk so they remain disabled after the restart
        PlayerPrefs.Save();

        // Format the found mods into a clean bulleted list, starting with a CRLF as requested
        string modList = Environment.NewLine + string.Join(Environment.NewLine, foundObsoleteMods.Select(m => $"- {m.DisplayName}"));

        string message = _loc.T("Calloatti.SyncModsPro.ObsoleteMods.Warning", modList);

        // Spawn the native UI dialog and wire the button to trigger our restart sequence
        _dialogBoxShower.Create()
          .SetMessage(message)
          .SetConfirmButton(() =>
          {
            ModRestarter.RequestStandardRestart();
          }, _loc.T("Calloatti.SyncModsPro.Button.Restart"))
          .Show();
      }

      // Nullify references immediately after doing its job to free resources early
      _modRepository = null;
      _dialogBoxShower = null;
      _loc = null;
    }
  }

  /// <summary>
  /// Injects our validator straight into the Main Menu load sequence.
  /// </summary>
  [Context("MainMenu")]
  public class ObsoleteModValidatorConfigurator : Configurator
  {
    protected override void Configure()
    {
      Bind<ObsoleteModValidator>().AsSingleton();
    }
  }
}