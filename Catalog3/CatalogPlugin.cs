
using Leap.Unity;
using SimpleJSON;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR;
using juniperD.Services;
using juniperD.Models;
using juniperD.Contracts;
using juniperD.Utils;
using MVR.FileManagementSecure;

namespace juniperD.StatefullServices
{

	public class CatalogPlugin : MVRScript
	{
		const string EXPAND_WITH_MORE_ROWS = "rows";
		const string EXPAND_WITH_MORE_COLUMNS = "columns";

		const string ATOM_TYPE_SESSION = "SessionPluginManager";
		const string ATOM_TYPE_PERSON = "Person";
		const string ATOM_TYPE_OBJECT = "Other";

		const string SELECTION_METHOD_RANDOM = "random";
		const string SELECTION_METHOD_SEQUENCE = "sequence";

		string _atomType;

		string _fileExtension = "catalog3";
		List<string> _catalogModes = new List<string>();

		#region PluginInfo
		public string pluginAuthor = "juniperD";
		public string pluginName = "Catalog3";
		public string pluginVersion = Global.HOST_PLUGIN_SYMANTIC_VERSION;
		public string pluginDate = "10/09/2020";
		public string pluginDescription = @"Create a catalog of the current scene";
		#endregion
		// Config...
		protected bool _debugMode = false;

		protected float _defaultNumberOfCatalogColumns = 10;
		protected float _defaultNumberOfCatalogRows = 1;
		protected float _defaultNumberOfCatalogEntries = 4;
		protected float _defaultFramesBetweenCaptures = 50;
		protected JSONStorableBool _firstEntryIsCurrentLook;
		protected Transform _characterAnchorTransform;
		protected int _catalogRowsCount = 2;
		protected int _defaultFrameSize = 200;
		protected int _defaultBorderWidth = 10;
		protected Color _defaultBorderColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
		protected Color _borderSelectedColor = new Color(1, 0, 1, 0.8f);
		protected int _relativeBorderWidth = 10;
		protected bool _defaultMinimize = false;
		protected bool _vrMode = XRDevice.isPresent;

		protected Catalog _catalog = new Catalog();
		protected Catalog _trashCatalog = new Catalog();

		// Catalogger settings...
		protected JSONStorableFloat _waitFramesBetweenCaptureJSON;
		protected JSONStorableBool _anchorOnHud;
		protected JSONStorableStringChooser _anchorOnAtom;
		protected JSONStorableBool _minimizeUi;

		// General state
		string _lastCatalogDirectory = "Saves/scene/SavedCatalogs";
		protected bool _updateLoopEnabled = true;

		// Screen-Capture control-state...
		protected Camera _captureCamera;
		protected int _captureWidth = 1000;
		protected int _captureHeight = 1000;
		protected bool _hudWasVisible;
		protected string _baseFileFormat;
		protected RenderTexture _originalCameraTexture;
		protected int _createCatalogEntry_Step = 0;
		protected int _skipFrames = 0;
		protected UIDynamicButton _floatingTriggerButton;
		protected Vector3 _floatingControlUiPosition;
		protected Vector3 _catalogUnHiddenPosition;
		protected Mutation _nextMutation;
		protected static string _generateButtonInitText = "Generate Faces";
		public CaptureRequest _currentCaptureRequest = null;
		public List<CaptureRequest> _currentCaptureRequestList = new List<CaptureRequest>();
		protected Action<DragHelper> _onCatalogDragFinishedEvent;
		Dictionary<string, DragHelper> _positionTrackers = new Dictionary<string, DragHelper>();

		// Create objects control state...
		List<string> _atomsIncubatingQueue = new List<string>();

		// Catalog state...
		JSONStorableStringChooser _catalogMode;
		public JSONStorableString _catalogName;
		public JSONStorableString _catalogLocalFileName;
		JSONStorableString _catalogRelativePath;
		JSONStorableFloat _mainWindowPositionX;
		JSONStorableFloat _mainWindowPositionY;

		// Capture-mode control-state...
		public JSONStorableBool _overlayMutations;
		protected JSONStorableBool _catalogCaptureHair;
		protected JSONStorableBool _catalogCaptureClothes;
		protected JSONStorableBool _catalogCaptureMorphs;
		protected JSONStorableBool _catalogCapturePose;
		protected JSONStorableBool _catalogCaptureDynamicItems;

		// Controls-state...
		protected JSONStorableBool _uiVisible;
		//protected JSONStorableStringChooser _expandDirection;
		protected JSONStorableBool _triggerButtonVisible;
		protected JSONStorableBool _alwaysFaceMe;
		protected JSONStorableFloat _catalogRowsCountJSON;
		protected JSONStorableFloat _catalogColumnsCountJSON;
		protected JSONStorableFloat _catalogTransparencyJSON;
		public JSONStorableFloat CatalogEntryFrameSize;
		protected JSONStorableFloat _catalogRequestedEntriesCountJSON;
		protected JSONStorableStringChooser _activeCameraName;
		protected UIDynamicPopup _cameraSelector;
		public JSONStorableBool _cycleEntriesOnInterval;
		public JSONStorableFloat _cycleEntriesInterval;
		public JSONStorableBool _playOnce;
		public int _numberOfEntriesToPlay;
		public JSONStorableFloat _defaultMorphTransitionTimeSeconds;
		protected JSONStorableString _entrySelectionMethod;
		protected Atom _handleObjectForCatalog;
		protected Color _dynamicButtonCheckColor; //= Color.red;
		protected Color _dynamicButtonUnCheckColor; //= Color.green;
		private int _nextAtomIndex;

		// Display-state...
		Atom _parentAtom;
		DebugService _debugService;
		public MutationsService _mutationsService;
		public ImageLoader _imageLoaderService;
		public MannequinHelper _mannequinService;
		CatalogUiHelper _catalogUi;
		CatalogUiHelper _floatingControlsUi;
		CatalogUiHelper _windowUi;

		// Confirmation dialog...
		protected GameObject _dynamicConfirmPanel;
		protected UIDynamicTextField _dynamicConfirmLabel;
		protected Action _lastConfirmAction;

		// Confirmation dialog...
		protected GameObject _dynamicHelpPanel;
		protected UIDynamicTextField _dynamicHelpLabel;

		// Popup select list
		public GameObject _dynamicSelectList;
		public List<EntrySubItem> _dynamicListItems = new List<EntrySubItem>();
		VerticalLayoutGroup _selectListVLayout;

		// Dynamic UI
		public DynamicMainWindow _mainWindow;

		// Dynamic Picker select list
		public List<DynamicMannequinPicker> _mannequinPickers = new List<DynamicMannequinPicker>();
		private bool _animationFeaturesVisible;

		public JSONStorableFloat _catalogUiSizeJSON { get; private set; }
		public object Enums { get; private set; }

		public override void PostRestore()
		{
			base.PostRestore();

			if (_catalogName.val == "") // ...This catalog has not yet been initialized
			{
				FirstTimeInitialization();
			}
			else // ...This catalog has already been initialized, this is loaded from file...
			{
				if (containingAtom.type == ATOM_TYPE_SESSION) return; //...don't restore session plugins

				// Restore Temp catalog...
				if (!string.IsNullOrEmpty(_catalogLocalFileName.val))
				{
					var filePath = GetLocalCatalogFilePath(_catalogLocalFileName.val);
					_catalog = LoadCatalogFromFile(filePath, false);
					if (containingAtom.type == "Person")
					{
						ApplyPreviouslyActiveMutation();
					}
				}

				// Set layout positions
				if (_anchorOnHud.val)
				{
					// ..._catalogPosition.val is already set
					AnchorOnHud();
				}
				else
				{
					// ..._catalogPosition.val is already set
					AnchorOnAtom();
				}
			}

			SetMinimizedState();
			UpdateUiForMode();
		}

		private string GetLocalCatalogFilePath(string filename)
		{
			return $"{GetSceneDirectoryPath()}/{filename}.{_fileExtension}";
		}

		private void ApplyPreviouslyActiveMutation()
		{
			for (var i = 0; i < _catalog.Entries.Count(); i++)
			{
				if (_catalog.Entries[i].Mutation.IsActive)
				{
					var mutation = _catalog.Entries[i].Mutation;
					_mutationsService.ApplyMutation(ref mutation);
					_mainWindow.CurrentCatalogEntry = _catalog.Entries[i];
				}
			}
		}

		private void FirstTimeInitialization()
		{
			// ...set the catalog name, with no callback (to prevent recursing back into this function)
			_catalogName.valNoCallback = GetNewCatalogName();
			_catalogLocalFileName.val = GetNewLocalFileName(_catalogName.val);
			_catalogMode.val = CatalogModeEnum.CATALOG_MODE_SCENE;
			if (_vrMode)
			{
				_captureCamera = Camera.allCameras.ToList().FirstOrDefault(c => c.name == "Camera (eye)");
			}
			UpdateCaptureClothesState(true);
			UpdateCaptureHairState(false);
			UpdateCaptureMorphsState(false);

			ResetCatalogPositionForHud();
			AnchorOnHud();

			UpdateUiForMode();
			CreateSceneCatalogEntries(GetSceneDirectoryPath());
		}

		private string GetNewLocalFileName(string baseName, bool addUniqueKey = true)
		{
			if (addUniqueKey == true) baseName += "_" + GetUniqueName();
			return baseName;
		}

		private string GetNewCatalogName()
		{
			// Create a unique name for this catalog instance (there may be multiple instances within a scene if the user adds more catalogs)
			const string searchFor = "plugin#";
			string lastHalf = this.name.Substring(this.name.IndexOf(searchFor) + searchFor.Length);
			string numberStr = lastHalf.Substring(0, lastHalf.IndexOf("_"));
			return containingAtom.name + "." + numberStr;
		}

		private void ResetCatalogPositionForPerson()
		{
			var anchor = GetPersonBasePositionOrDefault();
			//var baseX = (anchor == null) ? 0f : anchor.localPosition.x;
			//var baseY = (anchor == null) ? 0f : anchor.localPosition.y;
			//_windowUi.canvas.transform.position = new Vector3(baseX, baseY, 0);
			_mainWindowPositionX.val = -1000;
			_mainWindowPositionY.val = -1000;
		}

		public override void Init()
		{
			try
			{
				_imageLoaderService = new ImageLoader();

				switch (containingAtom.type)
				{
					case "SessionPluginManager":
						_atomType = ATOM_TYPE_SESSION;
						break;
					case "Person":
						_atomType = ATOM_TYPE_PERSON;
						break;
					default:
						_atomType = ATOM_TYPE_OBJECT;
						break;
				}

				_catalogModes = new List<string>();
				_catalogModes.Add(CatalogModeEnum.CATALOG_MODE_VIEW);
				_catalogModes.Add(CatalogModeEnum.CATALOG_MODE_SCENE);
				_catalogModes.Add(CatalogModeEnum.CATALOG_MODE_CAPTURE);
				_catalogModes.Add(CatalogModeEnum.CATALOG_MODE_MUTATIONS);
				if (_atomType == ATOM_TYPE_OBJECT) _catalogModes.Add(CatalogModeEnum.CATALOG_MODE_OBJECT);
				_catalogModes.Add(CatalogModeEnum.CATALOG_MODE_SESSION);

				if (_debugMode)
				{
					_debugService = new DebugService();
					_debugService.Init(this);
				}

				_dynamicButtonCheckColor = new Color(0.7f, 0.7f, 0.5f, 1);
				_dynamicButtonUnCheckColor = new Color(0.25f, 0.25f, 0.25f, 1);

				_currentCaptureRequest = new CaptureRequest();

				SuperController sc = SuperController.singleton;
				_baseFileFormat = SuperController.singleton.fileBrowserUI.fileFormat;


				pluginLabelJSON.setJSONCallbackFunction = (newVal) =>
				{
					_catalogName.SetVal(newVal.val);
				};

				SuperController.singleton.fileBrowserUI.selectButton.onClick.AddListener(() =>
				{
					var targetFilePath = GetLocalCatalogFilePath(_catalogLocalFileName.val);
					SaveCatalogToFile(targetFilePath);
				});

				// Catalog options...
				CreateCatalogConfigUi();

				CreateSpacer();

				// Capture settings...
				CreateCaptureConfigUi();

				CreateSpacer();

				// Mutations config UI...
				CreateMutationsConfigUi();

				CreateSpacer();

				CreateDebugConfigUi();
				//if (containingAtom.mainController == null)
				//{
				//	SuperController.LogError("Please add this plugin to a PERSON atom.");
				//	return;
				//}

				_cycleEntriesOnInterval.val = false;

				//generate UI
				_mainWindowPositionX = new JSONStorableFloat("catalogPositionX", 0, 0, 0, false);
				_mainWindowPositionX.storeType = JSONStorableFloat.StoreType.Full;
				_mainWindowPositionX.isStorable = true;
				_mainWindowPositionX.isRestorable = true;
				_mainWindowPositionX.restoreTime = JSONStorableParam.RestoreTime.Normal;
				RegisterFloat(_mainWindowPositionX);

				_mainWindowPositionY = new JSONStorableFloat("catalogPositionY", 0, 0, 0, false);
				_mainWindowPositionY.storeType = JSONStorableFloat.StoreType.Full;
				_mainWindowPositionY.isStorable = true;
				_mainWindowPositionY.isRestorable = true;
				_mainWindowPositionY.restoreTime = JSONStorableParam.RestoreTime.Normal;
				RegisterFloat(_mainWindowPositionY);

				_floatingControlsUi = new CatalogUiHelper(this, 0, 0, Color.clear);
				_floatingControlsUi.canvas.transform.Rotate(new Vector3(0, 180, 0));
				_catalogUi = new CatalogUiHelper(this, 0, 0, Color.clear);
				_catalogUi.canvas.transform.Rotate(new Vector3(0, 180, 0));
				_windowUi = new CatalogUiHelper(this, 0, 0, Color.clear);
				_windowUi.canvas.transform.Rotate(new Vector3(0, 180, 0));

				_mainWindow = new DynamicMainWindow(_windowUi);

				_mainWindow.ParentWindowContainer = CatalogUiHelper.CreatePanel(_windowUi.canvas.gameObject, 0, 0, 0, 0, Color.clear, Color.clear); ;
				_mainWindow.SubWindow = CatalogUiHelper.CreatePanel(_mainWindow.ParentWindowContainer, 0, 0, 0, 0, Color.clear, Color.clear);
				_mainWindow.CatalogRowContainer = _catalogUi.CreateUIPanel(_mainWindow.SubWindow, 0, 0, "topleft", 1, 1, Color.clear);
				_mainWindow.CatalogRowsVLayout = _catalogUi.CreateVerticalLayout(_mainWindow.CatalogRowContainer.gameObject, _relativeBorderWidth);

				_mainWindow.CatalogColumnContainers = new List<GameObject>() { CreateNewCatalogColumnContainer(0) };
				

				CreateDynamicUi(_mainWindow);

				if (!SuperController.singleton.isLoading)
				{
					// ...this indicates that the Init was not called as part of the scene being loaded, which means it must be just after the plugin was added.
					FirstTimeInitialization();
				}
				MinimizeControlPanel();
			}
			catch (Exception e)
			{
				SuperController.LogError(e.ToString());
			}
		}

		private GameObject CreateNewCatalogColumnContainer(int rowIndex)
		{
			var totalFrameSize = (int)GetTotalFrameSize();
			var columnContainer = _catalogUi.CreateUIPanel(_mainWindow.SubWindow, rowIndex * totalFrameSize, 0, "topleft", 1, 1, Color.clear);
			_mainWindow.CatalogColumnsHLayout = _catalogUi.CreateHorizontalLayout(columnContainer, _relativeBorderWidth);
			return columnContainer;
		}

		public void Start()
		{

		}

		private void CreateDynamicUi(DynamicMainWindow window)
		{
			try
			{
				_mannequinService = new MannequinHelper(this, _windowUi);

				CreateDynamicPanel_MainWindow(window);
				CreateDynamicButton_Trigger();

				// DYNAMIC UI...
				CreateDynamicButton_ResetCatalog(window);
				CreateDynamicButton_ZoomIn(window);
				CreateDynamicButton_ZoomOut(window);
				CreateDynamicButton_ScrollUp(window);
				CreateDynamicButton_ScrollDown(window);
				CreateDynamicButton_Sort(window);

				// Scene helper buttons...
				CreateDynamicButton_SelectSceneAtom(window);
				CreateDynamicButton_ResetPivot(window);
				CreateDynamicButton_CreateMannequinPicker(window);

				CreateDynamicButton_Modes();

				// File Menu...
				CreateDynamicButton_ToggleControlPanel();
				CreateDynamicButton_Spacer(_mainWindow.SubPanelFileMenu, 125, 35);
				CreateDynamicButton_QuickLoad();
				CreateDynamicButton_Save();
				CreateDynamicButton_Load();

				// Shortcut menu...
				CreateDynamicButton_ToggleControlPanelShortcut();
				CreateDynamicButton_QuickloadShortcut();
				CreateDynamicButton_LoadShortcut();
				CreateDynamicButton_HideCatalogShortcut();

				// Capture buttons...
				CreateDynamicButton_Play();
				CreateDynamicButton_Capture();
				CreateDynamicButton_CaptureAdditionalAtom();
				CreateDynamicButton_MergeFavoritedEntries();
				CreateDynamicButton_ToggleAnimFeatures();
				CreateDynamicButton_Capture_Clothes();
				CreateDynamicButton_Capture_Morphs();
				CreateDynamicButton_Capture_Hair();
				CreateDynamicButton_Capture_Pose();
				CreateDynamicButton_RemoveAllClothing();
				CreateDynamicButton_RemoveAllHair();
				CreateDynamicButton_OpenScenesFolder();

				// Message UI...

				CreateDynamicPanel_RightInfoPanel();
				CreateDynamicButton_MinimizeSubItemPanel();
				CreateDynamicPanel_LeftInfo();
				CreateDynamicButton_LeftSideLabel();
				CreateDynamicButton_Tooltip();
				CreateDynamicButton_SelectList();
				CreateDynamicButton_DynamicHelp();
				CreateDynamicButton_PopupMessage();
				CreateDynamicButton_DynamicConfirm();
				CreateDynamicButton_DebugPanel();
				if (_debugMode) ShowDebugPanel();
				if (_debugMode) CreateDynamicButton_ShowDebugButton(window);

			}
			catch (Exception e)
			{
				SuperController.LogError(e.ToString());
			}
		}


		private void CreateCaptureConfigUi()
		{

			CreateButton("Capture").button.onClick.AddListener(() =>
			{
				RequestNextCaptureSet(1, CaptureRequest.MUTATION_AQUISITION_MODE_CAPTURE);
			});

			_catalogCaptureClothes = new JSONStorableBool("Capture Clothes", true);
			RegisterBool(_catalogCaptureClothes);
			CreateToggle(_catalogCaptureClothes);
			_catalogCaptureClothes.toggle.onValueChanged.AddListener((value) =>
			{
				UpdateCaptureClothesState(value);
			});

			_catalogCaptureHair = new JSONStorableBool("Capture Hair", false);
			RegisterBool(_catalogCaptureHair);
			CreateToggle(_catalogCaptureHair);
			_catalogCaptureHair.toggle.onValueChanged.AddListener((value) =>
			{
				UpdateCaptureHairState(value);
			});

			//_captureMutationMorphs = new JSONStorableBool("Capture generated morphs", true);
			//RegisterBool(_captureMutationMorphs);
			//CreateToggle(_captureMutationMorphs);
			//_captureMutationMorphs.toggle.onValueChanged.AddListener((value) =>
			//{
			//	_mutationsService.CaptureFaceGenMorphs = value;
			//});

			_catalogCaptureMorphs = new JSONStorableBool("Capture active morphs", false);
			RegisterBool(_catalogCaptureMorphs);
			CreateToggle(_catalogCaptureMorphs);
			_catalogCaptureMorphs.toggle.onValueChanged.AddListener((value) =>
			{
				UpdateCaptureMorphsState(value);
			});

			_catalogCapturePose = new JSONStorableBool("Capture pose", false);
			RegisterBool(_catalogCapturePose);
			CreateToggle(_catalogCapturePose);
			_catalogCapturePose.toggle.onValueChanged.AddListener((value) =>
			{
				UpdateCapturePoseState(value);
			});
			//_catalogCaptureDynamicItems = new JSONStorableBool("Capture dynamic items", false);
			//RegisterBool(_catalogCaptureDynamicItems);
			//CreateToggle(_catalogCaptureDynamicItems);
			//_catalogCaptureDynamicItems.toggle.onValueChanged.AddListener((value) =>
			//{
			//	_mutationsService.MustCaptureDynamicItems = value;
			//});

			//CreateButton("Set base active morphs").button.onClick.AddListener(() =>
			//{
			//	_mutationsService.SetMorphBaseValuesForCurrentPerson();
			//});

			CreateButton("Remove unused items").button.onClick.AddListener(() =>
			{
				RemoveUnusedMutations();
			});

			CreateButton("Select custom image for entry").button.onClick.AddListener(() =>
			{
				AddCustomImageToCurrentEntry();
			});

			//var entryNameTextField = new JSONStorableString("Entry Name", "Some Text");
			//entryNameTextField.setCallbackFunction = new JSONStorableString.SetStringCallback((newString) =>
			//{
			//	UpdateNameForCurrentCatalogEntry(newString);
			//});
			//CreateTextField(entryNameTextField);

		}

		private void UpdateNameForCurrentCatalogEntry(string newString)
		{
			if (_mainWindow.CurrentCatalogEntry == null) SuperController.LogError("No catalog entry selected. Please select an entry");
			foreach (var entry in _catalog.Entries)
			{
				if (entry.UniqueName == newString)
				{
					SuperController.LogError("Entry cannot have the same name as another entry");
				}
			}
			_mainWindow.CurrentCatalogEntry.UniqueName = newString;
		}

		private void AddCustomImageToCurrentEntry()
		{
			try
			{
				_baseFileFormat = SuperController.singleton.fileBrowserUI.fileFormat;
				SuperController.singleton.ShowMainHUD();
				SuperController.singleton.fileBrowserUI.SetTextEntry(false);
				SuperController.singleton.fileBrowserUI.fileFormat = "jpg";
				SuperController.singleton.fileBrowserUI.defaultPath = GetSceneDirectoryPath();
				SuperController.singleton.fileBrowserUI.Show((filePath) =>
				{
					if (string.IsNullOrEmpty(filePath))
					{
						SuperController.singleton.fileBrowserUI.fileFormat = _baseFileFormat;
						return;
					}
					var texture = ImageLoader.GetFutureImageFromFile(filePath);
					if (_mainWindow.CurrentCatalogEntry == null) SuperController.LogMessage("REQUEST: No catalog entry selected. Please select an entry");
					_mainWindow.CurrentCatalogEntry.ImageInfo.ExternalPath = filePath;
					//_mainWindow._currentCatalogEntry.Mutation.Img_RGB24_W1000H1000_64bEncoded = filePath;
					_mainWindow.CurrentCatalogEntry.ImageInfo.Texture = texture;
					var image = _mainWindow.CurrentCatalogEntry.UiCatalogEntryPanel.GetComponent<Image>();
					Material mat = new Material(image.material);
					mat.SetTexture(filePath, texture);
					image.material = mat;
					image.material.mainTexture = texture;
					SuperController.singleton.fileBrowserUI.fileFormat = _baseFileFormat;
				});
			}
			catch (Exception e)
			{
				SuperController.LogError(e.ToString());
			}

		}

		private void RemoveUnusedMutations()
		{
			if (_mainWindow.CurrentCatalogEntry == null) return;
			//-----------------------
			foreach (var item in _mainWindow.CurrentCatalogEntry.Mutation.FaceGenMorphSet)
			{
				if (!item.Active)
				{
					RemoveToggle(item.UiToggle);
					//RemoveUiCatalogSubItem(item.DynamicCheckbox);
				}
			}
			_mainWindow.CurrentCatalogEntry.Mutation.FaceGenMorphSet = _mainWindow.CurrentCatalogEntry.Mutation.FaceGenMorphSet.Where(c => c.Active).ToList();
			//-----------------------
			foreach (var item in _mainWindow.CurrentCatalogEntry.Mutation.ClothingItems)
			{
				if (!item.Active)
				{
					RemoveToggle(item.UiToggle);
					//RemoveUiCatalogSubItem(item.DynamicCheckbox);
				}
			}
			_mainWindow.CurrentCatalogEntry.Mutation.ClothingItems = _mainWindow.CurrentCatalogEntry.Mutation.ClothingItems.Where(c => c.Active).ToList();
			//-----------------------
			foreach (var item in _mainWindow.CurrentCatalogEntry.Mutation.HairItems)
			{
				if (!item.Active)
				{
					RemoveToggle(item.UiToggle);
					//RemoveUiCatalogSubItem(item.DynamicCheckbox);
				}
			}
			_mainWindow.CurrentCatalogEntry.Mutation.HairItems = _mainWindow.CurrentCatalogEntry.Mutation.HairItems.Where(c => c.Active).ToList();
			//-----------------------
			foreach (var item in _mainWindow.CurrentCatalogEntry.Mutation.ActiveMorphs)
			{
				if (!item.Active)
				{
					RemoveToggle(item.UiToggle);
					//RemoveUiCatalogSubItem(item.DynamicCheckbox);
				}
			}
			_mainWindow.CurrentCatalogEntry.Mutation.ActiveMorphs = _mainWindow.CurrentCatalogEntry.Mutation.ActiveMorphs.Where(c => c.Active).ToList();
			//-----------------------
			foreach (var item in _mainWindow.CurrentCatalogEntry.Mutation.PoseMorphs)
			{
				if (!item.Active)
				{
					RemoveToggle(item.UiToggle);
					//RemoveUiCatalogSubItem(item.DynamicCheckbox);
				}
			}
			_mainWindow.CurrentCatalogEntry.Mutation.PoseMorphs = _mainWindow.CurrentCatalogEntry.Mutation.PoseMorphs.Where(c => c.Active).ToList();
			//-----------------------
		}

		private void CreateMutationsConfigUi()
		{

			CreateButton(_generateButtonInitText).button.onClick.AddListener(() =>
			{
				RequestNextCaptureSet((int)_catalogRequestedEntriesCountJSON.val, CaptureRequest.MUTATION_AQUISITION_MODE_SEQUENCE);
			});

			_catalogRequestedEntriesCountJSON = new JSONStorableFloat("Generate entries", _defaultNumberOfCatalogEntries, 1f, 500f);
			_catalogRequestedEntriesCountJSON.storeType = JSONStorableParam.StoreType.Full;
			RegisterFloat(_catalogRequestedEntriesCountJSON);
			var requestCountSlider = CreateSlider(_catalogRequestedEntriesCountJSON);
			requestCountSlider.valueFormat = "F0";
			requestCountSlider.slider.wholeNumbers = true;

			_waitFramesBetweenCaptureJSON = new JSONStorableFloat("Frames between captures", _defaultFramesBetweenCaptures, 0f, 1000f);
			_waitFramesBetweenCaptureJSON.storeType = JSONStorableParam.StoreType.Full;
			RegisterFloat(_waitFramesBetweenCaptureJSON);
			var framesSlider = CreateSlider(_waitFramesBetweenCaptureJSON);
			framesSlider.valueFormat = "F0";
			framesSlider.slider.wholeNumbers = true;

			_firstEntryIsCurrentLook = new JSONStorableBool("First capture is the current look", true);
			RegisterBool(_firstEntryIsCurrentLook);
			CreateToggle(_firstEntryIsCurrentLook);

			_mutationsService = new MutationsService();
			_mutationsService.Init(this);
			_mutationsService.MustCaptureHair = _catalogCaptureHair.val;
			_mutationsService.MustCaptureClothes = _catalogCaptureClothes.val;
			_mutationsService.MustCaptureFaceGenMorphs = true; //_captureMutationMorphs.val;
			_mutationsService.MustCaptureActiveMorphs = _catalogCaptureMorphs.val;
			_mutationsService.MustCapturePoseMorphs = _catalogCapturePose.val;
			//_mutationsService.MustCaptureDynamicItems = _catalogCaptureDynamicItems.val;
			//_mutationsService.SetMorphBaseValuesCheckpoint();
		}

		private void CreateDebugConfigUi()
		{

			CreateButton("Enable debug mode").button.onClick.AddListener(() =>
			{
				if (_debugMode == true) return; //...Debug mode already enabled
				_debugMode = true;
				_debugService = new DebugService();
				_debugService.Init(this);
				ShowDebugPanel();
				CreateDynamicButton_ShowDebugButton(_mainWindow);
			});

			CreateButton("Re-enable update loop").button.onClick.AddListener(() =>
			{
				_updateLoopEnabled = true;
			});

		}

		private void CreateCatalogConfigUi()
		{
			_catalogName = new JSONStorableString("CatalogName", "");
			RegisterString(_catalogName);
			_catalogName.storeType = JSONStorableParam.StoreType.Full;
			_catalogName.isStorable = true;
			_catalogName.isRestorable = true;
			_catalogName.restoreTime = JSONStorableParam.RestoreTime.Normal;

			_catalogLocalFileName = new JSONStorableString("CatalogPath", "");
			RegisterString(_catalogLocalFileName);
			_catalogLocalFileName.storeType = JSONStorableParam.StoreType.Full;
			_catalogLocalFileName.isStorable = true;
			_catalogLocalFileName.isRestorable = true;
			_catalogLocalFileName.restoreTime = JSONStorableParam.RestoreTime.Normal;

			_catalogRelativePath = new JSONStorableString("CatalogRelativePath", "");
			RegisterString(_catalogRelativePath);
			_catalogRelativePath.storeType = JSONStorableParam.StoreType.Full;
			_catalogRelativePath.isStorable = true;
			_catalogRelativePath.isRestorable = true;
			_catalogRelativePath.restoreTime = JSONStorableParam.RestoreTime.Normal;

			CreateButton("Show Catalog").button.onClick.AddListener(() =>
			{
				SetCatalogVisibility(true);
			});

			_catalogMode = new JSONStorableStringChooser("CatalogMode", _catalogModes, _catalogModes.First(), "Catalog Mode");
			_catalogMode.storeType = JSONStorableParam.StoreType.Full;
			RegisterStringChooser(_catalogMode);
			CreatePopup(_catalogMode);
			_catalogMode.setCallbackFunction = new JSONStorableStringChooser.SetStringCallback((chosenString) =>
			{
				UpdateUiForMode();
			});

			CreateButton("Reset Catalog").button.onClick.AddListener(() =>
			{
				ReinitializeCatalog(); // ...Reset catalog (from settings)
			});

			CreateButton("Remove missing Dependency").button.onClick.AddListener(() =>
			{
				CleanCatalog();
			});

			CreateButton("Make this a Dependency File").button.onClick.AddListener(() =>
			{
				MakeThisACatalogDependencyFile();
			});

			CreateSpacer();

			// Apply Entry functions.....................................
			_entrySelectionMethod = new JSONStorableString("Entry Selection Method", SELECTION_METHOD_SEQUENCE);
			RegisterString(_entrySelectionMethod);

			var applyEntryMoveNextAction = new JSONStorableAction("Apply entry move next", () => { ApplyEntryMoveNext(); });
			RegisterAction(applyEntryMoveNextAction);
			CreateButton("Apply entry and move next").button.onClick.AddListener(() =>
			{
				ApplyEntryMoveNext(); // ...Reset catalog (from settings)
				_entrySelectionMethod.val = SELECTION_METHOD_SEQUENCE;
			});

			var applyRandomEntry = new JSONStorableAction("Apply random entry", () => { ApplyRandomEntry(); });
			RegisterAction(applyRandomEntry);
			CreateButton("Apply random entry").button.onClick.AddListener(() =>
			{
				ApplyRandomEntry(); // ...Reset catalog (from settings)
				_entrySelectionMethod.val = SELECTION_METHOD_RANDOM;
			});

			var applyEntryAt = new JSONStorableFloat("Apply Entry At", 0, 0, 10000);
			RegisterFloat(applyEntryAt);
			var applyEntryAtSlider = CreateSlider(applyEntryAt);
			applyEntryAtSlider.valueFormat = "F0";
			applyEntryAtSlider.slider.wholeNumbers = true;
			applyEntryAtSlider.slider.onValueChanged.AddListener((newVal) =>
			{
				ApplyEntryAt((int)newVal); //...changing column count
			});

			_defaultMorphTransitionTimeSeconds = new JSONStorableFloat("Morph transition time (seconds)", 1f, 0, 10);
			RegisterFloat(_defaultMorphTransitionTimeSeconds);
			var morphTransitionTime = CreateSlider(_defaultMorphTransitionTimeSeconds);
			morphTransitionTime.valueFormat = "F1";

			_cycleEntriesOnInterval = new JSONStorableBool("Cycle entries on interval", false);
			RegisterBool(_cycleEntriesOnInterval);
			var cycleEntriesButton = CreateButton("Play >");
			cycleEntriesButton.buttonColor = _cycleEntriesOnInterval.val ? Color.red : new Color(0.8f, 1f, 0.8f);
			cycleEntriesButton.button.onClick.AddListener(() =>
			{
				var playState = ToggleCatalogPlay();
				cycleEntriesButton.buttonText.text = playState ? "Stop" : "Play >";
				cycleEntriesButton.buttonColor = playState ? Color.red : new Color(0.8f, 1f, 0.8f);
			});

			_cycleEntriesInterval = new JSONStorableFloat("...Interval (seconds)", 3, 0, 50);
			RegisterFloat(_cycleEntriesInterval);
			var applyEntryAfterSlider = CreateSlider(_cycleEntriesInterval);
			applyEntryAfterSlider.valueFormat = "F1";
			//applyEntryAfterSlider.slider.wholeNumbers = true;

			_playOnce = new JSONStorableBool("Play catalog once", false);
			RegisterBool(_playOnce);
			CreateToggle(_playOnce);

			CreateSpacer();

			_catalogColumnsCountJSON = new JSONStorableFloat("Catalog Columns", _defaultNumberOfCatalogColumns, 1f, 10f);
			_catalogColumnsCountJSON.storeType = JSONStorableParam.StoreType.Full;
			RegisterFloat(_catalogColumnsCountJSON);
			var rowsSlider = CreateSlider(_catalogColumnsCountJSON);
			rowsSlider.valueFormat = "F0";
			rowsSlider.slider.wholeNumbers = true;
			rowsSlider.slider.onValueChanged.AddListener((newVal) =>
			{
				RebuildCatalogFromEntriesCollection(); //...changing column count
			});


			_catalogRowsCountJSON = new JSONStorableFloat("Catalog Rows", _defaultNumberOfCatalogRows, 1f, 10f);
			_catalogRowsCountJSON.storeType = JSONStorableParam.StoreType.Full;
			RegisterFloat(_catalogRowsCountJSON);
			var colsSlider = CreateSlider(_catalogRowsCountJSON);
			colsSlider.valueFormat = "F0";
			colsSlider.slider.wholeNumbers = true;
			colsSlider.slider.onValueChanged.AddListener((newVal) =>
			{
				RebuildCatalogFromEntriesCollection(); //...changing row count
			});

			_catalogUiSizeJSON = new JSONStorableFloat("UI Size", 0, -100f, 100f);
			_catalogUiSizeJSON.storeType = JSONStorableParam.StoreType.Full;
			RegisterFloat(_catalogUiSizeJSON);
			var sizeSlider = CreateSlider(_catalogUiSizeJSON);
			sizeSlider.valueFormat = "F0";
			sizeSlider.slider.wholeNumbers = true;
			sizeSlider.slider.onValueChanged.AddListener((newVal) =>
			{
				ResizeUi(newVal); //...changing column count
			});

			//_expandDirection = new JSONStorableStringChooser("ExpandCatalogWithMore", new List<string> { EXPAND_WITH_MORE_ROWS, EXPAND_WITH_MORE_COLUMNS }, EXPAND_WITH_MORE_COLUMNS, "Expand with");
			//_expandDirection.storeType = JSONStorableParam.StoreType.Full;
			//RegisterStringChooser(_expandDirection);
			//CreatePopup(_expandDirection);
			//_expandDirection.setCallbackFunction = new JSONStorableStringChooser.SetStringCallback((chosenString) =>
			//{
			//	RebuildCatalogFromEntriesCollection(); //...changing catalog oriantation (expand by rows or columns)
			//});

			_catalogTransparencyJSON = new JSONStorableFloat("Opacity", 1, 0f, 1f);
			_catalogTransparencyJSON.storeType = JSONStorableParam.StoreType.Full;
			RegisterFloat(_catalogTransparencyJSON);
			var transparencySlider = CreateSlider(_catalogTransparencyJSON);
			transparencySlider.valueFormat = "F1";
			transparencySlider.slider.onValueChanged.AddListener((newVal) =>
			{
				var backPanelImage = _mainWindow.PanelBackground.GetComponent<Image>();
				backPanelImage.color = new Color(backPanelImage.color.r, backPanelImage.color.g, backPanelImage.color.b, newVal);
				foreach (var catalogEntry in _catalog.Entries)
				{
					var image = catalogEntry.UiCatalogEntryPanel.GetComponent<Image>();
					image.color = new Color(image.color.r, image.color.g, image.color.b, newVal);
				}
			});

			CatalogEntryFrameSize = new JSONStorableFloat("Frame Size", _defaultFrameSize, 100f, 1000f);
			CatalogEntryFrameSize.storeType = JSONStorableParam.StoreType.Full;
			RegisterFloat(CatalogEntryFrameSize);

			_minimizeUi = new JSONStorableBool("Minimize", _defaultMinimize);
			_minimizeUi.storeType = JSONStorableBool.StoreType.Full;
			RegisterBool(_minimizeUi);

			var cameraNames = Camera.allCameras.Select(c => c.name).ToList();
			cameraNames.Add("<< Refresh >>");
			var defaultCamera = _vrMode ? Camera.allCameras.FirstOrDefault(c => c.name == "Camera (eye)")?.name : cameraNames.First();
			_activeCameraName = new JSONStorableStringChooser("Active Camera", cameraNames, defaultCamera ?? cameraNames.First(), "Active Camera");
			_activeCameraName.storeType = JSONStorableParam.StoreType.Full;
			RegisterStringChooser(_activeCameraName);
			CreatePopup(_activeCameraName);
			_activeCameraName.setCallbackFunction = new JSONStorableStringChooser.SetStringCallback((chosenString) =>
			{
				if (chosenString == "<< Refresh >>")
				{
					cameraNames = Camera.allCameras.Select(c => c.name).ToList();
					cameraNames.Add("<< Refresh >>");
					_activeCameraName.choices = cameraNames;
					if (cameraNames.Count == 1) return;
					_activeCameraName.val = cameraNames.First();
				};
			});

			_uiVisible = new JSONStorableBool("Visible", true);
			RegisterBool(_uiVisible);
			CreateToggle(_uiVisible);
			_uiVisible.toggle.onValueChanged.AddListener((value) =>
			{
				SetCatalogVisibility(value);
			});

			_triggerButtonVisible = new JSONStorableBool("Show trigger buttons", false);
			RegisterBool(_triggerButtonVisible);
			CreateToggle(_triggerButtonVisible);
			_triggerButtonVisible.toggle.onValueChanged.AddListener((value) =>
			{
				_floatingTriggerButton.button.transform.localScale = value ? Vector3.one : Vector3.zero;
			});

			_alwaysFaceMe = new JSONStorableBool("Always Face Me", false);
			RegisterBool(_alwaysFaceMe);
			CreateToggle(_alwaysFaceMe);
			_alwaysFaceMe.toggle.onValueChanged.AddListener((value) =>
			{
				_floatingControlsUi.AlwaysFaceMe = value;
				_catalogUi.AlwaysFaceMe = value;
				_windowUi.AlwaysFaceMe = value;
			});

			_anchorOnHud = new JSONStorableBool("Anchor on HUD", true);
			RegisterBool(_anchorOnHud);
			CreateToggle(_anchorOnHud);
			_anchorOnHud.toggle.onValueChanged.AddListener((value) =>
			{
				if (value)
				{
					ResetCatalogPositionForHud();
					AnchorOnHud();
				}
				else
				{
					ResetCatalogPositionForPerson();
					AnchorOnAtom();
				}
			});


			var atomChoices = GetAtomNames();
			atomChoices.Add("<< Refresh List >>");
			var defaultAtom = containingAtom.name;
			_anchorOnAtom = new JSONStorableStringChooser("AnchorOnAtom", atomChoices, defaultAtom, "Anchor On Atom");
			_anchorOnAtom.storeType = JSONStorableParam.StoreType.Full;
			RegisterStringChooser(_anchorOnAtom);
			CreatePopup(_anchorOnAtom);
			_anchorOnAtom.setCallbackFunction = new JSONStorableStringChooser.SetStringCallback((chosenString) =>
			{
				if (chosenString == "<< Refresh List >>")
				{
					atomChoices = GetAtomNames();
					atomChoices.Add("<< Refresh List >>");
					_anchorOnAtom.choices = atomChoices;
					if (atomChoices.Count == 1) return;
					_anchorOnAtom.val = atomChoices.First();
				}
				else
					AnchorOnAtom();
			});

			_overlayMutations = new JSONStorableBool("Overlay Mutations", false);
			RegisterBool(_overlayMutations);
			CreateToggle(_overlayMutations);
		}

