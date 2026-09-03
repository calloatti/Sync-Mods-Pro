using System;
using System.Collections.Generic;
using System.Linq;
using Timberborn.CoreUI;
using Timberborn.GameSaveRepositorySystem;
using Timberborn.GameSaveRepositorySystemUI;
using Timberborn.GameSceneLoading;
using Timberborn.InputSystem;
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
    private ScrollView _scrollView;

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
    private readonly KeyboardListener _keyboardListener;
    private readonly WorkshopManager _workshopManager;
    private readonly WorkshopIdManager _workshopIdManager;
    private WorkshopViewController _currentWorkshopViewController;

    private Label _syncWarningLabel;

    public ModListBox(ILoc loc, ModRepository modRepository, DialogBoxShower dialogBoxShower, GameSceneLoader gameSceneLoader, GameSaveRepository gameSaveRepository, ValidatingGameLoader validatingGameLoader, PanelStack panelStack, KeyboardListener keyboardListener, WorkshopManager workshopManager, WorkshopIdManager workshopIdManager)
    {
      _loc = loc;
      _modRepository = modRepository;
      _dialogBoxShower = dialogBoxShower;
      _gameSceneLoader = gameSceneLoader;
      _gameSaveRepository = gameSaveRepository;
      _validatingGameLoader = validatingGameLoader;
      _panelStack = panelStack;
      _keyboardListener = keyboardListener;
      _workshopManager = workshopManager;       // NEW
      _workshopIdManager = workshopIdManager;   // NEW
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

      // Hook directly into Timberborn's keyboard listener
      _keyboardListener.KeyPressed += OnKeyPressed;

      _panelStack.HideAndPushOverlay(this);
    }

    public bool OnUIConfirmed()
    {
      if (_cachedCallback != null)
      {
        CustomTooltipManager.HideTooltip();

        _keyboardListener.KeyPressed -= OnKeyPressed;

        _panelStack.Pop(this);

        Action tempCallback = _cachedCallback;
        _cachedCallback = null;
        _cachedMetadata = null;
        _currentSaveReference = null;

        tempCallback.Invoke();

        return true;
      }

      return false;
    }

    public void OnUICancelled()
    {
      CustomTooltipManager.HideTooltip();

      _keyboardListener.KeyPressed -= OnKeyPressed;

      _panelStack.Pop(this);

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

      // CHANGED: Replace _filterByTargetState with the new mutually exclusive variables
      _filterChecked = false;
      _filterUnchecked = false;

      int calculatedTotalWidth = GetTotalTableWidth();

      // ... remainder of the method remains unchanged
      // 1. Transparent Root Wrapper
      _rootElement = new VisualElement();
      _rootElement.style.flexDirection = FlexDirection.Column;
      _rootElement.style.alignItems = Align.Center;
      _rootElement.style.justifyContent = Justify.Center;
      _rootElement.style.width = Length.Percent(100);
      _rootElement.style.height = Length.Percent(100);

      StyleSheet commonStyle = Resources.Load<StyleSheet>("UI/Views/Common/CommonStyle");
      if (commonStyle != null)
      {
        _rootElement.styleSheets.Add(commonStyle);
      }

      // --- THE LOGICAL APPROACH ---
      // We wrap the Main Window and the Side Dock together. 
      // The Root Element centers this wrapper perfectly.
      VisualElement alignmentWrapper = new VisualElement();

      // 2. Main Window (Native NineSlice Implementation)
      _mainWindow = new NineSliceVisualElement();
      _mainWindow.AddToClassList("content-centered");
      _mainWindow.AddToClassList("sliced-border");
      _mainWindow.AddToClassList("sliced-border--nontransparent");

      // --- ADD THE TOP CENTER BANNER ---
      NineSliceVisualElement headerBackground = new NineSliceVisualElement();
      headerBackground.AddToClassList("capsule-header");

      headerBackground.style.justifyContent = Justify.Center;
      headerBackground.style.alignItems = Align.Center;
      headerBackground.style.top = -10;

      Label headerLabel = new Label("Sync Mods");
      headerLabel.AddToClassList("capsule-header__text");
      headerLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
      headerLabel.style.top = -2;

      headerBackground.Add(headerLabel);
      _mainWindow.Add(headerBackground);
      // ---------------------------------

      VisualElement windowBox = new VisualElement();

      windowBox.AddToClassList("box");
      windowBox.style.maxWidth = StyleKeyword.None;
      windowBox.style.width = calculatedTotalWidth + 100;
      windowBox.style.minHeight = 694;
      windowBox.style.paddingBottom = 0f;

      _mainView = new VisualElement();
      _mainView.AddToClassList("box__content-margin");
      _mainView.style.marginBottom = 0f;

      _historyView = new VisualElement();
      _historyView.AddToClassList("box__content-margin");
      _historyView.style.marginBottom = 0f;

      _dependencyView = new VisualElement();
      _dependencyView.AddToClassList("box__content-margin");
      _dependencyView.style.marginBottom = 0f;

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

      _scrollView = CreateScrollView();
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
        _scrollView.Add(entryRow);
        rowIndex++;
      }

      listContainer.Add(_scrollView);
      _mainView.Add(listContainer);

      // --- NEW LABEL INJECTION ---
      _syncWarningLabel = new Label();
      _syncWarningLabel.AddToClassList("text--default");
      _syncWarningLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
      _syncWarningLabel.style.marginTop = 10f;
      _syncWarningLabel.style.fontSize = 14;
      _mainView.Add(_syncWarningLabel);
      // ---------------------------

      VisualElement legacyButtonContainer = new VisualElement();
      legacyButtonContainer.style.flexDirection = FlexDirection.Row;
      legacyButtonContainer.style.justifyContent = Justify.Center;

      legacyButtonContainer.style.marginTop = 20f;
      legacyButtonContainer.style.marginBottom = 42f;
      _mainView.Add(legacyButtonContainer);

      InjectToolbarButtons(legacyButtonContainer, modTable);

      windowBox.Add(_mainView);
      windowBox.Add(_historyView);
      windowBox.Add(_dependencyView);
      _mainWindow.Add(windowBox);

      Button closeButton = new Button();
      closeButton.AddToClassList("close-button");
      _mainWindow.Add(closeButton);
      closeButton.RegisterCallback<ClickEvent>(evt => OnUICancelled());

      alignmentWrapper.Add(_mainWindow);

      // 3. Side Dock Wrapper (Absolute Positioning for Vertical Centering)
      VisualElement absoluteRightContainer = new VisualElement();
      absoluteRightContainer.style.position = Position.Absolute;
      absoluteRightContainer.style.left = Length.Percent(100); // Anchor perfectly to the right edge
      absoluteRightContainer.style.top = 0f;
      absoluteRightContainer.style.bottom = 0f; // Forces container to match main window height
      absoluteRightContainer.style.marginLeft = 10f; // Visual gap
      absoluteRightContainer.style.flexDirection = FlexDirection.Column;
      absoluteRightContainer.style.justifyContent = Justify.Center; // Vertically center the inner dock!

      // Inner Dock (Maintains strict height)
      _bottomDock = new NineSliceVisualElement();
      _bottomDock.style.flexDirection = FlexDirection.Column;
      _bottomDock.style.justifyContent = Justify.Center;
      _bottomDock.style.alignItems = Align.Center;
      _bottomDock.AddToClassList("sliced-border");
      _bottomDock.AddToClassList("sliced-border--nontransparent");
      _bottomDock.style.paddingTop = 36f;
      _bottomDock.style.paddingBottom = 36f;
      _bottomDock.style.paddingLeft = 36f;
      _bottomDock.style.paddingRight = 36f;
      _bottomDock.style.flexGrow = 0;
      _bottomDock.style.flexShrink = 0;

      _btnMain = new NineSliceButton { text = "Mods List" };
      _btnMain.RegisterCallback<ClickEvent>(evt => SwitchView(0));
      ApplyStandardButtonStyles(_btnMain);
      _btnMain.style.marginTop = 5f;
      _btnMain.style.marginBottom = 5f;
      _btnMain.style.marginLeft = 0f;
      _btnMain.style.marginRight = 0f;

      _btnHistory = new NineSliceButton { text = "Steam History" };
      _btnHistory.RegisterCallback<ClickEvent>(evt => SwitchView(1));
      ApplyStandardButtonStyles(_btnHistory);
      _btnHistory.style.marginTop = 5f;
      _btnHistory.style.marginBottom = 5f;
      _btnHistory.style.marginLeft = 0f;
      _btnHistory.style.marginRight = 0f;

      _btnAudit = new NineSliceButton { text = "Dependencies" };
      _btnAudit.RegisterCallback<ClickEvent>(evt => SwitchView(2));
      ApplyStandardButtonStyles(_btnAudit);
      _btnAudit.style.marginTop = 5f;
      _btnAudit.style.marginBottom = 5f;
      _btnAudit.style.marginLeft = 0f;
      _btnAudit.style.marginRight = 0f;

      _bottomDock.Add(_btnMain);
      _bottomDock.Add(_btnAudit);
      _bottomDock.Add(_btnHistory);

      absoluteRightContainer.Add(_bottomDock);
      alignmentWrapper.Add(absoluteRightContainer);

      _rootElement.Add(alignmentWrapper);

      ApplyFilters();
      UpdateEnabledStats();

      SwitchView(0);

      // Give the new controller the container, the raw list, and the tab button. It handles the rest.
      new DependencyViewController(_loc).Initialize(_dependencyView, modTable, _btnAudit);

      _currentWorkshopViewController?.Cleanup();
      _currentWorkshopViewController = new WorkshopViewController(_loc, _workshopManager);
      _currentWorkshopViewController.Initialize(_historyView, modTable, _btnHistory);

      // --- ADDED: Dynamic Size Matcher ---
      // This waits for Unity to finish calculating the layout math, 
      // then forces the other panels to match the exact dimensions of the Main View.
      _mainView.RegisterCallback<GeometryChangedEvent>(evt =>
      {
        if (evt.newRect.height > 0 && evt.newRect.width > 0)
        {
          _historyView.style.height = evt.newRect.height;
          _historyView.style.width = evt.newRect.width;

          _dependencyView.style.height = evt.newRect.height;
          _dependencyView.style.width = evt.newRect.width;
        }
      });
      // -----------------------------------

      return _rootElement;
    }

    private void SwitchView(int viewIndex)
    {
      _mainView.style.display = viewIndex == 0 ? DisplayStyle.Flex : DisplayStyle.None;
      _historyView.style.display = viewIndex == 1 ? DisplayStyle.Flex : DisplayStyle.None;
      _dependencyView.style.display = viewIndex == 2 ? DisplayStyle.Flex : DisplayStyle.None;

      if (_btnMain != null && _btnHistory != null && _btnAudit != null)
      {
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
        evt => HandleRestartClick(evt),
        evt => HandleRestartLoadClick(),
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