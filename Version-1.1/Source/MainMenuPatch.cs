using HarmonyLib;
using UnityEngine;
using UnityEngine.UIElements;
using Timberborn.MainMenuPanels;
using Timberborn.Localization;
using Timberborn.SingletonSystem;
using Timberborn.TooltipSystem;
using System;
using System.Reflection;
using System.Linq;

namespace Calloatti.SyncModsPro
{
  [HarmonyPatch(typeof(MainMenuPanel), "GetPanel")]
  public static class MainMenuPanelPatch
  {
    private const string RestartButtonLocKey = "Calloatti.SyncModsPro.Button.Restart";
    private const string RestartButtonTooltipLocKey = "Calloatti.SyncModsPro.Tooltip.RestartShiftClick";

    private static ILoc _loc;
    private static ITooltipRegistrar _tooltipRegistrar;

    public static void SetLoc(ILoc loc)
    {
      _loc = loc;
    }

    public static void SetTooltipRegistrar(ITooltipRegistrar tooltipRegistrar)
    {
      _tooltipRegistrar = tooltipRegistrar;
    }

    private static void Postfix(MainMenuPanel __instance, VisualElement __result)
    {
      if (__result == null) return;

      // Prevent duplicates
      if (__result.Q("RestartGameButton") != null) return;

      // 1. Find the reference button ("ExitButton" confirmed from logs)
      VisualElement exitButton = __result.Q("ExitButton");
      if (exitButton == null)
      {
        UnityEngine.Debug.Log("SyncModsPro: Could not find 'ExitButton'.");
        return;
      }

      // 2. Create the clone
      Button restartButton = (Button)Activator.CreateInstance(exitButton.GetType());
      restartButton.name = "RestartGameButton";

      // Localization
      restartButton.text = _loc.T(RestartButtonLocKey);

      // Vanilla-style tooltip informing about the SHIFT click skip feature
      _tooltipRegistrar?.RegisterLocalizable(restartButton, RestartButtonTooltipLocKey);

      // 3. Copy Styles
      int sheetCount = exitButton.styleSheets.count;
      for (int i = 0; i < sheetCount; i++)
      {
        restartButton.styleSheets.Add(exitButton.styleSheets[i]);
      }

      foreach (var className in exitButton.GetClasses())
      {
        restartButton.AddToClassList(className);
      }

      // Copy Base Layout
      restartButton.style.width = exitButton.style.width;
      restartButton.style.height = exitButton.style.height;

      // 4. Click Event
      restartButton.RegisterCallback<ClickEvent>(evt => {
        if (evt.shiftKey)
        {
          UnityEngine.Debug.Log("SyncModsPro: Restarting (skip mod manager)...");
          GameRestarter.RequestSkipModManagerRestart();
        }
        else
        {
          UnityEngine.Debug.Log("SyncModsPro: Restarting...");
          GameRestarter.RequestStandardRestart();
        }
      });

      // 5. Inject and Compact
      VisualElement container = exitButton.parent;
      if (container != null)
      {
        // Insert above Exit
        int exitIndex = container.IndexOf(exitButton);
        container.Insert(exitIndex, restartButton);

        // --- SPACING ADJUSTMENT ---
        // To fit the new button without expanding the menu too much, 
        // we iterate all children and reduce their vertical margins.
        var menuItems = container.Children().ToList();

        foreach (var element in menuItems)
        {
          // Only apply to Buttons to avoid squashing labels or dividers if they exist
          if (element is Button)
          {
            // Set margins to 5px (Standard is often 10-15px)
            // This "subtracts" the added height by reclaiming space from the gaps.
            element.style.marginBottom = new Length(-1, LengthUnit.Pixel);
            element.style.marginTop = new Length(-1, LengthUnit.Pixel);
          }
        }

        UnityEngine.Debug.Log($"SyncModsPro: Inserted button and compacted spacing for {menuItems.Count} items.");
      }
      else
      {
        UnityEngine.Debug.Log("SyncModsPro: Container is null, cannot insert button.");
      }
    }
  }

  public class MainMenuPanelPatchInitializer : ILoadableSingleton
  {
    public MainMenuPanelPatchInitializer(ILoc loc, ITooltipRegistrar tooltipRegistrar)
    {
      MainMenuPanelPatch.SetLoc(loc);
      MainMenuPanelPatch.SetTooltipRegistrar(tooltipRegistrar);
    }

    public void Load()
    {
    }
  }
}