		private bool ToggleCatalogPlay()
		{
			_cycleEntriesOnInterval.val = !_cycleEntriesOnInterval.val;
			if (_cycleEntriesOnInterval.val == true)
			{
				_numberOfEntriesToPlay = _catalog.Entries.Count;
				StartCoroutine(ApplyNextEntryAfterInterval());
			}
			return _cycleEntriesOnInterval.val;
		}

		private void SetCatalogVisibility(bool setVisible)
		{
			_floatingControlsUi.Visible = setVisible;
			_catalogUi.Visible = setVisible;
			_windowUi.Visible = setVisible;
			if (setVisible)
			{
				_mainWindow.ParentWindowContainer.transform.localPosition = _catalogUnHiddenPosition;
			}
			else
			{
				_catalogUnHiddenPosition = _mainWindow.ParentWindowContainer.transform.localPosition;
				_mainWindow.ParentWindowContainer.transform.localPosition = new Vector3(0, -2000, 0);
			}
		}

		private void ResizeUi(float newVal)
		{
			float relativeValue = -newVal / 100;
			_catalogUi.canvas.transform.localPosition = new Vector3(_catalogUi.canvas.transform.localPosition.x, _catalogUi.canvas.transform.localPosition.y, relativeValue);
		}

		private List<string> GetAtomNames()
		{
			return SuperController.singleton.GetAtoms()?.Select(a => a.name).ToList() ?? new List<string>();
		}

		private void ResetCatalogPositionForHud()
		{
			_mainWindowPositionX.val = 670;
			_mainWindowPositionY.val = -50;
		}

		private void ReinitializeCatalog()
		{
			//_mutationsService.SetMorphBaseValuesForCurrentPerson();
			_catalog.Entries.ForEach(DestroyCatalogEntryUi);
			_mainWindow.CatalogRows.ToList().ForEach(p => Destroy(p));
			_mainWindow.CatalogColumns.ToList().ForEach(p => Destroy(p));
			_catalog.Entries = new List<CatalogEntry>();
			_mainWindow.CatalogColumns = new List<GameObject>();
			_mainWindow.CatalogRows = new List<GameObject>();
		}

		private void ToggleMode()
		{
			var newCurrentModeIndex = _catalogModes.IndexOf(_catalogMode.val);
			var newCatalogModeIndex = (newCurrentModeIndex + 1 < _catalogModes.Count()) ? newCurrentModeIndex + 1 : 0;
			_catalogMode.val = _catalogModes[newCatalogModeIndex];
			UpdateUiForMode();
		}

		private void UpdateUiForMode()
		{
			_floatingTriggerButton.button.transform.localScale = Vector3.zero;

			HideButtonInGroup(_mainWindow.ToggleButtonCaptureMorphs, _mainWindow.SubWindow);
			HideButtonInGroup(_mainWindow.ToggleButtonCaptureHair, _mainWindow.SubWindow);
			HideButtonInGroup(_mainWindow.ToggleButtonCaptureClothes, _mainWindow.SubWindow);
			HideButtonInGroup(_mainWindow.ToggleButtonCapturePose, _mainWindow.SubWindow);
			HideButtonInGroup(_mainWindow.ButtonRemoveAllClothing, _mainWindow.SubWindow);
			HideButtonInGroup(_mainWindow.ButtonRemoveAllHair, _mainWindow.SubWindow);
			HideButtonInGroup(_mainWindow.ButtonCapture, _mainWindow.SubWindow);
			HideButtonInGroup(_mainWindow.ButtonAddAtomToCapture, _mainWindow.SubWindow);
			HideButtonInGroup(_mainWindow.ButtonCreateMergedEntryGroup, _mainWindow.SubWindow);
			HideButtonInGroup(_mainWindow.ButtonToggleAnimFeatures, _mainWindow.SubWindow);
			HideButtonInGroup(_mainWindow.ButtonPlayStop, _mainWindow.SubWindow);
			HideButtonInGroup(_mainWindow.ButtonSelectScenesFolder, _mainWindow.SubWindow);

			_mainWindow.ButtonCapture.button.onClick.RemoveAllListeners();

			foreach (var modeButton in _mainWindow.ModeButtons)
			{
				modeButton.Value.buttonColor = new Color(0.25f, 0.25f, 0.25f, 1f);
			}

			if (_catalogMode.val == CatalogModeEnum.CATALOG_MODE_MUTATIONS)
			{
				_floatingTriggerButton.buttonText.text = "Generate Faces";
				_mainWindow.ButtonNameLabel.buttonText.text = "Generate Faces";
				if (_triggerButtonVisible.val) _floatingTriggerButton.button.transform.localScale = Vector3.one;
				_catalog.Entries.ForEach(e => e.UiBottomButtonGroup.transform.localScale = Vector3.one);
				ShowButtonInGroup(_mainWindow.ButtonCapture, _mainWindow.SubPanelCapture);
				_mainWindow.ButtonCapture.buttonText.text = "";
				_mainWindow.ButtonCapture.button.onClick.AddListener(() =>
				{
					var selectedAtom = _mutationsService.GetContainingOrSelectedPersonAtomOrDefault();
					if (containingAtom.type == "Person") selectedAtom = containingAtom;
					if (selectedAtom == null || selectedAtom.type != "Person")
					{
						ShowPopupMessage("Please select a Person", 2);
					}
					RequestNextCaptureSet((int)_catalogRequestedEntriesCountJSON.val, CaptureRequest.MUTATION_AQUISITION_MODE_SEQUENCE);
				});
				_mainWindow.ButtonCapture.button.image.sprite = _mainWindow.MainCaptureButtonIcon;
				SetTooltipForDynamicButton(_mainWindow.ButtonCapture, () => "Generate faces");
			}
			else if (_catalogMode.val == CatalogModeEnum.CATALOG_MODE_CAPTURE)
			{
				_mainWindow.ButtonNameLabel.buttonText.text = "Capture Styles";
				_floatingTriggerButton.buttonText.text = "Capture Styles";
				if (_triggerButtonVisible.val) _floatingTriggerButton.button.transform.localScale = Vector3.one;
				_catalog.Entries.ForEach(e => e.UiBottomButtonGroup.transform.localScale = Vector3.one);
				ShowButtonInGroup(_mainWindow.ButtonCapture, _mainWindow.SubPanelCapture);
				ShowButtonInGroup(_mainWindow.ToggleButtonCaptureClothes, _mainWindow.SubPanelCapture);
				ShowButtonInGroup(_mainWindow.ToggleButtonCaptureHair, _mainWindow.SubPanelCapture);
				ShowButtonInGroup(_mainWindow.ToggleButtonCaptureMorphs, _mainWindow.SubPanelCapture);
				ShowButtonInGroup(_mainWindow.ToggleButtonCapturePose, _mainWindow.SubPanelCapture);
				ShowButtonInGroup(_mainWindow.ButtonRemoveAllClothing, _mainWindow.SubPanelSceneTools);
				ShowButtonInGroup(_mainWindow.ButtonRemoveAllHair, _mainWindow.SubPanelSceneTools);
				_mainWindow.ButtonCapture.buttonText.text = "";
				_mainWindow.ButtonCapture.button.onClick.AddListener(() => RequestNextCaptureSet(1, CaptureRequest.MUTATION_AQUISITION_MODE_CAPTURE));
				_mainWindow.ButtonCapture.button.image.sprite = _mainWindow.IconForCapturePerson;
				SetTooltipForDynamicButton(_mainWindow.ButtonCapture, () => "Capture styles");
			}
			else if (_catalogMode.val == CatalogModeEnum.CATALOG_MODE_SCENE)
			{
				_mainWindow.ButtonNameLabel.buttonText.text = "Refresh Scenes";
				_catalog.Entries.ForEach(e => e.UiBottomButtonGroup.transform.localScale = Vector3.one);
				_mainWindow.ButtonCapture.buttonText.text = "";
				ShowButtonInGroup(_mainWindow.ButtonCapture, _mainWindow.SubPanelCapture);
				ShowButtonInGroup(_mainWindow.ButtonSelectScenesFolder, _mainWindow.SubPanelCapture);
				_mainWindow.ButtonCapture.button.image.sprite = _mainWindow.IconForCaptureScenes;
				_mainWindow.ButtonCapture.button.onClick.AddListener(() => CreateSceneCatalogEntries(GetSceneDirectoryPath()));
				SetTooltipForDynamicButton(_mainWindow.ButtonCapture, () => "Get Scenes from current directory");
			}
			else if (_catalogMode.val == CatalogModeEnum.CATALOG_MODE_OBJECT)
			{
				_mainWindow.ButtonNameLabel.buttonText.text = "Capture Object";
				_catalog.Entries.ForEach(e => e.UiBottomButtonGroup.transform.localScale = Vector3.one);
				_mainWindow.ButtonCapture.buttonText.text = "";
				ShowButtonInGroup(_mainWindow.ButtonCapture, _mainWindow.SubPanelCapture);
				ShowButtonInGroup(_mainWindow.ButtonAddAtomToCapture, _mainWindow.SubPanelCapture);
				_mainWindow.ButtonCapture.button.image.sprite = _mainWindow.IconForCaptureObject;
				SetTooltipForDynamicButton(_mainWindow.ButtonCapture, () => "Capture Object");
				_mainWindow.ButtonCapture.button.onClick.AddListener(() =>
				{
					RequestNextCaptureSet(1, CaptureRequest.MUTATION_AQUISITION_MODE_CAPTURE_OBJECT);
				});
			}
			else if (_catalogMode.val == CatalogModeEnum.CATALOG_MODE_SESSION)
			{
				_mainWindow.ButtonNameLabel.buttonText.text = "Capture Selected";
				_catalog.Entries.ForEach(e => e.UiBottomButtonGroup.transform.localScale = Vector3.one);
				_mainWindow.ButtonCapture.buttonText.text = "";
				ShowButtonInGroup(_mainWindow.ButtonCapture, _mainWindow.SubPanelCapture);
				ShowButtonInGroup(_mainWindow.ButtonAddAtomToCapture, _mainWindow.SubPanelCapture);
				_mainWindow.ButtonCapture.button.image.sprite = _mainWindow.IconForCaptureSelectedObject;
				SetTooltipForDynamicButton(_mainWindow.ButtonCapture, () => "Capture Selected Object");
				_mainWindow.ButtonCapture.button.onClick.AddListener(() =>
				{
					if (SuperController.singleton.GetSelectedAtom() == null)
					{
						ShowPopupMessage("Please select an atom from the scene", 2);
						return;
					}
					RequestNextCaptureSet(1, CaptureRequest.MUTATION_AQUISITION_MODE_CAPTURE_SESSION_OBJECT);
				});
			}
			else if (_catalogMode.val == CatalogModeEnum.CATALOG_MODE_VIEW)
			{
				foreach (var entry in _catalog.Entries)
				{
					entry.UiBottomButtonGroup.transform.localScale = (entry.UiBottomButtonGroup.transform.localScale == Vector3.zero)
							? entry.UiBottomButtonGroup.transform.localScale = Vector3.one
							: entry.UiBottomButtonGroup.transform.localScale = Vector3.zero;
				}
				_mainWindow.ButtonCapture.button.image.sprite = _mainWindow.IconForCaptureNone;
				_mainWindow.ButtonCapture.button.onClick.AddListener(() => { });
				ShowButtonInGroup(_mainWindow.ButtonPlayStop, _mainWindow.SubPanelCapture);
				ShowButtonInGroup(_mainWindow.ButtonCreateMergedEntryGroup, _mainWindow.SubPanelCapture);
				ShowButtonInGroup(_mainWindow.ButtonToggleAnimFeatures, _mainWindow.SubPanelCapture);
			}

			_mainWindow.ModeButtons[_catalogMode.val].buttonColor = Color.red;
		}

		private void ShowButtonInGroup(UIDynamicButton button, GameObject parentGroup)
		{
			button.button.transform.localScale = Vector3.one;
			button.transform.SetParent(parentGroup.transform, false);
		}

		private void HideButtonInGroup(UIDynamicButton button, GameObject parentWindow)
		{
			button.button.transform.localScale = Vector3.zero;
			button.transform.SetParent(parentWindow.transform, false);
		}

		private void CreateSceneCatalogEntries(string scenePath)
		{
			try
			{
				if (scenePath == null) return;
				ReinitializeCatalog(); //...CreateSceneCatalogEntries
				var fileEntries = SuperController.singleton.GetFilesAtPath(scenePath);
				foreach (var imagePath in fileEntries)
				{
					if (imagePath.Length < 4) continue;
					if (imagePath.Substring(imagePath.Length - 4) == ".jpg")
					{
						var jsonPath = imagePath.Substring(0, imagePath.Length - 4) + ".json";
						if (!fileEntries.Contains(jsonPath)) continue;
						var stringData = SuperController.singleton.ReadFileIntoString(imagePath.Replace("\\", "/"));

						var texture = ImageLoader.GetFutureImageFromFile(imagePath);
						if (texture == null) return;
						var catalogEntry = new CatalogEntry();
						catalogEntry.UniqueName = GetUniqueName();
						catalogEntry.CatalogEntryMode = _catalogMode.val;
						catalogEntry.Mutation = new Mutation();
						catalogEntry.Mutation.ScenePathToOpen = jsonPath;
						catalogEntry.ImageInfo = new ImageInfo();
						catalogEntry.ImageInfo.ExternalPath = imagePath;
						catalogEntry.ImageInfo.Texture = texture;
						MakeCatalogEntry(catalogEntry); // ...Creating "Scenes" catalog	
					}
				}

				RefreshCatalogPosition(); // ...After fetching scenes from folder
			}
			catch (Exception e)
			{
				SuperController.LogError(e.ToString());
			}
		}

		public string GetUniqueName()
		{
			return Guid.NewGuid().ToString().Substring(0, 5);
		}

		private void AnchorOnHud()
		{
			try
			{
				_catalogUi.canvas.transform.SetParent(SuperController.singleton.mainHUDAttachPoint, false);
				_catalogUi.canvas.transform.rotation = Quaternion.Euler(0, 180, 0);
				_catalogUi.canvas.transform.localRotation = Quaternion.Euler(0, 0, 0);
				_catalogUi.canvas.transform.localPosition = new Vector3(_mainWindowPositionX.val, _mainWindowPositionY.val, 0.5f);

				_windowUi.canvas.transform.SetParent(SuperController.singleton.mainHUDAttachPoint, false);
				_windowUi.canvas.transform.rotation = Quaternion.Euler(0, 180, 0);
				_windowUi.canvas.transform.localRotation = Quaternion.Euler(0, 0, 0);
				//_windowUi.canvas.transform.localRotation = Quaternion.Euler(0, 0, 0.5f);
				_windowUi.canvas.transform.localPosition = new Vector3(0, 0, 0.5f);
				_mainWindow.ParentWindowContainer.transform.localPosition = new Vector3(_mainWindowPositionX.val, _mainWindowPositionY.val, 0f);


				_floatingControlsUi.canvas.transform.SetParent(SuperController.singleton.mainHUDAttachPoint, false);
				_floatingControlsUi.canvas.transform.rotation = Quaternion.Euler(0, 180, 0);
				_floatingControlsUi.canvas.transform.localRotation = Quaternion.Euler(0, 0, 0);
				_floatingControlsUi.canvas.transform.localPosition = new Vector3(-0.573f, 1.22f, 0.5f);

			}
			catch (Exception e)
			{
				SuperController.LogError(e.ToString());
			};
		}

		private void AnchorOnAtom()
		{
			var anchorIsPerson = string.IsNullOrEmpty(_anchorOnAtom.val) || _anchorOnAtom.val == "Person";
			Transform targetAtomTransform = anchorIsPerson ? GetPersonBasePositionOrDefault() : GetAtomBasePosition(_anchorOnAtom.val);
			var handlePosition = targetAtomTransform.localPosition;// + new Vector3(-0.7f, 0.7f, 0f);
			Action<Atom> onHandleCreated = (newAtom) =>
			{
				//var handlePosition = anchorIsPerson ? new Vector3(_mainWindowPositionX.val, _mainWindowPositionY.val, 0f) : targetAtomTransform.position + new Vector3(-0.7f, 0.7f, 0f);
				var rotation = Quaternion.Euler(0, 180, 0);

				_windowUi.canvas.transform.SetParent(newAtom.mainController.transform, false);
				_windowUi.canvas.transform.rotation = rotation;
				_windowUi.canvas.transform.localPosition = new Vector3(0, 0, 0);

				_catalogUi.canvas.transform.SetParent(newAtom.mainController.transform, false);
				_catalogUi.canvas.transform.rotation = rotation;
				_catalogUi.canvas.transform.localPosition = new Vector3(0, 0, 0);

				var controlUiPosition = anchorIsPerson ? targetAtomTransform.localPosition + new Vector3(0.15f, 0.45f, 0) : Vector3.zero;
				if (!anchorIsPerson) _floatingTriggerButton.transform.localPosition = Vector3.zero;
				_floatingControlsUi.canvas.transform.SetParent(newAtom.mainController.transform, false);
				_floatingControlsUi.canvas.transform.rotation = rotation;
				_floatingControlsUi.canvas.transform.localPosition = controlUiPosition;
			};

			CreateHandleObject("HandleFor_" + _catalogName.val, handlePosition, onHandleCreated);

		}

		private Transform GetAtomBasePosition(string atomName)
		{
			_parentAtom = SuperController.singleton.GetAtoms().FirstOrDefault(a => a.name == atomName);
			_parentAtom.mainController.interactableInPlayMode = true;
			Transform anchor = _parentAtom.transform;
			if (anchor == null) anchor = _parentAtom.freeControllers.FirstOrDefault()?.transform;
			if (anchor == null) anchor = SuperController.singleton.GetAtoms().First().transform;
			return anchor;
		}

		private Transform GetPersonBasePositionOrDefault()
		{
			_parentAtom = _mutationsService.GetContainingOrSelectedPersonAtomOrDefault();// containingAtom;
			if (_parentAtom == null) return null;
			_parentAtom.mainController.interactableInPlayMode = true;
			Transform anchor = _parentAtom.freeControllers.Where(x => x.name == "headControl").FirstOrDefault()?.transform;
			if (anchor == null) anchor = _parentAtom.freeControllers.FirstOrDefault()?.transform;
			if (anchor == null) anchor = SuperController.singleton.GetAtoms().First().transform;
			return anchor;
		}

		public Atom GetContainingSelectedOrDefaultAtom()
		{
			if (containingAtom.type != ATOM_TYPE_SESSION) return containingAtom;
			var selectedAtom = SuperController.singleton.GetSelectedAtom();
			if (selectedAtom != null) return selectedAtom;
			//...else there are either no Person atoms, or more than one Person atom.
			SuperController.LogMessage("REQUEST: Please select an atom in the scene");
			return null;
		}

		private void ShiftCatalogEntriesForward()
		{
			try
			{
				if (_catalog.Entries.Count == 0) return;
				var firstEntry = _catalog.Entries.First();
				_catalog.Entries.Remove(firstEntry);
				_catalog.Entries.Add(firstEntry);
				RebuildCatalogFromEntriesCollection(); //...After shifting entries forward
			}
			catch (Exception e)
			{
				SuperController.LogError(e.ToString());
			}
		}

		private void ShiftCatalogEntriesBackward()
		{
			try
			{
				if (_catalog.Entries.Count == 0) return;
				var lastEntry = _catalog.Entries.Last();
				_catalog.Entries.Remove(lastEntry);
				_catalog.Entries.Insert(0, lastEntry);
				RebuildCatalogFromEntriesCollection();//...After shifting entries backward
			}
			catch (Exception e)
			{
				SuperController.LogError(e.ToString());
			}
		}

		private void ToggleMinimize()
		{
			try
			{
				var newMinimizedValue = _mainWindow.Minimized;
				_minimizeUi.SetVal(!_minimizeUi.val);
				_mainWindow.Minimized = true;
				SetMinimizedState();
				UpdateUiForMode();
			}
			catch (Exception e)
			{
				SuperController.LogError(e.ToString());
			}

		}

		void SetObjectSize(GameObject gameObject, float newWidth, float newHeight)
		{
			//var rect = gameObject.GetComponent<RectTransform>();
			//var currentHeight = rect.sizeDelta.y;
			//var currentWidth = rect.sizeDelta.x;
			//rect.sizeDelta = new Vector2(newWidth, newHeight);
			//var newY = -currentHeight / 2;
			gameObject.transform.localPosition = gameObject.transform.localPosition + new Vector3(0, 100, 0);
		}

		private void SetMinimizedState()
		{
			var window = _mainWindow.SubWindow.GetComponent<RectTransform>();
			window.localScale = _minimizeUi.val ? Vector3.zero : Vector3.one;
			var triggerButton = _floatingTriggerButton.GetComponent<RectTransform>();
			triggerButton.localScale = _minimizeUi.val ? Vector3.zero : Vector3.one;
		}

		private void MinimizeControlPanel()
		{
			_mainWindow.ControlPanelMinimized = !_mainWindow.ControlPanelMinimized;
			bool minimize = _mainWindow.ControlPanelMinimized;
			_mainWindow.SubPanelFileMenu.transform.localScale = minimize ? Vector3.zero : Vector3.one;
			_mainWindow.SubPanelCapture.transform.localScale = minimize ? Vector3.zero : Vector3.one;
			_mainWindow.SubPanelMode.transform.localScale = minimize ? Vector3.zero : Vector3.one;
			_mainWindow.SubPanelManageCatalog.transform.localScale = minimize ? Vector3.zero : Vector3.one;
			_mainWindow.SubPanelSceneTools.transform.localScale = minimize ? Vector3.zero : Vector3.one;
			_mainWindow.PanelBackground.transform.localScale = minimize ? Vector3.zero : Vector3.one;
			_mainWindow.MiniPanelBackground.transform.localScale = minimize ? Vector3.one : Vector3.zero;
			_mainWindow.MiniSubPanelShortcut.transform.localScale = minimize ? Vector3.one : Vector3.zero;
			_mainWindow.InfoLabel.transform.localScale = minimize ? Vector3.zero : Vector3.one;
			_mainWindow.TextToolTip.transform.localPosition = minimize ? new Vector3(100, -100, 0) : new Vector3(150, -_mainWindow.WindowHeight - 50, 0);
			_mainWindow.ButtonMinimizeSubItemPanel.transform.localScale = minimize ? Vector3.zero : Vector3.one;
			_mainWindow.DynamicInfoPanel.transform.localScale = (minimize || _mainWindow.SubItemPanelMinimized) ? Vector3.zero : Vector3.one;
		}

		private void CleanCatalog()
		{
			var newCatalogEntries = new List<CatalogEntry>();
			try
			{
				foreach (var entry in _catalog.Entries)
				{
					var hideEntry = false;
					if (entry.Mutation == null) continue;
					var mutation = entry.Mutation;

					// Check hair items...
					var hairItems = _mutationsService.GetHairItemsForSelectedPersonOrDefault() ?? new List<DAZHairGroup>();
					foreach (var item in mutation.HairItems)
					{
						if (!hairItems.Any(i => _mutationsService.IsHairItemAbsoluteMatch(item, i) || _mutationsService.IsHairItemFallbackMatch(item, i))) hideEntry = true;
					}

					// Check clothing items...
					var clothingItems = _mutationsService.GetClothingItemsForSelectedPersonOrDefault() ?? new List<DAZClothingItem>();
					foreach (var item in mutation.ClothingItems)
					{
						if (!clothingItems.Any(i => _mutationsService.IsClothingItemAbsoluteMatch(item, i) || _mutationsService.IsClothingItemFallbackMatch(item, i))) hideEntry = true;
					}

					// Check pose points...
					var controllers = _mutationsService.GetControllersForContainingOrSelectedPersonOrDefault() ?? new List<FreeControllerV3>();
					foreach (var item in mutation.PoseMorphs)
					{
						if (!controllers.Any(i => i.name == item.Id)) hideEntry = true;
					}

					// Check active morphs...
					var morphItems = _mutationsService.GetMorphsForSelectedPersonOrDefault() ?? new List<DAZMorph>();
					foreach (var item in mutation.ActiveMorphs)
					{
						if (!morphItems.Any(i => _mutationsService.GetMorphId(i) == item.Id)) hideEntry = true;
					}

					// Check facegen morphs...
					foreach (var item in mutation.FaceGenMorphSet)
					{
						if (!morphItems.Any(i => _mutationsService.GetMorphId(i) == item.Id)) hideEntry = true;
					}

					if (!hideEntry) newCatalogEntries.Add(entry);
				}

				_catalog.Entries = newCatalogEntries;
				RebuildCatalogFromEntriesCollection(); //...After sorting Catalog
			}
			catch (Exception e)
			{
				SuperController.LogError(e.ToString());
			}
		}

		private void SortCatalog()
		{
			try
			{
				_catalog.Entries
					.Where(c => c.Discarded)
					.ToList()
					.ForEach(DestroyCatalogEntryUi);

				_catalog.Entries = _catalog.Entries
					.Where(c => !c.Discarded)
					.OrderByDescending(c => c.Favorited)
					.ToList();

				RebuildCatalogFromEntriesCollection(); //...After sorting Catalog
			}
			catch (Exception e)
			{
				SuperController.LogError(e.ToString());
			}
		}

		private void RebuildCatalogFromEntriesCollection()
		{
			try
			{
				var newEntries = _catalog.Entries.Select(e => e).ToList();
				foreach (var catalogEntry in _catalog.Entries)
				{
					DestroyCatalogEntryUi(catalogEntry);
				}
				ReinitializeCatalog(); //...RebuildCatalogFromEntriesCollection()
				ResetScrollPosition();
				_catalog.Entries = new List<CatalogEntry>();
				// Reassign list to columns
				for (var i = 0; i < newEntries.Count; i++)
				{
					MakeCatalogEntry(newEntries[i]);
				}
				RefreshCatalogPosition(); // ...After rebuilding catalog
			}
			catch (Exception e)
			{
				SuperController.LogError(e.ToString());
			}
		}

		private void SelectHandle()
		{
			SuperController.singleton.SelectController(_handleObjectForCatalog.mainController);
		}

		private void ShowLoading(string message)
		{
			_mainWindow.TextToolTip.UItext.text = message;
			_mainWindow.TextToolTip.transform.localScale = Vector3.one;
		}

		private void HideLoading()
		{
			_mainWindow.TextToolTip.transform.localScale = Vector3.zero;
		}

		public void SetTooltipForDynamicButton(UIDynamicButton button, Func<string> returnTooltipText)
		{
			EventTrigger triggerEnter = button.button.gameObject.AddComponent<EventTrigger>();
			var pointerEnter = new EventTrigger.Entry();
			pointerEnter.eventID = EventTriggerType.PointerEnter;
			button.button.onClick.AddListener(() =>
			{
				ShowTooltip(returnTooltipText());
			});

			pointerEnter.callback.AddListener((e) =>
			{
				ShowTooltip(returnTooltipText());
			});
			triggerEnter.triggers.Add(pointerEnter);

			EventTrigger triggerExit = button.button.gameObject.AddComponent<EventTrigger>();
			var pointerExit = new EventTrigger.Entry();
			pointerExit.eventID = EventTriggerType.PointerExit;
			pointerExit.callback.AddListener((e) =>
			{
				HideTooltip();
			});
			triggerEnter.triggers.Add(pointerExit);
		}

		private void HideTooltip()
		{
			_mainWindow.TextToolTip.transform.localScale = Vector3.zero;
		}

		private void HideConfirmMessage()
		{
			_dynamicConfirmPanel.transform.localScale = Vector3.zero;
		}

		private void HideSelectList()
		{
			_dynamicSelectList.transform.localScale = Vector3.zero;
		}

		private void HidePopupMessage()
		{
			_mainWindow.ButtonPopupMessageLabel.transform.localScale = Vector3.zero;
		}

		public void ShowPopupMessage(string text, int? duration = null)
		{
			if (_mainWindow == null) return;
			if (_mainWindow.ButtonPopupMessageLabel == null) return;
			var minimizeBtnRect = _mainWindow.ButtonPopupMessageLabel.GetComponent<RectTransform>();
			var btnWidth = string.IsNullOrEmpty(text) ? 50 : 30 * text.Length;
			minimizeBtnRect.sizeDelta = new Vector2(btnWidth, 50);
			_mainWindow.ButtonPopupMessageLabel.buttonText.text = text;
			_mainWindow.ButtonPopupMessageLabel.transform.localScale = Vector3.one;
			CatalogUiHelper.SetAnchors(_mainWindow.ParentWindowContainer, _mainWindow.ButtonPopupMessageLabel.button.gameObject, "topleft", -10, -40);
			StartCoroutine(ShowPopupMessage(duration));
		}

		IEnumerator ShowPopupMessage(int? timeInSeconds = null)
		{
			if (timeInSeconds == null)
				yield return null;
			else
			{
				yield return new WaitForSeconds(timeInSeconds ?? 1);
				HidePopupMessage();
			}
		}

		private void ShowTooltip(string text)
		{
			if (_mainWindow.TextToolTip == null) return;
			_mainWindow.TextToolTip.UItext.text = text;
			_mainWindow.TextToolTip.transform.localScale = Vector3.one;
		}

