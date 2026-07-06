using System;
using System.Collections.Generic;
using System.Linq;
using Timberborn.CoreUI;
using Timberborn.GameSaveRepositorySystem;
using Timberborn.GameSaveRepositorySystemUI;
using Timberborn.GameSceneLoading;
using Timberborn.Localization;
using Timberborn.Modding;
using Timberborn.SaveMetadataSystem;
using Timberborn.SingletonSystem;
using UnityEngine;
using UnityEngine.UIElements;

namespace Calloatti.SyncModsPro
{
  public partial class ModListBox : ILoadableSingleton
  {
    public static ModListBox Instance;

    private readonly ILoc _loc;
    private readonly ModRepository _modRepository;
    private readonly DialogBoxShower _dialogBoxShower;
    private readonly GameSceneLoader _gameSceneLoader;
    private readonly GameSaveRepository _gameSaveRepository;
    private readonly ValidatingGameLoader _validatingGameLoader;

    public ModListBox(ILoc loc, ModRepository modRepository, DialogBoxShower dialogBoxShower, GameSceneLoader gameSceneLoader, GameSaveRepository gameSaveRepository, ValidatingGameLoader validatingGameLoader)
    {
      _loc = loc;
      _modRepository = modRepository;
      _dialogBoxShower = dialogBoxShower;
      _gameSceneLoader = gameSceneLoader;
      _gameSaveRepository = gameSaveRepository;
      _validatingGameLoader = validatingGameLoader;
    }

    public void Load()
    {
      Instance = this;
    }

