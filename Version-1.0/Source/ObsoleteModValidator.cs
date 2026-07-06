using System;
using System.Linq;
using Bindito.Core;
using Timberborn.CoreUI;
using Timberborn.Localization;
using Timberborn.Modding;
using Timberborn.SingletonSystem;

namespace Calloatti.SyncModsPro
{
  /// <summary>
  /// Checks for obsolete mods upon reaching the Main Menu and warns the user to uninstall them.
  /// </summary>
  public class ObsoleteModValidator : ILoadableSingleton
  {
    private readonly ModRepository _modRepository;
    private readonly DialogBoxShower _dialogBoxShower;
    private readonly ILoc _loc;

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
      // Search the entire repository (includes both enabled and disabled mods)
      var foundObsoleteMods = _modRepository.Mods
        .Where(m => _obsoleteIds.Contains(m.Manifest.Id, StringComparer.OrdinalIgnoreCase))
        .ToList();

      if (foundObsoleteMods.Any())
      {
        // Format the found mods into a clean bulleted list for the dialog
        string modList = string.Join("\n", foundObsoleteMods.Select(m => $"- {m.DisplayName}"));

        string message = _loc.T("Calloatti.SyncModsPro.ObsoleteMods.Warning", modList);

        // Spawn the native UI dialog
        _dialogBoxShower.Create()
          .SetMessage(message)
          .SetConfirmButton(() => { }, _loc.T("Calloatti.SyncModsPro.Button.OK"))
          .Show();
      }
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