		private void ShowConfirm(string text, Action action)
		{
			_lastConfirmAction = action;
			if (_dynamicConfirmLabel == null) return;
			_dynamicConfirmLabel.text = text;
			_dynamicConfirmPanel.transform.localScale = Vector3.one;
		}

		private void ShowHelp(string text)
		{
			if (_dynamicHelpLabel == null) return;
			_dynamicHelpLabel.text = text;
			_dynamicHelpPanel.transform.localScale = Vector3.one;
		}

		private void BrowseForAndLoadCatalog()
		{
			_baseFileFormat = SuperController.singleton.fileBrowserUI.fileFormat;
			SuperController.singleton.ShowMainHUD();
			SuperController.singleton.fileBrowserUI.SetTextEntry(false);
			SuperController.singleton.fileBrowserUI.fileFormat = _fileExtension;
			SuperController.singleton.fileBrowserUI.defaultPath = GetSceneDirectoryPath();
			SuperController.singleton.fileBrowserUI.Show((filePath) =>
			{
				SuperController.singleton.fileBrowserUI.fileFormat = "json";
				if (string.IsNullOrEmpty(filePath)) return;
				ShowLoading("Loading Catalog...");
				ReinitializeCatalog(); //...BrowseForAndLoadCatalog
				var catalog = LoadCatalogFromFile(filePath);
				DeselectAllEntries(catalog);
				pluginLabelJSON.val = _catalogName.val;
				HideLoading();
			});
		}

		private void BrowseForAndLoadScenesAsCatalog()
		{
			_baseFileFormat = SuperController.singleton.fileBrowserUI.fileFormat;
			SuperController.singleton.ShowMainHUD();
			SuperController.singleton.fileBrowserUI.SetTextEntry(false);
			SuperController.singleton.fileBrowserUI.fileFormat = "json";
			SuperController.singleton.fileBrowserUI.defaultPath = GetSceneDirectoryPath();
			SuperController.singleton.fileBrowserUI.Show((filePath) =>
			{
				var directoryPath = filePath.Substring(0, filePath.Replace("\\", "/").LastIndexOf("/"));
				CreateSceneCatalogEntries(directoryPath);
			});
		}

		private void BrowseForAndSaveCatalog()
		{
			try
			{
				SuperController.singleton.ShowMainHUD();
				_baseFileFormat = SuperController.singleton.fileBrowserUI.fileFormat;
				SuperController.singleton.fileBrowserUI.fileFormat = _fileExtension;
				SuperController.singleton.fileBrowserUI.SetTextEntry(true);
				SuperController.singleton.fileBrowserUI.defaultPath = GetSceneDirectoryPath();

				SuperController.singleton.fileBrowserUI.Show((filePath) =>
				{
					SuperController.singleton.fileBrowserUI.fileFormat = "json";
					if (string.IsNullOrEmpty(filePath)) return;
					var extension = "." + _fileExtension;
					if (filePath.Length > 3 && filePath.Substring(filePath.Length - extension.Length) == extension) extension = "";
					filePath += extension;
					ShowLoading("Saving Catalog...");
					SaveCatalogToFile(filePath);
					HideLoading();
					SuperController.LogMessage("INFO: Saved catalog to: " + filePath);
				});

			}
			catch (Exception e)
			{
				SuperController.LogError(e.ToString());
			}
		}

		private IEnumerator MakeThisACatalogDependencyFile()
		{
			for (var i = 0; i < _catalog.Entries.Count; i++)
			{
				var catalogEntry = _catalog.Entries[i];
				if (catalogEntry.CatalogEntryMode == CatalogModeEnum.CATALOG_MODE_SCENE) continue;
				var mutation = _catalog.Entries[i].Mutation;
				_mutationsService.ApplyMutation(ref mutation);
				yield return new WaitForEndOfFrame();
				yield return new WaitForSeconds(1);
			}
		}

		private void CreateDynamicButton_Save()
		{
			var texture = _imageLoaderService.GetFutureImageFromFileOrCached(GetPluginPath() + "/Resources/Save.png");
			_mainWindow.ButtonSaveCatalog = _catalogUi.CreateButton(_mainWindow.SubPanelFileMenu, "", 35, 35, 0, 0, new Color(0.5f, 0.5f, 1f), Color.red, new Color(1f, 1f, 1f), texture);
			_mainWindow.ButtonSaveCatalog.button.onClick.AddListener(() => BrowseForAndSaveCatalog());
			SetTooltipForDynamicButton(_mainWindow.ButtonSaveCatalog, () => "Save Catalog");
		}

		private void CreateDynamicButton_Load()
		{
			var texture = _imageLoaderService.GetFutureImageFromFileOrCached(GetPluginPath() + "/Resources/Open.png");
			_mainWindow.ButtonOpenCatalog = _catalogUi.CreateButton(_mainWindow.SubPanelFileMenu, "", 35, 35, 0, 0, new Color(0.5f, 0.5f, 1f), Color.green, new Color(1f, 1f, 1f), texture);
			_mainWindow.ButtonOpenCatalog.button.onClick.AddListener(() => BrowseForAndLoadCatalog());
			SetTooltipForDynamicButton(_mainWindow.ButtonOpenCatalog, () => "Load Catalog");
		}

		private void CreateDynamicButton_MinimizeSubItemPanel()
		{
			//var texture = _imageLoaderService.GetFutureImageFromFileOrCached(GetPluginPath() + "/Resources/SubItemMenu.png");
			_mainWindow.ButtonMinimizeSubItemPanel = _catalogUi.CreateButton(_mainWindow.SubWindow, "sub items", 100, 25, _mainWindow.WindowWidth + 50, -11, new Color(0f, 0f, 0f), new Color(0.7f, 0.2f, 0.2f), new Color(0.6f, 0.6f, 0.6f));
			_mainWindow.ButtonMinimizeSubItemPanel.buttonText.fontSize = 15;
			var checekdColor = new Color(0.7f, 0.2f, 0.2f);
			var unchecekdColor = new Color(0f, 0f, 0f);
			_mainWindow.ButtonMinimizeSubItemPanel.button.onClick.AddListener(() =>
			{
				_mainWindow.SubItemPanelMinimized = !_mainWindow.SubItemPanelMinimized;
				if (_mainWindow.SubItemPanelMinimized)
				{
					RemoveItemToggles();
					_mainWindow.ButtonMinimizeSubItemPanel.buttonText.text = "sub items";
					SetButtonColor(_mainWindow.ButtonMinimizeSubItemPanel, unchecekdColor);
					//_mainWindow.ButtonMinimizeSubItemPanel.buttonColor = _dynamicButtonUnCheckColor;
					//_mainWindow.ButtonMinimizeSubItemPanel.buttonColor = new Color(0f, 0f, 0f);
					_mainWindow.DynamicInfoPanel.transform.localScale = Vector3.zero;
					var pos = _mainWindow.ButtonMinimizeSubItemPanel.transform.localPosition;
					_mainWindow.ButtonMinimizeSubItemPanel.transform.localPosition = new Vector3((float)(_mainWindow.WindowWidth + 100), pos.y, pos.z);// _mainWindow.ButtonMinimizeSubItemPanel.transform.localPosition - new Vector3(200, 0,0);
					_mainWindow.ButtonMinimizeSubItemPanel.transform.localRotation = Quaternion.Euler(new Vector3(0, 0, 0));
				}
				else
				{
					SetButtonColor(_mainWindow.ButtonMinimizeSubItemPanel, checekdColor);
					_mainWindow.ButtonMinimizeSubItemPanel.buttonText.text = "SUB ITEMS";
					_mainWindow.DynamicInfoPanel.transform.localScale = Vector3.one;
					var pos = _mainWindow.ButtonMinimizeSubItemPanel.transform.localPosition;
					_mainWindow.ButtonMinimizeSubItemPanel.transform.localPosition = new Vector3((float)(_mainWindow.WindowWidth + 100 + 200), pos.y, pos.z); //_mainWindow.ButtonMinimizeSubItemPanel.transform.localPosition + new Vector3(200, 0, 0);
					//_mainWindow.ButtonMinimizeSubItemPanel.transform.localRotation = Quaternion.Euler(new Vector3(0, 0, 180));
					if (_mainWindow.CurrentCatalogEntry != null) SelectCatalogEntry(_mainWindow.CurrentCatalogEntry);
				}
			});
			SetTooltipForDynamicButton(_mainWindow.ButtonMinimizeSubItemPanel, () => _mainWindow.SubItemPanelMinimized ? "Show sub-items": "Hide sub-items");
		}

		private void SetButtonColor(UIDynamicButton button, Color color)
		{
			var image = button.GetComponent<Image>();
			image.color = color;
		}

		private void CreateDynamicButton_LoadShortcut()
		{
			var texture = _imageLoaderService.GetFutureImageFromFileOrCached(GetPluginPath() + "/Resources/Open.png");
			_mainWindow.ButtonOpenCatalogShortcut = _catalogUi.CreateButton(_mainWindow.MiniSubPanelShortcut, "", 35, 35, 0, 0, new Color(0.5f, 0.5f, 1f), Color.green, new Color(1f, 1f, 1f), texture);
			_mainWindow.ButtonOpenCatalogShortcut.button.onClick.AddListener(() => BrowseForAndLoadCatalog());
			SetTooltipForDynamicButton(_mainWindow.ButtonOpenCatalogShortcut, () => "Load Catalog");
		}

		private void CreateDynamicButton_HideCatalogShortcut()
		{
			var texture = _imageLoaderService.GetFutureImageFromFileOrCached(GetPluginPath() + "/Resources/Close2.png");
			_mainWindow.ButtonHideCatalogShortcut = _catalogUi.CreateButton(_mainWindow.MiniSubPanelShortcut, "", 35, 35, 0, 10, new Color(0.5f, 0.5f, 1f), Color.red, new Color(1f, 1f, 1f), texture);
			_mainWindow.ButtonHideCatalogShortcut.button.onClick.AddListener(() => SetCatalogVisibility(false));
			SetTooltipForDynamicButton(_mainWindow.ButtonHideCatalogShortcut, () => "Hide Catalog (unhide in settings)");
		}

		private void CreateDynamicButton_Spacer(GameObject parentObject, int width, int height)
		{
			var spacer = _catalogUi.CreateButton(parentObject, "", width, height, 0, 0, Color.clear, Color.clear, Color.clear);
		}

		private void CreateDynamicButton_QuickLoad()
		{
			var texture = _imageLoaderService.GetFutureImageFromFileOrCached(GetPluginPath() + "/Resources/List.png");
			_mainWindow.ButtonQuickload = _catalogUi.CreateButton(_mainWindow.SubPanelFileMenu, "", 35, 35, 0, 0, new Color(0.5f, 0.5f, 1f), Color.green, new Color(1f, 1f, 1f), texture);
			_mainWindow.ButtonQuickload.button.onClick.AddListener(() => ShowQuickLoadFileList());
			SetTooltipForDynamicButton(_mainWindow.ButtonQuickload, () => "Catalog Listing");
		}

		private void CreateDynamicButton_QuickloadShortcut()
		{
			var texture = _imageLoaderService.GetFutureImageFromFileOrCached(GetPluginPath() + "/Resources/List.png");
			_mainWindow.ButtonQuickloadShortcut = _catalogUi.CreateButton(_mainWindow.MiniSubPanelShortcut, "", 35, 35, 0, 0, new Color(0.5f, 0.5f, 1f), Color.green, new Color(1f, 1f, 1f), texture);
			_mainWindow.ButtonQuickloadShortcut.button.onClick.AddListener(() => ShowQuickLoadFileList());
			SetTooltipForDynamicButton(_mainWindow.ButtonQuickloadShortcut, () => "Catalog Listing");
		}

		private void CreateDynamicButton_ToggleControlPanel()
		{
			var texture = _imageLoaderService.GetFutureImageFromFileOrCached(GetPluginPath() + "/Resources/Minimize7.png");
			_mainWindow.ButtonMinimizeControlPanel = _catalogUi.CreateButton(_mainWindow.SubPanelFileMenu, "", 35, 35, 0, 0, new Color(0.5f, 0.7f, 0.7f), Color.green, new Color(1f, 1f, 1f), texture);
			_mainWindow.ButtonMinimizeControlPanel.button.onClick.AddListener(() => MinimizeControlPanel());
			SetTooltipForDynamicButton(_mainWindow.ButtonMinimizeControlPanel, () => "Toggle Control Panel");
		}

		private void CreateDynamicButton_ToggleControlPanelShortcut()
		{
			var texture = _imageLoaderService.GetFutureImageFromFileOrCached(GetPluginPath() + "/Resources/Maximize.png");
			_mainWindow.ButtonMaximizeControlPanel = _catalogUi.CreateButton(_mainWindow.MiniSubPanelShortcut, "", 35, 35, 0, 0, new Color(0.5f, 0.7f, 0.7f), Color.green, new Color(1f, 1f, 1f), texture);
			_mainWindow.ButtonMaximizeControlPanel.button.onClick.AddListener(() => MinimizeControlPanel());
			SetTooltipForDynamicButton(_mainWindow.ButtonMaximizeControlPanel, () => "Toggle Control Panel");
		}

		private void ShowQuickLoadFileList()
		{
			var files = SuperController.singleton.GetFilesAtPath(_lastCatalogDirectory, "*." + _fileExtension);
			Dictionary<string, UnityAction> filesToLoad = new Dictionary<string, UnityAction>();
			foreach (var file in files)
			{
				var fileName = file.Replace("\\", "/").Replace(_lastCatalogDirectory + "/", "").Replace("." + _fileExtension, "");
				UnityAction selectAction = () =>
				{
					try
					{
						ReinitializeCatalog(); //...Select quick load item
						var filePath = _lastCatalogDirectory + "/" + fileName + "." + _fileExtension;
						var catalog = LoadCatalogFromFile(filePath);
						DeselectAllEntries(catalog);
						pluginLabelJSON.val = _catalogName.val;
					}
					catch (Exception e)
					{
						SuperController.LogError(e.ToString());
					}
				};
				filesToLoad.Add(fileName, selectAction);
			}
			ShowSelectList(filesToLoad);
		}

		private void DeselectAllEntries(Catalog catalog)
		{
			if (catalog == null) return;
			foreach (var entry in catalog.Entries)
			{
				entry.Mutation.IsActive = false;
			}
		}

		private void CreateDynamicButton_Mode()
		{
			var texture = _imageLoaderService.GetFutureImageFromFileOrCached(GetPluginPath() + "/Resources/Mode.png");
			var button = _catalogUi.CreateButton(_mainWindow.SubWindow, "", 35, 35, _mainWindow.WindowWidth, 0, new Color(1f, 0f, 0f, 0.5f), Color.red, new Color(1f, 1f, 1f), texture);
			button.button.onClick.AddListener(() =>
			{
				ToggleMode();
			});
			SetTooltipForDynamicButton(button, () => _catalogMode.val);
		}

		private void CreateDynamicButton_Modes()
		{
			var index = 0;
			foreach (var mode in _catalogModes)
			{
				var iconFileName = "Mode.png";
				if (mode == CatalogModeEnum.CATALOG_MODE_VIEW) iconFileName = "CaptureNone.png";
				if (mode == CatalogModeEnum.CATALOG_MODE_SESSION) iconFileName = "CaptureSelected.png";
				if (mode == CatalogModeEnum.CATALOG_MODE_SCENE) iconFileName = "CaptureScenes.png";
				if (mode == CatalogModeEnum.CATALOG_MODE_OBJECT) iconFileName = "CaptureObject.png";
				if (mode == CatalogModeEnum.CATALOG_MODE_MUTATIONS) iconFileName = "CaptureFaceGen.png";
				if (mode == CatalogModeEnum.CATALOG_MODE_CAPTURE) iconFileName = "CapturePerson.png";
				var texture = _imageLoaderService.GetFutureImageFromFileOrCached(GetPluginPath() + "/Resources/" + iconFileName);
				var button = _catalogUi.CreateButton(_mainWindow.SubPanelMode, "", -20, 35, 10, index++ * 40, Color.white, Color.white, new Color(1f, 1f, 1f), texture);
				button.button.onClick.AddListener(() =>
				{
					_catalogMode.val = mode;
					UpdateUiForMode();
					button.buttonColor = Color.red;
				});
				_mainWindow.ModeButtons.Add(mode, button);
				SetTooltipForDynamicButton(button, () => mode);
			}
		}

		private void CreateDynamicButton_Capture()
		{
			var pluginPath = GetPluginPath();

			var captureFaceGen = _imageLoaderService.GetFutureImageFromFileOrCached(pluginPath + "/Resources/CaptureFaceGen.png");
			_mainWindow.MainCaptureButtonIcon = Sprite.Create(captureFaceGen, new Rect(0.0f, 0.0f, captureFaceGen.width, captureFaceGen.height), new Vector2(0.5f, 0.5f), 100.0f);

			var capturePersonTexture = _imageLoaderService.GetFutureImageFromFileOrCached(pluginPath + "/Resources/CapturePerson.png");
			_mainWindow.IconForCapturePerson = Sprite.Create(capturePersonTexture, new Rect(0.0f, 0.0f, capturePersonTexture.width, capturePersonTexture.height), new Vector2(0.5f, 0.5f), 100.0f);

			var captureObjectTexture = _imageLoaderService.GetFutureImageFromFileOrCached(pluginPath + "/Resources/CaptureObject.png");
			_mainWindow.IconForCaptureObject = Sprite.Create(captureObjectTexture, new Rect(0.0f, 0.0f, captureObjectTexture.width, captureObjectTexture.height), new Vector2(0.5f, 0.5f), 100.0f);

			var captureSceneTexture = _imageLoaderService.GetFutureImageFromFileOrCached(pluginPath + "/Resources/CaptureScenes.png");
			_mainWindow.IconForCaptureScenes = Sprite.Create(captureSceneTexture, new Rect(0.0f, 0.0f, captureSceneTexture.width, captureSceneTexture.height), new Vector2(0.5f, 0.5f), 100.0f);

			var captureSelectedTexture = _imageLoaderService.GetFutureImageFromFileOrCached(pluginPath + "/Resources/CaptureSelected.png");
			_mainWindow.IconForCaptureSelectedObject = Sprite.Create(captureSelectedTexture, new Rect(0.0f, 0.0f, captureSelectedTexture.width, captureSelectedTexture.height), new Vector2(0.5f, 0.5f), 100.0f);

			var captureNoneTexture = _imageLoaderService.GetFutureImageFromFileOrCached(pluginPath + "/Resources/CaptureNone.png");
			_mainWindow.IconForCaptureNone = Sprite.Create(captureNoneTexture, new Rect(0.0f, 0.0f, captureNoneTexture.width, captureNoneTexture.height), new Vector2(0.5f, 0.5f), 100.0f);

			_mainWindow.ButtonCapture = _catalogUi.CreateButton(_mainWindow.SubPanelCapture, "", 60, 60, 0, 10, new Color(1, 0.25f, 0.25f), new Color(1, 0.5f, 0.5f), new Color(1, 1, 1));
			SetTooltipForDynamicButton(_mainWindow.ButtonCapture, () => "Capture");
		}

		private void CreateDynamicButton_Play()
		{
			var pluginPath = GetPluginPath();

			var playIcon = _imageLoaderService.GetFutureImageFromFileOrCached(pluginPath + "/Resources/Play.png");
			var playSprite = Sprite.Create(playIcon, new Rect(0.0f, 0.0f, playIcon.width, playIcon.height), new Vector2(0.5f, 0.5f), 100.0f);

			var stopIcon = _imageLoaderService.GetFutureImageFromFileOrCached(pluginPath + "/Resources/Stop.png");
			var stopSprite = Sprite.Create(stopIcon, new Rect(0.0f, 0.0f, stopIcon.width, stopIcon.height), new Vector2(0.5f, 0.5f), 100.0f);

			_mainWindow.ButtonPlayStop = _catalogUi.CreateButton(_mainWindow.SubPanelCapture, "", 60, 60, 0, 10, new Color(0.25f, 0.7f, 0.25f), new Color(0.5f, 1f, 0.5f), new Color(1, 1, 1), playIcon);
			SetTooltipForDynamicButton(_mainWindow.ButtonPlayStop, () => "Play");
			_mainWindow.ButtonPlayStop.button.onClick.AddListener(() =>
			{
				var playState = ToggleCatalogPlay();
				if (playState == true)
				{
					_mainWindow.ButtonPlayStop.buttonColor = Color.red;
					_mainWindow.ButtonPlayStop.button.image.sprite = stopSprite;
					SetTooltipForDynamicButton(_mainWindow.ButtonPlayStop, () => "Stop playing catalog");
				}
				else
				{
					_mainWindow.ButtonPlayStop.buttonColor = Color.green;
					_mainWindow.ButtonPlayStop.button.image.sprite = playSprite;
					SetTooltipForDynamicButton(_mainWindow.ButtonPlayStop, () => "Play catalog");
				}
			});
		}

		private void CreateDynamicButton_CaptureAdditionalAtom()
		{
			try
			{
				var texture = _imageLoaderService.GetFutureImageFromFileOrCached(GetPluginPath() + "/Resources/Add.png");
				_mainWindow.ButtonAddAtomToCapture = _catalogUi.CreateButton(_mainWindow.SubPanelCapture, "", 30, 30, 0, 0, new Color(1, 0.25f, 0.25f), new Color(1, 0.5f, 0.5f), new Color(1, 1, 1), texture);
				_mainWindow.ButtonAddAtomToCapture.button.onClick.AddListener(() =>
				{
					if (_mainWindow.CurrentCatalogEntry == null)
					{
						ShowPopupMessage("Please select a catalog entry first", 2);
						return;
					}
					var selectList = SuperController.singleton.GetAtomUIDs();
					var atomNameAndSelectAction = new Dictionary<string, UnityAction>();
					foreach (var atomUid in selectList)
					{
						UnityAction action = () =>
						{
							var atom = SuperController.singleton.GetAtomByUid(atomUid);
							_mutationsService.CaptureAdditionalAtom(atom, _mainWindow.CurrentCatalogEntry);
							SelectCatalogEntry(_mainWindow.CurrentCatalogEntry);
						};
						atomNameAndSelectAction.Add(atomUid, action);
					}
					ShowSelectList(atomNameAndSelectAction, _dynamicSelectList.transform.position);
				});
				SetTooltipForDynamicButton(_mainWindow.ButtonAddAtomToCapture, () => "Add atom to capture");
			}
			catch (Exception e)
			{
				SuperController.LogError(e.ToString());
			}
		}

		private void CreateDynamicButton_MergeFavoritedEntries()
		{
			try
			{
				var iconTexture = _imageLoaderService.GetFutureImageFromFileOrCached(GetPluginPath() + "/Resources/MergeHearts.png");
				_mainWindow.ButtonCreateMergedEntryGroup = _catalogUi.CreateButton(_mainWindow.SubPanelCapture, "", 45, 45, 0, 0, new Color(1f, 0.5f, 0.05f, 0.5f), new Color(1f, 0.5f, 0.05f, 1f), new Color(1f, 1f, 1f), iconTexture);
				_mainWindow.ButtonCreateMergedEntryGroup.button.onClick.AddListener(() =>
				{
					MergeFavoriteEntries();
				});
				SetTooltipForDynamicButton(_mainWindow.ButtonCreateMergedEntryGroup, () => "Merge Favorite Entries");
			}
			catch (Exception e) { SuperController.LogError(e.ToString()); }
		}

		private void MergeFavoriteEntries(bool asReferencedEntries = false)
		{
			try
			{
				List<CatalogEntry> favoritedEntries = _catalog.Entries.Where(e => e.Favorited > 0).ToList();
				CatalogEntry masterCatalogEntry = CreateMasterCatalogEntry();
				masterCatalogEntry.TransitionTimeInSeconds = 0;
				foreach (var entry in favoritedEntries)
				{
					for (var i = 0; i < entry.Favorited; i++)
					{
						var duplicateEntry = asReferencedEntries ? entry : entry.Clone();
						AddEntryToEntry(duplicateEntry, masterCatalogEntry);
						masterCatalogEntry.TransitionTimeInSeconds += entry.TransitionTimeInSeconds;
					}
					entry.Favorited = 0;
				}
				GenerateNewMasterCatalogEntryImage(masterCatalogEntry);
				RefreshCatalogPosition();
			}
			catch (Exception e) { SuperController.LogError(e.ToString()); }
		}

		private void CreateDynamicButton_ToggleAnimFeatures()
		{
			try
			{
				var iconTexture = _imageLoaderService.GetFutureImageFromFileOrCached(GetPluginPath() + "/Resources/SubItemMenu.png");
				_mainWindow.ButtonToggleAnimFeatures = _catalogUi.CreateButton(_mainWindow.SubPanelCapture, "", 45, 45, 0, 0, _dynamicButtonUnCheckColor, new Color(1f, 0.5f, 0.05f, 1f), new Color(1f, 1f, 1f), iconTexture);
				_mainWindow.ButtonToggleAnimFeatures.button.onClick.AddListener(() =>
				{
					_animationFeaturesVisible = !_animationFeaturesVisible;
					_mainWindow.ButtonToggleAnimFeatures.buttonColor = _animationFeaturesVisible ? new Color(1f, 0.5f, 0.05f, 1f) : _dynamicButtonUnCheckColor;
					CatalogEntry selectedCatalogEntry = GetSelectedCatalogEntryOrDefault();
					if (selectedCatalogEntry != null && selectedCatalogEntry.UiAnimationPanel == null) AddOrShowAnimationUi(selectedCatalogEntry);
					selectedCatalogEntry.UiAnimationPanel.transform.localScale = _animationFeaturesVisible ? Vector3.one : Vector3.zero;
				});
				SetTooltipForDynamicButton(_mainWindow.ButtonToggleAnimFeatures, () => "Toggle Animation Features");
			}
			catch (Exception e)
			{
				SuperController.LogError(e.ToString());
			}
		}

		private CatalogEntry GetSelectedCatalogEntryOrDefault()
		{
			return _catalog.Entries.FirstOrDefault(e => e.Selected);
		}

		private void AddEntryToEntry(CatalogEntry selectedEntry, CatalogEntry masterCatalogEntry)
		{
			masterCatalogEntry.ChildEntries.Add(selectedEntry.Clone());
			masterCatalogEntry.TransitionTimeInSeconds += selectedEntry.TransitionTimeInSeconds;
			UpdateTransitionTimesUi(selectedEntry);
		}

		private void UpdateTransitionTimesUi(CatalogEntry selectedEntry)
		{
			//var masterElement = selectedEntry.AnimatedItem.MasterElement;
			//.UiLabel.buttonText.text = {  }
		}

		private void GenerateNewMasterCatalogEntryImage(CatalogEntry masterCatalogEntry)
		{
			var childCount = masterCatalogEntry.ChildEntries.Count();
			var parentImageMultiplier = (int)Math.Ceiling(Math.Sqrt(childCount)); ;
			var gridSize = parentImageMultiplier * parentImageMultiplier;
			int baseImageSize = 1000 * parentImageMultiplier;
			var baseImage = new Texture2D(baseImageSize, baseImageSize, TextureFormat.RGB24, false);
			float masterTotalTransitionTime = 0;
			for (var i = 0; i < masterCatalogEntry.ChildEntries.Count(); i++)
			{
				var childEntry = masterCatalogEntry.ChildEntries[i];
				masterTotalTransitionTime += childEntry.TransitionTimeInSeconds;
				var entryImageXInMasterImage = (i % Math.Sqrt(gridSize)) * 1000;
				var entryImageYInMasterImage = (Math.Floor(i / (decimal)parentImageMultiplier)) * 1000;
				var finalX = entryImageXInMasterImage;
				var finalY = (baseImageSize - entryImageYInMasterImage) - childEntry.ImageInfo.Texture.height;
				Texture2D entryImage = childEntry.ImageInfo.Texture;
				var pixels = entryImage.GetPixels(0, 0, childEntry.ImageInfo.Texture.width, childEntry.ImageInfo.Texture.height);
				var modifiedPixels = pixels.Select(p => new Color(p.r, p.g, p.b, 0.5f)).ToArray();
				baseImage.SetPixels((int)finalX, (int)finalY, 1000, 1000, pixels);
				//baseImage.Apply();
			}
			TextureScale.Bilinear(baseImage, 1000, 1000);
			baseImage.Apply();
			masterCatalogEntry.ImageInfo.Texture = baseImage;
			masterCatalogEntry.ImageInfo.Width = 1000;
			masterCatalogEntry.ImageInfo.Height = 1000;
			masterCatalogEntry.ImageInfo.Format = TextureFormat.RGB24;
			var image = masterCatalogEntry.UiCatalogEntryPanel.GetComponent<Image>();
			Material mat = new Material(image.material);
			mat.SetTexture(GetUniqueName(), baseImage);
			image.material = mat;
			image.material.mainTexture = baseImage;
		}

		private CatalogEntry CreateMasterCatalogEntry(Texture2D texture = null)
		{
			var imageInfo = new ImageInfo();

			imageInfo.Texture = texture ?? new Texture2D((int)CatalogEntryFrameSize.val, (int)CatalogEntryFrameSize.val);
			imageInfo.Width = CatalogEntryFrameSize.val;
			imageInfo.Height = CatalogEntryFrameSize.val;
			imageInfo.Format = TextureFormat.RGB24;

			var newCatalogEntry = new CatalogEntry();
			newCatalogEntry.UniqueName = GetUniqueName();
			newCatalogEntry.CatalogEntryMode = CatalogModeEnum.CATALOG_MODE_VIEW;
			newCatalogEntry.ImageInfo = imageInfo;
			newCatalogEntry.Mutation = new Mutation();
			MakeCatalogEntry(newCatalogEntry); // ...Capturing and entry
			return newCatalogEntry;
		}

		private void CreateDynamicButton_OpenScenesFolder()
		{
			try
			{
				var texture = _imageLoaderService.GetFutureImageFromFileOrCached(GetPluginPath() + "/Resources/Open.png");
				_mainWindow.ButtonSelectScenesFolder = _catalogUi.CreateButton(_mainWindow.SubPanelCapture, "", 40, 40, 0, 0, new Color(1, 0.25f, 0.25f), new Color(1, 0.5f, 0.5f), new Color(1, 1, 1), texture);
				_mainWindow.ButtonSelectScenesFolder.button.onClick.AddListener(() =>
				{
					BrowseForAndLoadScenesAsCatalog();
				});
				SetTooltipForDynamicButton(_mainWindow.ButtonSelectScenesFolder, () => "Select Scene directory");
			}
			catch (Exception e)
			{
				SuperController.LogError(e.ToString());
			}
		}

		private void CreateDynamicButton_Capture_Hair()
		{
			var texture = _imageLoaderService.GetFutureImageFromFileOrCached(GetPluginPath() + "/Resources/Hair2.png");
			_mainWindow.ToggleButtonCaptureHair = _catalogUi.CreateButton(_mainWindow.SubPanelCapture, "", 35, 35, 60, 0, _dynamicButtonCheckColor, _dynamicButtonCheckColor, new Color(1f, 1f, 1f), texture);
			_mainWindow.ToggleButtonCaptureHair.button.onClick.AddListener(() =>
			{
				UpdateCaptureHairState(!_catalogCaptureHair.val);
			});
			_mainWindow.ToggleButtonCaptureHair.buttonColor = _catalogCaptureHair.val ? _dynamicButtonCheckColor : _dynamicButtonUnCheckColor;
			SetTooltipForDynamicButton(_mainWindow.ToggleButtonCaptureHair, () => "Include Hair");
		}

		private void CreateDynamicButton_Capture_Morphs()
		{
			var texture = _imageLoaderService.GetFutureImageFromFileOrCached(GetPluginPath() + "/Resources/Morph.png");
			_mainWindow.ToggleButtonCaptureMorphs = _catalogUi.CreateButton(_mainWindow.SubPanelCapture, "", 35, 35, 100, 0, _dynamicButtonCheckColor, _dynamicButtonCheckColor, new Color(1f, 1f, 1f), texture);
			_mainWindow.ToggleButtonCaptureMorphs.button.onClick.AddListener(() =>
			{
				if (_catalogCaptureMorphs.val) _mutationsService.SetMorphBaseValuesForCurrentPerson();
				UpdateCaptureMorphsState(!_catalogCaptureMorphs.val);
			});
			_mainWindow.ToggleButtonCaptureMorphs.buttonColor = _catalogCaptureMorphs.val ? _dynamicButtonCheckColor : _dynamicButtonUnCheckColor;
			SetTooltipForDynamicButton(_mainWindow.ToggleButtonCaptureMorphs, () => "Include Morphs");
		}

		private void CreateDynamicButton_Capture_Clothes()
		{
			var texture = _imageLoaderService.GetFutureImageFromFileOrCached(GetPluginPath() + "/Resources/Clothing.png");
			_mainWindow.ToggleButtonCaptureClothes = _catalogUi.CreateButton(_mainWindow.SubPanelCapture, "", 35, 35, 60, 40, _dynamicButtonCheckColor, _dynamicButtonCheckColor, new Color(1f, 1f, 1f), texture);
			_mainWindow.ToggleButtonCaptureClothes.button.onClick.AddListener(() =>
			{
				UpdateCaptureClothesState(!_catalogCaptureClothes.val);
			});
			_mainWindow.ToggleButtonCaptureClothes.buttonColor = _catalogCaptureClothes.val ? _dynamicButtonCheckColor : _dynamicButtonUnCheckColor;
			SetTooltipForDynamicButton(_mainWindow.ToggleButtonCaptureClothes, () => "Include Clothes");
		}

		private void CreateDynamicButton_Capture_Pose()
		{
			var texture = _imageLoaderService.GetFutureImageFromFileOrCached(GetPluginPath() + "/Resources/Pose.png");
			_mainWindow.ToggleButtonCapturePose = _catalogUi.CreateButton(_mainWindow.SubPanelCapture, "", 35, 35, 100, 0, _dynamicButtonCheckColor, _dynamicButtonCheckColor, new Color(1f, 1f, 1f), texture);
			_mainWindow.ToggleButtonCapturePose.button.onClick.AddListener(() =>
			{
				UpdateCapturePoseState(!_catalogCapturePose.val);
			});
			_mainWindow.ToggleButtonCapturePose.buttonColor = _catalogCapturePose.val ? _dynamicButtonCheckColor : _dynamicButtonUnCheckColor;
			SetTooltipForDynamicButton(_mainWindow.ToggleButtonCapturePose, () => "Include Pose");
		}

		private void CreateDynamicButton_RemoveAllClothing()
		{
			var texture = _imageLoaderService.GetFutureImageFromFileOrCached(GetPluginPath() + "/Resources/RemoveAllClothing.png");
			_mainWindow.ButtonRemoveAllClothing = _catalogUi.CreateButton(_mainWindow.SubPanelSceneTools, "", 35, 35, 100, 0, new Color(0.25f, 0.5f, 0.5f, 1), Color.green, new Color(1f, 1f, 1f), texture);
			_mainWindow.ButtonRemoveAllClothing.button.onClick.AddListener(() =>
			{
				_mutationsService.RemoveAllClothing();
			});
			SetTooltipForDynamicButton(_mainWindow.ButtonRemoveAllClothing, () => "Remove All Clothing");
		}

