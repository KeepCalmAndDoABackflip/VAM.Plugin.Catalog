
using juniperD.Models;
using PluginBuilder.Contracts;
using PluginBuilder.Services;
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

namespace juniperD.StatefullServices
{

	public class CatalogPlugin : MVRScript
	{
		const string EXPAND_WITH_MORE_ROWS = "rows";
		const string EXPAND_WITH_MORE_COLUMNS = "columns";

		const string ATOM_TYPE_SESSION = "CoreControl";
		const string ATOM_TYPE_PERSON = "Person";
		const string ATOM_TYPE_OBJECT = "Other";
		string _atomType;

		string _fileExtension = "catalog";
		List<string> _catalogModes = new List<string>();

		#region PluginInfo
		public string pluginAuthor = "juniperD";
		public string pluginName = "Catalog";
		public string pluginVersion = Global.HOST_PLUGIN_SYMANTIC_VERSION;
		public string pluginDate = "10/04/2020";
		public string pluginDescription = @"Create a catalog of the current scene";
		#endregion

		// Catalogger settings...
		protected JSONStorableFloat _waitFramesBetweenCaptureJSON;
		protected JSONStorableBool _anchorOnHud;
		protected JSONStorableStringChooser _anchorOnAtom;
		protected JSONStorableBool _minimizeUi;

		protected Catalog _catalog = new Catalog();
		// Config...
		//protected float defaultNumberOfCatalogRows = 2;
		protected float defaultNumberOfCatalogColumns = 4;
		protected float defaultNumberOfCatalogRows = 1;
		protected float defaultNumberOfCatalogEntries = 4;
		protected float defaultFramesBetweenCaptures = 50;
		protected JSONStorableBool _firstEntryIsCurrentLook;
		protected Transform characterAnchorTransform;
		protected int catalogRows = 2;
		protected int defaultFrameSize = 250;
		protected int defaultBorderWidth = 10;
		protected Color defaultBorderColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
		protected int relativeBorderWidth = 10;
		protected bool _defaultMinimize = false;
		protected bool _vrMode = XRDevice.isPresent;

		// General state
		string _lastCatalogDirectory;

		// Screen-Capture control-state...
		protected Camera _captureCamera;
		protected int _captureWidth = 1000;
		protected int _captureHeight = 1000;
		protected bool _hudWasVisible;
		protected string _baseFileFormat;
		protected RenderTexture _originalCameraTexture;
		protected int _createCatalogEntry_Step = 0;
		protected int _skipFrames = 0;
		//protected UIDynamicButton _generateButton;
		//protected UIDynamicButton _captureButton;
		protected UIDynamicButton _floatingTriggerButton;
		protected UIDynamicButton _toolTipLabel;
		protected UIDynamicButton _modeLabel;
		protected UIDynamicButton _catalogNameLabel;
		protected UIDynamicButton _popupMessageLabel;
		//protected int _popupMessageFramesLeft = -1;
		protected UIDynamicButton _minimizeButton;
		protected UIDynamicButton _moveButton;
		protected Vector3 _floatingControlUiPosition;
		protected Vector3 _rightCatalogUiPosition;
		protected Mutation _nextMutation;
		protected static string _generateButtonInitText = "Generate Faces";
		public CaptureRequest _currentCaptureRequest = null;
		public List<CaptureRequest> _currentCaptureRequestList = new List<CaptureRequest>();
		protected Action _onCatalogDragFinishedEvent;
		Dictionary<string, DragHelper> _positionTrackers = new Dictionary<string, DragHelper>();

		// Capture-mode control-state...
		protected JSONStorableBool _catalogCaptureHair;
		protected JSONStorableBool _catalogCaptureClothes;
		protected JSONStorableBool _catalogCaptureMorphs;
		protected JSONStorableBool _catalogCaptureDynamicItems;

		// Controls-state...
		protected JSONStorableBool _uiVisible;
		protected JSONStorableStringChooser _expandDirection;
		protected JSONStorableBool _triggerButtonVisible;
		protected JSONStorableBool _alwaysFaceMe;
		protected JSONStorableFloat _catalogRowsCountJSON;
		protected JSONStorableFloat _catalogColumnsCountJSON;
		protected JSONStorableFloat _catalogTransparencyJSON;
		protected JSONStorableFloat _catalogEntryFrameSize;
		protected JSONStorableFloat _catalogRequestedEntriesCountJSON;
		protected JSONStorableStringChooser _activeCameraName;
		protected UIDynamicPopup _cameraSelector;

		// Application state

		// Display-state...
		int _windowWidth = 400;
		int _windowHeight = 250;
		Atom _parentAtom;
		DebugService _debugService;
		public MutationsService _mutationsService;
		CatalogUiHelper _catalogUi;
		CatalogUiHelper _floatingControlsUi;
		public GameObject _windowContainer;
		public GameObject _backPanel;

		// Confirmation dialog...
		public GameObject _dynamicConfirmPanel;
		protected UIDynamicTextField _confirmLabel;
		Action _lastConfirmAction;

		// Popup select list
		public GameObject _dynamicSelectList;
		public List<UiCatalogSubItem> _dynamicListItems = new List<UiCatalogSubItem>();

		// Info Panel...
		public GameObject _dynamicInfoPanel;
		protected UIDynamicTextField _infoLabel;
		protected List<UIDynamicToggle> _infoCheckLabels;
		VerticalLayoutGroup _infoVLayout;
		VerticalLayoutGroup _selectListVLayout;

		GameObject _rowContainer;
		GameObject _columnContainer;
		UIDynamicButton _dynamicButtonSort;
		UIDynamicButton _dynamicButtonRefresh;
		UIDynamicButton _dynamicButtonAddAtom;
		UIDynamicButton _dynamicButtonCaptureMorphs;
		UIDynamicButton _dynamicButtonCaptureClothes;
		UIDynamicButton _dynamicButtonCaptureHair;
		Dictionary<string, UIDynamicButton> _modeAndDynamicModeButton = new Dictionary<string, UIDynamicButton>();
		Sprite iconForCapturePerson;
		Sprite iconForCaptureFaceGen;
		Sprite iconForCaptureScenes;
		Sprite iconForCaptureObject;
		Sprite iconForCaptureSelectedObject;
		Sprite iconForCaptureNone;

		VerticalLayoutGroup _catalogRowsVLayout;
		HorizontalLayoutGroup _catalogColumnsHLayout;

		List<GameObject> _catalogRows = new List<GameObject>();
		List<GameObject> _catalogColumns = new List<GameObject>();
		CatalogEntry _currentCatalogEntry;

		JSONStorableStringChooser _catalogMode;
		JSONStorableString _catalogName;
		JSONStorableString _catalogRelativePath;
		JSONStorableFloat _catalogPositionX;
		JSONStorableFloat _catalogPositionY;
		Atom _handleObjectForCatalog;

		protected Color _dynamicButtonCheckColor; //= Color.red;
		protected Color _dynamicButtonUnCheckColor; //= Color.green;

		private int _nextAtomIndex;