    public void ShowDialog(SaveMetadata metadata, SaveReference saveReference, Action continueCallback = null)
    {
      _duplicateGroups.Clear();
      _orderedRows.Clear();
      _activeFilters.Clear();
      _isStrictOn = false;

      int calculatedTotalWidth = GetTotalTableWidth();
      VisualElement root = new VisualElement();
      root.style.width = calculatedTotalWidth;

      List<RowData> rowsData = GenerateUnifiedList(metadata);

      // Clean, unburdened top bar with just the filter toggles
      VisualElement topBar = CreateTopBar();
      root.Add(topBar);

      VisualElement listContainer = new VisualElement();

      VisualElement headerRow = CreateRow(
        _loc.T("Calloatti.SyncModsPro.Column.Name"),
        _loc.T("Calloatti.SyncModsPro.Column.Id"),
        _loc.T("Calloatti.SyncModsPro.Column.VersionFolder"),
        _loc.T("Calloatti.SyncModsPro.Column.MinGameVer"),
        _loc.T("Calloatti.SyncModsPro.Column.SavedVersion"),
        _loc.T("Calloatti.SyncModsPro.Column.CurrentVersion"),
        _loc.T("Calloatti.SyncModsPro.Column.Status"),
        _loc.T("Calloatti.SyncModsPro.Column.CurrentState"),
        _loc.T("Calloatti.SyncModsPro.Column.SavedState"),
        _loc.T("Calloatti.SyncModsPro.Column.TargetState"),
        _loc.T("Calloatti.SyncModsPro.Column.Active"),
        TextNormal, true, null
      );
      headerRow.style.marginTop = 15f;
      headerRow.style.paddingRight = 20;
      listContainer.Add(headerRow);

      ScrollView scrollView = CreateScrollView();

      int rowIndex = 0;

      foreach (var rowData in rowsData)
      {
        bool isEven = (rowIndex % 2 == 0);

        rowData.UpdateStatus();
        UnityEngine.Color dynamicRowColor = rowData.GetStatusColor();

        VisualElement entryRow = CreateRow(
          rowData.DisplayName,
          rowData.ModId,
          rowData.VersionFolder,
          rowData.MinimumGameVersion,
          rowData.SavedState == ModState.Enabled ? rowData.SavedVersion : "-",
          rowData.Version,
          rowData.Status == ModStatus.NotApplicable ? "" : _loc.T($"Calloatti.SyncModsPro.Status.{rowData.Status}"),
          GetStateString(rowData.CurrentState),
          GetStateString(rowData.SavedState),
          GetStateString(rowData.TargetState),
          "",
          dynamicRowColor,
          false,
          rowData,
          isEven
        );
        scrollView.Add(entryRow);
        rowIndex++;
      }
      listContainer.Add(scrollView);
      root.Add(listContainer);

      // Execute initial pass to calculate zebra-striping against only visible elements
      ApplyFilters();

      var builder = _dialogBoxShower.Create().AddContent(root).SetMaxWidth(calculatedTotalWidth + 100);

      builder.SetConfirmButton(() => { }, "HiddenConfirm")
             .SetCancelButton(() => { }, _loc.T("Calloatti.SyncModsPro.Button.Cancel"));

      DialogBox dialogBox = builder.Show();

      InjectToolbarButtons(root, dialogBox, metadata, saveReference, continueCallback, rowsData);
    }
    private void InjectToolbarButtons(VisualElement root, DialogBox dialogBox, SaveMetadata metadata, SaveReference saveReference, Action continueCallback, List<RowData> rowsData)
    {
      if (root.panel == null) return;

      List<Button> dialogButtons = root.panel.visualTree.Query<Button>().ToList();

      Button closeButton = new Button();
      closeButton.AddToClassList("close-button");
      VisualElement trueWindowFrame = root.parent?.parent ?? root;
      trueWindowFrame.Add(closeButton);
      closeButton.RegisterCallback<ClickEvent>(evt => dialogBox.OnUICancelled());

      Button nativeCancel = dialogButtons.FirstOrDefault(b => b.name == "CancelButton");
      if (nativeCancel != null) nativeCancel.style.display = DisplayStyle.None;

      Button nativeConfirm = dialogButtons.FirstOrDefault(b => b.name == "ConfirmButton");
      if (nativeConfirm != null) nativeConfirm.style.display = DisplayStyle.None;

      Button referenceNativeButton = nativeConfirm ?? nativeCancel;
      if (referenceNativeButton == null) return;

      Type nativeButtonType = referenceNativeButton.GetType();
      VisualElement nativeButtonContainer = referenceNativeButton.parent;

      if (nativeButtonContainer != null)
      {
        nativeButtonContainer.style.flexDirection = FlexDirection.Row;
        nativeButtonContainer.style.justifyContent = Justify.Center;
      }

      List<Button> createdButtons = new List<Button>();

      string[] buttonTexts = new string[]
      {
        _loc.T("Calloatti.SyncModsPro.Button.SaveProfile"),
        _loc.T("Calloatti.SyncModsPro.Button.StrictFlipOff"),
        _loc.T("Calloatti.SyncModsPro.Button.Sync"),
        _loc.T("Calloatti.SyncModsPro.Button.Restart"),
        _loc.T("Calloatti.SyncModsPro.Button.RestartLoad"),
        _loc.T("Calloatti.SyncModsPro.Button.LoadGame")
      };

      string[] tooltipKeys = new string[]
      {
        "Calloatti.SyncModsPro.Tooltip.SaveProfile",
        "Calloatti.SyncModsPro.Tooltip.StrictFlip",
        "Calloatti.SyncModsPro.Tooltip.Sync",
        "Calloatti.SyncModsPro.Tooltip.Restart",
        "Calloatti.SyncModsPro.Tooltip.RestartLoad",
        "Calloatti.SyncModsPro.Tooltip.LoadGame"
      };

      EventCallback<ClickEvent>[] buttonActions = new EventCallback<ClickEvent>[]
      {
        evt => HandleSaveProfileClick(saveReference),
        evt => HandleStrictFlipClick(createdButtons[1]),
        evt => HandleSyncClick(rowsData),
        evt => HandleRestartClick(),
        evt => HandleRestartLoadClick(rowsData, saveReference),
        evt => HandleLoadGameClick(dialogBox, continueCallback)
      };

      for (int i = 0; i < buttonTexts.Length; i++)
      {
        Button utilityBtn = (Button)Activator.CreateInstance(nativeButtonType);
        utilityBtn.text = buttonTexts[i];

        foreach (var className in referenceNativeButton.GetClasses())
        {
          utilityBtn.AddToClassList(className);
        }

        utilityBtn.style.minWidth = 150;
        utilityBtn.style.width = StyleKeyword.Auto;
        utilityBtn.style.maxWidth = 150;
        utilityBtn.style.overflow = Overflow.Hidden;
        utilityBtn.style.textOverflow = TextOverflow.Ellipsis;

        utilityBtn.style.paddingLeft = 14;
        utilityBtn.style.paddingRight = 14;
        utilityBtn.style.height = referenceNativeButton.style.height;
        utilityBtn.style.marginLeft = 4;
        utilityBtn.style.marginRight = 4;

        if (i == buttonTexts.Length - 1 && continueCallback == null)
        {
          utilityBtn.SetEnabled(false);
        }

        utilityBtn.RegisterCallback(buttonActions[i]);
        AttachButtonTooltipBehavior(utilityBtn, tooltipKeys[i]);

        createdButtons.Add(utilityBtn);

        if (nativeButtonContainer != null)
        {
          nativeButtonContainer.Add(utilityBtn);
        }
      }
    }
  }
}