		private void CreateDynamicButton_RemoveAllHair()
		{
			var texture = _imageLoaderService.GetFutureImageFromFileOrCached(GetPluginPath() + "/Resources/RemoveAllHair.png");
			_mainWindow.ButtonRemoveAllHair = _catalogUi.CreateButton(_mainWindow.SubPanelSceneTools, "", 35, 35, 100, 0, new Color(0.25f, 0.5f, 0.5f, 1), Color.green, new Color(1f, 1f, 1f), texture);
			_mainWindow.ButtonRemoveAllHair.button.onClick.AddListener(() =>
			{
				_mutationsService.RemoveAllHair();
			});
			SetTooltipForDynamicButton(_mainWindow.ButtonRemoveAllHair, () => "Remove All Hair");
		}

		private void UpdateCaptureClothesState(bool newValue)
		{
			_mutationsService.MustCaptureClothes = newValue;
			_catalog.CaptureClothes = newValue;
			_catalogCaptureClothes.val = newValue;
			_mainWindow.ToggleButtonCaptureClothes.buttonColor = _catalogCaptureClothes.val ? _dynamicButtonCheckColor : _dynamicButtonUnCheckColor;
		}

		private void UpdateCaptureHairState(bool newValue)
		{
			_mutationsService.MustCaptureHair = newValue;
			_catalog.CaptureHair = newValue;
			_catalogCaptureHair.val = newValue;
			_mainWindow.ToggleButtonCaptureHair.buttonColor = _catalogCaptureHair.val ? _dynamicButtonCheckColor : _dynamicButtonUnCheckColor;
		}

		private void UpdateCaptureMorphsState(bool newValue)
		{
			_mutationsService.MustCaptureActiveMorphs = newValue;
			_catalog.CaptureMorphs = newValue;
			_catalogCaptureMorphs.val = newValue;
			_mainWindow.ToggleButtonCaptureMorphs.buttonColor = _catalogCaptureMorphs.val ? _dynamicButtonCheckColor : _dynamicButtonUnCheckColor;
		}

		private void UpdateCapturePoseState(bool newValue)
		{
			_mutationsService.MustCapturePoseMorphs = newValue;
			_catalog.CapturePose = newValue;
			_catalogCapturePose.val = newValue;
			_mainWindow.ToggleButtonCapturePose.buttonColor = _catalogCapturePose.val ? _dynamicButtonCheckColor : _dynamicButtonUnCheckColor;
		}

		private void CreateDynamicButton_ResetCatalog(DynamicMainWindow mainWindow)
		{
			var texture = _imageLoaderService.GetFutureImageFromFileOrCached(GetPluginPath() + "/Resources/Reset3.png");
			mainWindow.ButtonResetCatalog = _catalogUi.CreateButton(mainWindow.SubPanelManageCatalog, "", 30, 30, 0, 0, new Color(1f, 0.5f, 0.05f, 0.5f), new Color(1f, 0.5f, 0.05f, 1f), new Color(1f, 1f, 1f), texture);
			mainWindow.ButtonResetCatalog.button.onClick.AddListener(() =>
			{
				Action confirmAction = () => ReinitializeCatalog(); //...Reset Catalog (from UI)
				ShowConfirm("Reset catalog?", confirmAction);
			});
			SetTooltipForDynamicButton(mainWindow.ButtonResetCatalog, () => "Reset catalog");
		}

		private void CreateDynamicButton_ZoomIn(DynamicMainWindow mainWindow)
		{
			var texture = _imageLoaderService.GetFutureImageFromFileOrCached(GetPluginPath() + "/Resources/ZoomIn.png");
			mainWindow.ButtonZoomIn = _catalogUi.CreateButton(mainWindow.SubPanelManageCatalog, "", 35, 35, 0, 0, new Color(1f, 0.5f, 0.05f, 0.5f), new Color(1f, 0.5f, 0.05f, 1f), new Color(1f, 1f, 1f), texture);
			mainWindow.ButtonZoomIn.button.onClick.AddListener(() => ZoomInCatalog());
			SetTooltipForDynamicButton(mainWindow.ButtonZoomIn, () => "Increase frame size");
		}

		private void CreateDynamicButton_ZoomOut(DynamicMainWindow mainWindow)
		{
			var texture = _imageLoaderService.GetFutureImageFromFileOrCached(GetPluginPath() + "/Resources/ZoomOut.png");
			mainWindow.ButtonZoomOut = _catalogUi.CreateButton(mainWindow.SubPanelManageCatalog, "", 35, 35, 0, 0, new Color(1f, 0.5f, 0.05f, 0.5f), new Color(1f, 0.5f, 0.05f, 1f), new Color(1f, 1f, 1f), texture);
			mainWindow.ButtonZoomOut.button.onClick.AddListener(() => ZoomOutCatalog());
			SetTooltipForDynamicButton(mainWindow.ButtonZoomOut, () => "Decrease frame size");
		}
		private void CreateDynamicButton_ScrollUp(DynamicMainWindow mainWindow)
		{
			var texture = _imageLoaderService.GetFutureImageFromFileOrCached(GetPluginPath() + "/Resources/Previous.png");
			mainWindow.ButtonScrollUp = _catalogUi.CreateButton(mainWindow.SubPanelManageCatalog, "", 35, 35, 0, 0, new Color(1f, 0.5f, 0.05f, 0.5f), new Color(1f, 0.5f, 0.05f, 1f), new Color(1f, 1f, 1f), texture);
			mainWindow.ButtonScrollUp.button.onClick.AddListener(() => ShiftCatalogEntriesForward());
			SetTooltipForDynamicButton(mainWindow.ButtonScrollUp, () => "Shift catalog entries forward");
		}

		private void CreateDynamicButton_ScrollDown(DynamicMainWindow mainWindow)
		{
			var texture = _imageLoaderService.GetFutureImageFromFileOrCached(GetPluginPath() + "/Resources/Next.png");
			mainWindow.ButtonScrollDown = _catalogUi.CreateButton(mainWindow.SubPanelManageCatalog, "", 35, 35, 0, 0, new Color(1f, 0.5f, 0.05f, 0.5f), new Color(1f, 0.5f, 0.05f, 1f), new Color(1f, 1f, 1f), texture);
			mainWindow.ButtonScrollDown.button.onClick.AddListener(() => ShiftCatalogEntriesBackward());
			SetTooltipForDynamicButton(mainWindow.ButtonScrollDown, () => "Shift catalog entries back");
		}

		private void CreateDynamicButton_Sort(DynamicMainWindow mainWindow)
		{
			var texture = _imageLoaderService.GetFutureImageFromFileOrCached(GetPluginPath() + "/Resources/Clean.png");
			_mainWindow.ButtonSort = _catalogUi.CreateButton(mainWindow.SubPanelManageCatalog, "", 35, 35, 0, 80, new Color(1f, 0.5f, 0.05f, 0.5f), new Color(1f, 0.5f, 0.05f, 1f), new Color(1f, 1f, 1f), texture);
			_mainWindow.ButtonSort.button.onClick.AddListener(() => SortCatalog());
			SetTooltipForDynamicButton(_mainWindow.ButtonSort, () => "Sort and discard");
		}

		private void CreateDynamicButton_ShowDebugButton(DynamicMainWindow mainWindow)
		{
			var texture = _imageLoaderService.GetFutureImageFromFileOrCached(GetPluginPath() + "/Resources/Debug.png");
			mainWindow.ButtonShowDebug = _catalogUi.CreateButton(mainWindow.SubPanelManageCatalog, "", 35, 35, 280, 80, new Color(1f, 0.5f, 0.05f, 0.5f), new Color(1f, 0.5f, 0.05f, 1f), new Color(1f, 1f, 1f), texture);
			mainWindow.ButtonShowDebug.button.onClick.AddListener(() => ToggleDebugPanel());
			SetTooltipForDynamicButton(mainWindow.ButtonShowDebug, () => "Show debug panel");
		}

		private void CreateDynamicButton_SelectSceneAtom(DynamicMainWindow mainWindow)
		{
			var texture = _imageLoaderService.GetFutureImageFromFileOrCached(GetPluginPath() + "/Resources/Select.png");
			mainWindow.ButtonSelectSceneAtom = _catalogUi.CreateButton(mainWindow.SubPanelSceneTools, "", 35, 35, 0, 120, new Color(0.25f, 0.5f, 0.5f), Color.green, new Color(1f, 1f, 1f), texture);
			mainWindow.ButtonSelectSceneAtom.buttonText.fontSize = 20;
			mainWindow.ButtonSelectSceneAtom.button.onClick.AddListener(() =>
			{
				UnityAction<string> withSelectedAtom = (atomName) =>
				{
					var atom = SuperController.singleton.GetAtomByUid(atomName);
					if (atom == null) return;
					SuperController.singleton.SelectController(atom.mainController);
				};
				ShowSelectAtomFromSceneList(withSelectedAtom);
			});
			SetTooltipForDynamicButton(mainWindow.ButtonSelectSceneAtom, () =>
			{
				var currentAtom = SuperController.singleton.GetSelectedAtom();
				var currentAtomName = currentAtom?.name;
				return $"Select Atom in Scene. Current Atom: {currentAtomName ?? "(none)"}";
			});
		}

		private void ShowSelectAtomFromSceneList(UnityAction<string> whatToDoWithSelectedAtom)
		{
			try
			{
				var atomNames = SuperController.singleton.GetAtomUIDs();
				Dictionary<string, UnityAction> atomsToSelect = new Dictionary<string, UnityAction>();
				foreach (var atomName in atomNames)
				{
					UnityAction selectAction = () =>
					{
						whatToDoWithSelectedAtom.Invoke(atomName);
					};
					atomsToSelect.Add(atomName, selectAction);
				}
				ShowSelectList(atomsToSelect);
			}
			catch (Exception e)
			{
				SuperController.LogError(e.ToString());
			}
		}

		private void CreateDynamicButton_ResetPivot(DynamicMainWindow mainWindow)
		{
			var texture = _imageLoaderService.GetFutureImageFromFileOrCached(GetPluginPath() + "/Resources/CenterPivot.png");
			mainWindow.ButtonResetPivot = _catalogUi.CreateButton(mainWindow.SubPanelSceneTools, "", 35, 35, 40, 120, new Color(0.25f, 0.5f, 0.5f), Color.green, new Color(1f, 1f, 1f), texture);
			mainWindow.ButtonResetPivot.buttonText.fontSize = 20;
			mainWindow.ButtonResetPivot.button.onClick.AddListener(() =>
			{
				var currentAtom = SuperController.singleton.GetSelectedAtom();
				if (currentAtom == null) currentAtom = containingAtom;
				if (currentAtom == null || currentAtom.type != "Person")
				{
					ShowPopupMessage("Can only do this for a 'Person' atom", 2);
					return;
				}
				Action confirmAction = () => CenterPivot(currentAtom);
				ShowConfirm("Center pivot for " + currentAtom.name + "?", confirmAction);
			});
			SetTooltipForDynamicButton(mainWindow.ButtonResetPivot, () => "Center Control Pivot (Person must be selected)");
		}

		public void AddDragging(GameObject handleObject, GameObject dragObject, Action<DragHelper> beforeDragAction = null, Action<DragHelper> afterDragAction = null, Func<float, float, bool> whileDragAction = null, bool dragX = true, bool dragY = true)
		{
			var positionTracker = new DragHelper();
			_positionTrackers.Add("draggableObject_" + DateTime.Now + GetUniqueName(), positionTracker);
			Action<DragHelper> onStartDraggingEvent = (dragHelper) =>
			{
				positionTracker.XStep = 20;
				positionTracker.YStep = 20;
				positionTracker.IsIn3DSpace = !IsAnchoredOnHUD();
				positionTracker.XMultiplier = -1000f;
				positionTracker.YMultiplier = 1000f;
				if (beforeDragAction != null) beforeDragAction.Invoke(dragHelper);
			};
			positionTracker.AddMouseDraggingToObject(handleObject, dragObject, dragX, dragY, onStartDraggingEvent, afterDragAction, whileDragAction);
		}

		//public void AddDragging(GameObject handleObject, GameObject dragObject, Action<DragHelper> beforeDragAction = null, Action<DragHelper> afterDragAction = null, Func<float, float, bool> whileDragAction = null, bool dragX = true, bool dragY = true)
		//{
		//	var positionTracker = new DragHelper();
		//	_positionTrackers.Add("draggableObject_" + DateTime.Now + GetUniqueName(), positionTracker);
		//	positionTracker.AddMouseDraggingToObject(handleObject, dragObject, dragX, dragY, beforeDragAction, afterDragAction, whileDragAction);
		//}

		private void CreateDynamicButton_CreateMannequinPicker(DynamicMainWindow mainWindow)
		{
			var texture = _imageLoaderService.GetFutureImageFromFileOrCached(GetPluginPath() + "/Resources/NextObject2.png");
			mainWindow.ButtonCreateMannequinPicker = _catalogUi.CreateButton(mainWindow.SubPanelSceneTools, "", 35, 35, 80, 120, new Color(0.25f, 0.5f, 0.5f), Color.green, new Color(1f, 1f, 1f), texture);
			mainWindow.ButtonCreateMannequinPicker.buttonText.fontSize = 20;
			mainWindow.ButtonCreateMannequinPicker.button.onClick.AddListener(() =>
			{
				var newMannequinPicker = _mannequinService.CreateMannequinPicker();
				_mannequinPickers.Add(newMannequinPicker);
			});
			SetTooltipForDynamicButton(mainWindow.ButtonCreateMannequinPicker, () =>
			{
				var currentAtom = SuperController.singleton.GetSelectedAtom();
				var currentAtomName = currentAtom?.name;
				return $"Show Mannequin";
			});
		}

		private void CreateDynamicButton_PopupMessage()
		{
			_mainWindow.ButtonPopupMessageLabel = _catalogUi.CreateButton(_mainWindow.ParentWindowContainer, "Popup...", 160, 40, 0, 0, new Color(0.0f, 0.0f, 0.0f, 1f), new Color(0.1f, 0.1f, 0.1f, 1f), new Color(1f, 0.3f, 0.3f, 1));
			_mainWindow.ButtonPopupMessageLabel.button.onClick.AddListener(() => HidePopupMessage());
			_mainWindow.ButtonPopupMessageLabel.buttonText.alignment = TextAnchor.MiddleLeft;
			_mainWindow.ButtonPopupMessageLabel.transform.localScale = Vector3.zero;
		}

		private void CreateDynamicButton_SelectList()
		{
			_dynamicSelectList = CatalogUiHelper.CreatePanel(_mainWindow.ParentWindowContainer, 0, 0, 0, 0, new Color(0.1f, 0.1f, 0.1f, 0.9f), Color.green);

			var backPanel = CatalogUiHelper.CreatePanel(_dynamicSelectList, 280, 550, -10, -310, new Color(0.15f, 0.15f, 0.15f), Color.green);
			var cancelButton = _catalogUi.CreateButton(_dynamicSelectList, "X", 40, 40, 230, -350, new Color(0.2f, 0.2f, 0.2f, 1f), new Color(0.5f, 0.5f, 0.5f, 1f), new Color(1f, 0.3f, 0.3f, 1));
			cancelButton.button.onClick.AddListener(() => HideSelectList());
			_selectListVLayout = _catalogUi.CreateVerticalLayout(backPanel, 0, true, false, false, false);
			_dynamicSelectList.transform.localScale = Vector3.zero;
		}

		public void SetPositionState(FreeControllerV3 controller, string positionState)
		{
			controller.SetPositionStateFromString(positionState);
		}

		public void SetRotationState(FreeControllerV3 controller, string state)
		{
			controller.SetRotationStateFromString(state);
		}

		public string TogglePositionOnOff(string controllerName, string atomName, string inputState = null)
		{
			var atom = SuperController.singleton.GetAtomByUid(atomName);
			if (atom == null) SuperController.LogError("could not find atom by name: " + atomName ?? "NULL");
			FreeControllerV3 controller = GetControllerForAtom(controllerName, atom);
			if (controller == null) SuperController.LogError("could not find controller by name: " + controllerName ?? "NULL");
			if (inputState == null)
			{
				inputState = (controller.currentPositionState != FreeControllerV3.PositionState.Off)
					? FreeControllerV3.PositionState.Off.ToString()
					: FreeControllerV3.PositionState.On.ToString();
			}
			controller.SetPositionStateFromString(inputState);
			return inputState;
		}

		public FreeControllerV3 GetControllerForAtom(string controllerName, Atom atom)
		{
			var controller = atom.GetComponentsInChildren<FreeControllerV3>(true).SingleOrDefault(c => c.storeId == controllerName || c.name == controllerName);
			if (controller == null)
			{
				controller = atom.freeControllers.SingleOrDefault(c => c.name == controllerName);
			}
			return controller;
		}

		public List<string> GetControllerNamesForAtom(Atom atom)
		{
			var controllerNames = atom.name != null ? SuperController.singleton.GetFreeControllerNamesInAtom(atom.name) : new List<string>();
			if (controllerNames.Count == 0) controllerNames = atom.GetComponentsInChildren<FreeControllerV3>(true).Select(c => c.name).ToList();
			return controllerNames;
		}

		public string ToggleRotationOnOff(string controllerName, string atomName, string inputState = null)
		{
			var atom = SuperController.singleton.GetAtomByUid(atomName);
			var controller = GetControllerForAtom(controllerName, atom);
			if (inputState == null)
			{
				inputState = (controller.currentRotationState != FreeControllerV3.RotationState.Off)
					? FreeControllerV3.RotationState.Off.ToString()
					: FreeControllerV3.RotationState.On.ToString();
			}
			controller.SetRotationStateFromString(inputState);
			return inputState;
		}

		public void SelectNextControllerPositionMode(string controllerName, string atomName)
		{
			var atom = SuperController.singleton.GetAtomByUid(atomName);
			var controller = GetControllerForAtom(controllerName, atom);
			if (controller.currentPositionState == FreeControllerV3.PositionState.On)
				controller.currentPositionState = FreeControllerV3.PositionState.Comply;
			else if (controller.currentPositionState == FreeControllerV3.PositionState.Comply)
				controller.currentPositionState = FreeControllerV3.PositionState.Off;
			else if (controller.currentPositionState == FreeControllerV3.PositionState.Off)
				controller.currentPositionState = FreeControllerV3.PositionState.ParentLink;
			else if (controller.currentPositionState == FreeControllerV3.PositionState.ParentLink)
				controller.currentPositionState = FreeControllerV3.PositionState.PhysicsLink;
			else if (controller.currentPositionState == FreeControllerV3.PositionState.PhysicsLink)
				controller.currentPositionState = FreeControllerV3.PositionState.Hold;
			else if (controller.currentPositionState == FreeControllerV3.PositionState.Hold)
				controller.currentPositionState = FreeControllerV3.PositionState.Lock;
			else if (controller.currentPositionState == FreeControllerV3.PositionState.Lock)
				controller.currentPositionState = FreeControllerV3.PositionState.On;
		}

		public void SelectNextControllerRotationMode(string controllerName, string atomName)
		{
			var atom = SuperController.singleton.GetAtomByUid(atomName);
			var controller = GetControllerForAtom(controllerName, atom);
			if (controller.currentRotationState == FreeControllerV3.RotationState.On)
				controller.currentRotationState = FreeControllerV3.RotationState.Comply;
			else if (controller.currentRotationState == FreeControllerV3.RotationState.Comply)
				controller.currentRotationState = FreeControllerV3.RotationState.Off;
			else if (controller.currentRotationState == FreeControllerV3.RotationState.Off)
				controller.currentRotationState = FreeControllerV3.RotationState.ParentLink;
			else if (controller.currentRotationState == FreeControllerV3.RotationState.ParentLink)
				controller.currentRotationState = FreeControllerV3.RotationState.PhysicsLink;
			else if (controller.currentRotationState == FreeControllerV3.RotationState.PhysicsLink)
				controller.currentRotationState = FreeControllerV3.RotationState.Hold;
			else if (controller.currentRotationState == FreeControllerV3.RotationState.Hold)
				controller.currentRotationState = FreeControllerV3.RotationState.Lock;
			else if (controller.currentRotationState == FreeControllerV3.RotationState.Lock)
				controller.currentRotationState = FreeControllerV3.RotationState.On;
		}

		public void UpdatePointerState(DynamicJointPoint jointPoint, string controllerName, Atom atom)
		{
			try
			{
				if (controllerName == null) return;
				if (atom == null) return;
				var controller = GetControllerForAtom(controllerName, atom);
				if (controller == null) return;
				jointPoint.rotationButton.buttonColor = GetColorForRotationState(controller.currentRotationState);
				jointPoint.positionButton.buttonColor = GetColorForPositionState(controller.currentPositionState);
			}
			catch (Exception e)
			{
				SuperController.LogError(e.ToString());
				SuperController.LogError("controllerName = " + controllerName);
				SuperController.LogError("atom.name = " + atom.name);
			}
		}

		private Color GetColorForRotationState(FreeControllerV3.RotationState rotationState)
		{
			switch (rotationState)
			{
				case FreeControllerV3.RotationState.On: return Color.green;
				case FreeControllerV3.RotationState.Comply: return Color.yellow;
				case FreeControllerV3.RotationState.Off: return Color.grey;
				case FreeControllerV3.RotationState.ParentLink: return Color.cyan;
				case FreeControllerV3.RotationState.PhysicsLink: return new Color(0.5f, 0.5f, 1);
				case FreeControllerV3.RotationState.Hold: return Color.magenta;
				case FreeControllerV3.RotationState.Lock: return Color.red;
			}
			return Color.black;
		}

		private Color GetColorForPositionState(FreeControllerV3.PositionState rotationState)
		{
			switch (rotationState)
			{
				case FreeControllerV3.PositionState.On: return Color.green;
				case FreeControllerV3.PositionState.Comply: return Color.yellow;
				case FreeControllerV3.PositionState.Off: return Color.grey;
				case FreeControllerV3.PositionState.ParentLink: return Color.cyan;
				case FreeControllerV3.PositionState.PhysicsLink: return new Color(0.7f, 0.7f, 1);
				case FreeControllerV3.PositionState.Hold: return Color.magenta;
				case FreeControllerV3.PositionState.Lock: return Color.red;
			}
			return Color.black;

		}

		public void SelectController(string controllerName, string atomName)
		{
			try
			{
				var selectedAtom = (atomName == null) ? SuperController.singleton.GetSelectedAtom() : SuperController.singleton.GetAtomByUid(atomName);
				if (containingAtom.type == "Person") selectedAtom = containingAtom;
				if (selectedAtom == null)
				{
					ShowPopupMessage("Please select a Person", 2);
					return;
				}
				if (controllerName == null)
				{
					ShowPopupMessage("Null Controller", 2);
					return;
				}

				SuperController.singleton.SelectController(atomName, controllerName);
			}
			catch (Exception e)
			{
				SuperController.LogError(e.ToString());
			}
		}

		private void CreateDynamicButton_DynamicConfirm()
		{
			_dynamicConfirmPanel = CatalogUiHelper.CreatePanel(_mainWindow.ParentWindowContainer, 0, 0, 0, 0, new Color(0.1f, 0.1f, 0.1f, 0.9f), Color.green);
			var backPanel = CatalogUiHelper.CreatePanel(_dynamicConfirmPanel, _mainWindow.WindowWidth, 210, 0, -_mainWindow.WindowHeight, new Color(1f, 0.5f, 0.05f, 1f), new Color(1f, 0.5f, 0.05f, 1f));
			_dynamicConfirmLabel = _catalogUi.CreateTextField(_dynamicConfirmPanel, "", _mainWindow.WindowWidth - 20, 140, 10, -_mainWindow.WindowHeight + 10, new Color(0.25f, 0.25f, 0.25f), Color.white);
			_dynamicConfirmLabel.UItext.fontSize = 20;
			var confirmButton = _catalogUi.CreateButton(_dynamicConfirmPanel, "OK", 140, 40, _mainWindow.WindowWidth - 300 - 10, -_mainWindow.WindowHeight + 160, new Color(0.2f, 0.2f, 0.2f, 1f), new Color(0.5f, 0.5f, 0.5f, 1f), new Color(1f, 0.3f, 0.3f, 1));
			confirmButton.button.onClick.AddListener(() =>
			{
				_lastConfirmAction.Invoke();
				HideConfirmMessage();
			});
			var cancelButton = _catalogUi.CreateButton(_dynamicConfirmPanel, "Cancel", 140, 40, _mainWindow.WindowWidth - 140 - 10, -_mainWindow.WindowHeight + 160, new Color(0.2f, 0.2f, 0.2f, 1f), new Color(0.5f, 0.5f, 0.5f, 1f), new Color(1f, 0.3f, 0.3f, 1));
			cancelButton.button.onClick.AddListener(() => HideConfirmMessage());
			_dynamicConfirmPanel.transform.localScale = Vector3.zero;
		}

		private void CreateDynamicButton_DynamicHelp()
		{
			_dynamicHelpPanel = CatalogUiHelper.CreatePanel(_mainWindow.ParentWindowContainer, 0, 0, 0, 0, new Color(0.1f, 0.1f, 0.1f, 0.9f), Color.green);
			var backPanel = CatalogUiHelper.CreatePanel(_dynamicHelpPanel, _mainWindow.WindowWidth, 210, 0, -_mainWindow.WindowHeight, new Color(0.25f, 0.25f, 0.25f), Color.green);
			_dynamicHelpLabel = _catalogUi.CreateTextField(_dynamicHelpPanel, "", _mainWindow.WindowWidth - 20, 140, 10, -_mainWindow.WindowHeight + 10, new Color(0.25f, 0.25f, 0.25f), Color.white);
			_dynamicHelpLabel.UItext.fontSize = 20;
			var cancelButton = _catalogUi.CreateButton(_dynamicHelpPanel, "Close", 140, 40, _mainWindow.WindowWidth - 140 - 10, -_mainWindow.WindowHeight + 160, new Color(0.2f, 0.2f, 0.2f, 1f), new Color(0.5f, 0.5f, 0.5f, 1f), new Color(1f, 0.3f, 0.3f, 1));
			cancelButton.button.onClick.AddListener(() => HideConfirmMessage());
			_dynamicHelpPanel.transform.localScale = Vector3.zero;
		}

		private void CreateDynamicPanel_RightInfoPanel()
		{
			_mainWindow.DynamicInfoPanel = CatalogUiHelper.CreatePanel(_mainWindow.SubWindow, 0, 0, _mainWindow.WindowWidth + 70, 0, Color.red, Color.clear);
			var innerPanel = CatalogUiHelper.CreatePanel(_mainWindow.DynamicInfoPanel, 200, _mainWindow.WindowHeight, -25, -10, new Color(0.1f, 0.1f, 0.1f, 0.9f), Color.clear);
			_mainWindow.InfoVLayout = _catalogUi.CreateVerticalLayout(innerPanel, 1, true, false, false, false);
			_mainWindow.DynamicInfoPanel.transform.localScale = Vector3.zero;
		}

		private void CreateDynamicPanel_LeftInfo()
		{
			_mainWindow.InfoLabel = _catalogUi.CreateTextField(_mainWindow.SubWindow, "", 200, _mainWindow.WindowHeight - 20, -270, -_defaultFrameSize, new Color(0, 0, 0, 0.3f), Color.white);
			_mainWindow.InfoLabel.UItext.fontSize = 15;
			_mainWindow.InfoLabel.UItext.fontStyle = FontStyle.Italic;
			_mainWindow.InfoLabel.UItext.alignment = TextAnchor.MiddleRight;
			_mainWindow.InfoLabel.transform.localScale = Vector3.zero;
		}

		public void AddEntrySubItemToggle(EntrySubItem entryItem, UnityAction<bool> onToggle, UnityAction<string> stopTrackingAction, List<EntrySubItemAction> tooltip_iconName_action = null)
		{
			if (_mainWindow.SubItemPanelMinimized) return;

			GameObject buttonRow = CreateButtonRow(_mainWindow.DynamicInfoPanel, 25);
			entryItem.ButtonRow = buttonRow;

			// Add stop tracking button
			var texture = _imageLoaderService.GetFutureImageFromFileOrCached(GetPluginPath() + "/Resources/Delete3.png");
			var stopTrackingButton = _catalogUi.CreateButton(buttonRow, "", 20, 20, 0, 0, Color.red, new Color(1f, 0.5f, 0.5f), Color.black, texture);
			stopTrackingButton.button.onClick.AddListener(() =>
			{
				stopTrackingAction.Invoke(entryItem.ItemName);
				RemoveUiCatalogSubItem(entryItem);
			});
			entryItem.StopTrackingItemButton = stopTrackingButton;
			SetTooltipForDynamicButton(stopTrackingButton, () => "Remove from catalog entry");

			// Main item button
			var currentTextColor = entryItem.CheckState ? new Color(1f, 0.3f, 0.3f, 1) : new Color(0.3f, 0.3f, 0.3f, 1);
			var itemButton = _catalogUi.CreateButton(buttonRow, entryItem.Label ?? entryItem.ItemName, 160, 25, 0, 0, Color.clear, new Color(0.3f, 0.3f, 0.2f), currentTextColor);
			buttonRow.transform.SetParent(_mainWindow.InfoVLayout.transform, false);
			itemButton.buttonText.fontSize = 15;
			itemButton.buttonText.fontStyle = FontStyle.Italic;
			itemButton.buttonText.alignment = TextAnchor.MiddleLeft;
			itemButton.button.onClick.AddListener(() =>
			{
				var newValue = itemButton.textColor == new Color(0.3f, 0.3f, 0.3f, 1) ? true : false;
				itemButton.textColor = newValue ? new Color(1f, 0.3f, 0.3f, 1) : new Color(0.3f, 0.3f, 0.3f, 1);
				onToggle.Invoke(newValue);
			});
			entryItem.ItemActiveCheckbox = itemButton;
			SetTooltipForDynamicButton(itemButton, () => itemButton.label);

			if (tooltip_iconName_action != null)
			{
				foreach (var extraAction in tooltip_iconName_action)
				{
					// Add stop tracking button
					var buttonIcon = extraAction.IconName != null ? _imageLoaderService.GetFutureImageFromFileOrCached(GetPluginPath() + "/Resources/" + extraAction.IconName) : null;
					var buttonText = extraAction.Text ?? "";
					var button = _catalogUi.CreateButton(buttonRow, buttonText, extraAction.ButtonWidth, 20, 0, 0, extraAction.ButtonColor, Color.green, extraAction.TextColor, buttonIcon);
					button.buttonText.fontSize = 15;
					button.buttonText.fontStyle = FontStyle.Italic;
					button.buttonText.alignment = TextAnchor.MiddleLeft;
					button.button.onClick.AddListener(() =>
					{
						extraAction.ClickAction.Invoke(entryItem);
					});
					SetTooltipForDynamicButton(button, () => extraAction.Tooltip);
					entryItem.ExtraButtons.Add(button);
				}
			}
		}

		public void ShowSelectList(Dictionary<string, UnityAction> itemList, Vector3? position = null)
		{
			foreach (var item in _dynamicListItems)
			{
				RemoveUiCatalogSubItem(item);
			}
			_dynamicListItems = itemList.Select(i => AddSelectButtonToPopupList(i.Key, i.Value)).ToList();
			_dynamicSelectList.transform.localScale = Vector3.one;
			if (position != null) _dynamicSelectList.transform.localPosition = position ?? Vector3.zero;
		}

		public EntrySubItem AddSelectButtonToPopupList(string label, UnityAction onSelect)
		{
			var labelXButtonAndGroup = new EntrySubItem();
			GameObject buttonColumn = CreateButtonColumn(_dynamicSelectList, 25);
			labelXButtonAndGroup.ButtonRow = buttonColumn;
			var button = _catalogUi.CreateButton(buttonColumn, label, 280, 25, 0, 0, new Color(0.15f, 0.15f, 0.15f), new Color(0.5f, 0.3f, 0.3f), new Color(0.8f, 0.8f, 0.8f));
			buttonColumn.transform.SetParent(_selectListVLayout.transform, false);
			button.buttonText.fontSize = 20;
			button.buttonText.fontStyle = FontStyle.Italic;
			button.buttonText.alignment = TextAnchor.MiddleLeft;
			button.button.onClick.AddListener(() =>
			{
				onSelect.Invoke();
				_dynamicSelectList.transform.localScale = Vector3.zero;
			});
			labelXButtonAndGroup.ItemActiveCheckbox = button;

			return labelXButtonAndGroup;
		}

		public void RemoveUiCatalogSubItem(EntrySubItem catalogSubItemItem)
		{
			try
			{
				if (catalogSubItemItem.ItemActiveCheckbox != null) RemoveButton(catalogSubItemItem.ItemActiveCheckbox);
				if (catalogSubItemItem.StopTrackingItemButton != null) RemoveButton(catalogSubItemItem.StopTrackingItemButton);
				if (catalogSubItemItem.ButtonRow != null) Destroy(catalogSubItemItem.ButtonRow);
				int overflow = 0;
				if (catalogSubItemItem.ExtraButtons != null)
				{
					while (catalogSubItemItem.ExtraButtons.Count > 0)
					{
						if (overflow > 100) throw new Exception("overflow");
						var item = catalogSubItemItem.ExtraButtons.RemoveLast();
						Destroy(item);
					}
				}
			}
			catch (Exception e)
			{
				SuperController.LogError(e.ToString());
			}
		}

		private void CreateDynamicButton_Tooltip()
		{
			_mainWindow.TextToolTip = _catalogUi.CreateTextField(_mainWindow.ParentWindowContainer, "Tooltip...", _mainWindow.WindowWidth, 100, 0, _mainWindow.WindowHeight, Color.clear, new Color(0.7f, 0.7f, 0.7f, 1));
			_mainWindow.TextToolTip.UItext.fontSize = 17;
			_mainWindow.TextToolTip.UItext.alignment = TextAnchor.MiddleLeft;
			_mainWindow.TextToolTip.transform.localScale = Vector3.zero;
		}

