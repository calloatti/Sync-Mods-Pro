using Bindito.Core;
using Timberborn.ModManagerScene;

namespace Calloatti.SyncModsPro
{
  [Context("MainMenu")]
  [Context("Game")]
  public class ModConfigurator : Configurator
  {
    protected override void Configure()
    {
      Bind<MainMenuPanelPatchInitializer>().AsSingleton();
      Bind<ModListBox>().AsSingleton();
      Bind<WorkshopIdManager>().AsSingleton();
      Bind<WorkshopManager>().AsSingleton();
    }
  }
}