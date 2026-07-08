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
  public partial class ModListBox : IPanelController, ILoadableSingleton
  {
    public static ModListBox Instance;

    private SaveReference _currentSaveReference; // Cached class-level reference
    private SaveMetadata _cachedMetadata;
    private Action _cachedCallback;

    private VisualElement _rootElement;
    private NineSliceVisualElement _mainWindow;
    private NineSliceVisualElement _bottomDock;

    // The 3 major views
    private VisualElement _mainView;
    private VisualElement _historyView;
    private VisualElement _dependencyView;

    // Tab Buttons
    private NineSliceButton _btnMain;
    private NineSliceButton _btnHistory;
    private NineSliceButton _btnAudit;

    private readonly ILoc _loc;
    private readonly ModRepository _modRepository;
    private readonly DialogBoxShower _dialogBoxShower;
    private readonly GameSceneLoader _gameSceneLoader;
    private readonly GameSaveRepository _gameSaveRepository;
    private readonly ValidatingGameLoader _validatingGameLoader;
    private readonly PanelStack _panelStack;

    public ModListBox(ILoc loc, ModRepository modRepository, DialogBoxShower dialogBoxShower, GameSceneLoader gameSceneLoader, GameSaveRepository gameSaveRepository, ValidatingGameLoader validatingGameLoader, PanelStack panelStack)
    {
      _loc = loc;
      _modRepository = modRepository;
      _dialogBoxShower = dialogBoxShower;
      _gameSceneLoader = gameSceneLoader;
      _gameSaveRepository = gameSaveRepository;
      _validatingGameLoader = validatingGameLoader;
      _panelStack = panelStack;
    }

    public void Load()
    {
      Instance = this;
    }

    public void Open(SaveMetadata metadata, SaveReference saveReference, Action continueCallback = null)
    {
      _currentSaveReference = saveReference;
      _cachedMetadata = metadata;
      _cachedCallback = continueCallback;

      _panelStack.HideAndPushOverlay(this);
    }

    public bool OnUIConfirmed()
    {
      // Only allow confirm/load if the callback is actually available
      if (_cachedCallback != null)
      {
        CustomTooltipManager.HideTooltip(); // Clean up tooltip before transitioning

        // Inlined the HandleLoadGameClick logic here directly
        _panelStack.Pop(this);

        // Safely invoke and then immediately dump the references to free memory
        Action tempCallback = _cachedCallback;
        _cachedCallback = null;
        _cachedMetadata = null;
        _currentSaveReference = null;

        tempCallback.Invoke();

        return true; // Tells Timberborn we consumed the Enter key event
      }

      return false; // Tells Timberborn we didn't do anything
    }

    public void OnUICancelled()
    {
      CustomTooltipManager.HideTooltip(); // Hide tooltip on close
      _panelStack.Pop(this);

      // Dump the references so the Garbage Collector can clean up the save data
      _cachedCallback = null;
      _cachedMetadata = null;
      _currentSaveReference = null;
    }

    public VisualElement GetPanel()
    {
      _duplicateGroups.Clear();
      _orderedRows.Clear();
      _activeFilters.Clear();
      _isStrictOn = true;
      _filterBySavedEnabled = false;

      int calculatedTotalWidth = GetTotalTableWidth();

      // 1. Transparent Root Wrapper
      _rootElement = new VisualElement();
      _rootElement.style.flexDirection = FlexDirection.Column;
      _rootElement.style.alignItems = Align.Center;
      _rootElement.style.justifyContent = Justify.Center;
      _rootElement.style.width = Length.Percent(100);
      _rootElement.style.height = Length.Percent(100);

      // Load CommonStyle to make the native green "selected" style rule accessible
      StyleSheet commonStyle = Resources.Load<StyleSheet>("UI/Views/Common/CommonStyle");
      if (commonStyle != null)
      {
        _rootElement.styleSheets.Add(commonStyle);
      }

      // 2. Main Window (Native NineSlice Implementation)
      _mainWindow = new NineSliceVisualElement();
      _mainWindow.AddToClassList("content-centered");
      _mainWindow.AddToClassList("sliced-border");
      _mainWindow.AddToClassList("sliced-border--nontransparent");

      // --- ADD THE TOP CENTER BANNER ---
      // Instantiate the container using the native capsule configuration class
      NineSliceVisualElement headerBackground = new NineSliceVisualElement();
      headerBackground.AddToClassList("capsule-header");

      // Center the inner text layout perfectly inside the ribbon asset
      headerBackground.style.justifyContent = Justify.Center;
      headerBackground.style.alignItems = Align.Center;

      // Apply your -42px layout adjustment to cleanly snap it over the top border
      headerBackground.style.top = -10;

      // Attach the header text token targeting vanilla layout systems
      Label headerLabel = new Label("Sync Mods");
      headerLabel.AddToClassList("capsule-header__text");
      headerLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
      headerLabel.style.top = -2; // Adjust for visual centering within the capsule

      headerBackground.Add(headerLabel);
      _mainWindow.Add(headerBackground);
      // ---------------------------------

      // Native inner box container required for padding
      VisualElement windowBox = new VisualElement();

      windowBox.AddToClassList("box");
      // Override the native 650px max width for our wide matrix
      windowBox.style.maxWidth = StyleKeyword.None;
      windowBox.style.width = calculatedTotalWidth + 100;
      windowBox.style.minHeight = 694;

      // Override the native .box bottom padding (which defaults to 45px) 
      // to perfectly balance the 10px top margin of the lower buttons.
      windowBox.style.paddingBottom = 0f;

      // Initialize the view containers using native margin classes
      _mainView = new VisualElement();
      _mainView.AddToClassList("box__content-margin");
      _mainView.style.marginBottom = 0f; // Overrides the 20px from .box__content-margin

      _historyView = new VisualElement();
      _historyView.AddToClassList("box__content-margin");
      _historyView.style.marginBottom = 0f;

      _dependencyView = new VisualElement();
      _dependencyView.AddToClassList("box__content-margin");
      _dependencyView.style.marginBottom = 0f;

      // Set initial states
      _mainView.style.display = DisplayStyle.Flex;
      _historyView.style.display = DisplayStyle.None;
      _dependencyView.style.display = DisplayStyle.None;

      List<ModRecord> modTable = GenerateUnifiedList(_cachedMetadata);

      // --- POPULATE MAIN VIEW ---
      VisualElement topBar = CreateTopBar(modTable);
      _mainView.Add(topBar);

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
        TextNormal, true, null
      );
      headerRow.style.marginTop = 15f;
      headerRow.style.paddingRight = 20;
      listContainer.Add(headerRow);

      ScrollView scrollView = CreateScrollView();
      int rowIndex = 0;

      foreach (var modRecord in modTable)
      {
        bool isEven = (rowIndex % 2 == 0);
        modRecord.UpdateStatus();
        UnityEngine.Color dynamicRowColor = modRecord.GetStatusColor();

        VisualElement entryRow = CreateRow(
          modRecord.ModName,
          modRecord.ModId,
          modRecord.VersionFolder,
          modRecord.MinimumGameVersion,
          modRecord.SavedState == ModState.Enabled ? modRecord.SavedVersion : "-",
          modRecord.Version,
          modRecord.Status.ToString(),
          GetStateString(modRecord.CurrentState),
          GetStateString(modRecord.SavedState),
          GetStateString(modRecord.TargetState),
          dynamicRowColor,
          false,
          modRecord,
          isEven
        );
        scrollView.Add(entryRow);
        rowIndex++;
      }

      listContainer.Add(scrollView);
      _mainView.Add(listContainer);

      // Dedicated container for legacy utility buttons
      VisualElement legacyButtonContainer = new VisualElement();
      legacyButtonContainer.style.flexDirection = FlexDirection.Row;
      legacyButtonContainer.style.justifyContent = Justify.Center;

      legacyButtonContainer.style.marginTop = 20f;
      legacyButtonContainer.style.marginBottom = 42f;
      _mainView.Add(legacyButtonContainer);

      InjectToolbarButtons(legacyButtonContainer, modTable);

      // Bundle views into the window box
      windowBox.Add(_mainView);
      windowBox.Add(_historyView);
      windowBox.Add(_dependencyView);
      _mainWindow.Add(windowBox);

      // Native close button placed directly on the sliced border frame
      Button closeButton = new Button();
      closeButton.AddToClassList("close-button");
      _mainWindow.Add(closeButton);
      closeButton.RegisterCallback<ClickEvent>(evt => OnUICancelled());

      _rootElement.Add(_mainWindow);

      // 3. Bottom Dock (Native NineSlice Implementation)
      _bottomDock = new NineSliceVisualElement();
      _bottomDock.AddToClassList("content-row-centered--no-grow");
      _bottomDock.AddToClassList("sliced-border");
      _bottomDock.AddToClassList("sliced-border--nontransparent");
      _bottomDock.style.marginTop = 10f;

      // Increased padding to make the bar taller and more substantial
      _bottomDock.style.paddingTop = 36f;
      _bottomDock.style.paddingBottom = 36f;
      _bottomDock.style.paddingLeft = 36f;
      _bottomDock.style.paddingRight = 36f;

      _bottomDock.style.flexGrow = 0; // Explicitly stop growth
      _bottomDock.style.flexShrink = 0;

      _btnMain = new NineSliceButton { text = "Mods List" };
      _btnMain.RegisterCallback<ClickEvent>(evt => SwitchView(0));
      ApplyStandardButtonStyles(_btnMain);
      _btnMain.style.marginLeft = 5f;
      _btnMain.style.marginRight = 5f;

      _btnHistory = new NineSliceButton { text = "Steam History" };
      _btnHistory.RegisterCallback<ClickEvent>(evt => SwitchView(1));
      ApplyStandardButtonStyles(_btnHistory);
      _btnHistory.style.marginLeft = 5f;
      _btnHistory.style.marginRight = 5f;

      _btnAudit = new NineSliceButton { text = "Dependencies" };
      _btnAudit.RegisterCallback<ClickEvent>(evt => SwitchView(2));
      ApplyStandardButtonStyles(_btnAudit);
      _btnAudit.style.marginLeft = 5f;
      _btnAudit.style.marginRight = 5f;

      _bottomDock.Add(_btnMain);
      _bottomDock.Add(_btnHistory);
      _bottomDock.Add(_btnAudit);

      _rootElement.Add(_bottomDock);

      ApplyFilters();
      UpdateEnabledStats();

      // Trigger this once to correctly highlight the first button at launch
      SwitchView(0);

      return _rootElement;
    }

    private void SwitchView(int viewIndex)
    {
      _mainView.style.display = viewIndex == 0 ? DisplayStyle.Flex : DisplayStyle.None;
      _historyView.style.display = viewIndex == 1 ? DisplayStyle.Flex : DisplayStyle.None;
      _dependencyView.style.display = viewIndex == 2 ? DisplayStyle.Flex : DisplayStyle.None;

      if (_btnMain != null && _btnHistory != null && _btnAudit != null)
      {
        // Toggle only the vanilla "selected" state style onto your standard thick buttons
        _btnMain.EnableInClassList("selected", viewIndex == 0);
        _btnHistory.EnableInClassList("selected", viewIndex == 1);
        _btnAudit.EnableInClassList("selected", viewIndex == 2);
      }
    }

    private void InjectToolbarButtons(VisualElement container, List<ModRecord> modTable)
    {
      string[] buttonTexts = new string[]
      {
        _loc.T("Calloatti.SyncModsPro.Button.SaveProfile"),
        _loc.T("Calloatti.SyncModsPro.Button.StrictFlipOn"),
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

      List<NineSliceButton> createdButtons = new List<NineSliceButton>();

      EventCallback<ClickEvent>[] buttonActions = new EventCallback<ClickEvent>[]
      {
        evt => HandleSaveProfileClick(),
        evt => HandleStrictFlipClick(createdButtons[1]),
        evt => HandleSyncClick(modTable),
        evt => HandleRestartClick(),
        evt => HandleRestartLoadClick(), // Removed modTable parameter here
        evt => OnUIConfirmed()
      };

      for (int i = 0; i < buttonTexts.Length; i++)
      {
        NineSliceButton utilityBtn = new NineSliceButton();
        utilityBtn.text = buttonTexts[i];

        ApplyStandardButtonStyles(utilityBtn);

        if (i == buttonTexts.Length - 1 && _cachedCallback == null)
        {
          utilityBtn.SetEnabled(false);
        }

        utilityBtn.RegisterCallback(buttonActions[i]);
        AttachButtonTooltipBehavior(utilityBtn, tooltipKeys[i]);

        createdButtons.Add(utilityBtn);
        container.Add(utilityBtn);
      }
    }

    private void ApplyStandardButtonStyles(NineSliceButton btn)
    {
      btn.AddToClassList("menu-button");
      btn.AddToClassList("menu-button--medium");

      btn.style.minWidth = 150;
      btn.style.maxWidth = 150;
      btn.style.marginLeft = 5;
      btn.style.marginRight = 5;
      btn.style.overflow = Overflow.Hidden;
      btn.style.textOverflow = TextOverflow.Ellipsis;
    }
  }
}