		private void CreateDynamicButton_DebugPanel()
		{
			var mainPanel = CatalogUiHelper.CreatePanel(_mainWindow.ParentWindowContainer, 0, 0, -300, -550, Color.clear, Color.clear);
			_mainWindow.DebugPanel = mainPanel;

			var backPanel = CatalogUiHelper.CreatePanel(mainPanel, 1000, 300, 0, 0, new Color(0.2f, 0.2f, 0.2f), Color.clear);

			var closeButton = _catalogUi.CreateButton(mainPanel, "X", 40, 40, 0, 0, new Color(0.0f, 0.2f, 0.2f, 0.95f), new Color(0.0f, 0.2f, 0.2f, 1f), new Color(0.7f, 0.2f, 0.2f, 1));
			var clearButton = _catalogUi.CreateButton(mainPanel, "Clear", 100, 40, 40, 0, new Color(0.0f, 0.2f, 0.2f, 0.95f), new Color(0.0f, 0.2f, 0.2f, 1f), new Color(0.7f, 0.7f, 0.7f, 1));

			// You can later assign actions to these buttons, by assigning an action to the ".button.onClick.AddListener(...)" event
			var dynamicButton1 = _catalogUi.CreateButton(mainPanel, "", 40, 40, 150, 0, new Color(0.7f, 0.5f, 0.5f, 0.95f), new Color(1.0f, 0.5f, 0.5f, 1f), new Color(0.7f, 0.7f, 0.7f, 1));
			var dynamicButton2 = _catalogUi.CreateButton(mainPanel, "", 40, 40, 200, 0, new Color(0.5f, 0.7f, 0.5f, 0.95f), new Color(0.5f, 1.0f, 0.5f, 1f), new Color(0.7f, 0.7f, 0.7f, 1));
			var dynamicButton3 = _catalogUi.CreateButton(mainPanel, "", 40, 40, 250, 0, new Color(0.5f, 0.5f, 0.7f, 0.95f), new Color(0.5f, 0.5f, 1.0f, 1f), new Color(0.7f, 0.7f, 0.7f, 1));
			var dynamicButton4 = _catalogUi.CreateButton(mainPanel, "", 40, 40, 300, 0, new Color(0.7f, 0.5f, 0.7f, 0.95f), new Color(1.0f, 0.5f, 1.0f, 1f), new Color(0.7f, 0.7f, 0.7f, 1));

			// ########### TEMP DYNAMIC ACTIONS #############
			// ########### TEMP DYNAMIC ACTIONS #############
			dynamicButton1.button.onClick.AddListener(() =>
			{
				//...put your custom actions here...
				try
				{
					SetCatalogPositionToCatalogEntry(2);
					//var selectedController = SuperController.singleton.GetSelectedController();
					//selectedController.transform.localRotation = selectedController.transform.localRotation * new Quaternion(0f, 1f, 0, 1f);
					//selectedController.transform.localRotation = Quaternion.Lerp(selectedController.transform.localRotation, new Quaternion(0f, 1f, 0, 1f), 0.1f);
					//selectedController.transform.RotateAround(selectedController.transform.position, new Vector3(1, 0, 0), 10);
				}
				catch (Exception e) { SuperController.LogError(e.ToString()); }
			});
			dynamicButton2.button.onClick.AddListener(() =>
			{
				//...put your custom actions here...
				var selectedController = SuperController.singleton.GetSelectedController();
				selectedController.transform.RotateAround(selectedController.transform.position, new Vector3(0, 1, 0), 10);
			});
			dynamicButton3.button.onClick.AddListener(() =>
			{
				//...put your custom actions here...
				var selectedController = SuperController.singleton.GetSelectedController();
				selectedController.transform.RotateAround(selectedController.transform.position, new Vector3(0, 1, 0), -10);
			});
			dynamicButton4.button.onClick.AddListener(() =>
			{
				//...put your custom actions here...
			});
			// ##############################################
			// ##############################################


			closeButton.button.onClick.AddListener(() => { ToggleDebugPanel(); });
			clearButton.button.onClick.AddListener(() => { _mainWindow.TextDebugPanelText.UItext.text = ""; });

			_mainWindow.TextDebugPanelText = _catalogUi.CreateTextField(mainPanel, "", 980, 240, 10, 50, new Color(0.3f, 0.3f, 0.3f), Color.white);
			_mainWindow.TextDebugPanelText.UItext.fontSize = 15;
			_mainWindow.TextDebugPanelText.UItext.alignment = TextAnchor.UpperLeft;
			_mainWindow.DebugPanel.transform.localScale = Vector3.zero;
			AddDragging(_mainWindow.DebugPanel, _mainWindow.DebugPanel);
		}

		private IEnumerator SetCatalogPositionToCatalogEntry(CatalogEntry entry)
		{
			yield return new WaitForEndOfFrame();
			var catalogEntryIndex = _catalog.Entries.IndexOf(entry);
			SetCatalogPositionToCatalogEntry(catalogEntryIndex);
			SelectCatalogEntry(entry);
		}

		private void SetCatalogPositionToCatalogEntry(int catalogEntryIndex)
		{
			var scrollPosition = GetTotalFrameSize() * catalogEntryIndex * -1;
			SetCatalogPositionToCatalogEntry(scrollPosition);
		}

		private void SetCatalogPositionToCatalogEntry(float scrollPosition)
		{
			_mainWindow.CatalogColumnContainers[0].transform.localPosition = new Vector3(scrollPosition, _mainWindow.CatalogColumnContainers[0].transform.localPosition.y, _mainWindow.CatalogColumnContainers[0].transform.localPosition.z);
			SetRowStateBasedOnScrollPosition(scrollPosition);
		}

		private float GetCatalogCurrentScrollPosition()
		{
			return -_mainWindow.CatalogColumnContainers[0].transform.localPosition.x;
		}

		public void DebugLog(string text, bool clean = false)
		{
			if (!_debugMode) return;
			if (clean) _mainWindow.TextDebugPanelText.UItext.text = "";
			if (text != null) _mainWindow.TextDebugPanelText.UItext.text += text + "\n";
		}

		public void ShowDebugPanel(string text = null, bool cleanFirst = true)
		{
			_mainWindow.DebugPanel.transform.localScale = Vector3.one;
			if (text != null) _mainWindow.TextDebugPanelText.UItext.text = text;
		}

		public void ToggleDebugPanel(string text = null)
		{
			if (_mainWindow.DebugPanel.transform.localScale == Vector3.one)
			{
				_mainWindow.DebugPanel.transform.localScale = Vector3.zero;
				return;
			}
			if (text != null) _mainWindow.TextDebugPanelText.UItext.text = text;
			_mainWindow.DebugPanel.transform.localScale = Vector3.one;
		}

		public void HideDebugPanel()
		{
			_mainWindow.DebugPanel.transform.localScale = Vector3.zero;
		}

		private void CreateDynamicButton_LeftSideLabel()
		{

			var totalSize = GetTotalFrameSize() + 70;
			_mainWindow.ButtonNameLabel = _catalogUi.CreateButton(_mainWindow.ParentWindowContainer, "", totalSize, 40, 0, -_mainWindow.WindowHeight, new Color(0.0f, 0.2f, 0.2f, 0.95f), new Color(0.0f, 0.2f, 0.2f, 1f), new Color(0.7f, 0.7f, 0.7f, 1));
			_mainWindow.ButtonNameLabel.buttonText.fontSize = 20;
			//_mainWindow._catalogNameLabel.buttonText.horizontalOverflow = HorizontalWrapMode.Overflow;

			_mainWindow.ButtonNameLabel.button.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
			_mainWindow.ButtonNameLabel.button.transform.localPosition = new Vector3(-40f, (totalSize / 2) - 42, 0f);
			_mainWindow.ButtonNameLabel.button.onClick.AddListener(() => ToggleMinimize());
			SetTooltipForDynamicButton(_mainWindow.ButtonNameLabel, () => (_minimizeUi.val ? "Maximize" : "Minimize") + "/Move");
			Action<DragHelper> afterDrag = (helper) =>
			{
				try
				{
					//positionTracker.XMultiplier = -1000f;
					//positionTracker.YMultiplier = 1000f;
					_mainWindowPositionX.val = helper.CurrentPosition.x;// - 1000; //helper.CurrentPosition.x * helper.XMultiplier;
					_mainWindowPositionY.val = helper.CurrentPosition.y; //helper.CurrentPosition.y * helper.YMultiplier;
				}
				catch (Exception e)
				{
					SuperController.LogError(e.ToString());
				}
			};
			AddDragging(_mainWindow.ButtonNameLabel.gameObject, _mainWindow.ParentWindowContainer, null, afterDrag);
		}

		private void CreateDynamicPanel_MainWindow(DynamicMainWindow mainWindow)
		{

			mainWindow.PanelBackground = CatalogUiHelper.CreatePanel(mainWindow.SubWindow, _mainWindow.WindowWidth, _mainWindow.WindowHeight, -10, -10, new Color(0.1f, 0.1f, 0.1f, 0.99f), Color.clear);
			mainWindow.MiniPanelBackground = CatalogUiHelper.CreatePanel(mainWindow.SubWindow, 190, _mainWindow.ControlPanelMinimizedHeight, -10, -10, new Color(0.1f, 0.1f, 0.1f, 0.99f), Color.clear);
			mainWindow.MiniPanelBackground.transform.localScale = Vector3.zero;

			Action<DragHelper> afterDrag = (helper) =>
			{
				try
				{
					_mainWindowPositionX.val = helper.CurrentPosition.x;
					_mainWindowPositionY.val = helper.CurrentPosition.y;
				}
				catch (Exception e)
				{
					SuperController.LogError(e.ToString());
				}
			};

			AddDragging(mainWindow.PanelBackground, mainWindow.ParentWindowContainer, null, afterDrag);

			mainWindow.SubPanelMode = CatalogUiHelper.CreatePanel(mainWindow.SubWindow, 55, _mainWindow.WindowHeight, _mainWindow.WindowWidth - 10, -10, new Color(0.02f, 0.02f, 0.02f, 0.99f), Color.clear);


			mainWindow.SubPanelVerticalStack = CreateButtonColumnTopAnchored(mainWindow.SubWindow, 0, 0, 0); // _catalogUi.CreateVerticalLayout(mainWindow.BackPanel, 0);

			var shortcutSubPanel = CatalogUiHelper.CreatePanel(mainWindow.SubWindow, 0, 30, 0, 0, Color.clear, Color.clear);
			AddDragging(shortcutSubPanel, mainWindow.ParentWindowContainer, null, afterDrag);
			mainWindow.MiniSubPanelShortcut = CreateButtonRowLeftAnchored(shortcutSubPanel, 0);
			mainWindow.MiniSubPanelShortcut.transform.localScale = Vector3.zero;

			var fileMenuSubPanel = CatalogUiHelper.CreatePanel(mainWindow.SubPanelVerticalStack.transform.gameObject, 0, 30, 0, 0, Color.clear, Color.clear);
			fileMenuSubPanel.transform.SetParent(mainWindow.SubPanelVerticalStack.transform, false);
			AddDragging(fileMenuSubPanel, mainWindow.ParentWindowContainer, null, afterDrag);
			mainWindow.SubPanelFileMenu = CreateButtonRowLeftAnchored(fileMenuSubPanel, 0);

			var captureSubPanel = CatalogUiHelper.CreatePanel(mainWindow.SubPanelVerticalStack.transform.gameObject, 0, 60, 0, 0, Color.clear, Color.clear);
			captureSubPanel.transform.SetParent(mainWindow.SubPanelVerticalStack.transform, false);
			AddDragging(captureSubPanel, mainWindow.ParentWindowContainer, null, afterDrag);
			mainWindow.SubPanelCapture = CreateButtonRowLeftAnchored(captureSubPanel, 0);

			var sceneToolsSubPanel = CatalogUiHelper.CreatePanel(mainWindow.SubPanelVerticalStack.transform.gameObject, 0, 40, 0, 0, Color.clear, Color.clear);
			sceneToolsSubPanel.transform.SetParent(mainWindow.SubPanelVerticalStack.transform, false);
			AddDragging(sceneToolsSubPanel, mainWindow.ParentWindowContainer, null, afterDrag);
			mainWindow.SubPanelSceneTools = CreateButtonRowLeftAnchored(sceneToolsSubPanel, 0);

			var manageSubPanel = CatalogUiHelper.CreatePanel(mainWindow.SubPanelVerticalStack.transform.gameObject, 0, 40, 0, 0, Color.clear, Color.clear);
			manageSubPanel.transform.SetParent(mainWindow.SubPanelVerticalStack.transform, false);
			AddDragging(manageSubPanel, mainWindow.ParentWindowContainer, null, afterDrag);
			mainWindow.SubPanelManageCatalog = CreateButtonRowLeftAnchored(manageSubPanel, 0);

		}

		private void SaveCatalogToFile(string filePath)
		{
			try
			{
				var newCatalog = new Catalog
				{
					CaptureClothes = _catalogCaptureClothes.val,
					CaptureHair = _catalogCaptureHair.val,
					CaptureMorphs = _catalogCaptureMorphs.val,
					//DeapVersion = FileLoadManagement.CATALOG_DEAP_VERSION,
					Entries = _catalog.Entries,
				};

				// Json Serialize data...
				CatalogFileManager.SaveCatalog(newCatalog, filePath);
			}
			catch (Exception e)
			{
				SuperController.LogError(e.ToString());
			}
		}

		private Catalog LoadCatalogFromFile(string filePath, bool _autoUpdateName = true)
		{
			SuperController.LogMessage("INFO: Loading catalog from file...");
			try
			{
				if (string.IsNullOrEmpty(filePath)) return null;
				// Deserialize the file data...
				Catalog catalog = CatalogFileManager.LoadCatalog(filePath);

				var pathElements = filePath.Split('/');
				var catalogFileName = pathElements.Last();
				if (_autoUpdateName)
				{
					var extension = "." + _fileExtension;
					var catalogName = catalogFileName.Substring(0, catalogFileName.Length - extension.Length);
					_catalogName.SetVal(catalogName);
					if (string.IsNullOrEmpty(_catalogLocalFileName.val))
					{
						_catalogLocalFileName.val = GetNewLocalFileName(catalogName);
						SuperController.LogMessage("INFO: Created new file path: " + _catalogLocalFileName.val);
					}
				}
				_catalogRelativePath.SetVal(filePath);

				UpdateCaptureHairState(catalog.CaptureHair);
				UpdateCaptureClothesState(catalog.CaptureClothes);
				UpdateCaptureMorphsState(catalog.CaptureMorphs);

				if (CatalogHasMessage(catalog)) ShowPopupMessage(catalog.ActiveVersionMessage.ShortMessage, 500);
				ReinitializeCatalog(); //...Load catalog from file
				for (var i = 0; i < catalog.Entries.Count(); i++)
				{
					if (CannotLoad(catalog)) continue;
					var entry = catalog.Entries.ElementAt(i);
					MakeCatalogEntry(catalog.Entries.ElementAt(i)); //...Loading catalog from File
				}
				RefreshCatalogPosition(); // ...After loading catalog from file
				SuperController.LogMessage("INFO: Loaded catalog from " + filePath);
				return catalog;
			}
			catch (Exception e)
			{
				SuperController.LogError(e.ToString());
				//throw (e);
				return new Catalog();
			}
		}

		private bool CatalogHasMessage(Catalog catalog)
		{
			return catalog.ActiveVersionMessage != null && catalog.ActiveVersionMessage.ShortMessage != null;
		}

		private bool CannotLoad(Catalog catalog)
		{
			return false;
			//return catalog.ActiveVersionMessage != null && catalog.ActiveVersionMessage.Then == SerializerService_3_0_1.DO_NOT_LOAD;
		}

		public string GetSceneDirectoryPath()
		{
			try
			{
				return SuperController.singleton.currentLoadDir;
			}
			catch (Exception e)
			{
				SuperController.LogError(e.ToString());
				throw e;
			}
		}

		private void ZoomOutCatalog()
		{
			CatalogEntryFrameSize.SetVal(CatalogEntryFrameSize.val - 50);
			ResizeCatalog((int)CatalogEntryFrameSize.val);
		}

		private void ZoomInCatalog()
		{
			CatalogEntryFrameSize.SetVal(CatalogEntryFrameSize.val + 50);
			ResizeCatalog((int)CatalogEntryFrameSize.val);
		}

		private void ResizeCatalog(int newSize)
		{
			_catalog.Entries.ForEach(e => ResizeCatalogEntry(e, newSize));
			RefreshCatalogPosition();  // ...After resizing catalog
		}

		private void RefreshCatalogPosition()
		{
			ResetScrollPosition();
			SetRowStateBasedOnScrollPosition(0);
		}

		private void ResizeCatalogEntry(CatalogEntry catalogEntry, int newSize)
		{
			try
			{
				var catalogEntryPanel = catalogEntry.UiCatalogEntryPanel.GetComponents<RectTransform>().First();
				catalogEntryPanel.sizeDelta = new Vector2(newSize, newSize);

				var selectEntryButtonRect = catalogEntry.UiSelectButton.GetComponents<RectTransform>().First();
				selectEntryButtonRect.sizeDelta = new Vector2(10, newSize);
				CatalogUiHelper.SetAnchors(catalogEntry.UiCatalogEntryPanel, catalogEntry.UiSelectButton.gameObject, "bottom");

				var btnHeight = CatalogEntryFrameSize.val / 5;
				var btnWidth = (CatalogEntryFrameSize.val / 5);
				var smallerSize = (int)(btnWidth / 1.5);

				var applyButtonRect = catalogEntry.UiApplyButton.GetComponents<RectTransform>().First();
				applyButtonRect.sizeDelta = new Vector2(btnWidth, btnHeight);

				var discardButtonRect = catalogEntry.UiDiscardButton.GetComponents<RectTransform>().First();
				discardButtonRect.sizeDelta = new Vector2(smallerSize, smallerSize);

				var keepButtonRect = catalogEntry.UiKeepButton.GetComponents<RectTransform>().First();
				keepButtonRect.sizeDelta = new Vector2(smallerSize, smallerSize);

				var leftButtonRect = catalogEntry.UiShiftLeftButton.GetComponents<RectTransform>().First();
				leftButtonRect.sizeDelta = new Vector2(smallerSize, smallerSize);

				var rightButtonRect = catalogEntry.UiShiftRightButton.GetComponents<RectTransform>().First();
				rightButtonRect.sizeDelta = new Vector2(smallerSize, smallerSize);

				CatalogUiHelper.SetAnchors(catalogEntry.UiCatalogEntryPanel, catalogEntry.UiBottomButtonGroup, "bottom");

				_mainWindow.CatalogRowsVLayout.spacing = _relativeBorderWidth;
				_mainWindow.CatalogColumnsHLayout.spacing = _relativeBorderWidth;

				ResizeBorders(catalogEntry, newSize);
			}
			catch (Exception e)
			{
				SuperController.LogError(e.ToString());
			}
		}

		private void ResizeBorders(CatalogEntry catalogEntry, float newSize)
		{
			try
			{
				float originRatio = newSize / _defaultFrameSize;
				_relativeBorderWidth = (int)(originRatio * _defaultBorderWidth);
				float height = newSize;
				float width = newSize;
				float offsetX = 0;
				float offsetY = 0;
				ResizeBorder(catalogEntry.UiCatalogBorder.LeftBorder, _relativeBorderWidth, height + _relativeBorderWidth + offsetY, width / -2 - offsetX, 0);
				ResizeBorder(catalogEntry.UiCatalogBorder.RightBorder, _relativeBorderWidth, height + _relativeBorderWidth + offsetY, width / 2 + offsetX, 0);
				ResizeBorder(catalogEntry.UiCatalogBorder.TopBorder, width + _relativeBorderWidth + offsetX, _relativeBorderWidth, 0, height / -2 - offsetY);
				ResizeBorder(catalogEntry.UiCatalogBorder.BottomBorder, width + _relativeBorderWidth + offsetX, _relativeBorderWidth, 0, height / 2 + offsetY);
			}
			catch (Exception e)
			{
				SuperController.LogError(e.ToString());
			}
		}

		private static void ResizeBorder(GameObject border, float width, float height, float offsetX = 0, float offsetY = 0)
		{
			border.transform.localPosition = new Vector3(offsetX, offsetY, 0.0f);
			var image = border.GetComponent<Image>();
			RectTransform rect = image.rectTransform;
			rect.sizeDelta = new Vector2(width, height);
		}

		private void CreateDynamicButton_Trigger()
		{
			_floatingTriggerButton = _floatingControlsUi.CreateButton(_floatingControlsUi.canvas.gameObject, "Capture Scene", 300, 100, 0, 150, new Color(1, 0.25f, 0.25f), new Color(1, 0.5f, 0.5f), new Color(1, 1, 1));
			_floatingTriggerButton.transform.localScale = Vector3.zero;
			var positionTracker = new DragHelper();
			_positionTrackers.Add("FloatingTrigger", positionTracker);
			Action<DragHelper> onStartDraggingEvent = (helper) =>
			{
				positionTracker.IsIn3DSpace = !IsAnchoredOnHUD();
				positionTracker.XMultiplier = -1000f;
				positionTracker.YMultiplier = 1000f;
			};
			positionTracker.AddMouseDraggingToObject(_floatingTriggerButton.button.gameObject, _floatingTriggerButton.button.gameObject, true, true, onStartDraggingEvent);
		}

		void Update()
		{
			if (!_updateLoopEnabled) return;

			//####### TEMP #################################################################
			if (_debugMode == true)
			{
				var selectedController = SuperController.singleton.GetSelectedController();
				if (selectedController != null)
				{
					_mainWindow.TextDebugPanelText.UItext.text =
						$"{selectedController.name} " +
						$"\nrotation" +
						$"\n  x:{selectedController.transform.rotation.x}" +
						$"\n  y:{selectedController.transform.rotation.y}" +
						$"\n  z:{selectedController.transform.rotation.z}" +
						$"\n  w:{selectedController.transform.rotation.w}" +
						$"\nrotation.eulerAngles" +
						$"\n  x:{selectedController.transform.rotation.eulerAngles.x}" +
						$"\n  y:{selectedController.transform.rotation.eulerAngles.y}" +
						$"\n  z:{selectedController.transform.rotation.eulerAngles.z}" +
						$"\nlocalRotation" +
						$"\n  x:{selectedController.transform.localRotation.x}" +
						$"\n  y:{selectedController.transform.localRotation.y}" +
						$"\n  z:{selectedController.transform.localRotation.z}" +
						$"\nlocalRotation.eulerAngles" +
						$"\n  x:{selectedController.transform.localRotation.eulerAngles.x}" +
						$"\n  y:{selectedController.transform.localRotation.eulerAngles.y}" +
						$"\n  z:{selectedController.transform.localRotation.eulerAngles.z}";
				}
			}
			//##############################################################################

			try
			{

				if (_atomType == ATOM_TYPE_PERSON) _mutationsService.Update();

				ManageScreenshotCaptureSequence();

				if (_mainWindow.ButtonNameLabel.buttonText.text != _catalogName.val)
				{
					_mainWindow.ButtonNameLabel.buttonText.text = _catalogName.val;
				}

				foreach (var positionTracker in _positionTrackers)
				{
					positionTracker.Value.Update();
				}
				foreach (var catalogEntry in _catalog.Entries)
				{
					catalogEntry.XPositionTracker.Update();
					catalogEntry.YPositionTracker.Update();
				}
				if (_catalogUi != null) _catalogUi.Update();
				if (_floatingControlsUi != null) _floatingControlsUi.Update();
			}
			catch (Exception e)
			{
				SuperController.LogError(e.ToString());
				SuperController.LogError("Catalog Update-loop disabled to prevent continuous stream of errors. Can be re-enabled in settings.");
				_updateLoopEnabled = false;
			}
		}

		public void RequestNextCaptureSet(int itemCount, int mutationAquisitionMode, Action beforeMutationAction = null, Action afterMutationAction = null, Func<List<Vector3>> vertexFetcher = null)
		{
			var currentCaptureRequest = new CaptureRequest
			{
				RequestModeEnum = mutationAquisitionMode,
				LastRequestedCount = itemCount,
				CatalogEntriesStillLeftToCreate = itemCount,
				BeforeMutationAction = beforeMutationAction,
				AfterMutationAction = afterMutationAction,
				VertexFetcher = vertexFetcher
			};
			_currentCaptureRequestList.Add(currentCaptureRequest);
		}

		private void BeforeNextMutationCallback()
		{
			if (_currentCaptureRequest.BeforeMutationAction != null) _currentCaptureRequest.BeforeMutationAction.Invoke();
		}

		private void AfterNextMutationCallback()
		{
			if (_currentCaptureRequest.AfterMutationAction != null) _currentCaptureRequest.AfterMutationAction.Invoke();
		}

		private void CaptureSequenceIsCompleteCallback()
		{
			if (_catalogMode.val == CatalogModeEnum.CATALOG_MODE_OBJECT
				|| _catalogMode.val == CatalogModeEnum.CATALOG_MODE_SESSION
				|| _catalogMode.val == CatalogModeEnum.CATALOG_MODE_CAPTURE)
			{
				var lastEntry = _catalog.Entries.LastOrDefault();
				//if (lastEntry != null) SelectCatalogEntry(_catalog.Entries.Last());
			}
			//RefreshCatalogPosition(); // ...After capture sequence
		}

		private void ManageScreenshotCaptureSequence()
		{
			try
			{
				if (_currentCaptureRequest.CatalogEntriesStillLeftToCreate > 0)
				{
					_floatingTriggerButton.button.enabled = false;
					_floatingTriggerButton.buttonText.text = "Please wait. " + (_currentCaptureRequest.CatalogEntriesStillLeftToCreate - 1) + " remaining...";
					_floatingTriggerButton.textColor = new Color(1, 0, 0);

					if (_skipFrames > 0)
					{
						_skipFrames = _skipFrames - 1;
						return;
					}

					_createCatalogEntry_Step = _createCatalogEntry_Step + 1;

					// Setup Scene...
					if (_createCatalogEntry_Step == 1)
					{
						//ReinitializeCatalog();
						BeforeNextMutationCallback();
						var selectedCamera = Camera.allCameras.FirstOrDefault(c => c.name == _activeCameraName.val);
						if (selectedCamera == null) throw new Exception("Unable to acquire camera");

						_captureCamera = selectedCamera;
						_originalCameraTexture = _captureCamera.targetTexture;
						_captureCamera.targetTexture = RenderTexture.GetTemporary(_captureWidth, _captureHeight);

						HideUi();

						// Apply the next mutation...
						PerformNextSceneTransform();

						// Skip some frames to allow the transformations to settle...
						_skipFrames = (int)_waitFramesBetweenCaptureJSON.val;
					}

					// Take screenshot and create clickable catalog entry...
					if (_createCatalogEntry_Step == 2)
					{
						Mutation mutation = _nextMutation;
						if (mutation != null) {
							var newCatalogEntry = TakeScreenshotAndCreateClickableCatalogEntry(mutation);
							var selectedEntry = GetSelectedCatalogEntryOrDefault();
							if (selectedEntry == null)
							{
								_catalog.Entries.Insert(0, newCatalogEntry);
								//_catalog.Entries.Add(newCatalogEntry);
							}
							else
							{
								var insertionIndex = _catalog.Entries.IndexOf(selectedEntry);
								var scrollPositionIfOnSecondLastEntry = (_catalog.Entries.Count - 2) * GetTotalFrameSize();
								SuperController.LogMessage("GetCatalogCurrentScrollPosition(): " + GetCatalogCurrentScrollPosition() + ", scrollPositionIfOnSecondLastEntry: " + (scrollPositionIfOnSecondLastEntry));
								var appendToEnd = GetCatalogCurrentScrollPosition() > scrollPositionIfOnSecondLastEntry;
								if (appendToEnd) _catalog.Entries.Add(newCatalogEntry);
								if (!appendToEnd) _catalog.Entries.Insert(insertionIndex, newCatalogEntry);
							}
							RebuildCatalogFromEntriesCollection();
							RefreshCatalogPosition();
							StartCoroutine(SetCatalogPositionToCatalogEntry(newCatalogEntry));
							//SetCatalogPositionToCatalogEntry(newCatalogEntry);
						}
					}

					// Tear down scene...
					if (_createCatalogEntry_Step == 3)
					{
						--_currentCaptureRequest.CatalogEntriesStillLeftToCreate;
						_createCatalogEntry_Step = 0;

						ShowUi();

						AfterNextMutationCallback();
					}

					// Finished creating catalog...
					if (_currentCaptureRequest.CatalogEntriesStillLeftToCreate == 0)
					{
						_floatingTriggerButton.button.enabled = true;
						_floatingTriggerButton.buttonText.text = _generateButtonInitText;
						_floatingTriggerButton.textColor = new Color(1, 1, 1);
						CaptureSequenceIsCompleteCallback();
					}
				}
				else // current request is complete, dequeue the next request if any...
				{
					if (_currentCaptureRequestList.Count > 0)
					{
						_currentCaptureRequest = _currentCaptureRequestList[0];
						_currentCaptureRequestList.RemoveAt(0);
					}
				}
			}
			catch (Exception e)
			{
				--_currentCaptureRequest.CatalogEntriesStillLeftToCreate;
				SuperController.LogError(e.ToString());
			}
		}

		private void PerformNextSceneTransform()
		{
			Mutation mutation = null;
			if (_currentCaptureRequest.RequestModeEnum == CaptureRequest.MUTATION_AQUISITION_MODE_SEQUENCE)
			{
				_mutationsService.UndoPreviousMutation();
				mutation = IsCatalogFirstEntry() & _firstEntryIsCurrentLook.val ? _mutationsService.CreateBufferMutation() : _mutationsService.CreateMorphMutation();
				_mutationsService.ApplyMutation(ref mutation);
			}
			else if (_currentCaptureRequest.RequestModeEnum == CaptureRequest.MUTATION_AQUISITION_MODE_CAPTURE)
			{
				mutation = _mutationsService.CaptureCurrentMutation();
				_mutationsService.UndoPreviousMutation();
				_mutationsService.ApplyMutation(ref mutation);
			}
			else if (_currentCaptureRequest.RequestModeEnum == CaptureRequest.MUTATION_AQUISITION_MODE_CAPTURE_OBJECT)
			{
				mutation = _mutationsService.CaptureAtomVerbose(containingAtom);
			}
			else if (_currentCaptureRequest.RequestModeEnum == CaptureRequest.MUTATION_AQUISITION_MODE_CAPTURE_SESSION_OBJECT)
			{
				var selectedAtom = SuperController.singleton.GetSelectedAtom();
				if (selectedAtom == null)
				{
					ShowPopupMessage("Please select an atom from the scene", 2);
					SuperController.LogError("Please select an atom from the scene");
					_nextMutation = null;
					return;
				}
				mutation = _mutationsService.CaptureAtomVerbose(selectedAtom);
			}
			_nextMutation = mutation;
		}

		void ApplyMasterCatalogEntry(CatalogEntry catalogEntry)
		{
			SelectCatalogEntry(catalogEntry);
			ApplyCatalogEntryItem(catalogEntry);
		}

		private void ApplyCatalogEntryItem(CatalogEntry catalogEntry, float startDelay = 0, bool isChildEntry = false)
		{

			// Apply appropriate catalog entry action instead...
			if (catalogEntry.CatalogEntryMode == CatalogModeEnum.CATALOG_MODE_OBJECT)
			{
				CreateAtomsForCatalogEntry(catalogEntry);
			}
			else if (catalogEntry.CatalogEntryMode == CatalogModeEnum.CATALOG_MODE_SESSION)
			{
				CreateAtomsForCatalogEntry(catalogEntry);
			}
			else if (catalogEntry.Mutation?.ScenePathToOpen != null)
			{
				SuperController.singleton.Load(catalogEntry.Mutation.ScenePathToOpen);
			}
			else
			{
				ApplyCatalogEntryMutation(catalogEntry, startDelay, isChildEntry);
			}

			ApplyCatalogEntryChildEntries(catalogEntry, startDelay);
		}

		private void ApplyCatalogEntryChildEntries(CatalogEntry catalogEntry, float startDelay = 0)
		{
			float delayStartTime = startDelay;
			foreach (var childEntry in catalogEntry.ChildEntries)
			{
				ApplyCatalogEntryItem(childEntry, delayStartTime, true);
				delayStartTime += GetCompositeDuration(childEntry);
			}
		}

		private float GetCompositeDuration(CatalogEntry entry)
		{
			if (entry.ChildEntries.Count == 0) return entry.TransitionTimeInSeconds;
			return entry.ChildEntries.Select(e => e.TransitionTimeInSeconds).Aggregate((a,b) => a + b);
		}

		private Mutation ApplyCatalogEntryMutation(CatalogEntry catalogEntry, float startDelay = 0, bool isChildEntry = false)
		{
			float durationInSeconds = catalogEntry.TransitionTimeInSeconds;
			var mutation = catalogEntry.Mutation;

			_mutationsService.UndoPreviousMutation(durationInSeconds);

			if (catalogEntry.Mutation != null)
			{
				var excludeUi = false;
				if (isChildEntry == true) excludeUi = true;
				_mutationsService.ApplyMutation(ref mutation, startDelay, durationInSeconds, excludeUi);
			}

			return mutation;
		}

		private IEnumerator ApplyChildCatalogEntry(CatalogEntry catalogEntry, float startDelay = 0, bool isChildEntry = false)
		{
			if (startDelay != 0) yield return new WaitForSeconds(startDelay);
			ApplyCatalogEntryItem(catalogEntry, startDelay, isChildEntry);
		}

		void SelectCatalogEntry(CatalogEntry catalogEntry)
		{
			// Remove pink border for previously selected entries...
			var selectedEntries = _catalog.Entries
				.Where(e => e.Selected)
				.ToList();
			selectedEntries.ForEach(DeselectCatalogEntry);
			selectedEntries.ForEach(UpdateCatalogEntryBorderColorBasedOnState);
			selectedEntries.ForEach(HideAnimationUi);

			catalogEntry.Selected = true;
			UpdateCatalogEntryBorderColorBasedOnState(catalogEntry);

			_mainWindow.CurrentCatalogEntry = catalogEntry;
			RemoveItemToggles();
			if (!_mainWindow.SubItemPanelMinimized) AddItemToggles(catalogEntry);
			if (_animationFeaturesVisible) AddOrShowAnimationUi(catalogEntry);
		}

		private void HideAnimationUi(CatalogEntry entry)
		{
			if (entry.UiAnimationPanel != null) { 
				entry.UiAnimationPanel.transform.localScale = Vector3.zero;
			}
		}

		private void RemoveItemToggles()
		{
			foreach (var entry in _catalog.Entries)
			{
				foreach (var infoToggle in entry.EntrySubItemToggles)
				{
					RemoveUiCatalogSubItem(infoToggle);
				}
				entry.EntrySubItemToggles = new List<EntrySubItem>();
			}
		}

		private void AddItemToggles(CatalogEntry catalogEntry)
		{
			if (catalogEntry.CatalogEntryMode == CatalogModeEnum.CATALOG_MODE_SESSION || catalogEntry.CatalogEntryMode == CatalogModeEnum.CATALOG_MODE_OBJECT)
			{
				AddEntrySubItemTogglesForStoredAtoms(catalogEntry);
			}

			if (catalogEntry.CatalogEntryMode == CatalogModeEnum.CATALOG_MODE_MUTATIONS || catalogEntry.CatalogEntryMode == CatalogModeEnum.CATALOG_MODE_CAPTURE)
			{
				AddEntrySubItemTogglesForMutations(catalogEntry);
			}

			if (catalogEntry.CatalogEntryMode == CatalogModeEnum.CATALOG_MODE_SCENE)
			{
				AddEntrySubItemTogglesForScene(catalogEntry);
			}

		}