		public override void PostRestore()
		{
			base.PostRestore();

			if (_catalogName.val == "") // ...This catalog has not yet been initialized
			{
				FirstTimeInitialization();
			}
			else // ...This catalog has already been initialized, this is loaded from file...
			{
				if (containingAtom.type == "CoreControl") return; //...don't restore session plugins
																													// The catalog temp file should already exist, restore the temporary catalog data
				var scenePath = GetSceneDirectoryPath();
				var filePath = scenePath + "/" + _catalogName.val + "." + _fileExtension;
				_catalog = LoadCatalogFromFile(filePath);
				for (var i = 0; i < _catalog.Entries.Count(); i++)
				{
					if (_catalog.Entries[i].Mutation.IsActive)
					{
						var mutation = _catalog.Entries[i].Mutation;
						_mutationsService.ApplyMutation(ref mutation);
						_currentCatalogEntry = _catalog.Entries[i];
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
		private void FirstTimeInitialization()
		{
			// ...set the catalog name, with no callback (to prevent recursing back into this function)
			_catalog = new Catalog();
			_catalogName.valNoCallback = GetNewCatalogName();
			_catalogMode.val = Enums.CATALOG_MODE_VIEW;
			// Create a new file in which to save temporary catalog data for this catalog instance...
			var scenePath = GetSceneDirectoryPath();
			_lastCatalogDirectory = scenePath + "/SavedCatalogs"; //scenePath;
			SuperController.LogMessage(_lastCatalogDirectory);
			var filePath = scenePath + "/" + _catalogName.val + "." + _fileExtension;
			//SaveCatalogToFile(filePath);
			// Set starting position next to HUD...
			//ResetCatalogPositionAtPerson();
			//AnchorOnAtom();
			ResetCatalogPositionAtHud();
			AnchorOnHud();

			UpdateCaptureClothesState(true);
			UpdateCaptureHairState(false);
			UpdateCaptureMorphsState(false);
			if (_vrMode)
			{
				_captureCamera = Camera.allCameras.ToList().FirstOrDefault(c => c.name == "Camera (eye)");
			}

			UpdateUiForMode();
		}

		private string GetNewCatalogName()
		{
			// Create a unique name for this catalog instance (there may be multiple instances within a scene if the user adds more catalogs)
			const string searchFor = "plugin#";
			string lastHalf = this.name.Substring(this.name.IndexOf(searchFor) + searchFor.Length);
			string numberStr = lastHalf.Substring(0, lastHalf.IndexOf("_"));
			return containingAtom.name + "." + numberStr;
		}

		private void ResetCatalogPositionAtPerson()
		{
			var anchor = GetPersonBasePosition();
			var baseX = (anchor == null) ? 0f : anchor.localPosition.x;
			var baseY = (anchor == null) ? 0f : anchor.localPosition.y;
			_catalogPositionX.val = baseX + -0.5f;
			_catalogPositionY.val = baseY + 0;
		}

		public override void Init()
		{
			try
			{
				switch (containingAtom.type)
				{
					case "CoreControl":
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
				_catalogModes.Add(Enums.CATALOG_MODE_VIEW);
				_catalogModes.Add(Enums.CATALOG_MODE_SCENE);
				if (_atomType == ATOM_TYPE_PERSON) _catalogModes.Add(Enums.CATALOG_MODE_CAPTURE);
				if (_atomType == ATOM_TYPE_PERSON) _catalogModes.Add(Enums.CATALOG_MODE_MUTATIONS);
				if (_atomType == ATOM_TYPE_OBJECT) _catalogModes.Add(Enums.CATALOG_MODE_OBJECT);
				if (_atomType == ATOM_TYPE_SESSION) _catalogModes.Add(Enums.CATALOG_MODE_SESSION);

				_debugService = new DebugService();
				_debugService.Init(this);

				_dynamicButtonCheckColor = new Color(0.7f, 0.7f, 0.5f, 1);
				_dynamicButtonUnCheckColor = new Color(0.25f, 0.25f, 0.25f, 1);

				_currentCaptureRequest = new CaptureRequest();

				SuperController sc = SuperController.singleton;
				_baseFileFormat = SuperController.singleton.fileBrowserUI.fileFormat;

				SuperController.singleton.fileBrowserUI.selectButton.onClick.AddListener(() =>
				{
					var scenePath = GetSceneDirectoryPath();
					var filePath = scenePath + "/" + _catalogName.val + "." + _fileExtension;
					SaveCatalogToFile(filePath);
				});

				pluginLabelJSON.setJSONCallbackFunction = (newVal) =>
				{
					_catalogName.SetVal(newVal.val);
				};

				// Catalog options...
				CreateCatalogConfigUi();

				CreateSpacer();

				// Capture settings...
				CreateCaptureConfigUi();

				CreateSpacer();

				// Mutations config UI...
				CreateMutationsConfigUi();

				//if (containingAtom.mainController == null)
				//{
				//	SuperController.LogError("Please add this plugin to a PERSON atom.");
				//	return;
				//}

				//generate UI
				_catalogPositionX = new JSONStorableFloat("catalogPositionX", 0, 0, 0, false);
				_catalogPositionX.storeType = JSONStorableFloat.StoreType.Full;
				_catalogPositionX.isStorable = true;
				_catalogPositionX.isRestorable = true;
				_catalogPositionX.restoreTime = JSONStorableParam.RestoreTime.Normal;
				RegisterFloat(_catalogPositionX);

				_catalogPositionY = new JSONStorableFloat("catalogPositionY", 0, 0, 0, false);
				_catalogPositionY.storeType = JSONStorableFloat.StoreType.Full;
				_catalogPositionY.isStorable = true;
				_catalogPositionY.isRestorable = true;
				_catalogPositionY.restoreTime = JSONStorableParam.RestoreTime.Normal;
				RegisterFloat(_catalogPositionY);

				_floatingControlsUi = new CatalogUiHelper(this, 0, 0, Color.clear);
				_floatingControlsUi.canvas.transform.Rotate(new Vector3(0, 180, 0));
				_catalogUi = new CatalogUiHelper(this, 0, 0, Color.clear);
				_catalogUi.canvas.transform.Rotate(new Vector3(0, 180, 0));

				_windowContainer = CatalogUiHelper.CreatePanel(_catalogUi.canvas.gameObject, 0, 0, 0, 0, Color.clear, Color.clear);

				_rowContainer = _catalogUi.CreateUIPanel(_windowContainer, 0, 0, "topleft", 1, 1, Color.clear);
				_columnContainer = _catalogUi.CreateUIPanel(_windowContainer, 0, 0, "topleft", 1, 1, Color.clear);

				_catalogRowsVLayout = _catalogUi.CreateVerticalLayout(_rowContainer.gameObject, relativeBorderWidth);
				_catalogColumnsHLayout = _catalogUi.CreateHorizontalLayout(_columnContainer.gameObject, relativeBorderWidth);

				//_catalogRowsVLayout.transform.position = new Vector3(0.0f, -10.1f, 0.0f);
				//_catalogColumnsHLayout.transform.position = new Vector3(0.0f, -10.1f, 0.0f);

				CreateDynamicUi();

				if (!SuperController.singleton.isLoading)
				{
					// ...this indicates that the Init was not called as part of the scene being loaded, which means it must be just after the plugin was added.
					FirstTimeInitialization();
				}

				//SetMinimizedState();
			}
			catch (Exception e)
			{
				SuperController.LogError(e.ToString());
			}
		}

		public void Start()
		{
			_mutationsService.SetMorphBaseValuesCheckpoint();
		}

		private void CreateDynamicUi()
		{
			CreateDynamicPanel_BackPanel();
			//CreateDynamicButton_GenerateCatalog();
			//CreateDynamicButton_CaptureControl();
			CreateDynamicButton_Trigger();

			// DYNAMIC UI...
			//Top Right Corner
			//CreateDynamicButton_Minimize();
			//CreateDynamicButton_Move();
			// Left bar
			CreateDynamicButton_ResetCatalog();
			CreateDynamicButton_ZoomIn();
			CreateDynamicButton_ZoomOut();
			CreateDynamicButton_ScrollUp();
			CreateDynamicButton_ScrollDown();
			// Scene helpers...
			CreateDynamicButton_CycleAtoms();
			CreateDynamicButton_ResetPivot();
			// Top bar
			//CreateDynamicButton_Mode();
			CreateDynamicButton_Modes();
			CreateDynamicButton_Save();
			CreateDynamicButton_Load();
			CreateDynamicButton_Sort();
			CreateDynamicButton_Refresh();
			CreateDynamicButton_CaptureAdditionalAtom();
			// Top Bar (Capture mode specific)
			CreateDynamicButton_Capture_Clothes();
			CreateDynamicButton_Capture_Morphs();
			CreateDynamicButton_Capture_Hair();
			//CreateDynamicButton_Reset();
			// Message UI...
			//CreateDynamicButton_Label();
			CreateDynamicPanel_RightInfo();
			CreateDynamicPanel_LeftInfo();

			CreateDynamicButton_SideLabel();
			CreateDynamicButton_Tooltip();
			CreateDynamicButton_SelectList();
			CreateDynamicButton_QuickLoad();
			CreateDynamicButton_PopupMessage();
			CreateDynamicButton_DynamicConfirm();
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

			_catalogCaptureDynamicItems = new JSONStorableBool("Capture dynamic items", false);
			RegisterBool(_catalogCaptureDynamicItems);
			CreateToggle(_catalogCaptureDynamicItems);
			_catalogCaptureDynamicItems.toggle.onValueChanged.AddListener((value) =>
			{
				_mutationsService.CaptureDynamicItems = value;
			});

			CreateButton("Set base active morphs").button.onClick.AddListener(() =>
			{
				_mutationsService.SetMorphBaseValuesCheckpoint();
			});

			CreateButton("Remove unused items").button.onClick.AddListener(() =>
			{
				RemoveUnusedMutations();
			});

			CreateButton("Select custom image").button.onClick.AddListener(() =>
			{
				AddCustomImageToCurrentEntry();
			});

			var entryNameTextField = new JSONStorableString("Entry Name", "Some Text");
			entryNameTextField.setCallbackFunction = new JSONStorableString.SetStringCallback((newString) =>
			{
				UpdateNameForCurrentCatalogEntry(newString);
			});
			CreateTextField(entryNameTextField);

		}

		private void UpdateNameForCurrentCatalogEntry(string newString)
		{
			if (_currentCatalogEntry == null) SuperController.LogError("No catalog entry selected. Please select an entry");
			foreach (var entry in _catalog.Entries)
			{
				if (entry.UniqueName == newString)
				{
					SuperController.LogError("Entry cannot have the same name as another entry");
				}
			}
			_currentCatalogEntry.UniqueName = newString;
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
					var texture = TextureLoader.LoadTexture(filePath);
					if (_currentCatalogEntry == null) SuperController.LogMessage("No catalog entry selected. Please select an entry");
					_currentCatalogEntry.ImageInfo.ExternalPath = filePath;
					//_currentCatalogEntry.Mutation.Img_RGB24_W1000H1000_64bEncoded = filePath;
					_currentCatalogEntry.ImageInfo.Texture = texture;
					var image = _currentCatalogEntry.UiCatalogEntryPanel.GetComponent<Image>();
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
			if (_currentCatalogEntry == null) return;
			//-----------------------
			foreach (var item in _currentCatalogEntry.Mutation.ClothingItems)
			{
				if (!item.Active)
				{
					RemoveToggle(item.UiToggle);
					RemoveUiCatalogSubItem(item.DynamicCheckbox);
				}
			}
			_currentCatalogEntry.Mutation.ClothingItems = _currentCatalogEntry.Mutation.ClothingItems.Where(c => c.Active).ToList();
			//-----------------------
			foreach (var item in _currentCatalogEntry.Mutation.HairItems)
			{
				if (!item.Active)
				{
					RemoveToggle(item.UiToggle);
					RemoveUiCatalogSubItem(item.DynamicCheckbox);
				}
			}
			_currentCatalogEntry.Mutation.HairItems = _currentCatalogEntry.Mutation.HairItems.Where(c => c.Active).ToList();
			//-----------------------
			foreach (var item in _currentCatalogEntry.Mutation.ActiveMorphs)
			{
				if (!item.Active)
				{
					RemoveToggle(item.UiToggle);
					RemoveUiCatalogSubItem(item.DynamicCheckbox);
				}
			}
			_currentCatalogEntry.Mutation.ActiveMorphs = _currentCatalogEntry.Mutation.ActiveMorphs.Where(c => c.Active).ToList();
			//-----------------------
			foreach (var item in _currentCatalogEntry.Mutation.FaceGenMorphSet)
			{
				if (!item.Active)
				{
					RemoveToggle(item.UiToggle);
					RemoveUiCatalogSubItem(item.DynamicCheckbox);
				}
			}
			_currentCatalogEntry.Mutation.FaceGenMorphSet = _currentCatalogEntry.Mutation.FaceGenMorphSet.Where(c => c.Active).ToList();
			//-----------------------
		}

		private void CreateMutationsConfigUi()
		{

			CreateButton(_generateButtonInitText).button.onClick.AddListener(() =>
			{
				RequestNextCaptureSet((int)_catalogRequestedEntriesCountJSON.val, CaptureRequest.MUTATION_AQUISITION_MODE_SEQUENCE);
			});

			_catalogRequestedEntriesCountJSON = new JSONStorableFloat("Generate entries", defaultNumberOfCatalogEntries, 1f, 500f);
			_catalogRequestedEntriesCountJSON.storeType = JSONStorableParam.StoreType.Full;
			RegisterFloat(_catalogRequestedEntriesCountJSON);
			var requestCountSlider = CreateSlider(_catalogRequestedEntriesCountJSON);
			requestCountSlider.valueFormat = "F0";
			requestCountSlider.slider.wholeNumbers = true;

			_waitFramesBetweenCaptureJSON = new JSONStorableFloat("Frames between captures", defaultFramesBetweenCaptures, 0f, 1000f);
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
			_mutationsService.CaptureHair = _catalogCaptureHair.val;
			_mutationsService.CaptureClothes = _catalogCaptureClothes.val;
			_mutationsService.CaptureFaceGenMorphs = true; //_captureMutationMorphs.val;
			_mutationsService.CaptureActiveMorphs = _catalogCaptureMorphs.val;
			_mutationsService.CaptureDynamicItems = _catalogCaptureDynamicItems.val;
			//_mutationsService.SetMorphBaseValuesCheckpoint();
		}

		private void CreateCatalogConfigUi()
		{
			_catalogName = new JSONStorableString("CatalogName", "");
			RegisterString(_catalogName);
			_catalogName.storeType = JSONStorableParam.StoreType.Full;
			_catalogName.isStorable = true;
			_catalogName.isRestorable = true;
			_catalogName.restoreTime = JSONStorableParam.RestoreTime.Normal;

			_catalogRelativePath = new JSONStorableString("CatalogRelativePath", "");
			RegisterString(_catalogRelativePath);
			_catalogRelativePath.storeType = JSONStorableParam.StoreType.Full;
			_catalogRelativePath.isStorable = true;
			_catalogRelativePath.isRestorable = true;
			_catalogRelativePath.restoreTime = JSONStorableParam.RestoreTime.Normal;

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
				ResetCatalog();
			});

			_catalogColumnsCountJSON = new JSONStorableFloat("Catalog Columns", defaultNumberOfCatalogColumns, 1f, 10f);
			_catalogColumnsCountJSON.storeType = JSONStorableParam.StoreType.Full;
			RegisterFloat(_catalogColumnsCountJSON);
			var rowsSlider = CreateSlider(_catalogColumnsCountJSON);
			rowsSlider.valueFormat = "F0";
			rowsSlider.slider.wholeNumbers = true;
			rowsSlider.slider.onValueChanged.AddListener((newVal) =>
			{
				RebuildCatalogFromEntriesCollection();
			});

			_catalogRowsCountJSON = new JSONStorableFloat("Catalog Rows", defaultNumberOfCatalogRows, 1f, 10f);
			_catalogRowsCountJSON.storeType = JSONStorableParam.StoreType.Full;
			RegisterFloat(_catalogRowsCountJSON);
			var colsSlider = CreateSlider(_catalogRowsCountJSON);
			colsSlider.valueFormat = "F0";
			colsSlider.slider.wholeNumbers = true;
			colsSlider.slider.onValueChanged.AddListener((newVal) =>
			{
				RebuildCatalogFromEntriesCollection();
			});

			_expandDirection = new JSONStorableStringChooser("ExpandCatalogWithMore", new List<string> { EXPAND_WITH_MORE_ROWS, EXPAND_WITH_MORE_COLUMNS }, EXPAND_WITH_MORE_COLUMNS, "Expand with");
			_expandDirection.storeType = JSONStorableParam.StoreType.Full;
			RegisterStringChooser(_expandDirection);
			CreatePopup(_expandDirection);
			_expandDirection.setCallbackFunction = new JSONStorableStringChooser.SetStringCallback((chosenString) =>
			{
				RebuildCatalogFromEntriesCollection();
			});

			_catalogTransparencyJSON = new JSONStorableFloat("Opacity", 1, 0f, 1f);
			_catalogTransparencyJSON.storeType = JSONStorableParam.StoreType.Full;
			RegisterFloat(_catalogTransparencyJSON);
			var transparencySlider = CreateSlider(_catalogTransparencyJSON);
			transparencySlider.valueFormat = "F1";
			transparencySlider.slider.onValueChanged.AddListener((newVal) =>
			{
				var backPanelImage = _backPanel.GetComponent<Image>();
				backPanelImage.color = new Color(backPanelImage.color.r, backPanelImage.color.g, backPanelImage.color.b, newVal);
				foreach (var catalogEntry in _catalog.Entries)
				{
					var image = catalogEntry.UiCatalogEntryPanel.GetComponent<Image>();
					image.color = new Color(image.color.r, image.color.g, image.color.b, newVal);
				}
			});

			_catalogEntryFrameSize = new JSONStorableFloat("Frame Size", defaultFrameSize, 100f, 1000f);
			_catalogEntryFrameSize.storeType = JSONStorableParam.StoreType.Full;
			RegisterFloat(_catalogEntryFrameSize);

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
				_floatingControlsUi.Visible = value;
				_catalogUi.Visible = value;
			});

			_triggerButtonVisible = new JSONStorableBool("Show trigger buttons", false);
			RegisterBool(_triggerButtonVisible);
			CreateToggle(_triggerButtonVisible);
			_triggerButtonVisible.toggle.onValueChanged.AddListener((value) =>
			{
				_floatingTriggerButton.button.transform.localScale = value ? Vector3.one : Vector3.zero;
				//_captureButton.button.transform.localScale = value ? Vector3.one: Vector3.zero;
				//_generateButton.button.transform.localScale = value ? Vector3.one : Vector3.zero;
			});

			_alwaysFaceMe = new JSONStorableBool("Always Face Me", false);
			RegisterBool(_alwaysFaceMe);
			CreateToggle(_alwaysFaceMe);
			_alwaysFaceMe.toggle.onValueChanged.AddListener((value) =>
			{
				_floatingControlsUi.AlwaysFaceMe = value;
				_catalogUi.AlwaysFaceMe = value;
			});

			_anchorOnHud = new JSONStorableBool("Anchor on HUD", true);
			RegisterBool(_anchorOnHud);
			CreateToggle(_anchorOnHud);
			_anchorOnHud.toggle.onValueChanged.AddListener((value) =>
			{
				if (value)
				{
					ResetCatalogPositionAtHud();
					AnchorOnHud();
				}
				else
				{
					ResetCatalogPositionAtPerson();
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

		}

		private List<string> GetAtomNames()
		{
			return SuperController.singleton.GetAtoms()?.Select(a => a.name).ToList() ?? new List<string>();
		}

		private void ResetCatalogPositionAtHud()
		{
			_catalogPositionX.val = 0.65f; //-0.573f;
			_catalogPositionY.val = -0.1f; //0.92f;
		}

		private void ResetCatalog()
		{
			_catalog.Entries.ForEach(DestroyCatalogEntry);
			_catalog.Entries = new List<CatalogEntry>();
			_catalogRows.Skip(1).ToList().ForEach(p => Destroy(p));
			_catalogRows = _catalogRows.Take(1).ToList();
			//ResizeBackpanel();
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
			//_dynamicButtonSort.button.transform.localScale = Vector3.zero;
			_dynamicButtonCaptureMorphs.button.transform.localScale = Vector3.zero;
			_dynamicButtonCaptureHair.button.transform.localScale = Vector3.zero;
			_dynamicButtonCaptureClothes.button.transform.localScale = Vector3.zero;
			_dynamicButtonRefresh.button.transform.localScale = Vector3.zero;
			_dynamicButtonAddAtom.button.transform.localScale = Vector3.zero;
			_dynamicButtonRefresh.button.onClick.RemoveAllListeners();

			foreach (var modeButton in _modeAndDynamicModeButton)
			{
				modeButton.Value.buttonColor = new Color(0.25f, 0.25f, 0.25f, 1f);
			}

			if (_catalogMode.val == Enums.CATALOG_MODE_MUTATIONS)
			{
				_floatingTriggerButton.buttonText.text = "Generate Faces";
				_catalogNameLabel.buttonText.text = "Generate Faces";
				if (_triggerButtonVisible.val) _floatingTriggerButton.button.transform.localScale = Vector3.one; //_generateButton.button.transform.localScale = Vector3.one;
				_catalog.Entries.ForEach(e => e.UiBottomButtonGroup.transform.localScale = Vector3.one);
				//_dynamicButtonSort.button.transform.localScale = Vector3.one;
				_dynamicButtonRefresh.buttonText.text = "";
				_dynamicButtonRefresh.button.transform.localScale = Vector3.one;
				_dynamicButtonRefresh.button.onClick.AddListener(() => RequestNextCaptureSet((int)_catalogRequestedEntriesCountJSON.val, CaptureRequest.MUTATION_AQUISITION_MODE_SEQUENCE));
				_dynamicButtonRefresh.button.image.sprite = iconForCaptureFaceGen;
				SetTooltipForDynamicButton(_dynamicButtonRefresh, () => "Generate faces");
			}
			else if (_catalogMode.val == Enums.CATALOG_MODE_CAPTURE)
			{
				_catalogNameLabel.buttonText.text = "Capture Styles";
				_floatingTriggerButton.buttonText.text = "Capture Styles";
				if (_triggerButtonVisible.val) _floatingTriggerButton.button.transform.localScale = Vector3.one; //_captureButton.button.transform.localScale = Vector3.one;
				_catalog.Entries.ForEach(e => e.UiBottomButtonGroup.transform.localScale = Vector3.one);
				//_dynamicButtonSort.button.transform.localScale = Vector3.one;
				_dynamicButtonCaptureMorphs.button.transform.localScale = Vector3.one;
				_dynamicButtonCaptureHair.button.transform.localScale = Vector3.one;
				_dynamicButtonCaptureClothes.button.transform.localScale = Vector3.one;
				_dynamicButtonRefresh.buttonText.text = "";
				_dynamicButtonRefresh.button.transform.localScale = Vector3.one;
				_dynamicButtonRefresh.button.onClick.AddListener(() => RequestNextCaptureSet(1, CaptureRequest.MUTATION_AQUISITION_MODE_CAPTURE));
				_dynamicButtonRefresh.button.image.sprite = iconForCapturePerson;
				SetTooltipForDynamicButton(_dynamicButtonRefresh, () => "Capture styles");
			}
			else if (_catalogMode.val == Enums.CATALOG_MODE_SCENE)
			{
				_catalogNameLabel.buttonText.text = "Refresh Scenes";
				_catalog.Entries.ForEach(e => e.UiBottomButtonGroup.transform.localScale = Vector3.one);
				//_dynamicButtonSort.button.transform.localScale = Vector3.one;
				_dynamicButtonRefresh.buttonText.text = "";
				_dynamicButtonRefresh.button.transform.localScale = Vector3.one;
				_dynamicButtonRefresh.button.image.sprite = iconForCaptureScenes;
				_dynamicButtonRefresh.button.onClick.AddListener(() => CreateSceneCatalogEntries());
				SetTooltipForDynamicButton(_dynamicButtonRefresh, () => "Get Scenes");
			}
			else if (_catalogMode.val == Enums.CATALOG_MODE_OBJECT)
			{
				_catalogNameLabel.buttonText.text = "Capture Object";
				_catalog.Entries.ForEach(e => e.UiBottomButtonGroup.transform.localScale = Vector3.one);
				//_dynamicButtonSort.button.transform.localScale = Vector3.one;
				_dynamicButtonAddAtom.button.transform.localScale = Vector3.one;
				_dynamicButtonRefresh.buttonText.text = "";
				_dynamicButtonRefresh.button.transform.localScale = Vector3.one;
				_dynamicButtonRefresh.button.image.sprite = iconForCaptureObject;
				SetTooltipForDynamicButton(_dynamicButtonRefresh, () => "Capture Object");
				_dynamicButtonRefresh.button.onClick.AddListener(() => {
					RequestNextCaptureSet(1, CaptureRequest.MUTATION_AQUISITION_MODE_CAPTURE_OBJECT);
				});
			}
			else if (_catalogMode.val == Enums.CATALOG_MODE_SESSION)
			{
				_catalogNameLabel.buttonText.text = "Capture Selected";
				_catalog.Entries.ForEach(e => e.UiBottomButtonGroup.transform.localScale = Vector3.one);
				//_dynamicButtonSort.button.transform.localScale = Vector3.one;
				_dynamicButtonAddAtom.button.transform.localScale = Vector3.one;
				_dynamicButtonRefresh.buttonText.text = "";
				_dynamicButtonRefresh.button.transform.localScale = Vector3.one;
				_dynamicButtonRefresh.button.image.sprite = iconForCaptureSelectedObject;
				SetTooltipForDynamicButton(_dynamicButtonRefresh, () => "Capture Selected Object");
				_dynamicButtonRefresh.button.onClick.AddListener(() => {
					if (SuperController.singleton.GetSelectedAtom() == null)
					{
						ShowPopupMessage("Please select an atom from the scene", 2);
						//SuperController.LogError("Please select an atom");
						return;
					}
					RequestNextCaptureSet(1, CaptureRequest.MUTATION_AQUISITION_MODE_CAPTURE_SESSION_OBJECT);
				});
			}
			else if (_catalogMode.val == Enums.CATALOG_MODE_VIEW)
			{
				foreach (var entry in _catalog.Entries)
				{
					entry.UiBottomButtonGroup.transform.localScale = (entry.UiBottomButtonGroup.transform.localScale == Vector3.zero)
							? entry.UiBottomButtonGroup.transform.localScale = Vector3.one
							: entry.UiBottomButtonGroup.transform.localScale = Vector3.zero;
				}
				_dynamicButtonRefresh.button.image.sprite = iconForCaptureNone;
				_dynamicButtonRefresh.button.onClick.AddListener(() => { });
			}

			_modeAndDynamicModeButton[_catalogMode.val].buttonColor = Color.red;
		}

		private void CreateSceneCatalogEntries()
		{
			try
			{
				ResetCatalog();
				var scenePath = GetSceneDirectoryPath();
				var fileEntries = SuperController.singleton.GetFilesAtPath(scenePath);
				foreach (var imagePath in fileEntries)
				{
					if (imagePath.Length < 4) continue;
					if (imagePath.Substring(imagePath.Length - 4) == ".jpg")
					{
						var jsonPath = imagePath.Substring(0, imagePath.Length - 4) + ".json";
						if (!fileEntries.Contains(jsonPath)) continue;
						var stringData = SuperController.singleton.ReadFileIntoString(imagePath.Replace("\\", "/"));
						var texture = TextureLoader.LoadTexture(imagePath);
						texture.Apply();
						var catalogEntry = new CatalogEntry();
						catalogEntry.UniqueName = GetUniqueName();
						catalogEntry.CatalogMode = _catalogMode.val;
						catalogEntry.Mutation = new Mutation();
						catalogEntry.Mutation.ScenePathToOpen = jsonPath;
						catalogEntry.ImageInfo = new ImageInfo();
						catalogEntry.ImageInfo.ExternalPath = imagePath;
						catalogEntry.ImageInfo.Texture = texture;
						var builtCatalogEntry = BuildCatalogEntry(catalogEntry);
						//builtCatalogEntry.Mutation.Img_RGB24_W1000H1000_64bEncoded = imagePath;
					}
				}
			}
			catch (Exception e)
			{
				SuperController.LogError(e.ToString());
			}
		}

		private string GetUniqueName()
		{
			return Guid.NewGuid().ToString().Substring(0, 5);
		}

		private void AnchorOnHud()
		{
			try
			{
				var sc = SuperController.singleton;
				_floatingControlsUi.canvas.transform.SetParent(sc.mainHUDAttachPoint, false);
				_floatingControlsUi.canvas.transform.rotation = Quaternion.Euler(0, 180, 0);
				_floatingControlsUi.canvas.transform.localRotation = Quaternion.Euler(0, 0, 0);
				_floatingControlsUi.canvas.transform.localPosition = new Vector3(-0.573f, 1.22f, 0.5f);

				_catalogUi.canvas.transform.SetParent(sc.mainHUDAttachPoint, false);
				_catalogUi.canvas.transform.rotation = Quaternion.Euler(0, 180, 0);
				_catalogUi.canvas.transform.localRotation = Quaternion.Euler(0, 0, 0);
				_catalogUi.canvas.transform.localPosition = new Vector3(_catalogPositionX.val, _catalogPositionY.val, 0.5f);

			}
			catch (Exception e)
			{
				SuperController.LogError(e.ToString());
			};
		}

		private void AnchorOnAtom()
		{
			var anchorIsPerson = string.IsNullOrEmpty(_anchorOnAtom.val) || _anchorOnAtom.val == "Person";
			Transform targetAtomTransform = anchorIsPerson ? GetPersonBasePosition() : GetAtomBasePosition(_anchorOnAtom.val);
			var handlePosition = anchorIsPerson ? new Vector3(_catalogPositionX.val, _catalogPositionY.val, 0f) : targetAtomTransform.position + new Vector3(-0.7f, 0.7f, 0f);
			Action<Atom> onHandleCreated = (newAtom) =>
			{

				if (!anchorIsPerson) _floatingTriggerButton.transform.localPosition = Vector3.zero;
				var rotation = Quaternion.Euler(0, 180, 0);

				var controlUiPosition = anchorIsPerson ? targetAtomTransform.localPosition + new Vector3(0.15f, 0.45f, 0) : Vector3.zero;
				_floatingControlsUi.canvas.transform.SetParent(newAtom.mainController.transform, false);
				_floatingControlsUi.canvas.transform.rotation = rotation;
				_floatingControlsUi.canvas.transform.localPosition = controlUiPosition;

				_catalogUi.canvas.transform.SetParent(newAtom.mainController.transform, false);
				_catalogUi.canvas.transform.rotation = rotation;
				_catalogUi.canvas.transform.localPosition = new Vector3(0, 0, 0);
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

		private Transform GetPersonBasePosition()
		{
			_parentAtom = containingAtom;
			_parentAtom.mainController.interactableInPlayMode = true;
			Transform anchor = _parentAtom.freeControllers.Where(x => x.name == "headControl").FirstOrDefault()?.transform;
			if (anchor == null) anchor = _parentAtom.freeControllers.FirstOrDefault()?.transform;
			if (anchor == null) anchor = SuperController.singleton.GetAtoms().First().transform;
			return anchor;
		}

		private void ScrollUpCatalog()
		{
			try
			{
				if (_catalog.Entries.Count == 0) return;
				var firstEntry = _catalog.Entries.First();
				_catalog.Entries.Remove(firstEntry);
				_catalog.Entries.Add(firstEntry);
				RebuildCatalogFromEntriesCollection();
			}
			catch (Exception e)
			{
				SuperController.LogError(e.ToString());
			}
		}

		private void ScrollDownCatalog()
		{
			try
			{
				if (_catalog.Entries.Count == 0) return;
				var lastEntry = _catalog.Entries.Last();
				_catalog.Entries.Remove(lastEntry);
				_catalog.Entries.Insert(0, lastEntry);
				RebuildCatalogFromEntriesCollection();
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
				_minimizeUi.SetVal(!_minimizeUi.val);
				SetMinimizedState();
				UpdateUiForMode();
			}
			catch (Exception e)
			{
				SuperController.LogError(e.ToString());
			}

		}

		private void SetMinimizedState()
		{
			var window = _windowContainer.GetComponent<RectTransform>();
			window.localScale = _minimizeUi.val ? Vector3.zero : Vector3.one;

			//var generateButton = _generateButton.GetComponent<RectTransform>();
			//generateButton.localScale = _minimizeUi.val ? Vector3.zero : Vector3.one;
			//var captureButton = _captureButton.GetComponent<RectTransform>();
			//captureButton.localScale = _minimizeUi.val ? Vector3.zero : Vector3.one;

			var triggerButton = _floatingTriggerButton.GetComponent<RectTransform>();
			triggerButton.localScale = _minimizeUi.val ? Vector3.zero : Vector3.one;


			//var minimizeBtnRect = _minimizeButton.GetComponent<RectTransform>();
			//var btnWidth = string.IsNullOrEmpty(pluginLabelJSON.val) ? 35 : 30 * pluginLabelJSON.val.Length;
			//minimizeBtnRect.sizeDelta = _minimizeUi.val ? new Vector2(btnWidth, 35) : new Vector2(35, 35);
			//_minimizeButton.buttonText.text = _minimizeUi.val ? pluginLabelJSON.val : "";
			//UIHelper.SetAnchors(_windowContainer, _minimizeButton.button.gameObject, "topright", _windowWidth);
		}

		private void SortCatalog()
		{
			try
			{
				_catalog.Entries
					.Where(c => c.Discarded)
					.ToList()
					.ForEach(DestroyCatalogEntry);

				_catalog.Entries = _catalog.Entries
					.Where(c => !c.Discarded)
					.OrderByDescending(c => c.Favorited)
					.ToList();

				RebuildCatalogFromEntriesCollection();
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
				// Detach entry from catalog..
				foreach (var catalogEntry in _catalog.Entries)
				{
					catalogEntry.UiCatalogEntryPanel.transform.SetParent(_windowContainer.transform);
					catalogEntry.UiParentCatalogRow = null;
					catalogEntry.UiParentCatalogColumn = null;
				}

				//var frameSizeInWorldScale = _catalogEntryFrameSize.val * 0.001f;
				//_rowContainer.transform.position = new Vector3(50, -frameSizeInWorldScale - 50, 0);

				// Reassign list to columns
				for (var i = 0; i < _catalog.Entries.Count; i++)
				{
					AddEntryToCatalog(_catalog.Entries[i], i);
				}
				ResetScrollPosition();
				//ResizeBackpanel();
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
			_toolTipLabel.buttonText.text = message;
			_toolTipLabel.transform.localScale = Vector3.one;
		}

		private void HideLoading()
		{
			_toolTipLabel.transform.localScale = Vector3.zero;
		}

		private void SetTooltipForDynamicButton(UIDynamicButton button, Func<string> returnTooltipText)
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
			_toolTipLabel.transform.localScale = Vector3.zero;
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
			_popupMessageLabel.transform.localScale = Vector3.zero;
		}

		private void ShowPopupMessage(string text, int? duration = null)
		{
			if (_popupMessageLabel == null) return;
			var minimizeBtnRect = _popupMessageLabel.GetComponent<RectTransform>();
			var btnWidth = string.IsNullOrEmpty(text) ? 50 : 30 * text.Length;
			minimizeBtnRect.sizeDelta = new Vector2(btnWidth, 50);
			_popupMessageLabel.buttonText.text = text;
			_popupMessageLabel.transform.localScale = Vector3.one;
			CatalogUiHelper.SetAnchors(_windowContainer, _popupMessageLabel.button.gameObject, "topleft", -10, -40);
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
			if (_toolTipLabel == null) return;
			var minimizeBtnRect = _toolTipLabel.GetComponent<RectTransform>();
			var btnWidth = string.IsNullOrEmpty(text) ? 50 : 30 * text.Length;
			minimizeBtnRect.sizeDelta = new Vector2(btnWidth, 30);
			_toolTipLabel.buttonText.text = "  " + text;
			_toolTipLabel.transform.localScale = Vector3.one;
			CatalogUiHelper.SetAnchors(_windowContainer, _toolTipLabel.button.gameObject, "left", -20, -_windowHeight - 5);
		}

		private void ShowConfirm(string text, Action action)
		{
			_lastConfirmAction = action;
			if (_confirmLabel == null) return;
			_confirmLabel.text = "  " + text;
			_dynamicConfirmPanel.transform.localScale = Vector3.one;
		}

		private void BrowseForAndLoadCatalog()
		{
			_baseFileFormat = SuperController.singleton.fileBrowserUI.fileFormat;
			SuperController.singleton.ShowMainHUD();
			//SuperController.singleton.SetLoadFlag();
			SuperController.singleton.fileBrowserUI.SetTextEntry(false);
			SuperController.singleton.fileBrowserUI.fileFormat = _fileExtension;
			SuperController.singleton.fileBrowserUI.defaultPath = GetSceneDirectoryPath();
			SuperController.singleton.fileBrowserUI.Show((file) =>
			{
				SuperController.singleton.fileBrowserUI.fileFormat = "json";
				if (string.IsNullOrEmpty(file)) return;
				ShowLoading("Loading Catalog...");
				ResetCatalog();
				LoadCatalogFromFile(file);
				HideLoading();
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

				SuperController.singleton.fileBrowserUI.Show((file) =>
				{
					SuperController.singleton.fileBrowserUI.fileFormat = "json";
					if (string.IsNullOrEmpty(file)) return;
					var extension = "." + _fileExtension;
					if (file.Length > 3 && file.Substring(file.Length - extension.Length) == extension) extension = "";
					file += extension;
					ShowLoading("Saving Catalog...");
					SaveCatalogToFile(file);
					HideLoading();
					SuperController.LogMessage("Saved catalog to: " + file);
				});

			}
			catch (Exception e)
			{
				SuperController.LogError(e.ToString());
			}
		}

		private void CreateDynamicButton_Minimize()
		{
			var texture = TextureLoader.LoadTexture(GetPluginPath() + "/Resources/Minimize2.png");
			_minimizeButton = _catalogUi.CreateButton(_catalogUi.canvas.gameObject, "", 35, 35, _windowWidth - 40 - 10, 0, new Color(0.5f, 0.5f, 1f), new Color(1f, 1f, 1f), new Color(0.2f, 0.2f, 0.2f), texture);
			_minimizeButton.button.onClick.AddListener(() => ToggleMinimize());
			SetTooltipForDynamicButton(_minimizeButton, () => _minimizeUi.val ? "Maximize" : "Minimize");
		}

		private void CreateDynamicButton_Move()
		{
			var texture = TextureLoader.LoadTexture(GetPluginPath() + "/Resources/Move.png");
			_moveButton = _catalogUi.CreateButton(_catalogUi.canvas.gameObject, "", 35, 35, _windowWidth - 80 - 10, 0, new Color(0.5f, 0.5f, 1f), new Color(0.8f, 0.8f, 1f), new Color(0.2f, 0.2f, 0.2f), texture);
			_moveButton.button.onClick.AddListener(() => SelectHandle());
			SetTooltipForDynamicButton(_moveButton, () => "Move");

			var positionTracker = new DragHelper();
			_positionTrackers.Add("DragButton", positionTracker);
			_onCatalogDragFinishedEvent = () =>
			{
				try
				{
					_catalogPositionX.val = positionTracker.CurrentPosition.x;
					_catalogPositionY.val = positionTracker.CurrentPosition.y;
				}
				catch (Exception e)
				{
					SuperController.LogError(e.ToString());
				}
			};
			Action onStartDraggingEvent = () =>
			{
				positionTracker.AllowDragX = IsAnchoredOnHUD(); // ...only allow dragging if anchored on HUD
				positionTracker.AllowDragY = IsAnchoredOnHUD();// ...only allow dragging if anchored on HUD
				positionTracker.IsIn3DSpace = !IsAnchoredOnHUD();
				positionTracker.XMultiplier = positionTracker.IsIn3DSpace ? 1f : -1f;
				positionTracker.YMultiplier = 1f;
			};
			positionTracker.AddMouseDraggingToObject(_moveButton.gameObject, _catalogUi.canvas.gameObject, true, true, onStartDraggingEvent, _onCatalogDragFinishedEvent);

		}



		private void CreateDynamicButton_Save()
		{
			var texture = TextureLoader.LoadTexture(GetPluginPath() + "/Resources/Save.png");
			var button = _catalogUi.CreateButton(_windowContainer, "", 35, 35, _windowWidth - 40 - 10, 0, new Color(0.5f, 0.5f, 1f), Color.red, new Color(1f, 1f, 1f), texture);
			button.button.onClick.AddListener(() => BrowseForAndSaveCatalog());
			SetTooltipForDynamicButton(button, () => "Save Catalog");
		}

		private void CreateDynamicButton_Load()
		{
			var texture = TextureLoader.LoadTexture(GetPluginPath() + "/Resources/Open.png");
			var button = _catalogUi.CreateButton(_windowContainer, "", 35, 35, _windowWidth - 80 - 10, 0, new Color(0.5f, 0.5f, 1f), Color.green, new Color(1f, 1f, 1f), texture);
			button.button.onClick.AddListener(() => BrowseForAndLoadCatalog());
			SetTooltipForDynamicButton(button, () => "Load Catalog");
		}

		private void CreateDynamicButton_QuickLoad()
		{
			var texture = TextureLoader.LoadTexture(GetPluginPath() + "/Resources/List.png");
			var button = _catalogUi.CreateButton(_windowContainer, "", 35, 35, _windowWidth - 120 - 10, 0, new Color(0.5f, 0.5f, 1f), Color.green, new Color(1f, 1f, 1f), texture);
			button.button.onClick.AddListener(() =>
			{
				ShowRecentDirectoryFileList();
			});

			SetTooltipForDynamicButton(button, () => "Catalog Listing");
		}

		private void ShowRecentDirectoryFileList()
		{
			var files = SuperController.singleton.GetFilesAtPath(_lastCatalogDirectory, "*." + _fileExtension);
			Dictionary<string, UnityAction> filesToLoad = new Dictionary<string, UnityAction>();
			foreach (var file in files)
			{
				var name = file.Replace("\\", "/").Replace(_lastCatalogDirectory + "/", "").Replace("." + _fileExtension, "");
				UnityAction selectAction = () =>
				{
					ResetCatalog();
					LoadCatalogFromFile(_lastCatalogDirectory + "/" + name + "." + _fileExtension);
				};
				filesToLoad.Add(name, selectAction);
			}
			ShowDynamicSelectList(filesToLoad);
		}

		private void CreateDynamicButton_Mode()
		{
			var texture = TextureLoader.LoadTexture(GetPluginPath() + "/Resources/Mode.png");
			var button = _catalogUi.CreateButton(_windowContainer, "", 35, 35, _windowWidth, 0, new Color(1f, 0f, 0f, 0.5f), Color.red, new Color(1f, 1f, 1f), texture);
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
				if (mode == Enums.CATALOG_MODE_VIEW) iconFileName = "CaptureNone.png";
				if (mode == Enums.CATALOG_MODE_SESSION) iconFileName = "CaptureSelected.png";
				if (mode == Enums.CATALOG_MODE_SCENE) iconFileName = "CaptureScenes.png";
				if (mode == Enums.CATALOG_MODE_OBJECT) iconFileName = "CaptureObject.png";
				if (mode == Enums.CATALOG_MODE_MUTATIONS) iconFileName = "CaptureFaceGen.png";
				if (mode == Enums.CATALOG_MODE_CAPTURE) iconFileName = "CapturePerson.png";
				var texture = TextureLoader.LoadTexture(GetPluginPath() + "/Resources/" + iconFileName);
				var button = _catalogUi.CreateButton(_windowContainer, "", 35, 35, _windowWidth, index++ * 40, Color.white, Color.white, new Color(1f, 1f, 1f), texture);
				button.button.onClick.AddListener(() =>
				{
					_catalogMode.val = mode;
					UpdateUiForMode();
					button.buttonColor = Color.red;
				});
				_modeAndDynamicModeButton.Add(mode, button);
				SetTooltipForDynamicButton(button, () => mode);
			}
		}

		private void CreateDynamicButton_Refresh()
		{
			var captureFaceGen = TextureLoader.LoadTexture(GetPluginPath() + "/Resources/CaptureFaceGen.png");
			iconForCaptureFaceGen = Sprite.Create(captureFaceGen, new Rect(0.0f, 0.0f, captureFaceGen.width, captureFaceGen.height), new Vector2(0.5f, 0.5f), 100.0f);

			var capturePersonTexture = TextureLoader.LoadTexture(GetPluginPath() + "/Resources/CapturePerson.png");
			iconForCapturePerson = Sprite.Create(capturePersonTexture, new Rect(0.0f, 0.0f, capturePersonTexture.width, capturePersonTexture.height), new Vector2(0.5f, 0.5f), 100.0f);

			var captureObjectTexture = TextureLoader.LoadTexture(GetPluginPath() + "/Resources/CaptureObject.png");
			iconForCaptureObject = Sprite.Create(captureObjectTexture, new Rect(0.0f, 0.0f, captureObjectTexture.width, captureObjectTexture.height), new Vector2(0.5f, 0.5f), 100.0f);

			var captureSceneTexture = TextureLoader.LoadTexture(GetPluginPath() + "/Resources/CaptureScenes.png");
			iconForCaptureScenes = Sprite.Create(captureSceneTexture, new Rect(0.0f, 0.0f, captureSceneTexture.width, captureSceneTexture.height), new Vector2(0.5f, 0.5f), 100.0f);

			var captureSelectedTexture = TextureLoader.LoadTexture(GetPluginPath() + "/Resources/CaptureSelected.png");
			iconForCaptureSelectedObject = Sprite.Create(captureSelectedTexture, new Rect(0.0f, 0.0f, captureSelectedTexture.width, captureSelectedTexture.height), new Vector2(0.5f, 0.5f), 100.0f);

			var captureNoneTexture = TextureLoader.LoadTexture(GetPluginPath() + "/Resources/CaptureNone.png");
			iconForCaptureNone = Sprite.Create(captureNoneTexture, new Rect(0.0f, 0.0f, captureNoneTexture.width, captureNoneTexture.height), new Vector2(0.5f, 0.5f), 100.0f);

			_dynamicButtonRefresh = _catalogUi.CreateButton(_windowContainer, "", 60, 60, 0, 10, new Color(1, 0.25f, 0.25f), new Color(1, 0.5f, 0.5f), new Color(1, 1, 1));
			_dynamicButtonRefresh.buttonText.fontSize = 21;
			SetTooltipForDynamicButton(_dynamicButtonRefresh, () => "Capture");
		}

		private void CreateDynamicButton_CaptureAdditionalAtom()
		{
			try
			{
				var texture = TextureLoader.LoadTexture(GetPluginPath() + "/Resources/Add.png");
				_dynamicButtonAddAtom = _catalogUi.CreateButton(_windowContainer, "", 30, 30, 70, 20, new Color(1, 0.25f, 0.25f), new Color(1, 0.5f, 0.5f), new Color(1, 1, 1), texture);
				_dynamicButtonAddAtom.button.onClick.AddListener(() =>
				{
					if (_currentCatalogEntry == null)
					{
						ShowPopupMessage("Please select a catalog entry first", 2);
						return;
					}
					var selectList = SuperController.singleton.GetAtomUIDs();
					var atomNameAndSelectAction = new Dictionary<string, UnityAction>();
					foreach (var atomUid in selectList)
					{
						UnityAction action = () => {
							var atom = SuperController.singleton.GetAtomByUid(atomUid);
							_mutationsService.CaptureAdditionalAtom(atom, _currentCatalogEntry);
							SelectCatalogEntry(_currentCatalogEntry);
						};
						atomNameAndSelectAction.Add(atomUid, action);
					}
					ShowDynamicSelectList(atomNameAndSelectAction, _dynamicSelectList.transform.position);
				});
				SetTooltipForDynamicButton(_dynamicButtonAddAtom, () => "Add atom to capture");
			}
			catch (Exception e)
			{
				SuperController.LogError(e.ToString());
			}

			//	Atom currentAtom = SuperController.singleton.GetSelectedAtom();
			//	if (currentAtom == null) {
			//		ShowPopupMessage("Please select an atom", 2);
			//		return;
			//	}
			//	if (_currentCatalogEntry == null) {
			//		ShowPopupMessage("Please select a catalog entry", 2);
			//		return;
			//	}
			//	if (_currentCatalogEntry.Mutation == null) return;
			//	if (_currentCatalogEntry.Mutation.StoredAtoms.Any(a => a.AtomName == currentAtom.name)) {
			//		ShowPopupMessage("This atom has already been captured", 2);
			//		return;
			//	}
			//	_mutationsService.CaptureAdditionalAtom(currentAtom, _currentCatalogEntry);
			//});
			//SetTooltipForDynamicButton(_dynamicButtonAddAtom, () => "Add selected atom");
		}

		private void CreateDynamicButton_Capture_Clothes()
		{
			var texture = TextureLoader.LoadTexture(GetPluginPath() + "/Resources/Clothing.png");
			_dynamicButtonCaptureClothes = _catalogUi.CreateButton(_windowContainer, "", 35, 35, 80, 10, _dynamicButtonCheckColor, _dynamicButtonCheckColor, new Color(1f, 1f, 1f), texture);
			_dynamicButtonCaptureClothes.button.onClick.AddListener(() =>
			{
				UpdateCaptureClothesState(!_catalogCaptureClothes.val);
			});
			_dynamicButtonCaptureClothes.buttonColor = _catalogCaptureClothes.val ? _dynamicButtonCheckColor : _dynamicButtonUnCheckColor;
			SetTooltipForDynamicButton(_dynamicButtonCaptureClothes, () => "Include Clothes");
		}



		private void CreateDynamicButton_Capture_Hair()
		{
			var texture = TextureLoader.LoadTexture(GetPluginPath() + "/Resources/Hair2.png");
			_dynamicButtonCaptureHair = _catalogUi.CreateButton(_windowContainer, "", 35, 35, 120, 10, _dynamicButtonCheckColor, _dynamicButtonCheckColor, new Color(1f, 1f, 1f), texture);
			_dynamicButtonCaptureHair.button.onClick.AddListener(() =>
			{
				UpdateCaptureHairState(!_catalogCaptureHair.val);
			});
			_dynamicButtonCaptureHair.buttonColor = _catalogCaptureHair.val ? _dynamicButtonCheckColor : _dynamicButtonUnCheckColor;
			SetTooltipForDynamicButton(_dynamicButtonCaptureHair, () => "Include Hair");
		}

		private void CreateDynamicButton_Capture_Morphs()
		{
			var texture = TextureLoader.LoadTexture(GetPluginPath() + "/Resources/Morph.png");
			_dynamicButtonCaptureMorphs = _catalogUi.CreateButton(_windowContainer, "", 35, 35, 160, 10, _dynamicButtonCheckColor, _dynamicButtonCheckColor, new Color(1f, 1f, 1f), texture);
			_dynamicButtonCaptureMorphs.button.onClick.AddListener(() =>
			{
				UpdateCaptureMorphsState(!_catalogCaptureMorphs.val);
			});
			_dynamicButtonCaptureMorphs.buttonColor = _catalogCaptureMorphs.val ? _dynamicButtonCheckColor : _dynamicButtonUnCheckColor;
			SetTooltipForDynamicButton(_dynamicButtonCaptureMorphs, () => "Include Morphs");
		}

		private void UpdateCaptureClothesState(bool newValue)
		{
			_mutationsService.CaptureClothes = newValue;
			_catalog.CaptureClothes = newValue;
			_catalogCaptureClothes.val = newValue;
			_dynamicButtonCaptureClothes.buttonColor = _catalogCaptureClothes.val ? _dynamicButtonCheckColor : _dynamicButtonUnCheckColor;
		}

		private void UpdateCaptureHairState(bool newValue)
		{
			_mutationsService.CaptureHair = newValue;
			_catalog.CaptureHair = newValue;
			_catalogCaptureHair.val = newValue;
			_dynamicButtonCaptureHair.buttonColor = _catalogCaptureHair.val ? _dynamicButtonCheckColor : _dynamicButtonUnCheckColor;
		}

		private void UpdateCaptureMorphsState(bool newValue)
		{
			_mutationsService.CaptureActiveMorphs = newValue;
			_catalog.CaptureMorphs = newValue;
			_catalogCaptureMorphs.val = newValue;
			_dynamicButtonCaptureMorphs.buttonColor = _catalogCaptureMorphs.val ? _dynamicButtonCheckColor : _dynamicButtonUnCheckColor;
		}

		private void CreateDynamicButton_ResetCatalog()
		{
			var texture = TextureLoader.LoadTexture(GetPluginPath() + "/Resources/Reset3.png");
			var button = _catalogUi.CreateButton(_windowContainer, "", 30, 30, 0, 80, new Color(1f, 0.5f, 0.05f, 0.5f), new Color(1f, 0.5f, 0.05f, 1f), new Color(1f, 1f, 1f), texture);
			button.button.onClick.AddListener(() => {
				Action confirmAction = () => ResetCatalog();
				ShowConfirm("Reset catalog?", confirmAction);
			});
			SetTooltipForDynamicButton(button, () => "Reset catalog");
		}

		private void CreateDynamicButton_ZoomIn()
		{
			var texture = TextureLoader.LoadTexture(GetPluginPath() + "/Resources/ZoomIn.png");
			var button = _catalogUi.CreateButton(_windowContainer, "", 35, 35, 40, 80, new Color(1f, 0.5f, 0.05f, 0.5f), new Color(1f, 0.5f, 0.05f, 1f), new Color(1f, 1f, 1f), texture);
			button.button.onClick.AddListener(() => ZoomInCatalog());
			SetTooltipForDynamicButton(button, () => "Increase frame size");
		}

		private void CreateDynamicButton_ZoomOut()
		{
			var texture = TextureLoader.LoadTexture(GetPluginPath() + "/Resources/ZoomOut.png");
			var button = _catalogUi.CreateButton(_windowContainer, "", 35, 35, 80, 80, new Color(1f, 0.5f, 0.05f, 0.5f), new Color(1f, 0.5f, 0.05f, 1f), new Color(1f, 1f, 1f), texture);
			button.button.onClick.AddListener(() => ZoomOutCatalog());
			SetTooltipForDynamicButton(button, () => "Decrease frame size");
		}
		private void CreateDynamicButton_ScrollUp()
		{
			var texture = TextureLoader.LoadTexture(GetPluginPath() + "/Resources/Previous.png");
			var button = _catalogUi.CreateButton(_windowContainer, "", 35, 35, 120, 80, new Color(1f, 0.5f, 0.05f, 0.5f), new Color(1f, 0.5f, 0.05f, 1f), new Color(1f, 1f, 1f), texture);
			button.button.onClick.AddListener(() => ScrollUpCatalog());
			SetTooltipForDynamicButton(button, () => "Shift catalog entries forward");
		}

		private void CreateDynamicButton_ScrollDown()
		{
			var texture = TextureLoader.LoadTexture(GetPluginPath() + "/Resources/Next.png");
			var button = _catalogUi.CreateButton(_windowContainer, "", 35, 35, 160, 80, new Color(1f, 0.5f, 0.05f, 0.5f), new Color(1f, 0.5f, 0.05f, 1f), new Color(1f, 1f, 1f), texture);
			button.button.onClick.AddListener(() => ScrollDownCatalog());
			SetTooltipForDynamicButton(button, () => "Shift catalog entries back");
		}

		private void CreateDynamicButton_Sort()
		{
			var texture = TextureLoader.LoadTexture(GetPluginPath() + "/Resources/Sort2.png");
			_dynamicButtonSort = _catalogUi.CreateButton(_windowContainer, "", 35, 35, 200, 80, new Color(1f, 0.5f, 0.05f, 0.5f), new Color(1f, 0.5f, 0.05f, 1f), new Color(1f, 1f, 1f), texture);
			_dynamicButtonSort.button.onClick.AddListener(() => SortCatalog());
			SetTooltipForDynamicButton(_dynamicButtonSort, () => "Sort Catalog");
		}

		private void CreateDynamicButton_CycleAtoms()
		{
			var texture = TextureLoader.LoadTexture(GetPluginPath() + "/Resources/NextObject2.png");
			var button = _catalogUi.CreateButton(_windowContainer, "", 35, 35, 0, 120, new Color(0.25f, 0.5f, 0.5f), Color.green, new Color(1f, 1f, 1f), texture);
			button.buttonText.fontSize = 20;
			button.button.onClick.AddListener(() => SelectNextAtom());
			SetTooltipForDynamicButton(button, () =>
			{
				var currentAtom = SuperController.singleton.GetSelectedAtom();
				var currentAtomName = currentAtom?.name;
				return $"Current Atom: {currentAtomName ?? "(none)"}. Click to select next atom.";
			});
		}

		private void CreateDynamicButton_ResetPivot()
		{
			var texture = TextureLoader.LoadTexture(GetPluginPath() + "/Resources/CenterPivot.png");
			var button = _catalogUi.CreateButton(_windowContainer, "", 35, 35, 40, 120, new Color(0.25f, 0.5f, 0.5f), Color.green, new Color(1f, 1f, 1f), texture);
			button.buttonText.fontSize = 20;
			button.button.onClick.AddListener(() =>
			{
				var currentAtom = SuperController.singleton.GetSelectedAtom();
				if (currentAtom == null) currentAtom = containingAtom;
				if (currentAtom == null || currentAtom.type != "Person")
				{
					ShowPopupMessage("Can only do this for a 'Person' atom", 2);
					return;
				}
				CenterPivot(currentAtom);
			});
			SetTooltipForDynamicButton(button, () => "Center Control Pivot (Person: 'control' must be selected)");
		}

		private void CreateDynamicButton_PopupMessage()
		{
			_popupMessageLabel = _catalogUi.CreateButton(_windowContainer, "Popup...", 160, 40, 0, 0, new Color(0.0f, 0.0f, 0.0f, 1f), new Color(0.5f, 0.5f, 0.5f, 1f), new Color(1f, 0.3f, 0.3f, 1));
			_popupMessageLabel.button.onClick.AddListener(() => HidePopupMessage());
			_popupMessageLabel.buttonText.alignment = TextAnchor.MiddleLeft;
			_popupMessageLabel.transform.localScale = Vector3.zero;
		}

		private void CreateDynamicButton_SelectList()
		{
			_dynamicSelectList = CatalogUiHelper.CreatePanel(_windowContainer, 0, 0, 0, 0, new Color(0.1f, 0.1f, 0.1f, 0.9f), Color.green);
			var backPanel = CatalogUiHelper.CreatePanel(_dynamicSelectList, 200, 550, -10, -310, new Color(0.15f, 0.15f, 0.15f), Color.green);
			var cancelButton = _catalogUi.CreateButton(_dynamicSelectList, "X", 40, 40, 150, -350, new Color(0.2f, 0.2f, 0.2f, 1f), new Color(0.5f, 0.5f, 0.5f, 1f), new Color(1f, 0.3f, 0.3f, 1));
			cancelButton.button.onClick.AddListener(() => HideSelectList());
			_selectListVLayout = _catalogUi.CreateVerticalLayout(backPanel, 0, true, false, false, false);
			_dynamicSelectList.transform.localScale = Vector3.zero;
		}


		private void CreateDynamicButton_DynamicConfirm()
		{
			_dynamicConfirmPanel = CatalogUiHelper.CreatePanel(_windowContainer, 0, 0, 0, 0, new Color(0.1f, 0.1f, 0.1f, 0.9f), Color.green);
			var backPanel = CatalogUiHelper.CreatePanel(_dynamicConfirmPanel, _windowWidth, 210, 0, -_windowHeight, new Color(0.25f, 0.25f, 0.25f), Color.green);
			_confirmLabel = _catalogUi.CreateTextField(_dynamicConfirmPanel, "", _windowWidth - 20, 140, 10, -_windowHeight + 10, new Color(0.25f, 0.25f, 0.25f), Color.white);
			var confirmButton = _catalogUi.CreateButton(_dynamicConfirmPanel, "OK", 160, 40, _windowWidth - 320 - 10, -_windowHeight + 160, new Color(0.2f, 0.2f, 0.2f, 1f), new Color(0.5f, 0.5f, 0.5f, 1f), new Color(1f, 0.3f, 0.3f, 1));
			confirmButton.button.onClick.AddListener(() => {
				_lastConfirmAction.Invoke();
				HideConfirmMessage();
			});
			var cancelButton = _catalogUi.CreateButton(_dynamicConfirmPanel, "Cancel", 160, 40, _windowWidth - 160 - 10, -_windowHeight + 160, new Color(0.2f, 0.2f, 0.2f, 1f), new Color(0.5f, 0.5f, 0.5f, 1f), new Color(1f, 0.3f, 0.3f, 1));
			cancelButton.button.onClick.AddListener(() => HideConfirmMessage());
			_dynamicConfirmPanel.transform.localScale = Vector3.zero;
		}

		private void CreateDynamicPanel_RightInfo()
		{
			_dynamicInfoPanel = CatalogUiHelper.CreatePanel(_windowContainer, 0, 0, _windowWidth + 70, 0, Color.red, Color.clear);
			var innerPanel = CatalogUiHelper.CreatePanel(_dynamicInfoPanel, 200, _windowHeight, -10, -10, new Color(0.1f, 0.1f, 0.1f, 0.9f), Color.clear);
			_infoVLayout = _catalogUi.CreateVerticalLayout(innerPanel, 1, true, false, false, false);
		}

		private void CreateDynamicPanel_LeftInfo()
		{
			_infoLabel = _catalogUi.CreateTextField(_windowContainer, "", 200, _windowHeight - 20, -270, 0, new Color(0, 0, 0, 0.1f), Color.white);
			_infoLabel.UItext.fontSize = 15;
			_infoLabel.UItext.fontStyle = FontStyle.Italic;
			_infoLabel.UItext.alignment = TextAnchor.MiddleRight;
			_infoLabel.transform.localScale = Vector3.zero;
		}

		public UiCatalogSubItem AddInfoCheckbox(string label, bool value, UnityAction<bool> onToggle, UnityAction<string> stopTrackingAction)
		{
			var labelXButtonAndGroup = new UiCatalogSubItem();

			GameObject buttonRow = CreateButtonRow(_dynamicInfoPanel, 25);
			labelXButtonAndGroup.ButtonRow = buttonRow;

			var currentTextColor = value ? new Color(1f, 0.3f, 0.3f, 1) : new Color(0.3f, 0.3f, 0.3f, 1);
			// Add stop tracking button
			var texture = TextureLoader.LoadTexture(GetPluginPath() + "/Resources/Delete3.png");
			var stopTrackingButton = _catalogUi.CreateButton(buttonRow, "", 20, 20, 0, 0, Color.red, new Color(1f, 0.5f, 0.5f), Color.black, texture);
			stopTrackingButton.button.onClick.AddListener(() => {
				stopTrackingAction.Invoke(label);
				RemoveUiCatalogSubItem(labelXButtonAndGroup);
			});
			labelXButtonAndGroup.StopTrackingItemButton = stopTrackingButton;

			var checkButton = _catalogUi.CreateButton(buttonRow, label, 160, 25, 0, 0, Color.clear, new Color(0.3f, 0.3f, 0.2f), currentTextColor);
			buttonRow.transform.SetParent(_infoVLayout.transform, false);
			checkButton.buttonText.fontSize = 15;
			checkButton.buttonText.fontStyle = FontStyle.Italic;
			checkButton.buttonText.alignment = TextAnchor.MiddleLeft;
			checkButton.button.onClick.AddListener(() => {
				var newValue = checkButton.textColor == new Color(0.3f, 0.3f, 0.3f, 1) ? true : false;
				checkButton.textColor = newValue ? new Color(1f, 0.3f, 0.3f, 1) : new Color(0.3f, 0.3f, 0.3f, 1);
				onToggle.Invoke(newValue);
			});
			labelXButtonAndGroup.ItemActiveCheckbox = checkButton;

			return labelXButtonAndGroup;
		}

		public void ShowDynamicSelectList(Dictionary<string, UnityAction> itemList, Vector3? position = null)
		{
			foreach (var item in _dynamicListItems)
			{
				RemoveUiCatalogSubItem(item);
			}
			_dynamicListItems = itemList.Select(i => AddSelectButtonToPopupList(i.Key, i.Value)).ToList();
			_dynamicSelectList.transform.localScale = Vector3.one;
			if (position != null) _dynamicSelectList.transform.localPosition = position ?? Vector3.zero;
		}

		public UiCatalogSubItem AddSelectButtonToPopupList(string label, UnityAction onSelect)
		{
			var labelXButtonAndGroup = new UiCatalogSubItem();
			GameObject buttonColumn = CreateButtonColumn(_dynamicSelectList, 25);
			labelXButtonAndGroup.ButtonRow = buttonColumn;
			var button = _catalogUi.CreateButton(buttonColumn, label, 200, 25, 0, 0, new Color(0.15f, 0.15f, 0.15f), new Color(0.5f, 0.3f, 0.3f), new Color(0.8f, 0.8f, 0.8f));
			buttonColumn.transform.SetParent(_selectListVLayout.transform, false);
			button.buttonText.fontSize = 15;
			button.buttonText.fontStyle = FontStyle.Italic;
			button.buttonText.alignment = TextAnchor.MiddleLeft;
			button.button.onClick.AddListener(() => {
				onSelect.Invoke();
				_dynamicSelectList.transform.localScale = Vector3.zero;
			});
			labelXButtonAndGroup.ItemActiveCheckbox = button;

			return labelXButtonAndGroup;
		}

		public void RemoveUiCatalogSubItem(UiCatalogSubItem catalogSubItemItem)
		{
			if (catalogSubItemItem.ItemActiveCheckbox != null) RemoveButton(catalogSubItemItem.ItemActiveCheckbox);
			if (catalogSubItemItem.StopTrackingItemButton != null) RemoveButton(catalogSubItemItem.StopTrackingItemButton);
			if (catalogSubItemItem.ButtonRow != null) Destroy(catalogSubItemItem.ButtonRow);
		}

		private void CreateDynamicButton_Tooltip()
		{
			_toolTipLabel = _catalogUi.CreateButton(_windowContainer, "Tooltip...", 0, 0, 0, _windowHeight, new Color(0.0f, 0.0f, 0.0f, 0.9f), Color.green, new Color(0.7f, 0.7f, 0.7f, 1));
			_toolTipLabel.buttonText.fontSize = 20;
			_toolTipLabel.buttonText.alignment = TextAnchor.MiddleLeft;
			_toolTipLabel.transform.localScale = Vector3.zero;
		}

		private void CreateDynamicButton_Label()
		{
			_modeLabel = _catalogUi.CreateButton(_catalogUi.canvas.gameObject, "Hello", _windowWidth - 90, 40, 0, 0, new Color(0.0f, 0.0f, 0.0f, 0.7f), new Color(0.0f, 0.0f, 0.0f, 0.7f), new Color(0.7f, 0.7f, 0.7f, 1));
			_modeLabel.buttonText.fontSize = 20;
			_modeLabel.buttonText.alignment = TextAnchor.MiddleRight;
			//_windowLabel.buttonText.alignment = TextAnchor.MiddleLeft;
			//_toolTipLabel.transform.localScale = Vector3.zero;
		}

		private void CreateDynamicButton_SideLabel()
		{
			_catalogNameLabel = _catalogUi.CreateButton(_catalogUi.canvas.gameObject, "", _windowHeight, 40, 0, 0, new Color(0.0f, 0.2f, 0.2f, 0.95f), new Color(0.0f, 0.2f, 0.2f, 1f), new Color(0.7f, 0.7f, 0.7f, 1));
			_catalogNameLabel.buttonText.fontSize = 20;
			//_catalogNameLabel.buttonText.horizontalOverflow = HorizontalWrapMode.Overflow;

			_catalogNameLabel.button.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
			_catalogNameLabel.button.transform.localPosition = new Vector3(-40f, (-_windowHeight / 2) + 10, 0f);
			_catalogNameLabel.button.onClick.AddListener(() => ToggleMinimize());
			SetTooltipForDynamicButton(_catalogNameLabel, () => (_minimizeUi.val ? "Maximize" : "Minimize") + "/Move");

			var positionTracker = new DragHelper();
			_positionTrackers.Add("DragPanel", positionTracker);
			_onCatalogDragFinishedEvent = () =>
			{
				try
				{
					_catalogPositionX.val = positionTracker.CurrentPosition.x;
					_catalogPositionY.val = positionTracker.CurrentPosition.y;
				}
				catch (Exception e)
				{
					SuperController.LogError(e.ToString());
				}
			};
			Action onStartDraggingEvent = () =>
			{
				positionTracker.AllowDragX = IsAnchoredOnHUD(); // ...only allow dragging if anchored on HUD
				positionTracker.AllowDragY = IsAnchoredOnHUD();// ...only allow dragging if anchored on HUD
				positionTracker.IsIn3DSpace = !IsAnchoredOnHUD();
				positionTracker.XMultiplier = positionTracker.IsIn3DSpace ? 1f : -1f;
				positionTracker.YMultiplier = 1f;
			};
			positionTracker.AddMouseDraggingToObject(_catalogNameLabel.gameObject, _catalogUi.canvas.gameObject, true, true, onStartDraggingEvent, _onCatalogDragFinishedEvent);


		}

		private void CreateDynamicPanel_BackPanel()
		{
			_backPanel = CatalogUiHelper.CreatePanel(_windowContainer, _windowWidth, _windowHeight, -10, -10, new Color(0.1f, 0.1f, 0.1f, 0.99f), Color.clear);
			CatalogUiHelper.CreatePanel(_windowContainer, 60, _windowHeight, _windowWidth - 10, -10, new Color(0.02f, 0.02f, 0.02f, 0.99f), Color.clear);

			var positionTracker = new DragHelper();
			_positionTrackers.Add("DragPanel2", positionTracker);
			_onCatalogDragFinishedEvent = () =>
			{
				try
				{
					_catalogPositionX.val = positionTracker.CurrentPosition.x;
					_catalogPositionY.val = positionTracker.CurrentPosition.y;
				}
				catch (Exception e)
				{
					SuperController.LogError(e.ToString());
				}
			};
			Action onStartDraggingEvent = () =>
			{
				positionTracker.AllowDragX = IsAnchoredOnHUD(); // ...only allow dragging if anchored on HUD
				positionTracker.AllowDragY = IsAnchoredOnHUD();// ...only allow dragging if anchored on HUD
				positionTracker.IsIn3DSpace = !IsAnchoredOnHUD();
				positionTracker.XMultiplier = positionTracker.IsIn3DSpace ? 1f : -1f;
				positionTracker.YMultiplier = 1f;
			};
			positionTracker.AddMouseDraggingToObject(_backPanel.gameObject, _catalogUi.canvas.gameObject, true, true, onStartDraggingEvent, _onCatalogDragFinishedEvent);

		}

		private bool DynamicConfirm(string v)
		{
			throw new NotImplementedException();
		}

		private void SelectNextAtom()
		{
			_nextAtomIndex++;
			var atoms = SuperController.singleton.GetAtoms();
			if (_nextAtomIndex >= atoms.Count) _nextAtomIndex = 0;
			if (atoms.Count == 0) return;
			SuperController.singleton.SelectController(atoms[_nextAtomIndex].mainController);
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

		private Catalog LoadCatalogFromFile(string filePath)
		{
			try
			{
				if (string.IsNullOrEmpty(filePath)) return null;
				// Deserialize the file data...
				Catalog catalog = CatalogFileManager.LoadCatalog(filePath);
				var pathElements = filePath.Split('/');
				var catalogFileName = pathElements.Last();
				_lastCatalogDirectory = string.Join("/", pathElements.Take(pathElements.Length - 1).ToArray());
				//var catalogNameParts = catalogFileName.Substring(0, catalogFileName.Length - ("." + _fileExtension).Length); //catalogFileName.Split('.');
				var extension = "." + _fileExtension;
				var catalogName = catalogFileName.Substring(0, catalogFileName.Length - extension.Length);
				pluginLabelJSON.val = catalogName;
				_catalogName.SetVal(catalogName);
				_catalogRelativePath.SetVal(filePath);

				UpdateCaptureHairState(catalog.CaptureHair);
				UpdateCaptureClothesState(catalog.CaptureClothes);
				UpdateCaptureMorphsState(catalog.CaptureMorphs);

				if (CatalogHasMessage(catalog)) ShowPopupMessage(catalog.ActiveVersionMessage.ShortMessage, 500);
				for (var i = 0; i < catalog.Entries.Count(); i++)
				{
					if (CannotLoad(catalog)) continue;
					var entry = catalog.Entries.ElementAt(i);
					BuildCatalogEntry(catalog.Entries.ElementAt(i));
				}
				SuperController.LogMessage("Loaded catalog from " + filePath);
				return catalog;
			}
			catch (Exception e)
			{
				SuperController.LogError(e.ToString());
				throw (e);
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

		public string GetScenesDirectoryPath()
		{
			var dataPath = $"{Application.dataPath}";
			var currentLoadDir = SuperController.singleton.currentLoadDir;
			var pathElements = dataPath.Split('/');
			var scenePath = pathElements.Take(pathElements.Length - 1).ToList().Aggregate((a, b) => $"{a}/{b}") + "/" + currentLoadDir;
			return scenePath;
		}

		public string GetSceneDirectoryPath()
		{
			var dataPath = $"{Application.dataPath}";
			SuperController.LogMessage("dataPath: " + dataPath);
			var currentLoadDir = SuperController.singleton.currentLoadDir;
			SuperController.LogMessage("currentLoadDir: " + currentLoadDir);
			var pathElements = dataPath.Split('/');
			var scenePath = currentLoadDir;
			//var scenePath = pathElements.Take(pathElements.Length - 1).ToList().Aggregate((a, b) => $"{a}/{b}") + "/" + currentLoadDir;
			SuperController.LogMessage("scenePath: " + scenePath);
			return scenePath;
		}

		private void ZoomOutCatalog()
		{
			_catalogEntryFrameSize.SetVal(_catalogEntryFrameSize.val - 50);
			_catalog.Entries.ForEach(e => ResizeCatalogEntry(e, (int)_catalogEntryFrameSize.val));
		}

		private void ZoomInCatalog()
		{
			_catalogEntryFrameSize.SetVal(_catalogEntryFrameSize.val + 50);
			_catalog.Entries.ForEach(e => ResizeCatalogEntry(e, (int)_catalogEntryFrameSize.val));
		}

		private void ResizeCatalogEntry(CatalogEntry catalogEntry, int newSize)
		{
			var catalogEntryPanel = catalogEntry.UiCatalogEntryPanel.GetComponents<RectTransform>().First();
			catalogEntryPanel.sizeDelta = new Vector2(newSize, newSize);

			var applyButtonRect = catalogEntry.UiApplyButton.GetComponents<RectTransform>().First();
			applyButtonRect.sizeDelta = new Vector2(10, newSize);
			CatalogUiHelper.SetAnchors(catalogEntry.UiCatalogEntryPanel, catalogEntry.UiApplyButton.gameObject, "bottom");

			var btnHeight = _catalogEntryFrameSize.val / 5;
			var btnWidth = (_catalogEntryFrameSize.val / 5) - _catalogEntryFrameSize.val / 50;
			var discardButtonRect = catalogEntry.UiDiscardButton.GetComponents<RectTransform>().First();
			discardButtonRect.sizeDelta = new Vector2(btnWidth, btnHeight);
			var keepButtonRect = catalogEntry.UiKeepButton.GetComponents<RectTransform>().First();
			keepButtonRect.sizeDelta = new Vector2(btnWidth, btnHeight);
			var selectButtonRect = catalogEntry.UiApplyButton.GetComponents<RectTransform>().First();
			selectButtonRect.sizeDelta = new Vector2(btnWidth, btnHeight);
			CatalogUiHelper.SetAnchors(catalogEntry.UiCatalogEntryPanel, catalogEntry.UiBottomButtonGroup, "bottom", 10, 0);

			_catalogRowsVLayout.spacing = relativeBorderWidth;
			_catalogColumnsHLayout.spacing = relativeBorderWidth;

			ResizeBorders(catalogEntry, newSize);
			ResetScrollPosition();
		}

		private void ResizeBorders(CatalogEntry catalogEntry, float newSize)
		{
			float originRatio = newSize / defaultFrameSize;
			relativeBorderWidth = (int)(originRatio * defaultBorderWidth);
			float height = newSize;
			float width = newSize;
			float offsetX = 0;
			float offsetY = 0;
			ResizeBorder(catalogEntry.UiCatalogBorder.LeftBorder, relativeBorderWidth, height + relativeBorderWidth + offsetY, width / -2 - offsetX, 0);
			ResizeBorder(catalogEntry.UiCatalogBorder.RightBorder, relativeBorderWidth, height + relativeBorderWidth + offsetY, width / 2 + offsetX, 0);
			ResizeBorder(catalogEntry.UiCatalogBorder.TopBorder, width + relativeBorderWidth + offsetX, relativeBorderWidth, 0, height / -2 - offsetY);
			ResizeBorder(catalogEntry.UiCatalogBorder.BottomBorder, width + relativeBorderWidth + offsetX, relativeBorderWidth, 0, height / 2 + offsetY);
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
			Action onStartDraggingEvent = () =>
			{
				positionTracker.IsIn3DSpace = !IsAnchoredOnHUD();
				positionTracker.XMultiplier = -1000f;
				positionTracker.YMultiplier = 1000f;
			};
			positionTracker.AddMouseDraggingToObject(_floatingTriggerButton.button.gameObject, _floatingTriggerButton.button.gameObject, true, true, onStartDraggingEvent);
		}

		void Update()
		{

			try
			{
				if (_atomType == ATOM_TYPE_PERSON) _mutationsService.Update();

				ManageScreenshotCaptureSequence();

				if (_catalogNameLabel.buttonText.text != _catalogName.val)
				{
					_catalogNameLabel.buttonText.text = _catalogName.val;
				}
				//ManagePopupMessage();

				//if (parentAtom == null) return;
				foreach (var positionTracker in _positionTrackers)
				{
					positionTracker.Value.Update();
				}
				foreach (var catalogEntry in _catalog.Entries)
				{
					catalogEntry.PositionTracker.Update();
				}
				if (_catalogUi != null) _catalogUi.Update();
				if (_floatingControlsUi != null) _floatingControlsUi.Update();
			}
			catch (Exception e)
			{
				SuperController.LogError(e.ToString());
			}
		}

		//private void ManagePopupMessage()
		//{
		//	if (_popupMessageFramesLeft < 0) return;
		//	if (--_popupMessageFramesLeft < 1) HidePopupMessage();
		//}

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
			if (_catalogMode.val == Enums.CATALOG_MODE_OBJECT
				|| _catalogMode.val == Enums.CATALOG_MODE_SESSION
				|| _catalogMode.val == Enums.CATALOG_MODE_CAPTURE)
			{
				var lastEntry = _catalog.Entries.LastOrDefault();
				if (lastEntry != null) SelectCatalogEntry(_catalog.Entries.Last());
			}
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
						if (mutation != null) TakeScreenshotAndCreateClickableCatalogEntry(mutation);
					}

					// Tear down scene...
					if (_createCatalogEntry_Step == 3)
					{
						--_currentCaptureRequest.CatalogEntriesStillLeftToCreate;
						_createCatalogEntry_Step = 0;
						_catalogUi.canvas.transform.localPosition = _rightCatalogUiPosition;
						_floatingControlsUi.canvas.transform.localPosition = _floatingControlUiPosition;
						if (_hudWasVisible) SuperController.singleton.ShowMainHUD();
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

		private void ApplyCatalogEntry(CatalogEntry catalogEntry)
		{
			SelectCatalogEntry(catalogEntry);
			if (catalogEntry.ApplyAction != null)
			{
				catalogEntry.ApplyAction.Invoke(catalogEntry);
				return;
			}
			DefaultCatalogEntrySelectAction(catalogEntry);
		}

		private void SelectCatalogEntry(CatalogEntry catalogEntry)
		{
			_catalog.Entries.ForEach(DeselectCatalogEntry);
			catalogEntry.Selected = true;
			_catalog.Entries.ForEach(UpdateCatalogEntryBorderColorBasedOnState);
			_currentCatalogEntry = catalogEntry;

			RemoveItemToggles();
			AddItemToggles(catalogEntry);
		}

		private void RemoveItemToggles()
		{
			foreach (var entry in _catalog.Entries)
			{
				foreach (var infoToggle in entry.InfoToggles)
				{
					SuperController.LogMessage("removing atom: " + infoToggle.ItemActiveCheckbox.label);
					RemoveUiCatalogSubItem(infoToggle);
				}
				entry.InfoToggles = new List<UiCatalogSubItem>();
			}
		}

		private void AddItemToggles(CatalogEntry catalogEntry)
		{


			if (_catalogMode.val == Enums.CATALOG_MODE_SESSION)
			{
				for (var i = 0; i < catalogEntry.Mutation.StoredAtoms.Count; i++)
				{
					var storedAtom = catalogEntry.Mutation.StoredAtoms[i];
					UnityAction<bool> onToggleAction = (value) =>
					{
						var atom = SuperController.singleton.GetAtomByUid(storedAtom.AtomName);
						atom.SetOn(value);
						storedAtom.Active = value;
					};
					UnityAction<string> stopTracking = (atomName) =>
					{
						var atomToRemove = catalogEntry.Mutation.StoredAtoms.FirstOrDefault(a => a.AtomName == atomName);
						catalogEntry.Mutation.StoredAtoms.Remove(atomToRemove);
						SelectCatalogEntry(catalogEntry);
					};
					var currentAtomState = SuperController.singleton.GetAtomByUid(storedAtom.AtomName);
					UiCatalogSubItem infoToggle = AddInfoCheckbox(storedAtom.AtomName, storedAtom.Active, onToggleAction, stopTracking);
					catalogEntry.InfoToggles.Add(infoToggle);
				}
			}
		}

		private void DefaultCatalogEntrySelectAction(CatalogEntry catalogEntry)
		{
			_mutationsService.UndoPreviousMutation();
			var mutation = catalogEntry.Mutation;
			_mutationsService.ApplyMutation(ref mutation);
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
			_hudWasVisible = SuperController.singleton.mainHUD.gameObject.activeSelf;
			if (_hudWasVisible) SuperController.singleton.HideMainHUD();
			_floatingControlUiPosition = _floatingControlsUi.canvas.transform.localPosition;
			_floatingControlsUi.canvas.transform.localPosition = new Vector3(10, 0, 0);
			_floatingControlsUi.Update();
			_rightCatalogUiPosition = _catalogUi.canvas.transform.localPosition;
			_catalogUi.canvas.transform.localPosition = new Vector3(10, 0, 0);
			_catalogUi.Update();
		}

		private CatalogEntry TakeScreenshotAndCreateClickableCatalogEntry(Mutation mutation, Action<CatalogEntry> customAction = null)
		{
			try
			{
				RenderTexture renderTexture = _captureCamera.targetTexture;

				Texture2D texture = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGB24, false);
				//Texture2D texture = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.DXT1, false);

				float captureCropLeft = 150;
				float captureCropBottom = 40;
				Rect rect = new Rect(captureCropLeft, captureCropBottom, 960, 960);
				texture.ReadPixels(rect, 20, 20);
				texture.Compress(true);
				texture.Apply();

				var imageInfo = new ImageInfo();
				imageInfo.Texture = texture;
				imageInfo.Width = _captureWidth;
				imageInfo.Height = _captureHeight;
				imageInfo.Format = TextureFormat.DXT1;

				CatalogEntry newCatalogEntry = CreateEntryInCatalog(mutation, imageInfo, customAction);

				RenderTexture.ReleaseTemporary(renderTexture);
				_captureCamera.targetTexture = _originalCameraTexture;

				return newCatalogEntry;
			}
			catch (Exception e)
			{
				SuperController.LogError(e.ToString());
				throw (e);
			}
		}

		private string EncodedImageFromTexture(Texture2D renderResult)
		{
			byte[] data = renderResult.GetRawTextureData();
			var encodedImage = Convert.ToBase64String(data);
			return encodedImage;
		}

		//private Texture2D TextureFrombRawData(byte[] rawData, int width = 1000, int height = 1000, TextureFormat textureFormat = TextureFormat.RGB24)
		//{
		//	try
		//	{
		//		Texture2D texture = new Texture2D(width, height, textureFormat, false);
		//		texture.LoadRawTextureData(rawData);
		//		return texture;
		//	}
		//	catch (Exception e)
		//	{
		//		SuperController.LogError(e.ToString());
		//		throw e;
		//	}
		//}

		private CatalogEntry CreateEntryInCatalog(Mutation mutation, ImageInfo imageInfo, Action<CatalogEntry> customAction = null)
		{
			var catalogEntry = new CatalogEntry();
			catalogEntry.UniqueName = GetUniqueName();
			catalogEntry.CatalogMode = _catalogMode.val;
			//catalogEntry.ImageAsTexture = imageCapture;
			catalogEntry.ImageInfo = imageInfo;
			catalogEntry.Mutation = mutation;
			BuildCatalogEntry(catalogEntry);
			return catalogEntry;
		}

		private CatalogEntry BuildCatalogEntry(CatalogEntry catalogEntry, Action<CatalogEntry> customAction = null)
		{
			try
			{
				//catalogEntry.ImageAsEncodedString = EncodedImageFromTexture(catalogEntry.ImageAsTexture);
				// Create main image panel...
				GameObject catalogEntryPanel = _catalogUi.CreateImagePanel(_windowContainer, catalogEntry.ImageInfo.Texture, (int)_catalogEntryFrameSize.val, (int)_catalogEntryFrameSize.val, 0, 0);
				//GameObject catalogEntryPanel = UIHelper.CreatePanel(_windowContainer, (int)_catalogEntryFrameSize.val, (int)_catalogEntryFrameSize.val, 0, 0, Color.red, Color.clear, catalogEntry.ImageAsTexture);
				catalogEntry.UiCatalogEntryPanel = catalogEntryPanel;
				// Add indication borders...
				catalogEntry.UiCatalogBorder = _catalogUi.CreateBorders(catalogEntryPanel, (int)_catalogEntryFrameSize.val, (int)_catalogEntryFrameSize.val, defaultBorderColor, 0, 0, relativeBorderWidth);

				_catalog.Entries.Add(catalogEntry);
				AddEntryToCatalog(catalogEntry, _catalog.Entries.Count - 1);

				int btnHeight = (int)_catalogEntryFrameSize.val / 6;
				int btnWidth = (int)((_catalogEntryFrameSize.val / 6) - _catalogEntryFrameSize.val / 50);
				AddEntrySelectionOverlay(catalogEntry, catalogEntryPanel);
				GameObject leftButtonGroup = CreateEntryLeftButtonGroup(catalogEntry, catalogEntryPanel, btnHeight);
				GameObject botttomButtonGroup = CreateEntryBottomButtonGroup(catalogEntry, catalogEntryPanel, btnHeight);
				AddEntryApplyButton(catalogEntry, customAction, btnHeight, btnWidth, botttomButtonGroup);
				AddEntryFavoriteButton(catalogEntry, btnHeight, btnWidth, botttomButtonGroup);
				AddEntryDiscardButton(catalogEntry, btnHeight, btnWidth, botttomButtonGroup);

				//ResizeBackpanel();
				return catalogEntry;
			}
			catch (Exception e)
			{
				SuperController.LogError(e.ToString());
				throw e;
			}
		}

		private GameObject CreateEntryLeftButtonGroup(CatalogEntry catalogEntry, GameObject catalogEntryPanel, int btnHeight)
		{
			GameObject buttonGroup = CreateButtonColumn(catalogEntryPanel, btnHeight);
			// Create container for sub-frame buttons...
			catalogEntry.UiBottomButtonGroup = buttonGroup;
			CatalogUiHelper.SetAnchors(catalogEntry.UiCatalogEntryPanel, catalogEntry.UiBottomButtonGroup, "left", 35, 90);
			return buttonGroup;
		}

		private GameObject CreateEntryBottomButtonGroup(CatalogEntry catalogEntry, GameObject catalogEntryPanel, int btnHeight)
		{
			GameObject buttonGroup = CreateButtonRow(catalogEntryPanel, btnHeight);
			// Create container for sub-frame buttons...
			catalogEntry.UiBottomButtonGroup = buttonGroup;
			CatalogUiHelper.SetAnchors(catalogEntry.UiCatalogEntryPanel, catalogEntry.UiBottomButtonGroup, "bottom", 10, 0);
			return buttonGroup;
		}

		private void AddEntryDiscardButton(CatalogEntry catalogEntry, int btnHeight, int btnWidth, GameObject buttonGroup)
		{
			// Reject button
			var baseButtonColor = new Color(0.7f, 0.7f, 0.7f, 0.5f);
			var rejectButtonHighlightedColor = new Color(0.5f, 0.0f, 0.0f, 1f);
			var rejectButtonTexture = TextureLoader.LoadTexture(GetPluginPath() + "/Resources/Delete.png");
			UIDynamicButton discardButton = _catalogUi.CreateButton(buttonGroup, "", btnWidth, btnHeight, 0, 0, baseButtonColor, rejectButtonHighlightedColor, Color.white, rejectButtonTexture);
			catalogEntry.UiDiscardButton = discardButton;
			discardButton.button.onClick.AddListener(() =>
			{
				catalogEntry.Discarded = !catalogEntry.Discarded;
				catalogEntry.Favorited = 0;
				catalogEntry.UiKeepButton.buttonText.text = "";
				UpdateCatalogEntryBorderColorBasedOnState(catalogEntry);
				catalogEntry.UiDiscardButton.buttonColor = catalogEntry.Discarded ? Color.red : baseButtonColor;
				if (catalogEntry.Discarded) catalogEntry.UiKeepButton.buttonColor = baseButtonColor;
			});
			SetTooltipForDynamicButton(discardButton, () => "Reject / Remove Likes (Use 'Sort' to remove)");
		}

		private void AddEntryFavoriteButton(CatalogEntry catalogEntry, int btnHeight, int btnWidth, GameObject buttonGroup)
		{
			// Favorite button
			var baseButtonColor = new Color(0.7f, 0.7f, 0.7f, 0.5f);
			var acceptButtonHighlightedColor = new Color(0.0f, 0.5f, 0.0f, 1f);
			var acceptButtonTexture = TextureLoader.LoadTexture(GetPluginPath() + "/Resources/Favorite.png");
			UIDynamicButton keepButton = _catalogUi.CreateButton(buttonGroup, "", btnWidth, btnHeight, 0, 0, baseButtonColor, acceptButtonHighlightedColor, Color.white, acceptButtonTexture);
			catalogEntry.UiKeepButton = keepButton;
			catalogEntry.UiKeepButton.buttonText.fontSize = 15;
			keepButton.button.onClick.AddListener(() =>
			{
				catalogEntry.Discarded = false;
				catalogEntry.Favorited += 1;
				catalogEntry.UiKeepButton.buttonText.text = "+" + catalogEntry.Favorited;
				UpdateCatalogEntryBorderColorBasedOnState(catalogEntry);
				catalogEntry.UiKeepButton.buttonColor = new Color(0, 0.7f, 0);
			});
			SetTooltipForDynamicButton(keepButton, () => "Add Like (Use 'Sort' to reorder)");
		}

		private void AddEntryApplyButton(CatalogEntry catalogEntry, Action<CatalogEntry> customAction, int btnHeight, int btnWidth, GameObject buttonGroup)
		{
			// Apply button
			var baseButtonColor = new Color(0.7f, 0.7f, 0.7f, 0.7f);
			var applyButtonHighlightedColor = new Color(1f, 0.647f, 0f, 1f);
			var applyButtonTexture = TextureLoader.LoadTexture(GetPluginPath() + "/Resources/Apply.png");
			UIDynamicButton applyButton = _catalogUi.CreateButton(buttonGroup, "", btnWidth, btnHeight, 0, 0, baseButtonColor, applyButtonHighlightedColor, Color.white, applyButtonTexture);
			catalogEntry.UiApplyButton = applyButton;
			SetTooltipForDynamicButton(catalogEntry.UiApplyButton, () => "Apply");
			catalogEntry.ApplyAction = GetAppropriateApplyAction(catalogEntry, customAction);
			catalogEntry.UiApplyButton.button.onClick.AddListener(() => {
				// Unhighlight the other apply button...
				_catalog.Entries.ForEach(e => e.UiApplyButton.buttonColor = new Color(0.7f, 0.7f, 0.7f, 1f));
				// Highlight this apply button...
				catalogEntry.UiApplyButton.buttonColor = new Color(1f, 0.647f, 0f, 1f);
				// Apply...
				ApplyCatalogEntry(catalogEntry);
			});
		}

		private void AddEntrySelectionOverlay(CatalogEntry catalogEntry, GameObject catalogEntryPanel)
		{
			// Select button
			//var selectButtonColor = new Color(0.5f, 0.5f, 0.5f, 0f);
			var selectButtonColor = new Color(1f, 0.5f, 0.5f, 0.5f);
			var selectButtonHighlightColor = selectButtonColor; //new Color(0.0f, 0.0f, 0.5f, 0.2f); ;
			UIDynamicButton selectButton = _catalogUi.CreateButton(catalogEntryPanel, "", 0, (int)_catalogEntryFrameSize.val, 0, 0, selectButtonColor, selectButtonHighlightColor, Color.white);
			catalogEntry.UiSelectButton = selectButton;
			catalogEntry.UiSelectButton.button.onClick.AddListener(() => SelectCatalogEntry(catalogEntry));
			SetTooltipForDynamicButton(catalogEntry.UiSelectButton, () => "Select");

			//Setup Drag-Scrolling for Apply button (allow click and drag)...
			catalogEntry.PositionTracker = new DragHelper();
			Action onStartDraggingEvent = () =>
			{
				if (_expandDirection.val == EXPAND_WITH_MORE_ROWS)
				{
					catalogEntry.PositionTracker.AllowDragX = false;
					catalogEntry.PositionTracker.AllowDragY = true;
					catalogEntry.PositionTracker.ObjectToDrag = _rowContainer;
					catalogEntry.PositionTracker.LimitX = null;
					catalogEntry.PositionTracker.LimitY = new Vector2((_catalogRows.Count * _catalogEntryFrameSize.val) - _catalogEntryFrameSize.val, _catalogEntryFrameSize.val);
				}
				else
				{
					catalogEntry.PositionTracker.AllowDragX = true;
					catalogEntry.PositionTracker.AllowDragY = false;
					catalogEntry.PositionTracker.ObjectToDrag = _columnContainer;
					catalogEntry.PositionTracker.LimitX = new Vector2(-_catalogColumns.Count * _catalogEntryFrameSize.val + (_catalogEntryFrameSize.val), 0);
					catalogEntry.PositionTracker.LimitY = null;
				}
				catalogEntry.PositionTracker.IsIn3DSpace = !IsAnchoredOnHUD();
				catalogEntry.PositionTracker.XMultiplier = -1000f;
				catalogEntry.PositionTracker.YMultiplier = 1000f;
			};
			Func<float, float, bool> onWhileDraggingEvent = (newX, newY) =>
			{
				// TODO:
				// Make overflow catalog entry dissapear.
				return true;
			};
			catalogEntry.PositionTracker.AddMouseDraggingToObject(catalogEntry.UiSelectButton.gameObject, _columnContainer, false, true, onStartDraggingEvent, null, onWhileDraggingEvent); // allow user to drag-scroll using this button aswell				
																																																																																											// Add mouse-over event...																																																																																				 // Create mouse-down event...
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
				if (catalogEntry.CatalogMode == Enums.CATALOG_MODE_SESSION || catalogEntry.CatalogMode == Enums.CATALOG_MODE_OBJECT)
				{
					var objectNames = catalogEntry.Mutation.StoredAtoms.Select(a => a.AtomName).ToArray();
					text = string.Join("\n", objectNames);
				}
				if (catalogEntry.CatalogMode == Enums.CATALOG_MODE_CAPTURE)
				{
					var clothingItems = catalogEntry.Mutation.ClothingItems.Select(c => "clothing: " + c.DAZClothingItemName).ToArray();
					var hairItems = catalogEntry.Mutation.HairItems.Select(c => "hair: " + c.DAZHairGroupName).ToArray();
					var morphItems = catalogEntry.Mutation.ActiveMorphs.Select(c => "morph: " + c.Name).ToArray();
					text = $"{string.Join("\n", clothingItems)}\n{string.Join("\n", hairItems)}\n{string.Join("\n", morphItems)}"; ;
				}
				if (catalogEntry.CatalogMode == Enums.CATALOG_MODE_MUTATIONS)
				{
					var morphItems = catalogEntry.Mutation.FaceGenMorphSet.Select(c => "morph: " + c.Name).ToArray();
					text = $"{string.Join("\n", morphItems)}";
				}
				_infoLabel.text = text;
				_infoLabel.transform.localScale = string.IsNullOrEmpty(text) ? Vector3.zero : Vector3.one;
			});
			triggerPointerEnter.triggers.RemoveAll(t => t.eventID == EventTriggerType.PointerEnter);
			triggerPointerEnter.triggers.Add(pointerEnter);

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
				_infoLabel.text = "";
				_infoLabel.transform.localScale = Vector3.zero;
			});
			triggerPointerExit.triggers.RemoveAll(t => t.eventID == EventTriggerType.PointerExit);
			triggerPointerExit.triggers.Add(pointerExit);

		}

		private Action<CatalogEntry> GetAppropriateApplyAction(CatalogEntry catalogEntry, Action<CatalogEntry> customAction)
		{
			if (customAction != null) return customAction;
			if (catalogEntry.CatalogMode == Enums.CATALOG_MODE_OBJECT) return (TheCatalogEntry) => CreateAtomsForCatalogEntry(TheCatalogEntry);
			if (catalogEntry.CatalogMode == Enums.CATALOG_MODE_SESSION) return (TheCatalogEntry) => CreateAtomsForCatalogEntry(TheCatalogEntry);
			if (catalogEntry.Mutation.ScenePathToOpen != null) return (TheCatalogEntry) => SuperController.singleton.Load(catalogEntry.Mutation.ScenePathToOpen);
			return (TheCatalogEntry) => DefaultCatalogEntrySelectAction(TheCatalogEntry);
		}

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
			var color = defaultBorderColor;
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
			CatalogUiHelper.AddBorderColorToTexture(newCatalogEntry.UiCatalogBorder.texture, color);
			// Selected
			if (newCatalogEntry.Selected)
			{
				CatalogUiHelper.AddBorderColorToTexture(newCatalogEntry.UiCatalogBorder.texture, new Color(1, 0, 1, 0.8f), 1);
			}
		}

		void DestroyCatalogEntry(CatalogEntry catalogEntry)
		{
			Destroy(catalogEntry.UiApplyButton);
			Destroy(catalogEntry.UiKeepButton);
			Destroy(catalogEntry.UiBottomButtonGroup);
			Destroy(catalogEntry.UiDiscardButton);
			Destroy(catalogEntry.UiSelectButton);
			Destroy(catalogEntry.UiCatalogEntryPanel);
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

		GameObject CreateButtonRow(GameObject parentPanel, int height, bool horizontal = true)
		{
			int spacer = 10;
			var subPanel = _catalogUi.CreateUIPanel(parentPanel, 0, height, "bottom", 0, 0, new Color(0.25f, 0.25f, 0.25f, 0.25f));
			if (horizontal) _catalogUi.CreateHorizontalLayout(subPanel, spacer, false, false, false, false);
			else _catalogUi.CreateVerticalLayout(subPanel, spacer, false, false, false, false);
			ContentSizeFitter psf = subPanel.AddComponent<ContentSizeFitter>();
			psf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
			psf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
			return subPanel;
		}

		private void AddEntryToCatalog(CatalogEntry newCatalogEntry, int imageIndex)
		{
			if (_expandDirection.val == EXPAND_WITH_MORE_ROWS)
			{
				int appropriateRowIndex = (int)(imageIndex / _catalogColumnsCountJSON.val);
				if (appropriateRowIndex >= _catalogRows.Count - 1) _catalogRows.Add(CreateCatalogRow());
				var catalogRow = _catalogRows[appropriateRowIndex];
				newCatalogEntry.UiParentCatalogRow = catalogRow;
				newCatalogEntry.UiCatalogEntryPanel.transform.SetParent(catalogRow.transform);
			}
			else // EXPAND_WITH_MORE_COLUMNS
			{
				int appropriateColIndex = (int)(imageIndex / _catalogRowsCountJSON.val);
				if (appropriateColIndex >= _catalogColumns.Count - 1) _catalogColumns.Add(CreateCatalogColumn());
				var catalogColumn = _catalogColumns[appropriateColIndex];
				newCatalogEntry.UiParentCatalogColumn = catalogColumn;
				newCatalogEntry.UiCatalogEntryPanel.transform.SetParent(catalogColumn.transform);
			}
			ResetScrollPosition();
		}

		private void ResetScrollPosition()
		{
			float rowCount = _catalog.Entries.Count() >= _catalogRowsCountJSON.val ? _catalogRowsCountJSON.val : _catalog.Entries.Count();
			var rowHeight = _catalogEntryFrameSize.val * rowCount;
			//var newX = (_rowContainer.transform.localPosition.x + rowHeight < 0 ) ? 0 : _rowContainer.transform.localPosition.x;
			_rowContainer.transform.localPosition = new Vector3(_rowContainer.transform.localPosition.x, rowHeight + (relativeBorderWidth * 2) + 10, _rowContainer.transform.localPosition.z);
			_columnContainer.transform.localPosition = new Vector3(_columnContainer.transform.localPosition.x, rowHeight + (relativeBorderWidth * 2) + 10, _rowContainer.transform.localPosition.z);
		}

		GameObject CreateCatalogColumn()
		{
			var col = _catalogUi.CreateUIPanel(_windowContainer, 400, 400, "left", 0, 0, Color.clear);
			_catalogUi.CreateVerticalLayout(col, 100.0f, false, false, false, false);
			//ContentSizeFitter psf = col.AddComponent<ContentSizeFitter>();
			//psf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
			//psf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
			col.transform.SetParent(_catalogColumnsHLayout.transform, false);
			return col;
		}

		GameObject CreateCatalogRow()
		{
			var row = _catalogUi.CreateUIPanel(_windowContainer, 400, 400, "left", 0, 0, Color.clear);
			_catalogUi.CreateHorizontalLayout(row, 100.0f, false, false, false, false);
			//ContentSizeFitter psf = row.AddComponent<ContentSizeFitter>();
			//psf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
			//psf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
			row.transform.SetParent(_catalogRowsVLayout.transform, false);
			//RectTransform rect = _rowContainer.GetComponent<RectTransform>();
			//_catalogRowsVLayout.transform.position = _catalogRowsVLayout.transform.position - new Vector3(-400f, 0f, 0f);
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
				//atom.transform.position = position;
				//atom.transform.rotation = rotation;
			}
			if (atom != null)
			{
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

				foreach (var catalogAtom in catalogEntry.Mutation.StoredAtoms)
				{
					CreateAtom(catalogAtom, Vector3.zero, Quaternion.identity);
				}
				HidePopupMessage();
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

		private void CreateAtom(StoredAtom storedAtom, Vector3 position, Quaternion rotation)
		{
			string newAtomName = GetNextAvailableName(storedAtom.AtomName, storedAtom.AtomType);
			this.StartCoroutine(this.CreateAtom(storedAtom.AtomType, newAtomName, position, rotation, newAtom =>
			{
				newAtom.collisionEnabledJSON.SetVal(false);
				try
				{
					/// Restore Storables...
					foreach (var storable in storedAtom.Storables)
					{
						try
						{
							var storeId = storable["id"];// SerializerService_3_0_1.LoadStringFromJsonStringProperty(storable, "id", null);
							if (storeId == null) continue;
							var existingStorable = newAtom.GetStorableByID(storeId);
							if (existingStorable != null)
							{
								existingStorable.RestoreFromJSON(storable);
							}
							else
							{
								var unregisteredStorable = GetNewStorableFromJSONClass(storable);
								unregisteredStorable.containingAtom = newAtom;
								newAtom.RegisterAdditionalStorable(unregisteredStorable);
							}
						}
						catch (Exception e)
						{
							SuperController.LogError(e.ToString());
						}
					}
					SuperController.singleton.SelectController(newAtom.mainController);
					HidePopupMessage();
					StartCoroutine(EnableCollisions(newAtom));
				}
				catch (Exception e)
				{
					SuperController.LogError(e.ToString());
				}
			}));
		}

		IEnumerator EnableCollisions(Atom atom)
		{
			yield return new WaitForSeconds(2);
			atom.collisionEnabledJSON.SetVal(true);
		}

		private void CreateAtom(List<JSONClass> storables, string atomType, string atomName, Vector3 position, Quaternion rotation)
		{
			this.StartCoroutine(this.CreateAtom(atomType, atomName, position, rotation, newAtom =>
			{
				try
				{
					newAtom.collisionEnabledJSON.SetVal(true);
					/// Restore Storables...
					foreach (var storable in storables)
					{
						try
						{
							var storeId = storable["id"]; //SerializerService_3_0_1.LoadStringFromJsonStringProperty(storable, "id", null);
							if (storeId == null) continue;
							var existingStorable = newAtom.GetStorableByID(storeId);
							if (existingStorable != null)
							{
								existingStorable.RestoreFromJSON(storable);
							}
							else
							{
								var unregisteredStorable = GetNewStorableFromJSONClass(storable);
								unregisteredStorable.containingAtom = newAtom;
								newAtom.RegisterAdditionalStorable(unregisteredStorable);
							}
						}
						catch (Exception e)
						{
							SuperController.LogError(e.ToString());
						}
					}
					SuperController.singleton.SelectController(newAtom.mainController);
					HidePopupMessage();
				}
				catch (Exception e)
				{
					SuperController.LogError(e.ToString());
				}
			}));
		}

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

		private string GetNextAvailableName(string atomName, string atomType)
		{
			var suggestedName = atomName ?? atomType ?? GetUniqueName();
			var sceneAtoms = SuperController.singleton.GetAtoms();
			string newAtomName = suggestedName;
			int instanceCount = 2;
			while (sceneAtoms.Any(a => a.name == newAtomName))
			{
				if (++instanceCount > 10000) throw new Exception("Overflow");
				newAtomName = suggestedName + "#" + instanceCount;
			}
			return newAtomName;
		}

		private void CenterPivot(Atom atom)
		{
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

			var hipController = atom.GetComponentsInChildren<FreeControllerV3>().FirstOrDefault(c => c.name == "hipControl");

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
			if (containingAtom.name == "CoreControl") return "Custom/Scripts/Catalog";
			string pluginId = storeId.Split('_')[0];
			MVRPluginManager manager = containingAtom.GetStorableByID("PluginManager") as MVRPluginManager;
			string pathToScriptFile = manager.GetJSON(true, true)["plugins"][pluginId].Value;
			string pathToScriptFolder = pathToScriptFile.Substring(0, pathToScriptFile.LastIndexOfAny(new char[] { '/', '\\' }));
			return pathToScriptFolder;
		}

		void OnDestroy()
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
		}

	}
}