		private void AddEntrySubItemTogglesForMutations(CatalogEntry catalogEntry)
		{
			var parentMutation = catalogEntry.Mutation;
			catalogEntry.EntrySubItemToggles = new List<EntrySubItem>();

			// Add Child-Entry toggles...
			for (var i = 0; i < catalogEntry.ChildEntries.Count; i++)
			{
				var childEntry = catalogEntry.ChildEntries[i];
				UnityAction<bool> toggleAction = (isChecked) =>
				{
					childEntry.Active = isChecked;
				};
				UnityAction<string> stopTracking = (name) =>
				{
					catalogEntry.ChildEntries.Remove(catalogEntry.ChildEntries.FirstOrDefault(e => e.UniqueName == name));
				};
				EntrySubItem entrySubItem = new EntrySubItem();
				entrySubItem.ItemName = childEntry.UniqueName;
				entrySubItem.CheckState = childEntry.Active;
				AddEntrySubItemToggle(entrySubItem, toggleAction, stopTracking);
				catalogEntry.EntrySubItemToggles.Add(entrySubItem);
			}

			// Add Hair toggles...
			for (var i = 0; i < parentMutation.HairItems.Count; i++)
			{
				var hairItem = parentMutation.HairItems[i];
				UnityAction<bool> toggleAction = (isChecked) =>
				{
					hairItem.Active = isChecked;
					if (isChecked) _mutationsService.ApplyHairItem(hairItem);
					else _mutationsService.RemoveHairItem(hairItem);
				};
				UnityAction<string> stopTracking = (name) =>
				{
					parentMutation.HairItems = parentMutation.HairItems.Where(m => m.Id != name).ToList();
				};

				EntrySubItem entrySubItem = new EntrySubItem();
				entrySubItem.ItemName = hairItem.Id;
				entrySubItem.CheckState = hairItem.Active;
				AddEntrySubItemToggle(entrySubItem, toggleAction, stopTracking);

				//hairItem.DynamicCheckbox = entrySubItem;
				catalogEntry.EntrySubItemToggles.Add(entrySubItem);
			}

			// Add Clothing toggles...
			for (var i = 0; i < parentMutation.ClothingItems.Count; i++)
			{
				var clothingItem = parentMutation.ClothingItems[i];
				UnityAction<bool> toggleAction = (isChecked) =>
				{
					clothingItem.Active = isChecked;
					if (isChecked) _mutationsService.ApplyClothingItem(clothingItem);
					else _mutationsService.RemoveClothingItem(clothingItem);
				};
				UnityAction<string> stopTracking = (name) =>
				{
					parentMutation.ClothingItems = parentMutation.ClothingItems.Where(m => m.Id != name).ToList();
				};

				EntrySubItem entrySubItem = new EntrySubItem();
				entrySubItem.ItemName = clothingItem.Id;
				entrySubItem.CheckState = clothingItem.Active;
				AddEntrySubItemToggle(entrySubItem, toggleAction, stopTracking);

				//clothingItem.DynamicCheckbox = entrySubItem;
				catalogEntry.EntrySubItemToggles.Add(entrySubItem);
			}

			// Add Morph toggles...
			for (var i = 0; i < parentMutation.ActiveMorphs.Count; i++)
			{
				var activeMorphItem = parentMutation.ActiveMorphs[i];
				UnityAction<bool> toggleAction = (isChecked) =>
				{
					activeMorphItem.Active = isChecked;
					if (isChecked) _mutationsService.ApplyActiveMorphItem(activeMorphItem);
					else _mutationsService.RemoveActiveMorphItem(activeMorphItem);
				};
				UnityAction<string> stopTracking = (name) =>
				{
					parentMutation.ActiveMorphs = parentMutation.ActiveMorphs.Where(m => m.Id != name).ToList();
				};

				EntrySubItem entrySubItem = new EntrySubItem();
				entrySubItem.ItemName = activeMorphItem.Id;
				entrySubItem.Label = activeMorphItem.Id.Split('/').Last().Split('.').First();
				entrySubItem.CheckState = activeMorphItem.Active;
				AddEntrySubItemToggle(entrySubItem, toggleAction, stopTracking);

				//activeMorphItem.DynamicCheckbox = entrySubItem;
				catalogEntry.EntrySubItemToggles.Add(entrySubItem);
			}

			// Add Pose toggles...
			for (var i = 0; i < parentMutation.PoseMorphs.Count; i++)
			{
				var activeMorphItem = parentMutation.PoseMorphs[i];
				UnityAction<bool> toggleAction = (isChecked) =>
				{
					activeMorphItem.Active = isChecked;
					if (isChecked) _mutationsService.ApplyActivePoseItem(activeMorphItem);
					else _mutationsService.RemoveActivePoseItem(activeMorphItem);
				};
				UnityAction<string> stopTracking = (name) =>
				{
					parentMutation.PoseMorphs = parentMutation.PoseMorphs.Where(m => m.Id != name).ToList();
				};

				EntrySubItem entrySubItem = new EntrySubItem();
				entrySubItem.ItemName = activeMorphItem.Id;
				entrySubItem.Label = activeMorphItem.Id.Split('/').Last().Split('.').First();
				entrySubItem.CheckState = activeMorphItem.Active;
				AddEntrySubItemToggle(entrySubItem, toggleAction, stopTracking);

				//activeMorphItem.DynamicCheckbox = entrySubItem;
				catalogEntry.EntrySubItemToggles.Add(entrySubItem);
			}

			// Add FaceGen morph toggles...
			for (var i = 0; i < parentMutation.FaceGenMorphSet.Count; i++)
			{
				var faceGenMorphItem = parentMutation.FaceGenMorphSet[i];
				UnityAction<bool> toggleAction = (isChecked) =>
				{
					faceGenMorphItem.Active = isChecked;
					if (isChecked) _mutationsService.ApplyMutationMorphItem(faceGenMorphItem);
					else _mutationsService.UndoMutationMorph(faceGenMorphItem);
				};
				UnityAction<string> stopTracking = (name) =>
				{
					parentMutation.FaceGenMorphSet = parentMutation.FaceGenMorphSet.Where(m => m.Id != name).ToList();
				};
				EntrySubItem entrySubItem = new EntrySubItem();
				entrySubItem.ItemName = faceGenMorphItem.Id;
				entrySubItem.Label = faceGenMorphItem.Id.Split('/').Last().Split('.').First();
				entrySubItem.CheckState = faceGenMorphItem.Active;
				AddEntrySubItemToggle(entrySubItem, toggleAction, stopTracking);

				//faceGenMorphItem.DynamicCheckbox = entrySubItem;
				catalogEntry.EntrySubItemToggles.Add(entrySubItem);
			}
		}

		private void AddEntrySubItemTogglesForStoredAtoms(CatalogEntry catalogEntry)
		{
			catalogEntry.EntrySubItemToggles = new List<EntrySubItem>();

			for (var i = 0; i < catalogEntry.Mutation.StoredAtoms.Count; i++)
			{
				StoredAtom storedAtom = catalogEntry.Mutation.StoredAtoms[i];
				UnityAction<bool> onToggleAction = (value) =>
				{
					var atom = SuperController.singleton.GetAtomByUid(storedAtom.AtomName);
					atom.SetOn(value);
					storedAtom.Active = value;
				};
				UnityAction<string> onStopTracking = (atomName) =>
				{
					var atomToRemove = catalogEntry.Mutation.StoredAtoms.FirstOrDefault(a => a.AtomName == atomName);
					catalogEntry.Mutation.StoredAtoms.Remove(atomToRemove);
					SelectCatalogEntry(catalogEntry);
				};
				var currentAtomState = SuperController.singleton.GetAtomByUid(storedAtom.AtomName);

				EntrySubItem entrySubItem = new EntrySubItem();
				entrySubItem.ItemName = storedAtom.AtomName;
				entrySubItem.CheckState = storedAtom.Active;

				// Select-Substitute-Atom item button...
				UnityAction<EntrySubItem> selectSubstituteAtom = (inputEntrySubItem) =>
				{
					UnityAction<string> withSelectedAtomCallback = (atomIdName) =>
					{
						var atom = SuperController.singleton.GetAtomByUid(atomIdName);
						if (atom == null) return;
						storedAtom.SubstituteWithSceneAtom = atomIdName;
						SelectCatalogEntry(catalogEntry);
					};
					ShowSelectAtomFromSceneList(withSelectedAtomCallback);
				};

				UnityAction<EntrySubItem> cancelSubsititute = (selectedAtomId) =>
				{
					var atom = SuperController.singleton.GetAtomByUid(selectedAtomId.ItemName);
					if (atom == null) return;
					storedAtom.SubstituteWithSceneAtom = null;
					SelectCatalogEntry(catalogEntry);
				};

				var extraActionButtons = new List<EntrySubItemAction>();
				//EntrySubItemAction selectSubstituteItemButton = AddSubstituteButton(storedAtom, selectSubstituteAtom, cancelSubsititute);
				//extraActionButtons.Add(selectSubstituteItemButton);
				AddEntrySubItemToggle(entrySubItem, onToggleAction, onStopTracking, extraActionButtons);

				//storedAtom.EntrySubItemToggle = entrySubItem;
				catalogEntry.EntrySubItemToggles.Add(entrySubItem);
			}
		}

		private void AddEntrySubItemTogglesForScene(CatalogEntry catalogEntry)
		{
			catalogEntry.EntrySubItemToggles = new List<EntrySubItem>();
			string scenePath = catalogEntry.Mutation.ScenePathToOpen;
			var sceneName = scenePath?.Replace("\\", "/").Split('/').Last();
			UnityAction<bool> onToggleAction = (value) => { };
			UnityAction<string> onStopTracking = (inputScenePath) => { };

			EntrySubItem entrySubItem = new EntrySubItem();
			entrySubItem.ItemName = sceneName;
			entrySubItem.CheckState = true;
			AddEntrySubItemToggle(entrySubItem, onToggleAction, onStopTracking);

			catalogEntry.EntrySubItemToggles.Add(entrySubItem);
		}

		private void DeselectCatalogEntry(CatalogEntry catalogEntry)
		{
			catalogEntry.Selected = false;
		}

		private bool IsCatalogFirstEntry()
		{
			return (_currentCaptureRequest.LastRequestedCount == _currentCaptureRequest.CatalogEntriesStillLeftToCreate);
		}

		private void HideUi()
		{
			try
			{
				SetCatalogVisibility(false);
				_hudWasVisible = SuperController.singleton.mainHUD.gameObject.activeSelf;
				if (_hudWasVisible) SuperController.singleton.HideMainHUD();

				_floatingControlUiPosition = _floatingControlsUi.canvas.transform.localPosition;
				_floatingControlsUi.canvas.transform.localPosition = new Vector3(10, 0, 0);
				_floatingControlsUi.Update();
				_catalogUi.Update();
			}
			catch (Exception e)
			{
				SuperController.LogError(e.ToString());
			}
		}

		private void ShowUi()
		{
			try
			{
				SetCatalogVisibility(true);
				if (_hudWasVisible) SuperController.singleton.ShowMainHUD();
			}
			catch (Exception e)
			{
				SuperController.LogError(e.ToString());
			}
		}

		private CatalogEntry TakeScreenshotAndCreateClickableCatalogEntry(Mutation mutation)
		{
			try
			{
				Texture2D texture = TakeScreenshot();

				var imageInfo = new ImageInfo();
				imageInfo.Texture = texture;
				imageInfo.Width = _captureWidth;
				imageInfo.Height = _captureHeight;
				imageInfo.Format = TextureFormat.DXT1;

				var newCatalogEntry = new CatalogEntry();
				newCatalogEntry.UniqueName = GetUniqueName();
				newCatalogEntry.CatalogEntryMode = _catalogMode.val;
				newCatalogEntry.ImageInfo = imageInfo;
				newCatalogEntry.Mutation = mutation;
				newCatalogEntry.TransitionTimeInSeconds = _defaultMorphTransitionTimeSeconds.val;
				
				//MakeCatalogEntry(newCatalogEntry); // ...Capturing and entry
				_captureCamera.targetTexture = _originalCameraTexture;

				return newCatalogEntry;
			}
			catch (Exception e)
			{
				SuperController.LogError(e.ToString());
				throw (e);
			}
		}

		private Texture2D TakeScreenshot()
		{
			RenderTexture renderTexture = _captureCamera.targetTexture;

			Texture2D texture = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGB24, false);
			//Texture2D texture = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.DXT1, false);

			float captureCropLeft = 150;
			float captureCropBottom = 40;
			Rect rect = new Rect(captureCropLeft, captureCropBottom, 960, 960);

			// Read pixels off the screen...
			texture.ReadPixels(rect, 20, 20); //..SCREEN CAPTURE

			texture.Compress(true);
			texture.Apply();
			RenderTexture.ReleaseTemporary(renderTexture);
			return texture;
		}

		private CatalogEntry MakeCatalogEntry(CatalogEntry catalogEntry)
		{
			try
			{
				//catalogEntry.ImageAsEncodedString = EncodedImageFromTexture(catalogEntry.ImageAsTexture);
				// Create main image panel...
				GameObject catalogEntryPanel = _catalogUi.CreateImagePanel(_mainWindow.SubWindow, catalogEntry.ImageInfo.Texture, (int)CatalogEntryFrameSize.val, (int)CatalogEntryFrameSize.val, 0, 0);
				//GameObject catalogEntryPanel = UIHelper.CreatePanel(_windowContainer, (int)_catalogEntryFrameSize.val, (int)_catalogEntryFrameSize.val, 0, 0, Color.red, Color.clear, catalogEntry.ImageAsTexture);
				catalogEntry.UiCatalogEntryPanel = catalogEntryPanel;
				// Add indication borders...
				catalogEntry.UiCatalogBorder = _catalogUi.CreateBorders(catalogEntryPanel, (int)CatalogEntryFrameSize.val, (int)CatalogEntryFrameSize.val, _defaultBorderColor, 0, 0, _relativeBorderWidth);
				catalogEntry.CurrentBorderColor = _defaultBorderColor;

				_catalog.Entries.Add(catalogEntry);
				AddEntryToCatalog(catalogEntry, _catalog.Entries.Count - 1);

				int btnHeight = (int)CatalogEntryFrameSize.val / 6;
				int btnWidth = (int)(CatalogEntryFrameSize.val / 6);// - _catalogEntryFrameSize.val / 50);
				int smallerBtnSize = (int)(btnHeight / 1.5);

				AddEntrySelectionOverlay(catalogEntry, catalogEntryPanel);
				//GameObject leftButtonGroup = CreateEntryLeftButtonGroup(catalogEntry, catalogEntryPanel, btnHeight);
				GameObject botttomButtonGroup = CreateEntryBottomButtonGroup(catalogEntry, catalogEntryPanel, btnHeight);

				AddEntryFavoriteButton(catalogEntry, smallerBtnSize, smallerBtnSize, botttomButtonGroup);
				AddEntryDiscardButton(catalogEntry, smallerBtnSize, smallerBtnSize, botttomButtonGroup);
				AddEntryApplyButton(catalogEntry, btnHeight, btnWidth, botttomButtonGroup);
				AddEntryShiftButtons(catalogEntry, smallerBtnSize, smallerBtnSize, botttomButtonGroup);

				//ResizeBackpanel();
				return catalogEntry;
			}
			catch (Exception e)
			{
				SuperController.LogError(e.ToString());
				throw e;
			}
		}

		private void AddOrShowAnimationUi(CatalogEntry catalogEntry)
		{
			var animatedElement = GetAnimationElementFromCatalogEntry(catalogEntry);
			animatedElement.OnDisplay = true;
			var displayElements = GetDisplayElementsFromAnimationTree(animatedElement);
			GameObject animationPanel = CatalogUiHelper.CreatePanel(catalogEntry.UiCatalogEntryPanel, 0, (int)CatalogEntryFrameSize.val, 0, 0, Color.clear, Color.white);
			var innerPanel = CatalogUiHelper.CreatePanel(animationPanel, (int)CatalogEntryFrameSize.val, displayElements.Count() * 25, 0, 0, new Color(0.1f, 0.1f, 0.1f, 0.9f), Color.clear);
			VerticalLayoutGroup animationVLayout = _catalogUi.CreateVerticalLayout(innerPanel, 0, true, false, false, false);
			catalogEntry.UiAnimationPanel = animationPanel;
			catalogEntry.UiAnimationInnerPanel = innerPanel;
			catalogEntry.UiAnimationVLayout = animationVLayout;
			foreach (var element in displayElements)
			{
				CreateAnimationBar(element, animationPanel, animationVLayout, catalogEntry);
			}
			animationPanel.transform.localPosition = new Vector3(animationPanel.transform.localPosition.x, (displayElements.Count * 25), animationPanel.transform.localPosition.z);
		}

		private List<AnimationElement> GetDisplayElementsFromAnimationTree(AnimationElement animatedElement)
		{
			List<AnimationElement> displayElements =  new List<AnimationElement>();
			if (animatedElement.OnDisplay) displayElements.Add(animatedElement);
			foreach (var childElement in animatedElement.ChildElements)
			{
				displayElements.AddRange(GetDisplayElementsFromAnimationTree(childElement));
			}
			foreach (var subElement in animatedElement.SubElements)
			{
				displayElements.AddRange(GetDisplayElementsFromAnimationTree(subElement));
			}
			return displayElements;
		}

		//private List<AnimatedItem> GetAnimatedItemsForChildCatalogEntries(CatalogEntry catalogEntry)
		//{
		//	var relevantItems = catalogEntry.ChildEntries
		//		.Where(p => p.Active)
		//		.ToList();
		//	List<AnimatedItem> animatedItems = new List<AnimatedItem>();
		//	foreach (var childEntry in relevantItems)
		//	{
		//		childEntry.AnimatedItem = GetAnimationElementFromCatalogEntry(childEntry);
		//		animatedItems.Add(childEntry.AnimatedItem);
		//	}
		//	return animatedItems;
		//}

		private List<AnimationElement> GetAnimationElementsForPoseMorphs(CatalogEntry catalogEntry)
		{
			if (catalogEntry.Mutation == null) return new List<AnimationElement>();
			var relevantPoseMorphs = catalogEntry.Mutation.PoseMorphs
				.Where(p => p.Active)
				.Where(p => p.PositionState != FreeControllerV3.PositionState.Off.ToString() || p.RotationState != FreeControllerV3.RotationState.Off.ToString())
				.ToList();
			List<AnimationElement> animatedItems = new List<AnimationElement>();
			foreach (var poseMorph in relevantPoseMorphs)
			{
				poseMorph.AnimationItem = GetAnimationElementFromPoseMorph(poseMorph);
				animatedItems.Add(poseMorph.AnimationItem);
			}
			return animatedItems;
		}

		private UIDynamicButton CreateAnimationBar(AnimationElement parentElement, GameObject parentPanel, VerticalLayoutGroup vLayout, CatalogEntry catalogEntry)
		{
			var barContrainer = CatalogUiHelper.CreatePanel(parentPanel, 0, 25, 0, 0, new Color(0.1f, 0.1f, 0.1f, 0.9f), Color.clear);
			barContrainer.transform.SetParent(vLayout.transform, false);

			var handleButtonWidth = 25;
			var handleButtonHeight = 25;
			var leftLimit = 0 + (handleButtonWidth / 2);
			var rightLimit = (int)CatalogEntryFrameSize.val - handleButtonWidth / 2;

			var labelInitialText = (catalogEntry != null) ? $"{catalogEntry.TransitionTimeInSeconds}s {parentElement.Name}" : parentElement.Name;
			var label = _catalogUi.CreateButton(barContrainer, labelInitialText, (int)CatalogEntryFrameSize.val, 25, 0, 0, new Color(0.1f, 0.1f, 0.1f), new Color(0.1f, 0.1f, 0.1f), Color.white);
			label.buttonText.fontSize = 15;
			label.buttonText.fontStyle = FontStyle.Italic;
			parentElement.UiLabel = label;
			
			var itemBar = _catalogUi.CreateButton(barContrainer, "", (int)CatalogEntryFrameSize.val - (handleButtonWidth * 2), 25, 25, 0, new Color(0.3f, 0.3f, 0.3f, 0.5f), new Color(0.3f, 0.3f, 0.3f, 0.5f), Color.white);
			parentElement.UiActiveAreaBar = itemBar;
			Action<DragHelper> onBeforeLabelDrag = (dragHelper) =>
			{
				dragHelper.XStep = 1f;
				dragHelper.YStep = 1f;
				dragHelper.YMultiplier = 100f;
				//dragHelper.LimitX = new Vector2(0, 100);
				//dragHelper.LimitY = new Vector2(0.01f, 100);
			};
			var numberTracker = new GameObject();
			Func<float, float, bool> whileDraggingUp = (newX, newY) => 
			{
				int yUpperLimit = 10000;
				int yLowerLimit = 0;
				if (numberTracker.transform.localPosition.y > yUpperLimit) numberTracker.transform.localPosition = new Vector3(numberTracker.transform.localPosition.x, yUpperLimit, numberTracker.transform.localPosition.z);
				if (numberTracker.transform.localPosition.y < yLowerLimit) numberTracker.transform.localPosition = new Vector3(numberTracker.transform.localPosition.x, yLowerLimit, numberTracker.transform.localPosition.z);
				float transitionTime = numberTracker.transform.localPosition.y / 10;
				catalogEntry.TransitionTimeInSeconds = transitionTime;
				parentElement.UiLabel.buttonText.text = $"{catalogEntry.TransitionTimeInSeconds}s {parentElement.Name}";
				return true;
			};
			AddDragging(label.gameObject, numberTracker, onBeforeLabelDrag, null, whileDraggingUp, false, true);
			AddDragging(itemBar.gameObject, numberTracker, onBeforeLabelDrag, null, whileDraggingUp, false, true);

			Action<DragHelper> onBeforeHandleDrag = (dragHelper) =>
			{
				dragHelper.LimitX = new Vector2(leftLimit, rightLimit);
				dragHelper.XStep = 1;
			};
			Func<float, float, bool> onLeftHandleDrag = (newX, newY) =>
			{
				// Update left bar...
				if (parentElement.UiLeftHandle.transform.localPosition.x > parentElement.UiRightHandle.transform.localPosition.x)
				{
					parentElement.UiRightHandle.transform.localPosition = new Vector3(parentElement.UiLeftHandle.transform.localPosition.x, parentElement.UiRightHandle.transform.localPosition.y, parentElement.UiRightHandle.transform.localPosition.z);
				}
				// Update center bar...
				UpdateAnimElementAndUiCenterBarWidth(parentElement);
				return true;
			};

			Func<float, float, bool> onRightHandleDrag = (newX, newY) =>
			{
				// Update right bar...
				if (parentElement.UiRightHandle.transform.localPosition.x < parentElement.UiLeftHandle.transform.localPosition.x)
				{
					parentElement.UiLeftHandle.transform.localPosition = new Vector3(parentElement.UiRightHandle.transform.localPosition.x, parentElement.UiLeftHandle.transform.localPosition.y, parentElement.UiLeftHandle.transform.localPosition.z);
				}
				// Update center bar...
				UpdateAnimElementAndUiCenterBarWidth(parentElement);
				return true;
			};

			var leftHandle = _catalogUi.CreateButton(barContrainer, "", handleButtonWidth, handleButtonHeight, 0, 0, new Color(0.5f, 0.7f, 0.5f, 0.5f), new Color(0.5f, 0.7f, 0.3f, 0.5f), Color.white);
			parentElement.UiLeftHandle = leftHandle;
			AddDragging(leftHandle.gameObject, leftHandle.gameObject, onBeforeHandleDrag, null, onLeftHandleDrag, true, false);

			var rightHandle = _catalogUi.CreateButton(barContrainer, "", handleButtonWidth, handleButtonHeight, (int)CatalogEntryFrameSize.val - handleButtonWidth, 0, new Color(0.7f, 0.5f, 0.5f), new Color(0.7f, 0.5f, 0.5f), Color.white);
			parentElement.UiRightHandle = rightHandle;
			AddDragging(rightHandle.gameObject, rightHandle.gameObject, onBeforeHandleDrag, null, onRightHandleDrag, true, false);

			UnityAction<float, float> onAdjustAnimarionBar = (startPosRatio, endPosRatio) =>
			{
				parentElement.StartAtRatio = startPosRatio;
				parentElement.EndAtRatio = endPosRatio;
			};

			UpdateAnimElementAndUiCenterBarWidth(parentElement, onAdjustAnimarionBar);
			return label;
		}

		private void UpdateAnimElementAndUiCenterBarWidth(AnimationElement parentElement, UnityAction<float,float> onAdjustAnimationBarWithStartAndEndRatios = null)
		{
			var rect = parentElement.UiActiveAreaBar.button.GetComponent<RectTransform>();
			var newWidth = Mathf.Abs(Mathf.Abs(parentElement.UiRightHandle.button.transform.localPosition.x) - Mathf.Abs(parentElement.UiLeftHandle.button.transform.localPosition.x));
			rect.sizeDelta = new Vector2(newWidth, rect.rect.height);

			var barPos = parentElement.UiActiveAreaBar.button.transform.localPosition;
			var leftButtonXPos = parentElement.UiLeftHandle.button.transform.localPosition.x;
			var rightButtonXPos = parentElement.UiRightHandle.button.transform.localPosition.x;

			var newLeftPos = leftButtonXPos + newWidth / 2;
			parentElement.UiActiveAreaBar.button.transform.localPosition = new Vector3(newLeftPos, barPos.y, barPos.z);
			var rowRect = parentElement.UiLabel.button.GetComponent<RectTransform>();

			var rowMoveableZone = rowRect.rect.width - 25; //...to compensate for the width of half the button
			var startPosRatio = (leftButtonXPos - 12) / rowMoveableZone;
			var endPosRatio = (rightButtonXPos - 12) / rowMoveableZone;

			// Overflow adjustments...
			if (startPosRatio > 1) startPosRatio = 1;
			if (startPosRatio < 0) startPosRatio = 0;
			if (endPosRatio > 1) endPosRatio = 1;
			if (endPosRatio < 0) endPosRatio = 0;

			if (onAdjustAnimationBarWithStartAndEndRatios != null) onAdjustAnimationBarWithStartAndEndRatios.Invoke(startPosRatio, endPosRatio);
		}

		private AnimationElement GetAnimationElementFromCatalogEntry(CatalogEntry catalogEntry)
		{
			var childElements = catalogEntry.ChildEntries.Select(GetAnimationElementFromCatalogEntry).ToList();
			var subElements = GetAnimationElementsForPoseMorphs(catalogEntry);
			var newAnimationItem = new AnimationElement()
			{
				Name = catalogEntry.UniqueName, 
				Curve = AnimationCurveEnum.GAUSSIAN, 
				DirectionFlipped = false, 
				StartAtRatio = catalogEntry.StartTimeRatio,
				EndAtRatio = catalogEntry.EndTimeRatio, 
				TransitionTimeInSeconds = catalogEntry.TransitionTimeInSeconds,
				ChildElements = childElements,
				SubElements = subElements
			};
			return newAnimationItem;
		}

		private AnimationElement GetAnimationElementFromPoseMorph(PoseMutation poseController)
		{
			List<AnimationElement> positionElements = GetAnimatedElementsFromVector("pos", poseController.Position);
			List<AnimationElement> rotationElements = GetAnimatedElementsFromVector("rot", poseController.Rotation);
			var newAnimationElement = new AnimationElement()
			{
				Name = poseController.Id, 
				StartAtRatio = poseController.StartAtTimeRatio, 
				EndAtRatio = poseController.EndAtTimeRatio,
				SubElements = positionElements.Concat(rotationElements).ToList()
			};
			return newAnimationElement;
		}

		private List<AnimationElement> GetAnimatedElementsFromVector(string vectorName, Quaternion targetPosition)
		{
			var newElementList = new List<AnimationElement>();
			var xComp = BuildAnimatedElementsFromValues($"{vectorName}.x", targetPosition.x);
			var yComp = BuildAnimatedElementsFromValues($"{vectorName}.y", targetPosition.y);
			var zComp = BuildAnimatedElementsFromValues($"{vectorName}.z", targetPosition.z);
			var wComp = BuildAnimatedElementsFromValues($"{vectorName}.w", targetPosition.w);
			newElementList.Add(xComp);
			newElementList.Add(yComp);
			newElementList.Add(zComp);
			newElementList.Add(wComp);
			return newElementList;
		}

		private List<AnimationElement> GetAnimatedElementsFromVector(string vectorName, Vector3 targetPosition)
		{
			var newElementList = new List<AnimationElement>();
			var xComp = BuildAnimatedElementsFromValues($"{vectorName}.x", targetPosition.x);
			var yComp = BuildAnimatedElementsFromValues($"{vectorName}.y", targetPosition.y);
			var zComp = BuildAnimatedElementsFromValues($"{vectorName}.z", targetPosition.z);
			newElementList.Add(xComp);
			newElementList.Add(yComp);
			newElementList.Add(zComp);
			return newElementList;
		}

		private AnimationElement BuildAnimatedElementsFromValues(string name, float targetValue)
		{
			return new AnimationElement()
			{
				Name = name,
				TargetValue = targetValue,
				TransitionTimeInSeconds = 1,
				StartAtRatio = 0,
				EndAtRatio = 1,
				DirectionFlipped = false
			};
		}

		private GameObject CreateEntryBottomButtonGroup(CatalogEntry catalogEntry, GameObject catalogEntryPanel, int btnHeight)
		{
			GameObject buttonGroup = CreateButtonRow(catalogEntryPanel, btnHeight);
			// Create container for sub-frame buttons...
			catalogEntry.UiBottomButtonGroup = buttonGroup;
			CatalogUiHelper.SetAnchors(catalogEntry.UiCatalogEntryPanel, catalogEntry.UiBottomButtonGroup, "bottom", 0, 0);
			return buttonGroup;
		}

		private void AddEntryDiscardButton(CatalogEntry catalogEntry, int btnHeight, int btnWidth, GameObject buttonGroup)
		{
			// Reject button
			var baseButtonColor = new Color(0.7f, 0.7f, 0.7f, 0.5f);
			var rejectButtonHighlightedColor = new Color(0.5f, 0.0f, 0.0f, 1f);
			var rejectButtonTexture = _imageLoaderService.GetFutureImageFromFileOrCached(GetPluginPath() + "/Resources/Delete.png");
			UIDynamicButton discardButton = _catalogUi.CreateButton(buttonGroup, "", btnWidth, btnHeight, 0, 0, baseButtonColor, rejectButtonHighlightedColor, Color.white, rejectButtonTexture);
			catalogEntry.UiDiscardButton = discardButton;
			discardButton.button.onClick.AddListener(() =>
			{
				catalogEntry.Discarded = !catalogEntry.Discarded;
				//SetCatalogEntryOpacity(catalogEntry, 0.1f);
				catalogEntry.Favorited = 0;
				UpdateCatalogEntryBorderColorBasedOnState(catalogEntry);
				UpdateCatalogEntryDiscardButtonBasedOnState(catalogEntry);
				UpdateCatalogEntryFavoriteButtonBasedOnState(catalogEntry);
				RemoveEntryFromCatalog(catalogEntry);
			});
			SetTooltipForDynamicButton(discardButton, () => "Reject / Remove Likes (Use 'Sort' to remove)");
		}

		private static void UpdateCatalogEntryDiscardButtonBasedOnState(CatalogEntry catalogEntry)
		{
			var baseButtonColor = new Color(0.7f, 0.7f, 0.7f, 0.5f);
			var rejectButtonHighlightedColor = new Color(1f, 0.0f, 0.0f, 1f);
			catalogEntry.UiDiscardButton.buttonColor = catalogEntry.Discarded ? rejectButtonHighlightedColor : baseButtonColor;
		}

		private void AddEntryShiftButtons(CatalogEntry catalogEntry, int btnHeight, int btnWidth, GameObject buttonGroup)
		{
			// Shift Left button
			var baseButtonColor = new Color(0.7f, 0.7f, 0.7f, 0.5f);
			var rejectButtonHighlightedColor = new Color(0.5f, 0.0f, 0.0f, 1f);
			var leftTexture = _imageLoaderService.GetFutureImageFromFileOrCached(GetPluginPath() + "/Resources/Previous.png");
			UIDynamicButton shiftLeft = _catalogUi.CreateButton(buttonGroup, "", btnWidth, btnHeight, 0, 0, baseButtonColor, rejectButtonHighlightedColor, Color.white, leftTexture);
			catalogEntry.UiShiftLeftButton = shiftLeft;
			shiftLeft.button.onClick.AddListener(() =>
			{
				int entryIndex = _catalog.Entries.IndexOf(catalogEntry);
				if (entryIndex == 0) return;
				var frontEntryIndex = entryIndex - 1;
				_catalog.Entries.Swap(frontEntryIndex, entryIndex);
				RebuildCatalogFromEntriesCollection(); //...After shifting single entry forward
			});
			SetTooltipForDynamicButton(shiftLeft, () => "Shift Entry");
			// Shift Right button
			var rightTexture = _imageLoaderService.GetFutureImageFromFileOrCached(GetPluginPath() + "/Resources/Next.png");
			UIDynamicButton shiftRight = _catalogUi.CreateButton(buttonGroup, "", btnWidth, btnHeight, 0, 0, baseButtonColor, rejectButtonHighlightedColor, Color.white, rightTexture);
			catalogEntry.UiShiftRightButton = shiftRight;
			shiftRight.button.onClick.AddListener(() =>
			{
				int entryIndex = _catalog.Entries.IndexOf(catalogEntry);
				if (entryIndex == _catalog.Entries.Count - 1) return;
				var backEntryIndex = entryIndex + 1;
				_catalog.Entries.Swap(backEntryIndex, entryIndex);
				RebuildCatalogFromEntriesCollection();//...After shifting single entry backward
			});
			SetTooltipForDynamicButton(shiftLeft, () => "Shift Entry");
		}

		private void AddEntryFavoriteButton(CatalogEntry catalogEntry, int btnHeight, int btnWidth, GameObject buttonGroup)
		{
			// Favorite button
			var baseButtonColor = new Color(0.7f, 0.7f, 0.7f, 0.5f);
			var acceptButtonHighlightedColor = new Color(0.0f, 0.5f, 0.0f, 1f);
			var acceptButtonTexture = _imageLoaderService.GetFutureImageFromFileOrCached(GetPluginPath() + "/Resources/Favorite.png");
			UIDynamicButton keepButton = _catalogUi.CreateButton(buttonGroup, "", btnWidth, btnHeight, 0, 0, baseButtonColor, acceptButtonHighlightedColor, Color.white, acceptButtonTexture);
			catalogEntry.UiKeepButton = keepButton;
			catalogEntry.UiKeepButton.buttonText.fontSize = 15;
			UpdateCatalogEntryFavoriteButtonBasedOnState(catalogEntry);
			keepButton.button.onClick.AddListener(() =>
			{
				catalogEntry.Discarded = false;
				catalogEntry.Favorited += 1;
				UpdateCatalogEntryFavoriteButtonBasedOnState(catalogEntry);
				UpdateCatalogEntryDiscardButtonBasedOnState(catalogEntry);
				UpdateCatalogEntryBorderColorBasedOnState(catalogEntry);
			});
			SetTooltipForDynamicButton(keepButton, () => "Add Like (Use 'Sort' to reorder)");
		}

		//private void AddMenuButton(CatalogEntry catalogEntry, int btnHeight, int btnWidth, GameObject buttonGroup)
		//{
		//	// Favorite button
		//	var baseButtonColor = new Color(0.7f, 0.7f, 0.7f, 0.5f);
		//	var acceptButtonHighlightedColor = new Color(0.0f, 0.5f, 0.0f, 1f);
		//	var acceptButtonTexture = _imageLoaderService.GetFutureImageFromFileOrCached(GetPluginPath() + "/Resources/SubItemMenu.png");
		//	UIDynamicButton keepButton = _catalogUi.CreateButton(buttonGroup, "", btnWidth, btnHeight, 0, 0, baseButtonColor, acceptButtonHighlightedColor, Color.white, acceptButtonTexture);
		//	catalogEntry.UiMenuButton = keepButton;
		//	catalogEntry.UiMenuButton.buttonText.fontSize = 15;
		//	UpdateCatalogEntryFavoriteButtonBasedOnState(catalogEntry);
		//	keepButton.button.onClick.AddListener(() =>
		//	{
		//			ShowCatalogEntryMenu
		//	});
		//	SetTooltipForDynamicButton(keepButton, () => "Add Like (Use 'Sort' to reorder)");
		//}

		private static void UpdateCatalogEntryFavoriteButtonBasedOnState(CatalogEntry catalogEntry)
		{
			var baseButtonColor = new Color(0.7f, 0.7f, 0.7f, 0.5f);
			var acceptButtonHighlightedColor = new Color(0.0f, 0.5f, 0.0f, 1f);
			if (catalogEntry.Favorited > 0)
			{
				catalogEntry.UiKeepButton.buttonText.text = "+" + catalogEntry.Favorited;
				catalogEntry.UiKeepButton.buttonColor = acceptButtonHighlightedColor;
			}
			else
			{
				catalogEntry.UiKeepButton.buttonText.text = "";
				catalogEntry.UiKeepButton.buttonColor = baseButtonColor;
			}
		}

		private void AddEntryApplyButton(CatalogEntry catalogEntry, int btnHeight, int btnWidth, GameObject buttonGroup)
		{
			// Apply button
			var baseButtonColor = new Color(0.7f, 0.7f, 0.7f, 0.7f);
			var applyButtonHighlightedColor = new Color(1f, 0.647f, 0f, 1f);
			var applyButtonTexture = _imageLoaderService.GetFutureImageFromFileOrCached(GetPluginPath() + "/Resources/Apply.png");
			UIDynamicButton applyButton = _catalogUi.CreateButton(buttonGroup, "", btnWidth, btnHeight, 0, 0, baseButtonColor, applyButtonHighlightedColor, Color.white, applyButtonTexture);
			catalogEntry.UiApplyButton = applyButton;
			SetTooltipForDynamicButton(catalogEntry.UiApplyButton, () => "Apply");
			catalogEntry.ApplyAction = (TheCatalogEntry) => ApplyMasterCatalogEntry(TheCatalogEntry);
			catalogEntry.UiApplyButton.button.onClick.AddListener((UnityAction)(() =>
			{
				// Unhighlight the other apply button...
				_catalog.Entries.ForEach(e => e.UiApplyButton.buttonColor = new Color(0.7f, 0.7f, 0.7f, 1f));
				// Highlight this apply button...
				catalogEntry.UiApplyButton.buttonColor = new Color(1f, 0.647f, 0f, 1f);
				// Apply...
				this.ApplyMasterCatalogEntry((CatalogEntry)catalogEntry);
			}));
		}

		private void AddEntrySelectionOverlay(CatalogEntry catalogEntry, GameObject catalogEntryPanel)
		{
			// Select button
			var selectButtonColor = new Color(1f, 0.5f, 0.5f, 0f);
			var selectButtonHighlightColor = selectButtonColor; //new Color(0.0f, 0.0f, 0.5f, 0.2f); ;
			UIDynamicButton selectButton = _catalogUi.CreateButton(catalogEntryPanel, "", 0, (int)CatalogEntryFrameSize.val, 0, 0, selectButtonColor, selectButtonHighlightColor, Color.white);
			catalogEntry.UiSelectButton = selectButton;
			catalogEntry.UiSelectButton.button.onClick.AddListener(() => SelectCatalogEntry(catalogEntry));
			SetTooltipForDynamicButton(catalogEntry.UiSelectButton, () => "Select");

			AddDraggingToCatalogEntryOverlay(catalogEntry);// Add mouse-over event...																																																																																				 // Create mouse-down event...
			AddHoverEnterEventToCatalogEntryOverlay(catalogEntry);
			AddHoverExitEventToCatalogEntryOverlay(catalogEntry);
		}

		private void AddHoverExitEventToCatalogEntryOverlay(CatalogEntry catalogEntry)
		{
			// Add leave hover...
			var existingExitTrigger = catalogEntry.UiSelectButton.gameObject.GetComponents<EventTrigger>().FirstOrDefault(t => t.name == "TriggerOnExit");
			EventTrigger triggerPointerExit = existingExitTrigger ?? catalogEntry.UiSelectButton.gameObject.AddComponent<EventTrigger>();
			triggerPointerExit.name = "TriggerOnExit";
			var pointerExit = triggerPointerExit.triggers.FirstOrDefault(t => t.eventID == EventTriggerType.PointerExit) ?? new EventTrigger.Entry();
			pointerExit.eventID = EventTriggerType.PointerExit;
			pointerExit.callback.RemoveAllListeners();
			pointerExit.callback.AddListener((e) =>
			{
				// Add hover information...
				_mainWindow.InfoLabel.text = "";
				_mainWindow.InfoLabel.transform.localScale = Vector3.zero;
			});
			triggerPointerExit.triggers.RemoveAll(t => t.eventID == EventTriggerType.PointerExit);
			triggerPointerExit.triggers.Add(pointerExit);
		}

		private void AddHoverEnterEventToCatalogEntryOverlay(CatalogEntry catalogEntry)
		{
			var existingHoverTrigger = catalogEntry.UiSelectButton.gameObject.GetComponents<EventTrigger>().FirstOrDefault(t => t.name == "TriggerOnEnter");
			EventTrigger triggerPointerEnter = existingHoverTrigger ?? catalogEntry.UiSelectButton.gameObject.AddComponent<EventTrigger>();
			triggerPointerEnter.name = "TriggerOnEnter";
			var pointerEnter = triggerPointerEnter.triggers.FirstOrDefault(t => t.eventID == EventTriggerType.PointerEnter) ?? new EventTrigger.Entry();
			pointerEnter.eventID = EventTriggerType.PointerEnter;
			pointerEnter.callback.RemoveAllListeners();
			pointerEnter.callback.AddListener((e) =>
			{
				// Add hover information...
				var text = "";
				if (catalogEntry.CatalogEntryMode == CatalogModeEnum.CATALOG_MODE_SESSION || catalogEntry.CatalogEntryMode == CatalogModeEnum.CATALOG_MODE_OBJECT)
				{
					var objectNames = catalogEntry.Mutation.StoredAtoms.Select(a => a.AtomName).ToArray();
					text = string.Join("\n", objectNames);
				}
				if (catalogEntry.CatalogEntryMode == CatalogModeEnum.CATALOG_MODE_CAPTURE)
				{
					var clothingItems = catalogEntry.Mutation.ClothingItems.Select(c => "clothing: " + c.Id).ToArray();
					var hairItems = catalogEntry.Mutation.HairItems.Select(c => "hair: " + c.Id).ToArray();
					var morphItems = catalogEntry.Mutation.ActiveMorphs.Select(c => "morph: " + c.Id).ToArray();
					var poseItems = catalogEntry.Mutation.PoseMorphs.Select(c => "pose: " + c.Id).ToArray();
					text = $"{string.Join("\n", clothingItems)}\n{string.Join("\n", hairItems)}\n{string.Join("\n", morphItems)}\n{string.Join("\n", poseItems)}";
				}
				if (catalogEntry.CatalogEntryMode == CatalogModeEnum.CATALOG_MODE_MUTATIONS)
				{
					var morphItems = catalogEntry.Mutation.FaceGenMorphSet.Select(c => "morph: " + c.Id).ToArray();
					text = $"{string.Join("\n", morphItems)}";
				}
				_mainWindow.InfoLabel.text = text;
				_mainWindow.InfoLabel.transform.localScale = string.IsNullOrEmpty(text) ? Vector3.zero : Vector3.one;
			});
			triggerPointerEnter.triggers.RemoveAll(t => t.eventID == EventTriggerType.PointerEnter);
			triggerPointerEnter.triggers.Add(pointerEnter);
		}

		private void AddDraggingToCatalogEntryOverlay(CatalogEntry catalogEntry)
		{
			catalogEntry.XPositionTracker = new DragHelper();
			catalogEntry.YPositionTracker = new DragHelper();

			Action<DragHelper> onStartDragLeftRight = (helper) =>
			{
				helper.AllowDragX = true;
				helper.AllowDragY = false;
				helper.ObjectToDrag = _mainWindow.CatalogColumnContainers[0];
				var totalFrameWidth = GetTotalFrameSize();
				helper.LimitX = new Vector2(-_mainWindow.CatalogColumns.Count * totalFrameWidth + totalFrameWidth, 0);
				helper.IsIn3DSpace = !IsAnchoredOnHUD();
				helper.XMultiplier = -1000f;
			};
			Action<DragHelper> onStartDragUpDown = (helper) =>
			{
				helper.AllowDragX = false;
				helper.AllowDragY = true;
				var totalFrameWidth = GetTotalFrameSize();
				helper.YStep = totalFrameWidth;
				helper.LimitY = new Vector2(totalFrameWidth, 0);
				//catalogEntry.YPositionTracker.IsIn3DSpace = !IsAnchoredOnHUD();
				//catalogEntry.YPositionTracker.XMultiplier = -1000f;
				helper.YMultiplier = 1000f;
			};
			Func<float, float, bool> onDragLeftRight = (newX, newY) =>
			{
				SetRowStateBasedOnScrollPosition(newX);
				return true;
			};

			Func<float, float, bool> onDragUpDown = (newX, newY) =>
			{
				MoveIntoNextRow();
				return true;
			};

			catalogEntry.XPositionTracker.AddMouseDraggingToObject(catalogEntry.UiSelectButton.gameObject, _mainWindow.CatalogColumnContainers[0], true, false, onStartDragLeftRight, null, onDragLeftRight); // allow user to drag-scroll using this button aswell				
			catalogEntry.YPositionTracker.AddMouseDraggingToObject(catalogEntry.UiSelectButton.gameObject, catalogEntry.UiCatalogEntryPanel, false, true, onStartDragUpDown, null, onDragUpDown); // allow user to drag-scroll using this button aswell				

		}

		private void MoveIntoNextRow()
		{
			//if ()
		}

		private float GetTotalFrameSize()
		{
			return CatalogEntryFrameSize.val + _relativeBorderWidth;
		}

		private void SetRowStateBasedOnScrollPosition(float newX)
		{
			var totalFrameWidth = GetTotalFrameSize();
			for (int i = 0; i < _catalog.Entries.Count; i++)
			{

				var entryPosition = newX + (i * totalFrameWidth);
				var rowLeftOverflow = 0;
				var rowRightOverflow = totalFrameWidth * (_catalogColumnsCountJSON.val);
				if (entryPosition <= rowLeftOverflow && entryPosition + totalFrameWidth >= rowLeftOverflow)
				{
					// Fading out on the left...
					var remaining = entryPosition + totalFrameWidth;
					var overflow = entryPosition;
					var squashX = 1 - (remaining / totalFrameWidth);
					var angle = 90 * squashX;
					SetCatalogEntryOpacity(_catalog.Entries[i], 1 - squashX);// ...SetRowStateBasedOnScrollPosition
					_catalog.Entries[i].UiCatalogEntryPanel.transform.localRotation = Quaternion.Euler(0, angle, 0);// ...Rotation
					_catalog.Entries[i].UiParentCatalogColumn.transform.localScale = Vector3.one; // ...Scale
					_catalog.Entries[i].UiCatalogEntryPanel.transform.localPosition = new Vector3(-overflow / 2, _catalog.Entries[i].UiCatalogEntryPanel.transform.localPosition.y, _catalog.Entries[i].UiCatalogEntryPanel.transform.localPosition.z);
					//SetCatalogEntryOpacity(_catalog.Entries[i], 1);// ...SetRowStateBasedOnScrollPosition
					//_catalog.Entries[i].UiCatalogEntryPanel.transform.localRotation = Quaternion.Euler(0, 0, 0);// ...Rotation
					//_catalog.Entries[i].UiParentCatalogColumn.transform.localScale = Vector3.one;// ...Scale
					//_catalog.Entries[i].UiCatalogEntryPanel.transform.localPosition = new Vector3(-entryPosition, _catalog.Entries[i].UiCatalogEntryPanel.transform.localPosition.y, _catalog.Entries[i].UiCatalogEntryPanel.transform.localPosition.z);
				}
				else if (entryPosition <= rowLeftOverflow && HackToPreventLastEntryFromDisapearing(i))
				{
					// Totally off the left...
					SetCatalogEntryOpacity(_catalog.Entries[i], 1 - 1);// ...SetRowStateBasedOnScrollPosition
					_catalog.Entries[i].UiCatalogEntryPanel.transform.localRotation = Quaternion.Euler(0, 0, 0); // ...Rotation
					_catalog.Entries[i].UiParentCatalogColumn.transform.localScale = Vector3.zero;// ...Scale
					_catalog.Entries[i].UiCatalogEntryPanel.transform.localPosition = new Vector3(0, _catalog.Entries[i].UiCatalogEntryPanel.transform.localPosition.y, _catalog.Entries[i].UiCatalogEntryPanel.transform.localPosition.z); // ...Position
				}
				else if (entryPosition + totalFrameWidth >= rowRightOverflow && entryPosition <= rowRightOverflow)
				{
					// Fading out on the right...
					var remaining = entryPosition + totalFrameWidth - rowRightOverflow;
					var overflow = entryPosition + totalFrameWidth - rowRightOverflow;
					var squashX = remaining / totalFrameWidth;
					var angle = 90 * squashX;
					SetCatalogEntryOpacity(_catalog.Entries[i], 1 - squashX);// ...SetRowStateBasedOnScrollPosition
					_catalog.Entries[i].UiCatalogEntryPanel.transform.localRotation = Quaternion.Euler(0, angle, 0);// ...Rotation
					_catalog.Entries[i].UiParentCatalogColumn.transform.localScale = Vector3.one; // ...Scale
					_catalog.Entries[i].UiCatalogEntryPanel.transform.localPosition = new Vector3(-overflow / 2, _catalog.Entries[i].UiCatalogEntryPanel.transform.localPosition.y, _catalog.Entries[i].UiCatalogEntryPanel.transform.localPosition.z);
				}
				else if (entryPosition >= rowRightOverflow)
				{
					// Totally off the right...
					SetCatalogEntryOpacity(_catalog.Entries[i], 1 - 1);// ...SetRowStateBasedOnScrollPosition
					_catalog.Entries[i].UiCatalogEntryPanel.transform.localRotation = Quaternion.Euler(0, 0, 0); // ...Rotation
					_catalog.Entries[i].UiParentCatalogColumn.transform.localScale = Vector3.zero;// ...Scale
					_catalog.Entries[i].UiCatalogEntryPanel.transform.localPosition = new Vector3(0, _catalog.Entries[i].UiCatalogEntryPanel.transform.localPosition.y, _catalog.Entries[i].UiCatalogEntryPanel.transform.localPosition.z); // ...Position
				}
				else
				{
					// Somewhere in the middle...
					SetCatalogEntryOpacity(_catalog.Entries[i], 1); // ...SetRowStateBasedOnScrollPosition
					_catalog.Entries[i].UiCatalogEntryPanel.transform.localRotation = Quaternion.Euler(0, 0, 0); // ...Rotation
					_catalog.Entries[i].UiParentCatalogColumn.transform.localScale = Vector3.one; // ...Scale
					_catalog.Entries[i].UiCatalogEntryPanel.transform.localPosition = new Vector3(0, _catalog.Entries[i].UiCatalogEntryPanel.transform.localPosition.y, _catalog.Entries[i].UiCatalogEntryPanel.transform.localPosition.z); // ...Position
				}
			}
		}

		private IEnumerator RedrawCatalog()
		{
			
			var totalFrameWidth = GetTotalFrameSize();
			for (int i = 0; i < _catalog.Entries.Count; i++)
			{
				var entryPosition = -GetCatalogCurrentScrollPosition() + (i * totalFrameWidth);
				var rowLeftOverflow = 0;
				var rowRightOverflow = totalFrameWidth * (_catalogColumnsCountJSON.val);
				if (entryPosition <= rowLeftOverflow && entryPosition + totalFrameWidth >= rowLeftOverflow)
				{
					_catalog.Entries[i].UiCatalogEntryPanel.transform.localScale = Vector3.zero;
					yield return new WaitForFixedUpdate();
					_catalog.Entries[i].UiCatalogEntryPanel.transform.localScale = Vector3.one;
					// Fading out on the left...
					if (i == 0) SuperController.LogMessage("Fading off the left");
					SetCatalogEntryOpacity(_catalog.Entries[i], 1);// ...SetRowStateBasedOnScrollPosition
					_catalog.Entries[i].UiCatalogEntryPanel.transform.localRotation = Quaternion.Euler(0, 0, 0);// ...Rotation
					_catalog.Entries[i].UiParentCatalogColumn.transform.localScale = Vector3.one;// ...Scale
					_catalog.Entries[i].UiCatalogEntryPanel.transform.localPosition = new Vector3(-entryPosition, _catalog.Entries[i].UiCatalogEntryPanel.transform.localPosition.y, _catalog.Entries[i].UiCatalogEntryPanel.transform.localPosition.z);
				}
				else if (entryPosition <= rowLeftOverflow && HackToPreventLastEntryFromDisapearing(i))
				{
					if (i == 0) SuperController.LogMessage("Totally off the left");
					// Totally off the left...
					SetCatalogEntryOpacity(_catalog.Entries[i], 1 - 1);// ...SetRowStateBasedOnScrollPosition
					_catalog.Entries[i].UiCatalogEntryPanel.transform.localRotation = Quaternion.Euler(0, 0, 0); // ...Rotation
					_catalog.Entries[i].UiParentCatalogColumn.transform.localScale = Vector3.zero;// ...Scale
					_catalog.Entries[i].UiCatalogEntryPanel.transform.localPosition = new Vector3(0, _catalog.Entries[i].UiCatalogEntryPanel.transform.localPosition.y, _catalog.Entries[i].UiCatalogEntryPanel.transform.localPosition.z); // ...Position
				}
				else if (entryPosition + totalFrameWidth >= rowRightOverflow && entryPosition <= rowRightOverflow)
				{
					// Fading out on the right...
					var remaining = entryPosition + totalFrameWidth - rowRightOverflow;
					var overflow = entryPosition + totalFrameWidth - rowRightOverflow;
					var squashX = remaining / totalFrameWidth;
					var angle = 90 * squashX;
					SetCatalogEntryOpacity(_catalog.Entries[i], 1 - squashX);// ...SetRowStateBasedOnScrollPosition
					_catalog.Entries[i].UiCatalogEntryPanel.transform.localRotation = Quaternion.Euler(0, angle, 0);// ...Rotation
					_catalog.Entries[i].UiParentCatalogColumn.transform.localScale = Vector3.one; // ...Scale
					_catalog.Entries[i].UiCatalogEntryPanel.transform.localPosition = new Vector3(-overflow / 2, _catalog.Entries[i].UiCatalogEntryPanel.transform.localPosition.y, _catalog.Entries[i].UiCatalogEntryPanel.transform.localPosition.z);
				}
				else if (entryPosition >= rowRightOverflow)
				{
					// Totally off the right...
					SetCatalogEntryOpacity(_catalog.Entries[i], 1 - 1);// ...SetRowStateBasedOnScrollPosition
					_catalog.Entries[i].UiCatalogEntryPanel.transform.localRotation = Quaternion.Euler(0, 0, 0); // ...Rotation
					_catalog.Entries[i].UiParentCatalogColumn.transform.localScale = Vector3.zero;// ...Scale
					_catalog.Entries[i].UiCatalogEntryPanel.transform.localPosition = new Vector3(0, _catalog.Entries[i].UiCatalogEntryPanel.transform.localPosition.y, _catalog.Entries[i].UiCatalogEntryPanel.transform.localPosition.z); // ...Position
				}
				else
				{
					if (i == 0) SuperController.LogMessage("Somewhere in the middle");
					// Somewhere in the middle...
					SetCatalogEntryOpacity(_catalog.Entries[i], 1); // ...SetRowStateBasedOnScrollPosition
					_catalog.Entries[i].UiCatalogEntryPanel.transform.localRotation = Quaternion.Euler(0, 0, 0); // ...Rotation
					_catalog.Entries[i].UiParentCatalogColumn.transform.localScale = Vector3.one; // ...Scale
					_catalog.Entries[i].UiCatalogEntryPanel.transform.localPosition = new Vector3(0, _catalog.Entries[i].UiCatalogEntryPanel.transform.localPosition.y, _catalog.Entries[i].UiCatalogEntryPanel.transform.localPosition.z); // ...Position
				}
			}

		}

		private bool HackToPreventLastEntryFromDisapearing(int i)
		{
			return i != _catalog.Entries.Count - 1;
		}

		private void SetCatalogEntryOpacity(CatalogEntry entry, float opacity)
		{
			var entryImage = entry.UiCatalogEntryPanel.GetComponent<Image>();
			entryImage.color = new Color(entryImage.color.r, entryImage.color.g, entryImage.color.b, opacity);
			var buttonRow = entry.UiBottomButtonGroup.GetComponent<Image>();
			buttonRow.color = new Color(buttonRow.color.r, buttonRow.color.g, buttonRow.color.b, opacity * 0.25f);
			entry.UiKeepButton.buttonColor = new Color(entry.UiKeepButton.buttonColor.r, entry.UiKeepButton.buttonColor.g, entry.UiKeepButton.buttonColor.b, opacity);
			entry.UiDiscardButton.buttonColor = new Color(entry.UiApplyButton.buttonColor.r, entry.UiApplyButton.buttonColor.g, entry.UiApplyButton.buttonColor.b, opacity);
			entry.UiApplyButton.buttonColor = new Color(entry.UiApplyButton.buttonColor.r, entry.UiApplyButton.buttonColor.g, entry.UiApplyButton.buttonColor.b, opacity);
			entry.UiSelectButton.buttonColor = new Color(entry.UiSelectButton.buttonColor.r, entry.UiSelectButton.buttonColor.g, entry.UiSelectButton.buttonColor.b, opacity);
			entry.UiShiftLeftButton.buttonColor = new Color(entry.UiShiftLeftButton.buttonColor.r, entry.UiShiftLeftButton.buttonColor.g, entry.UiShiftLeftButton.buttonColor.b, opacity);
			entry.UiShiftRightButton.buttonColor = new Color(entry.UiShiftRightButton.buttonColor.r, entry.UiShiftRightButton.buttonColor.g, entry.UiShiftRightButton.buttonColor.b, opacity);
		}

		//private Action<CatalogEntry> GetAppropriateApplyAction()
		//{
		//	return 
		//}

		public bool IsAnchoredOnHUD()
		{
			return _anchorOnHud.val;
		}

		//private void ResizeBackpanel()
		//{
		//	if (_anchorOnHud.val) return;
		//	var backPanelRect = _backPanel.GetComponent<RectTransform>();
		//	var width = (_catalogEntryFrameSize.val * (_catalog.Entries.Count > _catalogColumnsCountJSON.val ? _catalogColumnsCountJSON.val : _catalog.Entries.Count)) + 75; //(_catalogEntryFrameSize.val * _catalogColumnsCountJSON.val) + 100;
		//	var height = (float)(_catalogEntryFrameSize.val * Math.Ceiling(_catalog.Entries.Count / _catalogColumnsCountJSON.val)) + 75;
		//	backPanelRect.sizeDelta = new Vector2(width, height);
		//	CatalogUiHelper.SetAnchors(_windowContainer, _backPanel, "topleft", 0, 0);
		//}

		private void UpdateCatalogEntryBorderColorBasedOnState(CatalogEntry newCatalogEntry)
		{
			if (newCatalogEntry.UiCatalogBorder == null) return;
			//UIHelper.AddBorderColorToTexture(newCatalogEntry.UiCatalogBorder.texture, Color.magenta, 10);
			var color = _defaultBorderColor;
			// Favorited...
			if (newCatalogEntry.Favorited > 0)
			{
				float greenValue = ((float)newCatalogEntry.Favorited + 3) / 10;
				color = new Color(0f, greenValue, 0f, 0.5f);
			}
			// Discarded
			else if (newCatalogEntry.Discarded)
			{
				color = new Color(1, 0, 0, 0.8f); //...red
			}
			// Selected
			if (newCatalogEntry.Selected)
			{
				color = _borderSelectedColor;
			}

			CatalogUiHelper.AddBorderColorToTexture(newCatalogEntry.UiCatalogBorder.texture, color, 1);
			newCatalogEntry.CurrentBorderColor = color;
		}

		void DestroyCatalogEntryUi(CatalogEntry catalogEntry)
		{
			catalogEntry.EntrySubItemToggles.ForEach(RemoveUiCatalogSubItem);

			//catalogEntry.Mutation.ActiveMorphs.ForEach(m => RemoveUiCatalogSubItem(m.DynamicCheckbox));
			//catalogEntry.Mutation.ClothingItems.ForEach(m => RemoveUiCatalogSubItem(m.DynamicCheckbox));
			//catalogEntry.Mutation.HairItems.ForEach(m => RemoveUiCatalogSubItem(m.DynamicCheckbox));
			//catalogEntry.Mutation.FaceGenMorphSet.ForEach(m => RemoveUiCatalogSubItem(m.DynamicCheckbox));

			catalogEntry.EntrySubItemToggles.ForEach(m => RemoveUiCatalogSubItem(m));

			Destroy(catalogEntry.UiApplyButton);
			Destroy(catalogEntry.UiKeepButton);
			Destroy(catalogEntry.UiBottomButtonGroup);
			Destroy(catalogEntry.UiDiscardButton);
			Destroy(catalogEntry.UiShiftLeftButton);
			Destroy(catalogEntry.UiShiftRightButton);
			Destroy(catalogEntry.UiSelectButton);
			Destroy(catalogEntry.UiCatalogEntryPanel);

			//if (catalogEntry.AnimatedItem.UiItemBar != null) RemoveButton(catalogEntry.AnimatedItem.UiItemBar);
			//if (catalogEntry.AnimatedItem.UiItemRow != null) RemoveButton(catalogEntry.AnimatedItem.UiItemRow);
			//if (catalogEntry.AnimatedItem.UiLeftHandle != null) RemoveButton(catalogEntry.AnimatedItem.UiLeftHandle);
			//if (catalogEntry.AnimatedItem.UiRightHandle != null) RemoveButton(catalogEntry.AnimatedItem.UiRightHandle);
			if (catalogEntry.UiAnimationPanel != null) Destroy(catalogEntry.UiAnimationPanel);
		}

		GameObject CreateButtonColumn(GameObject parentPanel, int height, bool horizontal = true)
		{
			int spacer = 10;
			var subPanel = _catalogUi.CreateUIPanel(parentPanel, 0, height, "left", 0, 0, new Color(0.25f, 0.25f, 0.25f, 0.25f));
			_catalogUi.CreateHorizontalLayout(subPanel, spacer, false, false, false, false);
			ContentSizeFitter psf = subPanel.AddComponent<ContentSizeFitter>();
			psf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
			psf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
			return subPanel;
		}

		GameObject CreateButtonRow(GameObject parentPanel, int height, int spacer = 10)
		{
			var subPanel = _catalogUi.CreateUIPanel(parentPanel, 0, height, "bottom", 0, 0, new Color(0.25f, 0.25f, 0.25f, 0.25f));
			_catalogUi.CreateHorizontalLayout(subPanel, spacer, false, false, false, false);
			//else _catalogUi.CreateVerticalLayout(subPanel, spacer, false, false, false, false);
			ContentSizeFitter psf = subPanel.AddComponent<ContentSizeFitter>();
			psf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
			psf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
			return subPanel;
		}

		GameObject CreateButtonRowLeftAnchored(GameObject parentPanel, int height, int spacer = 10)
		{
			var subPanel = _catalogUi.CreateUIPanel(parentPanel, 0, height, "left", 0, 0, Color.clear);
			_catalogUi.CreateHorizontalLayout(subPanel, spacer, false, false, false, false);
			//else _catalogUi.CreateVerticalLayout(subPanel, spacer, false, false, false, false);
			ContentSizeFitter psf = subPanel.AddComponent<ContentSizeFitter>();
			psf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
			psf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
			return subPanel;
		}

		//GameObject CreateButtonRowRightAnchored(GameObject parentPanel, int rowHeight, int offsetX = 0, int offsetY = 0)
		//{s
		//	int spacer = 10;
		//	var subPanel = _catalogUi.CreateUIPanel(parentPanel, 0, rowHeight, "right", offsetX, offsetY, Color.clear);
		//	if (horizontal) _catalogUi.CreateHorizontalLayout(subPanel, spacer, false, false, false, false);
		//	else _catalogUi.CreateVerticalLayout(subPanel, spacer, false, false, false, false);
		//	ContentSizeFitter psf = subPanel.AddComponent<ContentSizeFitter>();
		//	psf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
		//	psf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
		//	return subPanel;
		//}

		GameObject CreateButtonColumnTopAnchored(GameObject parentPanel, int width, int offsetX = 0, int offsetY = 0)
		{
			int spacer = 10;
			var subPanel = _catalogUi.CreateUIPanel(parentPanel, width, 0, "top", width / 2 + offsetX, offsetY, Color.clear);
			_catalogUi.CreateVerticalLayout(subPanel, spacer, false, false, false, false);
			ContentSizeFitter psf = subPanel.AddComponent<ContentSizeFitter>();
			psf.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
			psf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
			return subPanel;
		}

		private void AddEntryToCatalog(CatalogEntry newCatalogEntry, int entryIndex)
		{
			//if (_expandDirection.val == EXPAND_WITH_MORE_ROWS)
			//{
			//	//int appropriateRowIndex = (int)(entryIndex / _catalogColumnsCountJSON.val);
			//	//if (appropriateRowIndex >= _mainWindow.CatalogRows.Count - 1) _mainWindow.CatalogRows.Add(CreateCatalogRow());
			//	//var catalogRow = _mainWindow.CatalogRows[appropriateRowIndex];
			//	//newCatalogEntry.UiParentCatalogRow = catalogRow;
			//	//newCatalogEntry.UiCatalogEntryPanel.transform.SetParent(catalogRow.transform);
			//}
			//else // EXPAND_WITH_MORE_COLUMNS
			//{
			//int appropriateColIndex = (int)(entryIndex / _catalogRowsCountJSON.val);
			//if (appropriateColIndex >= _mainWindow.CatalogColumns.Count - 1)
			var catalogColumn = CreateCatalogColumn();
			_mainWindow.CatalogColumns.Add(catalogColumn);
			newCatalogEntry.UiParentCatalogColumn = catalogColumn;
			newCatalogEntry.UiCatalogEntryPanel.transform.SetParent(catalogColumn.transform);
			//}
			ResetScrollPosition();
		}

		private void RemoveEntryFromCatalog(CatalogEntry catalogEntry)
		{
			try { 
			var index = _catalog.Entries.IndexOf(catalogEntry);
			_trashCatalog.Entries.Add(catalogEntry);
			_mainWindow.CatalogColumns.Remove(catalogEntry.UiParentCatalogColumn);
			_catalog.Entries.Remove(catalogEntry);
			Destroy(catalogEntry.UiParentCatalogColumn);
			Destroy(catalogEntry.UiCatalogEntryPanel);
				//catalogEntry.UiCatalogEntryPanel.transform.SetParent(null);
				StartCoroutine(RedrawCatalog());
			//_mainWindow.CatalogColumnContainer.transform.localPosition.x
			//SetCatalogPositionToCatalogEntry(GetCatalogCurrentScrollPosition());
				//if (index > 0) StartCoroutine(SetCatalogPositionToCatalogEntry(index-1));

				//StartCoroutine(RedrawCatalog());
				//ResetScrollPosition();
			}
			catch (Exception e)
			{
				SuperController.LogError(e.ToString());
			}
		}

		private void ResetScrollPosition()
		{
			float rowCount = _catalog.Entries.Count() >= _catalogRowsCountJSON.val ? _catalogRowsCountJSON.val : _catalog.Entries.Count();
			var rowHeight = CatalogEntryFrameSize.val * rowCount;
			var newX = 0; //-500;
			_mainWindow.CatalogRowContainer.transform.localPosition = new Vector3(_mainWindow.CatalogRowContainer.transform.localPosition.x, rowHeight + (_relativeBorderWidth * 2) + 10, _mainWindow.CatalogRowContainer.transform.localPosition.z);
			_mainWindow.CatalogColumnContainers[0].transform.localPosition = new Vector3(newX, _mainWindow.CatalogRowContainer.transform.localPosition.y, _mainWindow.CatalogRowContainer.transform.localPosition.z);
		}

		GameObject CreateCatalogColumn()
		{
			var col = _catalogUi.CreateUIPanel(_mainWindow.SubWindow, 400, 400, "left", 0, 0, Color.clear);
			_catalogUi.CreateVerticalLayout(col, 100.0f, false, false, false, false);
			col.transform.SetParent(_mainWindow.CatalogColumnsHLayout.transform, false);
			return col;
		}

		GameObject CreateCatalogRow()
		{
			var row = _catalogUi.CreateUIPanel(_mainWindow.SubWindow, 400, 400, "left", 0, 0, Color.clear);
			_catalogUi.CreateHorizontalLayout(row, 100.0f, false, false, false, false);
			row.transform.SetParent(_mainWindow.CatalogRowsVLayout.transform, false);
			return row;
		}

		private void CreateHandleObject(string assignHandleUniqueName, Vector3 handlesInitialPosition, Action<Atom> onHandleCreatedCallback)
		{
			base.StartCoroutine(CreateAtom("GrabPoint", assignHandleUniqueName, handlesInitialPosition, Quaternion.identity, newAtom =>
			{
				newAtom.GetStorableByID("scale").SetFloatParamValue("scaleX", 0.08f);
				newAtom.GetStorableByID("scale").SetFloatParamValue("scaleY", 0.08f);
				newAtom.GetStorableByID("scale").SetFloatParamValue("scaleZ", 0.08f);
				newAtom.collisionEnabledJSON.SetVal(false);
				onHandleCreatedCallback.Invoke(newAtom);
				_handleObjectForCatalog = newAtom;
			}));
		}

		public virtual IEnumerator CreateAtom(string atomType, string atomId, Vector3 position, Quaternion rotation, Action<Atom> onAtomCreated)
		{
			Atom atom = SuperController.singleton.GetAtomByUid(atomId);
			if (atom == null)
			{
				yield return SuperController.singleton.AddAtomByType(atomType, atomId);
				atom = SuperController.singleton.GetAtomByUid(atomId);
			}
			if (atom != null)
			{
				atom.transform.position = position;
				atom.transform.rotation = rotation;
				onAtomCreated(atom);
			}
		}

		//private bool SelectAtomFromCatalogEntry(CatalogEntry catalogEntry)
		//{
		//	var existingAtom = SuperController.singleton.GetAtomByUid(catalogEntry.Mutation.AtomName);
		//	var selectedAtom = SuperController.singleton.GetSelectedAtom();

		//	if (existingAtom == null || selectedAtom.name == catalogEntry.Mutation.AtomName)
		//	{
		//		// See if there is an atom by the same type, and select that instead...
		//		var atomType = catalogEntry.Mutation.AtomType;
		//		if (atomType == null) return false;
		//		var sceneAtoms = SuperController.singleton.GetAtoms();
		//		var otherAtom = sceneAtoms.FirstOrDefault(a => a.type == atomType);
		//		if (otherAtom == null) return false;
		//		SuperController.singleton.SelectController(existingAtom.mainController);
		//		return true;
		//	}

		//	if (selectedAtom == null) // Atom exits but is not selected
		//	{
		//		//... Select the atom...
		//		SuperController.singleton.SelectController(existingAtom.mainController);
		//		return true;
		//	}
		//	return false;
		//}

		private void CreateAtomsForCatalogEntry(CatalogEntry catalogEntry)
		{
			try
			{
				ShowPopupMessage("Please Wait...");

				// Predetermine all new names, and add all items to the incubating queue...
				Dictionary<int, string> atomSceneNameToCatalogEntryMappings = new Dictionary<int, string>();
				for (var i = 0; i < catalogEntry.Mutation.StoredAtoms.Count; i++)
				{
					var catalogAtom = catalogEntry.Mutation.StoredAtoms[i];
					if (catalogAtom.SubstituteWithSceneAtom != null) continue;
					// Add all incubating atoms to a queue...
					_atomsIncubatingQueue.Add(catalogAtom.AtomName);
					// Generate scene appropriate names for each atom...
					string newAtomSceneName = GetNextAvailableName(catalogAtom.AtomName, catalogAtom.AtomType, atomSceneNameToCatalogEntryMappings);
					atomSceneNameToCatalogEntryMappings.Add(i, newAtomSceneName);
				}

				// Update storables (links and other references) with new atom names...
				for (var lookingInAtomIndex = 0; lookingInAtomIndex < catalogEntry.Mutation.StoredAtoms.Count; lookingInAtomIndex++)
				{
					var lookingInAtom = catalogEntry.Mutation.StoredAtoms[lookingInAtomIndex];
					if (lookingInAtom.SubstituteWithSceneAtom != null) continue;
					lookingInAtom.StagedStorables = new List<JSONClass>();
					for (var inAtomStorableIndex = 0; inAtomStorableIndex < lookingInAtom.Storables.Count; inAtomStorableIndex++)
					{
						var inAtomStorable = lookingInAtom.Storables[inAtomStorableIndex];
						var newStagedStorable = inAtomStorable;
						for (var lookingForAtomIndex = 0; lookingForAtomIndex < catalogEntry.Mutation.StoredAtoms.Count; lookingForAtomIndex++)
						{
							var lookingForAtom = catalogEntry.Mutation.StoredAtoms[lookingForAtomIndex];

							string replacementAtomId = atomSceneNameToCatalogEntryMappings[lookingForAtomIndex];
							if (lookingForAtom.SubstituteWithSceneAtom != null) replacementAtomId = lookingForAtom.SubstituteWithSceneAtom;
							string storableAsString = inAtomStorable.ToString();
							var editedStorable = storableAsString.Replace(lookingForAtom.AtomName + ":", replacementAtomId + ":");
							var newStorableObject = JSONClass.Parse(editedStorable);
							//lookingInAtom.Storables[inAtomStorableIndex] = newStorableObject.AsObject;
							newStagedStorable = newStorableObject.AsObject; // ...overwrite with updated storable
																															//lookingInAtom.Storables[inAtomStorableIndex].StagingInstance = 
						}
						lookingInAtom.StagedStorables.Add(newStagedStorable);
					}
				}

				// Create atoms...
				for (var i = 0; i < catalogEntry.Mutation.StoredAtoms.Count; i++)
				{
					var catalogAtom = catalogEntry.Mutation.StoredAtoms[i];
					if (catalogAtom.SubstituteWithSceneAtom != null) continue;
					var catalogSceneName = atomSceneNameToCatalogEntryMappings[i];
					CreateAtom(catalogSceneName, catalogAtom, Vector3.zero, Quaternion.identity);
				}

			}
			catch (Exception e)
			{
				SuperController.LogError(e.ToString());
				HidePopupMessage();
			}
		}

		//private void SelectOrCreateAtomForCatalogEntry(CatalogEntry catalogEntry)
		//{
		//	try
		//	{
		//		ShowPopupMessage("Please Wait...");

		//		var atomType = catalogEntry.Mutation.AtomType ?? "Cube";
		//		var selectedAtom = SuperController.singleton.GetSelectedAtom();

		//		var existingAtom = SuperController.singleton.GetAtomByUid(catalogEntry.Mutation.AtomName);
		//		if (existingAtom == null)
		//		{
		//			// Does not exist in scene...
		//			string newAtomName = GetNextAvailableName(catalogEntry.Mutation.AtomName, catalogEntry.Mutation.AtomType);
		//			CreateAtom(catalogEntry.Mutation.Storables, atomType, newAtomName, Vector3.zero, Quaternion.identity);
		//			HidePopupMessage();
		//			return;
		//		}
		//		if (selectedAtom == null || selectedAtom.name == catalogEntry.Mutation.AtomName)
		//		{
		//			// Already exists in scene, and is already selected, create clone...
		//			string newAtomName = GetNextAvailableName(catalogEntry.Mutation.AtomName, catalogEntry.Mutation.AtomType);
		//			CreateAtom(catalogEntry.Mutation.Storables, atomType, newAtomName, existingAtom.transform.position, existingAtom.transform.rotation);
		//			HidePopupMessage();
		//			return;
		//		}
		//		// Already exists in scene but is not selected. Select it.
		//		SuperController.singleton.SelectController(existingAtom.mainController);
		//		HidePopupMessage();
		//	}
		//	catch (Exception e)
		//	{
		//		SuperController.LogError(e.ToString());
		//		HidePopupMessage();
		//	}
		//}

		private void CreateAtom(string newAtomName, StoredAtom storedAtom, Vector3 position, Quaternion rotation)
		{
			this.StartCoroutine(this.CreateAtom(storedAtom.AtomType, newAtomName, position, rotation, newAtom =>
			{
				newAtom.collisionEnabledJSON.SetVal(false);
				_atomsIncubatingQueue.Remove(_atomsIncubatingQueue.First(a => a == storedAtom.AtomName)); // ...Remove this atom from the incubation queue (there might still be others though)
				this.StartCoroutine(RestoreAfterIncubation(storedAtom, newAtom, newAtomName));
			}));
		}

		private IEnumerator RestoreAfterIncubation(StoredAtom storedAtom, Atom newAtom, string newAtomName)
		{
			// Wait for ALL atoms to be incubated before restoring ANY of them...
			int overflow = 0;
			while (_atomsIncubatingQueue.Count() > 0)
			{
				if (overflow++ > 1000)
				{
					SuperController.LogError("Oveflow waiting for atoms to be incubated");
					break;
				}
				yield return new WaitForEndOfFrame();
			}

			if (storedAtom.FullAtom != null)
			{
				newAtom.Restore(storedAtom.FullAtom);
				RestoreAtomFromJSON(newAtom, storedAtom.FullAtom, newAtom.transform.position, newAtom.transform.rotation);
			}
			//else
			//{
			//	RestoreAtomStorables(storedAtom, newAtom);
			//}
			RestoreAtomStorables(storedAtom, newAtom);

			StartCoroutine(RestoreAppearance(newAtom, storedAtom));

			SuperController.singleton.SelectController(newAtom.mainController);
			HidePopupMessage();
			StartCoroutine(EnableCollisions(newAtom));
		}

		private void RestoreAtomStorables(StoredAtom storedAtom, Atom newAtom)
		{

			/// Restore Storables...
			foreach (var storable in storedAtom.StagedStorables)
			{
				try
				{
					var storeId = storable["id"];
					if (storeId == null) continue;
					var existingStorable = newAtom.GetStorableByID(storeId);

					if (existingStorable != null)
					{
						//existingStorable.PreRestore();
						existingStorable.RestoreFromJSON(storable, true, true, null);
						//existingStorable.LateRestoreFromJSON(storable);
						//existingStorable.PostRestore();
					}
					else
					{
						var newStorable = new JSONStorable();
						//newStorable.RestoreFromJSON(storable);
						newStorable.containingAtom = newAtom;
						newStorable.RestoreFromJSON(storable);
						var registered = newAtom.RegisterAdditionalStorable(newStorable);
					}

					//newAtom.Cl
				}
				catch (Exception e)
				{
					SuperController.LogError(e.ToString());
				}
			}

			//atom.RestoreFromJSON(atomJSON);
			//newAtom.PreRestore();
			//atom.RestoreTransform(atomJSON);
			//newAtom.Restore(atomJSON);
			//atom.LateRestore(atomJSON);
			//newAtom.PostRestore();

		}

		public IEnumerator RestoreAppearance(Atom newAtom, StoredAtom storedAtom)
		{
			yield return new WaitForFixedUpdate();
			try
			{
				if (newAtom.type == "Person")
				{
					//JSONStorable geometry = newAtom.GetStorableByID("geometry");
					//var atomGeometry = newAtom.GetStorableByID("geometry");
					//var storedStorables = storedAtom.FullAtom["storables"].AsArray;
					////var storedGeometry = storedStorables.Childs.First(c => c.AsObject["id"].ToString() == "geometry").AsObject;
					//foreach (var storable in storedStorables.Childs)
					//{
					//	if (storable.AsObject["id"].ToString() == "geometry")
					//	{
					//		var storedGeom = storable.AsObject;
					//		atomGeometry.RestoreFromJSON(storedGeom);
					//		atomGeometry.LateRestoreFromJSON(storedGeom);
					//	}
					//}

					JSONStorable geometry = newAtom.GetStorableByID("geometry");
					DAZCharacterSelector character = geometry as DAZCharacterSelector;
					var items = character.clothingItems;
					var storedGeometry = storedAtom.Storables.FirstOrDefault(s => s["id"] + "" == "geometry");

					var characterName = storedGeometry["character"].Value;
					if (characterName != null)
					{
						var characterSkin = character.characters.FirstOrDefault(c => c.displayName == characterName);
						if (characterSkin != null) character.selectedCharacter = characterSkin;//character.SelectCharacterByName(characterSkin.name);
					}

					var clothingItems = storedGeometry["clothing"].AsArray;
					if (clothingItems.Childs.Count() > 0) character.RemoveAllClothing();
					for (var i = 0; i < clothingItems.Count; i++)
					{
						string clothingItemId = clothingItems[i].AsObject["id"] + "";
						DAZClothingItem dazClothingItem = character.clothingItems.FirstOrDefault(h => h.displayName == clothingItemId || h.uid == clothingItemId);
						if (dazClothingItem != null) character.SetActiveClothingItem(dazClothingItem, true);
						if (dazClothingItem == null) SuperController.LogMessage("WARNING: Couldn't find clothing item: " + clothingItemId);
					}

					var hairItems = storedGeometry["hair"].AsArray;
					if (hairItems.Childs.Count() > 0) character.RemoveAllHair();
					for (var i = 0; i < hairItems.Count; i++)
					{
						string hairItemId = hairItems[i].AsObject["id"] + "";
						DAZHairGroup item = character.hairItems.FirstOrDefault((h => h.displayName == hairItemId || h.uid == hairItemId));
						character.SetActiveHairItem(item, true);
					}

					var presetFile = "";
					//-----------------------------------------------
					JSONStorable posePreset = newAtom.GetStorableByID("PosePresets");
					JSONStorableUrl presetPathJSON = posePreset.GetUrlJSONParam("presetBrowsePath");
					presetPathJSON.val = SuperController.singleton.NormalizePath(presetFile);
					posePreset.CallAction("LoadPreset");
					//-----------------------------------------------
					JSONStorable appearancePreset = newAtom.GetStorableByID("AppearancePresets");
					JSONStorableUrl appearancePresetPath = appearancePreset.GetUrlJSONParam("presetBrowsePath");
					appearancePresetPath.val = SuperController.singleton.NormalizePath(presetFile);
					appearancePreset.CallAction("LoadPreset");
					//-----------------------------------------------

				}
			}
			catch (Exception e)
			{
				SuperController.LogError(e.ToString());
			}
		}

		public void RestoreAtomFromJSON(Atom atom, JSONClass atomJSON, Vector3 position, Quaternion rotation, bool applyPresets = false)
		{
			atom.RestoreFromJSON(atomJSON);
			atom.PreRestore();
			atom.RestoreTransform(atomJSON);
			atom.Restore(atomJSON);
			atom.LateRestore(atomJSON);
			atom.PostRestore();

			//if (atom != null)
			//{
			//	JSONClass atomsJSON = SuperController.singleton.GetSaveJSON(atom);
			//	JSONArray atomsArrayJSON = atomsJSON["atoms"].AsArray;
			//	JSONClass atomJSON = (JSONClass)atomsArrayJSON[0];

			//atomJSON["position"]["x"].AsFloat = position.x;
			//atomJSON["position"]["y"].AsFloat = position.y;
			//atomJSON["position"]["z"].AsFloat = position.z;
			//atomJSON["containerPosition"]["x"].AsFloat = position.x;
			//atomJSON["containerPosition"]["y"].AsFloat = position.y;
			//atomJSON["containerPosition"]["z"].AsFloat = position.z;
			//atomJSON["rotation"]["x"].AsFloat = rotation.eulerAngles.x;
			//atomJSON["rotation"]["y"].AsFloat = rotation.eulerAngles.y;
			//atomJSON["rotation"]["z"].AsFloat = rotation.eulerAngles.z;
			//atomJSON["containerRotation"]["x"].AsFloat = rotation.eulerAngles.x;
			//atomJSON["containerRotation"]["y"].AsFloat = rotation.eulerAngles.y;
			//atomJSON["containerRotation"]["z"].AsFloat = rotation.eulerAngles.z;

			//Dictionary<string, int> storablesIndexDict = new Dictionary<string, int>();
			//JSONArray storablesArrayJSON = new JSONArray();
			//storables.ForEach(storablesArrayJSON.Add);
			//for (int i = 0; i < storablesArrayJSON.Count; i++)
			//{
			//	JSONClass storableJSON = (JSONClass)storablesArrayJSON[i];
			//	storablesIndexDict.Add(storableJSON["id"].Value, i);
			//	if (storableJSON["id"].Value == "control")
			//	{
			//		atomJSON["storables"][i]["position"]["x"].AsFloat = position.x;
			//		atomJSON["storables"][i]["position"]["y"].AsFloat = position.y;
			//		atomJSON["storables"][i]["position"]["z"].AsFloat = position.z;
			//	}
			//	if (storableJSON["id"].Value == "hip")
			//	{
			//		atomJSON["storables"][i]["rootPosition"]["x"].AsFloat = position.x;
			//		atomJSON["storables"][i]["rootPosition"]["y"].AsFloat = atomJSON["storables"][i]["rootPosition"]["y"].AsFloat + position.y;
			//		atomJSON["storables"][i]["rootPosition"]["z"].AsFloat = position.z;
			//	}
			//	if (storableJSON["id"].Value.StartsWith("hair"))
			//	{
			//		atomJSON["storables"][i]["position"]["x"].AsFloat = atomJSON["storables"][i]["position"]["x"].AsFloat + position.x;
			//		atomJSON["storables"][i]["position"]["y"].AsFloat = atomJSON["storables"][i]["position"]["y"].AsFloat + position.y;
			//		atomJSON["storables"][i]["position"]["z"].AsFloat = atomJSON["storables"][i]["position"]["z"].AsFloat + position.z;
			//	}

			//	if (storableJSON["id"].Value == "control" || storableJSON["id"].Value == "hip" || storableJSON["id"].Value.StartsWith("hair"))
			//	{
			//		string rotationKeyName = "";
			//		if (storableJSON["id"].Value == "hip") rotationKeyName = "rootRotation";
			//		else rotationKeyName = "rotation";
			//		atomJSON["storables"][i][rotationKeyName]["x"].AsFloat = rotation.eulerAngles.x;
			//		atomJSON["storables"][i][rotationKeyName]["y"].AsFloat = rotation.eulerAngles.y;
			//		atomJSON["storables"][i][rotationKeyName]["z"].AsFloat = rotation.eulerAngles.z;
			//	}
			//}
			//if (applyPresets)
			//{
			//	if (FileManagerSecure.FileExists(_selectedPreset1File))
			//	{
			//		JSONNode presetJSON = _mvrScript.LoadJSON(_selectedPreset1File);
			//		JSONArray presetStorablesJSON = presetJSON["storables"]?.AsArray;
			//		if (presetStorablesJSON != null)
			//		{
			//			for (int i = 0; i < presetStorablesJSON.Count; i++)
			//			{
			//				if (storablesIndexDict.ContainsKey(presetStorablesJSON[i]["id"]))
			//				{
			//					atomJSON["storables"][storablesIndexDict[presetStorablesJSON[i]["id"]]] = presetStorablesJSON[i];
			//				}
			//				else
			//				{
			//					atomJSON["storables"].Add(presetStorablesJSON[i]);
			//				}
			//			}
			//		}
			//	}
			//	if (FileManagerSecure.FileExists(_selectedPreset3File))
			//	{
			//		JSONNode presetJSON = _mvrScript.LoadJSON(_selectedPreset3File);
			//		JSONArray presetStorablesJSON = presetJSON["storables"]?.AsArray;
			//		if (presetStorablesJSON != null)
			//		{
			//			for (int i = 0; i < presetStorablesJSON.Count; i++)
			//			{
			//				if (storablesIndexDict.ContainsKey(presetStorablesJSON[i]["id"]))
			//				{
			//					atomJSON["storables"][storablesIndexDict[presetStorablesJSON[i]["id"]]] = presetStorablesJSON[i];
			//				}
			//				else
			//				{
			//					atomJSON["storables"].Add(presetStorablesJSON[i]);
			//				}
			//			}
			//		}
			//	}
			//}

			//}

		}

		private IEnumerator RestoreFromJson(JSONStorable storable, JSONClass stored)
		{
			yield return new WaitForEndOfFrame();
			storable.RestoreFromJSON(stored);
		}

		IEnumerator EnableCollisions(Atom atom)
		{
			yield return new WaitForSeconds(2);
			atom.collisionEnabledJSON.SetVal(true);
		}

		//private void CreateAtom(List<JSONClass> storables, string atomType, string atomName, Vector3 position, Quaternion rotation)
		//{
		//	this.StartCoroutine(this.CreateAtom(atomType, atomName, position, rotation, newAtom =>
		//	{
		//		try
		//		{
		//			newAtom.collisionEnabledJSON.SetVal(true);
		//			/// Restore Storables...
		//			foreach (var storable in storables)
		//			{
		//				try
		//				{
		//					var storeId = storable["id"]; //SerializerService_3_0_1.LoadStringFromJsonStringProperty(storable, "id", null);
		//					if (storeId == null) continue;
		//					var existingStorable = newAtom.GetStorableByID(storeId);
		//					if (existingStorable != null)
		//					{
		//						existingStorable.RestoreFromJSON(storable);
		//					}
		//					else
		//					{
		//						var unregisteredStorable = GetNewStorableFromJSONClass(storable);
		//						unregisteredStorable.containingAtom = newAtom;
		//						newAtom.RegisterAdditionalStorable(unregisteredStorable);
		//					}
		//				}
		//				catch (Exception e)
		//				{
		//					SuperController.LogError(e.ToString());
		//				}
		//			}
		//			SuperController.singleton.SelectController(newAtom.mainController);
		//			HidePopupMessage();
		//		}
		//		catch (Exception e)
		//		{
		//			SuperController.LogError(e.ToString());
		//		}
		//	}));
		//}

		public JSONStorable GetNewStorableFromJSONClass(JSONClass storable)
		{
			var newStorable = new JSONStorable();
			newStorable.RestoreFromJSON(storable);
			return newStorable;
		}

		//public void OverrideStorableWithJSON(JSONStorable existingStorable, JSONClass newStorable)
		//{
		//	if (existingStorable == null || newStorable == null) return;

		//	var boolParamNames = existingStorable.GetBoolParamNames();
		//	if (boolParamNames != null)
		//	{
		//		foreach (var paramName in boolParamNames)
		//		{
		//			var paramValue = SerializerService_3_0_1.LoadBoolFromJsonStringProperty(newStorable, paramName, null);
		//			if (paramValue == null) continue;
		//			existingStorable.SetBoolParamValue(paramName, paramValue ?? false);
		//		}
		//	}
		//	var colorParamNames = existingStorable.GetColorParamNames();
		//	if (colorParamNames != null)
		//	{
		//		foreach (var paramName in colorParamNames)
		//		{
		//			var paramValue = SerializerService_3_0_1.LoadObjectFromJsonObjectProperty(newStorable, paramName, null);
		//			if (paramValue == null) continue;
		//			existingStorable.SetColorParamValue(paramName, SerializerService_3_0_1.GetColorFromJsonClass(paramValue));
		//		}
		//	}
		//	var floatParamNames = existingStorable.GetFloatParamNames();
		//	if (floatParamNames != null)
		//	{
		//		foreach (var paramName in floatParamNames)
		//		{
		//			var paramValue = SerializerService_3_0_1.LoadFloatFromJsonFloatProperty(newStorable, paramName, null);
		//			if (paramValue == null) continue;
		//			existingStorable.SetFloatParamValue(paramName, paramValue ?? 0);
		//		}
		//	}
		//	var newUrlParamNames = existingStorable.GetUrlParamNames();
		//	if (newUrlParamNames != null)
		//	{
		//		foreach (var paramName in newUrlParamNames)
		//		{
		//			var paramValue = SerializerService_3_0_1.LoadStringFromJsonStringProperty(newStorable, paramName, null);
		//			if (paramValue == null) continue;
		//			existingStorable.SetUrlParamValue(paramName, paramValue);
		//		}
		//	}
		//	var newStringParamNames = existingStorable.GetStringParamNames();
		//	if (newStringParamNames != null)
		//	{
		//		foreach (var paramName in newStringParamNames)
		//		{
		//			var paramValue = SerializerService_3_0_1.LoadStringFromJsonStringProperty(newStorable, paramName, null);
		//			if (paramValue == null) continue;
		//			existingStorable.SetStringParamValue(paramName, paramValue);
		//		}
		//	}
		//	var vector3ParamNames = existingStorable.GetVector3ParamNames();
		//	if (existingStorable.storeId == "control") vector3ParamNames = vector3ParamNames.Concat(new List<string>() { "position", "rotation" }).ToList();
		//	if (vector3ParamNames != null)
		//	{
		//		foreach (var paramName in vector3ParamNames)
		//		{
		//			var paramValue = SerializerService_3_0_1.LoadObjectFromJsonObjectProperty(newStorable, paramName, null);
		//			if (paramValue == null) continue;
		//			existingStorable.SetVector3ParamValue(paramName, SerializerService_3_0_1.GetVector3FromJsonClass(paramValue));
		//		}
		//	}
		//	var newStringChooserParamNames = existingStorable.GetStringChooserParamNames();
		//	if (newStringChooserParamNames != null)
		//	{
		//		foreach (var paramName in newStringChooserParamNames)
		//		{
		//			var paramValue = SerializerService_3_0_1.LoadStringFromJsonStringProperty(newStorable, paramName, null);
		//			if (paramValue == null) continue;
		//			existingStorable.SetStringChooserParamValue(paramName, paramValue);
		//		}
		//	}
		//}

		public void OverrideStorableWithStorable(JSONStorable existingStorable, JSONStorable newStorable)
		{
			var newBoolParamNames = newStorable.GetBoolParamNames();
			foreach (var paramName in newBoolParamNames)
			{
				existingStorable.SetBoolParamValue(paramName, newStorable.GetBoolParamValue(paramName));
			}

			var newColorParamNames = newStorable.GetColorParamNames();
			foreach (var paramName in newColorParamNames)
			{
				existingStorable.SetColorParamValue(paramName, newStorable.GetColorParamValue(paramName));
			}

			var newFloatParamNames = newStorable.GetFloatParamNames();
			foreach (var paramName in newFloatParamNames)
			{
				existingStorable.SetFloatParamValue(paramName, newStorable.GetFloatParamValue(paramName));
			}

			var newStringChooserParamNames = newStorable.GetStringChooserParamNames();
			foreach (var paramName in newStringChooserParamNames)
			{
				existingStorable.SetStringChooserParamValue(paramName, newStorable.GetStringChooserParamValue(paramName));
			}

			var newStringParamNames = newStorable.GetStringParamNames();
			foreach (var paramName in newStringParamNames)
			{
				existingStorable.SetStringParamValue(paramName, newStorable.GetStringParamValue(paramName));
			}

			var newUrlParamNames = newStorable.GetUrlParamNames();
			foreach (var paramName in newUrlParamNames)
			{
				existingStorable.SetUrlParamValue(paramName, newStorable.GetUrlParamValue(paramName));
			}

			var newVector3ParamNames = newStorable.GetVector3ParamNames();
			foreach (var paramName in newVector3ParamNames)
			{
				existingStorable.SetVector3ParamValue(paramName, newStorable.GetVector3ParamValue(paramName));
			}

		}

		//private void CreateCustomUnityAsset(CatalogEntry catalogEntry, string catalogName)
		//{
		//	try
		//	{
		//		var existingAsset = SuperController.singleton.GetAtomByUid(catalogEntry.Mutation.AtomName);
		//		var selectedAtom = SuperController.singleton.GetSelectedAtom();
		//		// Create new atom...
		//		if (existingAsset == null || selectedAtom == null || selectedAtom.name == catalogEntry.Mutation.AtomName)
		//		{
		//			Vector3 position = (existingAsset != null) ? existingAsset.transform.position : Vector3.zero;
		//			Quaternion rotation = (existingAsset != null) ? existingAsset.transform.rotation : Quaternion.identity;
		//			string newAtomName = GetNextAvailableName(catalogEntry.Mutation.AtomName, catalogEntry.Mutation.AtomType);
		//			this.StartCoroutine(this.CreateAtom("CustomUnityAsset", newAtomName, position, rotation, newAtom =>
		//			{
		//				newAtom.GetStorableByID("asset").SetStringParamValue("assetUrl", catalogEntry.Mutation.AssetUrl);
		//				newAtom.GetStorableByID("asset").SetStringParamValue("assetName", catalogEntry.Mutation.AssetName);
		//				CustomUnityAssetLoader customAsset = newAtom.GetStorableByID("asset") as CustomUnityAssetLoader;
		//				customAsset.SetUrlParamValue("assetUrl", catalogEntry.Mutation.AssetUrl);
		//				customAsset.SetStringChooserParamValue("assetName", catalogEntry.Mutation.AssetName);
		//				newAtom.collisionEnabledJSON.SetVal(true);
		//				SuperController.singleton.SelectController(newAtom.mainController);
		//			}));
		//			return;
		//		}
		//		// Select existing Atom...
		//		SuperController.singleton.SelectController(existingAsset.mainController);
		//	}
		//	catch (Exception e)
		//	{
		//		SuperController.LogError(e.ToString());
		//	}
		//}

		public string GetNextAvailableName(string atomName, string atomType = null, Dictionary<int, string> reservedNames = null)
		{
			if (reservedNames == null) reservedNames = new Dictionary<int, string>();
			var suggestedName = atomName ?? atomType ?? GetUniqueName();
			var sceneAtoms = SuperController.singleton.GetAtoms();
			string newAtomName = suggestedName;
			int instanceCount = 2;
			while (sceneAtoms.Any(a => a.name == newAtomName) || reservedNames.Values.Any(rn => rn == newAtomName))
			{
				if (++instanceCount > 10000) throw new Exception("Overflow");
				if (suggestedName.Contains("#"))
					newAtomName = suggestedName.Substring(0, suggestedName.IndexOf("#")) + "#" + instanceCount;
				else
					newAtomName = suggestedName + "#" + instanceCount;
			}
			return newAtomName;
		}

		private void CenterPivot(Atom atom)
		{
			SuperController.singleton.SelectController(atom.mainController);

			var allJointsController = atom.GetComponentInChildren<AllJointsController>();
			/// Detach control of joints...
			allJointsController.SetAllJointsControlOff();
			/// start Move-pivot routine...
			StartCoroutine(MoveControllerAndThenTurnOnJoints(atom));
		}

		IEnumerator MoveControllerAndThenTurnOnJoints(Atom atom)
		{
			/// Reset pivot rotation...
			var mainController = atom.mainController;
			mainController.transform.rotation = Quaternion.identity;

			var hipController = GetControllerForAtom("hipControl", atom);// atom.GetComponentsInChildren<FreeControllerV3>().FirstOrDefault(c => c.name == "hipControl");

			if (hipController != null)
			{
				mainController.transform.position = hipController.transform.position;
			}
			/// Perform...
			yield return new WaitForFixedUpdate();
			/// Resume control of joints
			var allJointsController = atom.GetComponentInChildren<AllJointsController>();
			allJointsController.SetAllJointsControlPositionAndRotation();
		}

		public string GetPluginPath()
		{
			if (containingAtom.type == ATOM_TYPE_SESSION) return "Custom/Scripts/" + this.pluginName;
			string pluginId = storeId.Split('_')[0];
			MVRPluginManager manager = containingAtom.GetStorableByID("PluginManager") as MVRPluginManager;
			string pathToScriptFile = manager.GetJSON(true, true)["plugins"][pluginId].Value;
			string pathToScriptFolder = pathToScriptFile.Substring(0, pathToScriptFile.LastIndexOfAny(new char[] { '/', '\\' }));
			return pathToScriptFolder;
		}

		public void ApplyEntryMoveNext()
		{
			if (_catalog.Entries.Count == 0) return;
			var selectedEntry = _catalog.Entries.FirstOrDefault(e => e.Selected) ?? _catalog.Entries.FirstOrDefault();
			if (selectedEntry == null) selectedEntry = _catalog.Entries.First();
			var indexOfCurrentEntry = _catalog.Entries.IndexOf(selectedEntry);
			CatalogEntry nextEntry;
			if (indexOfCurrentEntry == _catalog.Entries.IndexOf(_catalog.Entries.Last()))
			{
				nextEntry = _catalog.Entries.First();
			}
			else
			{
				nextEntry = _catalog.Entries.ElementAt(indexOfCurrentEntry + 1);
			}
			if (_playOnce.val == true && _numberOfEntriesToPlay-- == 0)
			{
				_cycleEntriesOnInterval.val = false;
				return;
			}
			ApplyMasterCatalogEntry(selectedEntry);
			SelectCatalogEntry(nextEntry);
		}

		public void ApplyRandomEntry()
		{
			if (_catalog.Entries.Count == 0) return;
			var randomIndex = UnityEngine.Random.Range(0, _catalog.Entries.Count - 1);
			var catalogEntry = _catalog.Entries.ElementAt(randomIndex);
			ApplyMasterCatalogEntry(catalogEntry);
			SelectCatalogEntry(catalogEntry);
		}

		public void ApplyEntryAt(int entryIndex)
		{
			if (entryIndex >= _catalog.Entries.Count) return;
			var catalogEntry = _catalog.Entries.ElementAt(entryIndex);
			ApplyMasterCatalogEntry(catalogEntry);
			SelectCatalogEntry(catalogEntry);
		}

		IEnumerator ApplyNextEntryAfterInterval()
		{
			while (_cycleEntriesOnInterval.val)
			{
				switch (_entrySelectionMethod.val)
				{
					case SELECTION_METHOD_RANDOM:
						ApplyRandomEntry();
						break;
					case SELECTION_METHOD_SEQUENCE:
						ApplyEntryMoveNext();
						break;
				}
				yield return new WaitForSeconds(_cycleEntriesInterval.val);
			}
		}

		public FreeControllerV3 GetControllerOrDefault(string atomName, string controllerName, bool withValidation = true)
		{
			if (withValidation && atomName == null) SuperController.LogMessage("WARNING: No atom name provided");
			if (withValidation && atomName == null) SuperController.LogMessage("WARNING: No controller name provided");
			if (atomName == null || controllerName == null) return null;
			var atom = SuperController.singleton.GetAtomByUid(atomName);
			if (withValidation && atom == null) SuperController.LogMessage("WARNING: No atom found by the name of '" + atomName + "'");
			if (atom == null) return null;
			return GetControllerOrDefault(atom, controllerName, withValidation);
		}

		public FreeControllerV3 GetControllerOrDefault(Atom atom, string controllerName, bool withValidation = true)
		{
			var controller = GetControllerForAtom(controllerName, atom);
			if (withValidation && controller == null) SuperController.LogMessage("WARNING: No controller found by the name of '" + controllerName + "'");
			return controller;
		}

		//public void UpdateActiveClothing(Atom atom)
		//{
		//	_activeHAClothingItems = new List<string>();
		//	_activeClothingTags = new Dictionary<string, List<string>>();

		//	foreach (Transform child in atom.gameObject.transform.Find("rescale2/geometry/FemaleClothes"))
		//	{
		//		if (child.gameObject.name == "FemaleClothingPrefab(Clone)")
		//		{
		//			foreach (Transform subchild in atom.gameObject.transform.Find("rescale2/geometry/FemaleClothes/FemaleClothingPrefab(Clone)"))
		//			{
		//				DAZClothingItem dci = subchild.GetComponent<DAZClothingItem>();
		//				if (dci.isActiveAndEnabled)
		//				{
		//					string[] tags = dci.tagsArray;
		//					if (tags.Length > 0)
		//					{
		//						List<string> newTagList = tags.ToList();
		//						_activeClothingTags.Add(dci.uid, newTagList);
		//					}

		//					string haClothingItemName = StripNonAlphaNum(dci.creatorName) + "-" + StripNonAlphaNum(dci.uid);
		//					_activeHAClothingItems.Add(haClothingItemName);
		//				}
		//			}
		//		}
		//		else
		//		{
		//			if (child.GetComponent<DAZClothingItem>().isActiveAndEnabled)
		//			{
		//				DAZClothingItem dci = child.GetComponent<DAZClothingItem>();
		//				if (dci.isActiveAndEnabled)
		//				{
		//					string[] tags = dci.tagsArray;
		//					if (tags.Length > 0)
		//					{
		//						List<string> newTagList = tags.ToList();
		//						_activeClothingTags.Add(dci.uid, newTagList);
		//					}
		//					string haClothingItemName = StripNonAlphaNum(dci.creatorName) + "-" + StripNonAlphaNum(GetHAItemNameFromCustomClothingUID(dci.uid));
		//					_activeHAClothingItems.Add(haClothingItemName);
		//				}
		//			}
		//		}
		//	}
		//}

		void OnDestroy()
		{
			try
			{

				if (_handleObjectForCatalog != null)
				{
					_handleObjectForCatalog.Remove();
				}
				if (_catalogUi != null)
				{
					_catalogUi.OnDestroy();
				}
				if (_floatingControlsUi != null)
				{
					_floatingControlsUi.OnDestroy();
				}
				if (_windowUi != null)
				{
					_windowUi.OnDestroy();
				}
			}
			catch (Exception e)
			{
				SuperController.LogError(e.ToString());
			}

		}

		private void RemoveFile(string filePath)
		{
			if (!FileManagerSecure.FileExists(filePath)) return;
			FileManagerSecure.DeleteFile(filePath);
		}

		private void RenameFile(string fromFilePath, string toFilePath)
		{
			if (string.IsNullOrEmpty(fromFilePath) || string.IsNullOrEmpty(toFilePath)) return;
			if (!FileManagerSecure.FileExists(fromFilePath)) return;
			FileManagerSecure.CopyFile(fromFilePath, toFilePath);
			FileManagerSecure.DeleteFile(fromFilePath);
		}

	}
}
