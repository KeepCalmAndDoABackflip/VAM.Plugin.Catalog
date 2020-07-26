
using CataloggerPlugin.Models;
using CataloggerPlugin.Services;
using PluginBuilder.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR;

namespace CataloggerPlugin.StatefullServices
{

	public class CataloggerPlugin : MVRScript
	{

		const string EXPAND_WITH_MORE_ROWS = "rows";
		const string EXPAND_WITH_MORE_COLUMNS = "columns";

		const string ATOM_TYPE_SESSION = "CoreControl";
		const string ATOM_TYPE_PERSON = "Person";
		const string ATOM_TYPE_ASSET = "CustomUnityAsset";
		const string ATOM_TYPE_OBJECT = "Other";
		string _atomType;

		const string CATALOG_MODE_VIEW = "View mode";
		const string CATALOG_MODE_CAPTURE = "Capture mode";
		const string CATALOG_MODE_MUTATIONS = "Face-Gen mode";
		const string CATALOG_MODE_SCENE = "Scenes-Directory mode";
		const string CATALOG_MODE_ASSET = "Asset mode";
		const string CATALOG_MODE_OBJECT = "Object mode";
		const string CATALOG_MODE_SESSION = "Session mode";

		string _fileExtension = "catalog";
		List<string> _catalogModes = new List<string>();

		#region PluginInfo
		public string pluginAuthor = "juniperD";
		public string pluginName = "Catalog";
		public string pluginVersion = SerializerService.HOST_PLUGIN_SYMANTIC_VERSION;
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
		protected bool _defaultMinimize = false;
		protected bool _vrMode = XRDevice.isPresent;

		// Screen-Capture control-state...
		protected Camera _captureCamera;
		protected bool _hudWasVisible;
		protected string _baseFileFormat;
		protected RenderTexture _originalCameraTexture;
		protected int _createCatalogEntry_Step = 0;
		protected int _skipFrames = 0;
		//protected UIDynamicButton _generateButton;
		//protected UIDynamicButton _captureButton;
		protected UIDynamicButton _floatingTriggerButton;
		protected UIDynamicButton _toolTipLabel;
		protected UIDynamicButton _popupMessageLabel;
		protected int _popupMessageFramesLeft = -1;
		protected UIDynamicButton _minimizeButton;
		protected UIDynamicButton _handleButton;
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
		Atom _parentAtom;
		DebugService _debugService;
		public MutationsService _mutationsService;
		UIHelper _catalogUi;
		UIHelper _floatingControlsUi;
		GameObject _windowContainer;
		GameObject _backPanel;

		GameObject _rowContainer;
		GameObject _columnContainer;

		UIDynamicButton _dynamicButtonSort;
		UIDynamicButton _dynamicButtonRefresh;

		UIDynamicButton _dynamicButtonCaptureMorphs;
		UIDynamicButton _dynamicButtonCaptureClothes;
		UIDynamicButton _dynamicButtonCaptureHair;

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

		Color _dynamicButtonCheckColor = new Color(1.0f, 1.0f, 1.0f, 1.0f);
		Color _dynamicButtonUnCheckColor = new Color(0.25f, 0.25f, 0.25f, 1f);
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
			_catalogName.valNoCallback = GetNewCatalogName();
			_catalogMode.val = CATALOG_MODE_VIEW;
			// Create a new file in which to save temporary catalog data for this catalog instance...
			var scenePath = GetSceneDirectoryPath();
			var filePath = scenePath + "/" + _catalogName.val + "." + _fileExtension;
			SaveCatalogToFile(filePath);

			// Set starting position next to HUD...
			//ResetCatalogPositionAtPerson();
			//AnchorOnAtom();
			ResetCatalogPositionAtHud();
			AnchorOnHud();

			if (_vrMode)
			{
				_captureCamera = Camera.allCameras.ToList().FirstOrDefault(c => c.name == "Camera (eye)");
			}

			UpdateUiForMode();
		}

		private string GetNewCatalogName()
		{
			// Create a unique name for this catalog instance (there may be multiple instances within a scene if the user adds more catalogs)
			return containingAtom.name + "." + Guid.NewGuid().ToString().Substring(0, 5);
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
					case "CustomUnityAsset":
						_atomType = ATOM_TYPE_ASSET;
						break;
					default:
						_atomType = ATOM_TYPE_OBJECT;
						break;
				}

				_catalogModes = new List<string>();
				_catalogModes.Add(CATALOG_MODE_VIEW);
				_catalogModes.Add(CATALOG_MODE_SCENE);
				if (_atomType == ATOM_TYPE_PERSON) _catalogModes.Add(CATALOG_MODE_CAPTURE);
				if (_atomType == ATOM_TYPE_PERSON) _catalogModes.Add(CATALOG_MODE_MUTATIONS);
				if (_atomType == ATOM_TYPE_ASSET) _catalogModes.Add(CATALOG_MODE_ASSET);
				if (_atomType == ATOM_TYPE_OBJECT) _catalogModes.Add(CATALOG_MODE_OBJECT);
				if (_atomType == ATOM_TYPE_SESSION) _catalogModes.Add(CATALOG_MODE_SESSION);

				_debugService = new DebugService();
				_debugService.Init(this);
				//_dynamicButtonCheckColor = new Color(0.5f, 0.5f, 1f, 1); 
				//_dynamicButtonUnCheckColor = new Color(0.25f, 0.25f, 0.25f, 1);

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

				_floatingControlsUi = new UIHelper(this, 0, 0, Color.clear);
				_floatingControlsUi.canvas.transform.Rotate(new Vector3(0, 180, 0));
				_catalogUi = new UIHelper(this, 0, 0, Color.clear);
				_catalogUi.canvas.transform.Rotate(new Vector3(0, 180, 0));
				_windowContainer = UIHelper.CreatePanel(_catalogUi.canvas.gameObject, 0, 0, 0, 0, Color.clear, Color.clear);

				_backPanel = UIHelper.CreatePanel(_windowContainer, 50, 50, 0, 0, new Color(0.1f, 0.1f, 0.1f, 0.9f), Color.clear);

				_rowContainer = _catalogUi.CreateUIPanel(_windowContainer, 0, 0, "topleft", 1, 1, Color.clear);
				_columnContainer = _catalogUi.CreateUIPanel(_windowContainer, 0, 0, "topleft", 1, 1, Color.clear);

				_catalogRowsVLayout = _catalogUi.CreateVerticalLayout(_rowContainer.gameObject, defaultBorderWidth);
				_catalogColumnsHLayout = _catalogUi.CreateHorizontalLayout(_columnContainer.gameObject, defaultBorderWidth);

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
			//CreateDynamicButton_GenerateCatalog();
			//CreateDynamicButton_CaptureControl();
			CreateDynamicButton_Trigger();

			// DYNAMIC UI...
			//Top Right Corner
			CreateDynamicButton_Minimize();
			CreateDynamicButton_Handle();
			// Left bar
			CreateDynamicButton_ZoomIn();
			CreateDynamicButton_ZoomOut();
			CreateDynamicButton_ScrollUp();
			CreateDynamicButton_ScrollDown();
			// Scene helpers...
			CreateDynamicButton_CycleAtoms();
			CreateDynamicButton_ResetPivot();
			// Top bar
			CreateDynamicButton_Tooltip();
			CreateDynamicButton_PopupMessage();
			CreateDynamicButton_Mode();
			CreateDynamicButton_Save();
			CreateDynamicButton_Load();
			CreateDynamicButton_Sort();
			CreateDynamicButton_Refresh();
			// Top Bar (Capture mode specific)
			CreateDynamicButton_Capture_Clothes();
			CreateDynamicButton_Capture_Morphs();
			CreateDynamicButton_Capture_Hair();
			//CreateDynamicButton_Reset();
		}

		private void CreateCaptureConfigUi()
		{

			CreateButton("Capture").button.onClick.AddListener(() =>
			{
				RequestNextCaptureSet(1, CaptureRequest.MUTATION_AQUISITION_MODE_CAPTURE);
			});

			_catalogCaptureHair = new JSONStorableBool("Capture Hair", true);
			RegisterBool(_catalogCaptureHair);
			CreateToggle(_catalogCaptureHair);
			_catalogCaptureHair.toggle.onValueChanged.AddListener((value) =>
			{
				_catalog.CaptureHair = value;
				_mutationsService.CaptureHair = value;
			});

			_catalogCaptureClothes = new JSONStorableBool("Capture Clothes", true);
			RegisterBool(_catalogCaptureClothes);
			CreateToggle(_catalogCaptureClothes);
			_catalogCaptureClothes.toggle.onValueChanged.AddListener((value) =>
			{
				_catalog.CaptureClothes = value;
				_mutationsService.CaptureClothes = value;
			});

			//_captureMutationMorphs = new JSONStorableBool("Capture generated morphs", true);
			//RegisterBool(_captureMutationMorphs);
			//CreateToggle(_captureMutationMorphs);
			//_captureMutationMorphs.toggle.onValueChanged.AddListener((value) =>
			//{
			//	_mutationsService.CaptureFaceGenMorphs = value;
			//});

			_catalogCaptureMorphs = new JSONStorableBool("Capture active morphs", true);
			RegisterBool(_catalogCaptureMorphs);
			CreateToggle(_catalogCaptureMorphs);
			_catalogCaptureMorphs.toggle.onValueChanged.AddListener((value) =>
			{
				_catalog.CaptureMorphs = value;
				_mutationsService.CaptureActiveMorphs = value;
			});

			_catalogCaptureDynamicItems = new JSONStorableBool("Capture dynamic items", true);
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
					_currentCatalogEntry.Mutation.ImageExternalPath = filePath;
					_currentCatalogEntry.Mutation.Img_RGB24_W1000H1000_64bEncoded = filePath;
					_currentCatalogEntry.ImageAsTexture = texture;
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
				if (!item.Active) RemoveToggle(item.UiToggle);
			}
			_currentCatalogEntry.Mutation.ClothingItems = _currentCatalogEntry.Mutation.ClothingItems.Where(c => c.Active).ToList();
			//-----------------------
			foreach (var item in _currentCatalogEntry.Mutation.HairItems)
			{
				if (!item.Active) RemoveToggle(item.UiToggle);
			}
			_currentCatalogEntry.Mutation.HairItems = _currentCatalogEntry.Mutation.HairItems.Where(c => c.Active).ToList();
			//-----------------------
			foreach (var item in _currentCatalogEntry.Mutation.ActiveMorphs)
			{
				if (!item.Active) RemoveToggle(item.UiToggle);
			}
			_currentCatalogEntry.Mutation.ActiveMorphs = _currentCatalogEntry.Mutation.ActiveMorphs.Where(c => c.Active).ToList();
			//-----------------------
			foreach (var item in _currentCatalogEntry.Mutation.FaceGenMorphSet)
			{
				if (!item.Active) RemoveToggle(item.UiToggle);
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
			_catalogPositionX.val = 0.38f; //-0.573f;
			_catalogPositionY.val = -0.1f; //0.92f;
		}

		private void ResetCatalog()
		{
			_catalog.Entries.ForEach(DestroyCatalogEntry);
			_catalog.Entries = new List<CatalogEntry>();
			_catalogRows.Skip(1).ToList().ForEach(p => Destroy(p));
			_catalogRows = _catalogRows.Take(1).ToList();
			ResizeBackpanel();
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
			//_generateButton.button.transform.localScale = Vector3.zero;
			//_captureButton.button.transform.localScale = Vector3.zero;
			_floatingTriggerButton.button.transform.localScale = Vector3.zero;
			_dynamicButtonSort.button.transform.localScale = Vector3.zero;
			_dynamicButtonRefresh.button.transform.localScale = Vector3.zero;
			_dynamicButtonCaptureMorphs.button.transform.localScale = Vector3.zero;
			_dynamicButtonCaptureHair.button.transform.localScale = Vector3.zero;
			_dynamicButtonCaptureClothes.button.transform.localScale = Vector3.zero;
			_dynamicButtonRefresh.button.onClick.RemoveAllListeners();

			if (_catalogMode.val == CATALOG_MODE_MUTATIONS)
			{
				_floatingTriggerButton.buttonText.text = "Generate Faces";
				if (_triggerButtonVisible.val) _floatingTriggerButton.button.transform.localScale = Vector3.one; //_generateButton.button.transform.localScale = Vector3.one;
				_catalog.Entries.ForEach(e => e.UiButtonGroup.transform.localScale = Vector3.one);
				_dynamicButtonSort.button.transform.localScale = Vector3.one;
				_dynamicButtonRefresh.buttonText.text = "Faces";
				_dynamicButtonRefresh.button.transform.localScale = Vector3.one;
				_dynamicButtonRefresh.button.onClick.AddListener(() => RequestNextCaptureSet((int)_catalogRequestedEntriesCountJSON.val, CaptureRequest.MUTATION_AQUISITION_MODE_SEQUENCE));
			}
			else if (_catalogMode.val == CATALOG_MODE_CAPTURE)
			{
				_floatingTriggerButton.buttonText.text = "Capture Scene";
				if (_triggerButtonVisible.val) _floatingTriggerButton.button.transform.localScale = Vector3.one; //_captureButton.button.transform.localScale = Vector3.one;
				_catalog.Entries.ForEach(e => e.UiButtonGroup.transform.localScale = Vector3.one);
				_dynamicButtonSort.button.transform.localScale = Vector3.one;
				_dynamicButtonRefresh.buttonText.text = "Capture";
				_dynamicButtonRefresh.button.transform.localScale = Vector3.one;
				_dynamicButtonCaptureMorphs.button.transform.localScale = Vector3.one;
				_dynamicButtonCaptureHair.button.transform.localScale = Vector3.one;
				_dynamicButtonCaptureClothes.button.transform.localScale = Vector3.one;
				_dynamicButtonRefresh.button.onClick.AddListener(() => RequestNextCaptureSet(1, CaptureRequest.MUTATION_AQUISITION_MODE_CAPTURE));
			}
			else if (_catalogMode.val == CATALOG_MODE_SCENE)
			{
				_catalog.Entries.ForEach(e => e.UiButtonGroup.transform.localScale = Vector3.zero);
				_dynamicButtonSort.button.transform.localScale = Vector3.one;
				_dynamicButtonRefresh.buttonText.text = "Refresh";
				_dynamicButtonRefresh.button.transform.localScale = Vector3.one;
				_dynamicButtonRefresh.button.onClick.AddListener(() => CreateSceneCatalogEntries());
			}
			else if (_catalogMode.val == CATALOG_MODE_ASSET)
			{
				_catalog.Entries.ForEach(e => e.UiButtonGroup.transform.localScale = Vector3.zero);
				_dynamicButtonSort.button.transform.localScale = Vector3.one;
				_dynamicButtonRefresh.buttonText.text = "Capture";
				_dynamicButtonRefresh.button.transform.localScale = Vector3.one;
				_dynamicButtonRefresh.button.onClick.AddListener(() => RequestNextCaptureSet(1, CaptureRequest.MUTATION_AQUISITION_MODE_CAPTURE_ASSET));
			}
			else if (_catalogMode.val == CATALOG_MODE_OBJECT)
			{
				_catalog.Entries.ForEach(e => e.UiButtonGroup.transform.localScale = Vector3.zero);
				_dynamicButtonSort.button.transform.localScale = Vector3.one;
				_dynamicButtonRefresh.buttonText.text = "Capture";
				_dynamicButtonRefresh.button.transform.localScale = Vector3.one;
				_dynamicButtonRefresh.button.onClick.AddListener(() => RequestNextCaptureSet(1, CaptureRequest.MUTATION_AQUISITION_MODE_CAPTURE_OBJECT));
			}
			else if (_catalogMode.val == CATALOG_MODE_SESSION)
			{
				_catalog.Entries.ForEach(e => e.UiButtonGroup.transform.localScale = Vector3.zero);
				_dynamicButtonSort.button.transform.localScale = Vector3.one;
				_dynamicButtonRefresh.buttonText.text = "Capture";
				_dynamicButtonRefresh.button.transform.localScale = Vector3.one;
				_dynamicButtonRefresh.button.onClick.AddListener(() => RequestNextCaptureSet(1, CaptureRequest.MUTATION_AQUISITION_MODE_CAPTURE_SESSION_OBJECT));
			}
			else if (_catalogMode.val == CATALOG_MODE_VIEW)
			{
				_catalog.Entries.ForEach(e => e.UiButtonGroup.transform.localScale = Vector3.zero);
				_dynamicButtonRefresh.button.onClick.AddListener(() => { });
			}
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
					if (imagePath.Length > 3) ;
					if (imagePath.Substring(imagePath.Length - 4) == ".jpg")
					{
						var jsonPath = imagePath.Substring(0, imagePath.Length - 4) + ".json";
						if (!fileEntries.Contains(jsonPath)) return;
						var stringData = SuperController.singleton.ReadFileIntoString(imagePath.Replace("\\", "/"));
						var texture = TextureLoader.LoadTexture(imagePath);
						texture.Apply();
						var catalogEntry = new CatalogEntry();
						catalogEntry.UniqueName = GetUniqueName();
						catalogEntry.CatalogMode = _catalogMode.val;
						catalogEntry.ImageAsTexture = texture;
						catalogEntry.Mutation = new Mutation();
						catalogEntry.Mutation.ImageExternalPath = imagePath;
						catalogEntry.Mutation.ScenePathToOpen = jsonPath;
						var builtCatalogEntry = BuildCatalogEntry(catalogEntry);
						builtCatalogEntry.Mutation.Img_RGB24_W1000H1000_64bEncoded = imagePath;
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


			var minimizeBtnRect = _minimizeButton.GetComponent<RectTransform>();
			var btnWidth = string.IsNullOrEmpty(pluginLabelJSON.val) ? 40 : 30 * pluginLabelJSON.val.Length;
			minimizeBtnRect.sizeDelta = _minimizeUi.val ? new Vector2(btnWidth, 40) : new Vector2(40, 40);
			_minimizeButton.buttonText.text = _minimizeUi.val ? pluginLabelJSON.val : "_";
			UIHelper.SetAnchors(_windowContainer, _minimizeButton.button.gameObject, "topleft", 40);
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
				ResizeBackpanel();
			}
			catch (Exception e)
			{
				SuperController.LogError(e.ToString());
			}
		}

		private void CreateDynamicButton_Handle()
		{
			_handleButton = _catalogUi.CreateButton(_catalogUi.canvas.gameObject, "<>", 40, 40, 0, 0, new Color(0.5f, 0.5f, 1f), new Color(0.8f, 0.8f, 1f), new Color(0.2f, 0.2f, 0.2f));
			_handleButton.button.onClick.AddListener(() => SelectHandle());
			AddTooltipToDynamicButton(_handleButton, () => "Move");

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
			positionTracker.AddMouseDraggingToObject(_handleButton.gameObject, _catalogUi.canvas.gameObject, true, true, onStartDraggingEvent, _onCatalogDragFinishedEvent);

		}

		private void SelectHandle()
		{
			SuperController.singleton.SelectController(_handleObjectForCatalog.mainController);
		}

		private void CreateDynamicButton_Minimize()
		{
			_minimizeButton = _catalogUi.CreateButton(_catalogUi.canvas.gameObject, "_", 40, 40, 40, 0, new Color(0.7f, 0.7f, 0.7f), new Color(1f, 1f, 1f), new Color(0.2f, 0.2f, 0.2f));
			_minimizeButton.button.onClick.AddListener(() => ToggleMinimize());
			AddTooltipToDynamicButton(_minimizeButton, () => _minimizeUi.val ? "Maximize" : "Minimize");
		}

		private void CreateDynamicButton_PopupMessage()
		{
			_popupMessageLabel = _catalogUi.CreateButton(_windowContainer, "Loading...", 160, 40, 0, 0, new Color(0, 0, 0, 0), new Color(0.5f, 0.5f, 0.5f, 0.5f), new Color(1f, 0.3f, 0.3f, 1));
			_popupMessageLabel.button.onClick.AddListener(() => HidePopupMessage());
			_popupMessageLabel.buttonText.alignment = TextAnchor.MiddleLeft;
			_popupMessageLabel.transform.localScale = Vector3.zero;
		}

		private void CreateDynamicButton_Tooltip()
		{
			_toolTipLabel = _catalogUi.CreateButton(_windowContainer, "Loading...", 160, 40, 0, 0, new Color(0.0f, 0.0f, 0.0f, 0.7f), Color.green, new Color(0.7f, 0.7f, 0.7f, 1));
			_toolTipLabel.buttonText.alignment = TextAnchor.MiddleLeft;
			_toolTipLabel.transform.localScale = Vector3.zero;
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

		private void CreateDynamicButton_Mode()
		{
			var button = _catalogUi.CreateButton(_windowContainer, "Mode", 80, 40, 80, 0, new Color(0.25f, 0.25f, 0.25f), Color.green, new Color(1f, 1f, 1f));
			button.button.onClick.AddListener(() =>
			{
				ToggleMode();
			});
			AddTooltipToDynamicButton(button, () => _catalogMode.val);
		}

		private void AddTooltipToDynamicButton(UIDynamicButton button, Func<string> returnTooltipText)
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

		private void HidePopupMessage()
		{
			_popupMessageLabel.transform.localScale = Vector3.zero;
		}

		private void ShowPopupMessage(string text, int duration)
		{
			if (_popupMessageLabel == null) return;
			var minimizeBtnRect = _popupMessageLabel.GetComponent<RectTransform>();
			var btnWidth = string.IsNullOrEmpty(text) ? 50 : 30 * text.Length;
			minimizeBtnRect.sizeDelta = new Vector2(btnWidth, 50);
			_popupMessageLabel.buttonText.text = text;
			_popupMessageLabel.transform.localScale = Vector3.one;
			UIHelper.SetAnchors(_windowContainer, _popupMessageLabel.button.gameObject, "topleft", 0, -150);
			_popupMessageFramesLeft = duration;
		}

		private void ShowTooltip(string text)
		{
			if (_toolTipLabel == null) return;
			var minimizeBtnRect = _toolTipLabel.GetComponent<RectTransform>();
			var btnWidth = string.IsNullOrEmpty(text) ? 50 : 30 * text.Length;
			minimizeBtnRect.sizeDelta = new Vector2(btnWidth, 50);
			_toolTipLabel.buttonText.text = "  " + text;
			_toolTipLabel.transform.localScale = Vector3.one;
			UIHelper.SetAnchors(_windowContainer, _toolTipLabel.button.gameObject, "topleft", 50, -50);
		}

		private void CreateDynamicButton_Save()
		{
			var button = _catalogUi.CreateButton(_windowContainer, "Save", 80, 40, 160, 0, new Color(0.25f, 0.25f, 0.25f), Color.green, new Color(1f, 1f, 1f));
			button.button.onClick.AddListener(() => BrowseForAndSaveCatalog());
		}

		private void CreateDynamicButton_Load()
		{
			var button = _catalogUi.CreateButton(_windowContainer, "Load", 80, 40, 240, 0, new Color(0.25f, 0.25f, 0.25f), Color.green, new Color(1f, 1f, 1f));
			button.button.onClick.AddListener(() => BrowseForAndLoadCatalog());
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
					//SuperController.LogMessage("file.Substring(file.Length - 4): " + file.Substring(file.Length - 4));
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

		private void CreateDynamicButton_Sort()
		{
			_dynamicButtonSort = _catalogUi.CreateButton(_windowContainer, "Sort", 80, 40, 0, 80, new Color(0.25f, 0.25f, 0.25f), Color.green, new Color(1f, 1f, 1f));
			_dynamicButtonSort.button.onClick.AddListener(() => SortCatalog());
		}
		private void CreateDynamicButton_Refresh()
		{
			_dynamicButtonRefresh = _catalogUi.CreateButton(_windowContainer, "Refresh", 80, 40, 80, 80, new Color(1, 0.25f, 0.25f), new Color(1, 0.5f, 0.5f), new Color(1, 1, 1));
			_dynamicButtonRefresh.buttonText.fontSize = 21;
		}
		private void CreateDynamicButton_Capture_Morphs()
		{
			_dynamicButtonCaptureMorphs = _catalogUi.CreateButton(_windowContainer, "mrph", 50, 33, 242, 92, _dynamicButtonUnCheckColor, _dynamicButtonUnCheckColor, new Color(1f, 1f, 1f));
			_dynamicButtonCaptureMorphs.button.onClick.AddListener(() =>
			{
				_catalogCaptureMorphs.val = !_catalogCaptureMorphs.val;
				_dynamicButtonCaptureMorphs.buttonColor = _catalogCaptureMorphs.val ? _dynamicButtonCheckColor : _dynamicButtonUnCheckColor;
			});
			_dynamicButtonCaptureMorphs.buttonText.fontSize = 20;
			_dynamicButtonCaptureMorphs.gameObject.transform.Rotate(Vector3.forward * 90);
			_dynamicButtonCaptureMorphs.buttonColor = _catalogCaptureMorphs.val ? _dynamicButtonCheckColor : _dynamicButtonUnCheckColor;
			AddTooltipToDynamicButton(_dynamicButtonCaptureMorphs, () => (_catalogCaptureMorphs.val ? "Include" : "Exclude") + " Morphs");
		}
		private void CreateDynamicButton_Capture_Hair()
		{
			_dynamicButtonCaptureHair = _catalogUi.CreateButton(_windowContainer, "hair", 50, 33, 276, 92, _dynamicButtonUnCheckColor, _dynamicButtonUnCheckColor, new Color(1f, 1f, 1f));
			_dynamicButtonCaptureHair.button.onClick.AddListener(() =>
			{
				_catalogCaptureHair.val = !_catalogCaptureHair.val;
				_dynamicButtonCaptureHair.buttonColor = _catalogCaptureHair.val ? _dynamicButtonCheckColor : _dynamicButtonUnCheckColor;
			});
			_dynamicButtonCaptureHair.buttonText.fontSize = 23;
			_dynamicButtonCaptureHair.gameObject.transform.Rotate(Vector3.forward * 90);
			_dynamicButtonCaptureHair.buttonColor = _catalogCaptureHair.val ? _dynamicButtonCheckColor : _dynamicButtonUnCheckColor;
			AddTooltipToDynamicButton(_dynamicButtonCaptureHair, () => (_catalogCaptureHair.val ? "Include" : "Exclude") + " Hair");
		}
		private void CreateDynamicButton_Capture_Clothes()
		{
			_dynamicButtonCaptureClothes = _catalogUi.CreateButton(_windowContainer, "cloth", 50, 33, 310, 92, _dynamicButtonUnCheckColor, _dynamicButtonUnCheckColor, new Color(1f, 1f, 1f));
			_dynamicButtonCaptureClothes.button.onClick.AddListener(() =>
			{
				_catalogCaptureClothes.val = !_catalogCaptureClothes.val;
				_dynamicButtonCaptureClothes.buttonColor = _catalogCaptureClothes.val ? _dynamicButtonCheckColor : _dynamicButtonUnCheckColor;
			});
			_dynamicButtonCaptureClothes.buttonText.fontSize = 20;
			_dynamicButtonCaptureClothes.gameObject.transform.Rotate(Vector3.forward * 90);
			_dynamicButtonCaptureClothes.buttonColor = _catalogCaptureClothes.val ? _dynamicButtonCheckColor : _dynamicButtonUnCheckColor;
			AddTooltipToDynamicButton(_dynamicButtonCaptureClothes, () => (_catalogCaptureClothes.val ? "Include" : "Exclude") + " Clothes");
		}
		private void CreateDynamicButton_ZoomIn()
		{
			var button = _catalogUi.CreateButton(_windowContainer, "+", 40, 40, 0, 40, new Color(0.25f, 0.25f, 0.25f), Color.green, new Color(1f, 1f, 1f));
			button.button.onClick.AddListener(() => ZoomInCatalog());
			AddTooltipToDynamicButton(button, () => "Increase frame size");
		}

		private void CreateDynamicButton_ZoomOut()
		{
			var button = _catalogUi.CreateButton(_windowContainer, "-", 40, 40, 40, 40, new Color(0.25f, 0.25f, 0.25f), Color.green, new Color(1f, 1f, 1f));
			button.button.onClick.AddListener(() => ZoomOutCatalog());
			AddTooltipToDynamicButton(button, () => "Decrease frame size");
		}
		private void CreateDynamicButton_ScrollUp()
		{
			var button = _catalogUi.CreateButton(_windowContainer, "<", 40, 40, 80, 40, new Color(0.25f, 0.25f, 0.25f), Color.green, new Color(1f, 1f, 1f));
			button.button.onClick.AddListener(() => ScrollUpCatalog());
			AddTooltipToDynamicButton(button, () => "Shift catalog entries forward");
		}

		private void CreateDynamicButton_ScrollDown()
		{
			var button = _catalogUi.CreateButton(_windowContainer, ">", 40, 40, 120, 40, new Color(0.25f, 0.25f, 0.25f), Color.green, new Color(1f, 1f, 1f));
			button.button.onClick.AddListener(() => ScrollDownCatalog());
			AddTooltipToDynamicButton(button, () => "Shift catalog entries back");
		}

		private void CreateDynamicButton_CycleAtoms()
		{
			var button = _catalogUi.CreateButton(_windowContainer, "#", 40, 40, 160, 40, new Color(0.25f, 0.5f, 0.5f), Color.green, new Color(1f, 1f, 1f));
			button.button.onClick.AddListener(() => SelectNextAtom());
			AddTooltipToDynamicButton(button, () =>
			{
				var currentAtom = SuperController.singleton.GetSelectedAtom();
				var currentAtomName = currentAtom.name;
				return $"{currentAtomName}. Select next atom.";
			});
		}

		private void CreateDynamicButton_ResetPivot()
		{
			var button = _catalogUi.CreateButton(_windowContainer, "%", 40, 40, 200, 40, new Color(0.25f, 0.5f, 0.5f), Color.green, new Color(1f, 1f, 1f));
			var currentAtom = SuperController.singleton.GetSelectedAtom();
			if (currentAtom == null) currentAtom = containingAtom;
			if (currentAtom == null || currentAtom.type != "Person") {
				SuperController.LogError("Can only do this for a 'Person' atom");
				return;
			}
			button.button.onClick.AddListener(() => ResetPivotRotation(currentAtom));
			AddTooltipToDynamicButton(button, () => "Reset pivot rotation");
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
					DeapVersion = SerializerService.CATALOG_DEAP_VERSION,
					Entries = _catalog.Entries,
				};

				// Json Serialize data...
				var catalogData = SerializerService.SaveCatalog(newCatalog);

				// Save the data to file...
				SuperController.singleton.SaveStringIntoFile(filePath, catalogData);
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
				string fileData = SuperController.singleton.ReadFileIntoString(filePath);
				// Deserialize the file data...
				Catalog catalog = SerializerService.LoadCatalog(fileData);
				var catalogFileName = filePath.Split('/').Last();
				//var catalogNameParts = catalogFileName.Substring(0, catalogFileName.Length - ("." + _fileExtension).Length); //catalogFileName.Split('.');
				var extension = "." + _fileExtension;
				var catalogName = catalogFileName.Substring(0, catalogFileName.Length - extension.Length);
				pluginLabelJSON.val = catalogName;
				_catalogName.SetVal(catalogName);
				_catalogRelativePath.SetVal(filePath);
				_mutationsService.CaptureHair = catalog.CaptureHair;
				_mutationsService.CaptureClothes = catalog.CaptureClothes;
				_mutationsService.CaptureActiveMorphs = catalog.CaptureMorphs;
				if (CatalogHasMessage(catalog)) ShowPopupMessage(catalog.ActiveVersionMessage.ShortMessage, 500);
				for (var i = 0; i < catalog.Entries.Count(); i++)
				{
					if (CannotLoad(catalog)) continue;
					var entry = catalog.Entries.ElementAt(i);
					BuildCatalogEntry(catalog.Entries.ElementAt(i));
				}
				ResizeBackpanel();
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
			return catalog.ActiveVersionMessage != null && catalog.ActiveVersionMessage.Then == SerializerService.DO_NOT_LOAD;
		}

		public string GetSceneDirectoryPath()
		{
			var dataPath = $"{Application.dataPath}";
			var currentLoadDir = SuperController.singleton.currentLoadDir;
			var pathElements = dataPath.Split('/');
			var scenePath = pathElements.Take(pathElements.Length - 1).ToList().Aggregate((a, b) => $"{a}/{b}") + "/" + currentLoadDir;
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
			UIHelper.SetAnchors(catalogEntry.UiCatalogEntryPanel, catalogEntry.UiApplyButton.gameObject, "bottom");

			var btnHeight = _catalogEntryFrameSize.val / 5;
			var btnWidth = (_catalogEntryFrameSize.val / 5) - _catalogEntryFrameSize.val / 50;
			var discardButtonRect = catalogEntry.UiDiscardButton.GetComponents<RectTransform>().First();
			discardButtonRect.sizeDelta = new Vector2(btnWidth, btnHeight);
			var keepButtonRect = catalogEntry.UiKeepButton.GetComponents<RectTransform>().First();
			keepButtonRect.sizeDelta = new Vector2(btnWidth, btnHeight);
			UIHelper.SetAnchors(catalogEntry.UiCatalogEntryPanel, catalogEntry.UiButtonGroup, "bottom");

			_catalogRowsVLayout.spacing = defaultBorderWidth;
			_catalogColumnsHLayout.spacing = defaultBorderWidth;

			ResizeBorders(catalogEntry, newSize);
			ResizeBackpanel();
			ResetScrollPosition();
		}

		private void ResizeBorders(CatalogEntry catalogEntry, float newSize)
		{
			float originRatio = newSize / defaultFrameSize;
			float borderWidth = originRatio * defaultBorderWidth;
			float height = newSize;
			float width = newSize;
			float offsetX = 0;
			float offsetY = 0;
			ResizeBorder(catalogEntry.UiCatalogBorder.LeftBorder, borderWidth, height + borderWidth + offsetY, width / -2 - offsetX, 0);
			ResizeBorder(catalogEntry.UiCatalogBorder.RightBorder, borderWidth, height + borderWidth + offsetY, width / 2 + offsetX, 0);
			ResizeBorder(catalogEntry.UiCatalogBorder.TopBorder, width + borderWidth + offsetX, borderWidth, 0, height / -2 - offsetY);
			ResizeBorder(catalogEntry.UiCatalogBorder.BottomBorder, width + borderWidth + offsetX, borderWidth, 0, height / 2 + offsetY);
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
			//_floatingTriggerButton.button.onClick.AddListener(() =>
			//{
			//	try
			//	{
			//		RequestNextCaptureSet(1, CaptureRequest.MUTATION_AQUISITION_MODE_CAPTURE);
			//	}
			//	catch (Exception exc)
			//	{
			//		SuperController.LogError(exc.ToString());
			//	}
			//});
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
		//private void CreateDynamicButton_CaptureControl()
		//{
		//	_captureButton = _controlsUi.CreateButton(_controlsUi.canvas.gameObject, "Capture Scene", 300, 100, 0, 150, new Color(1, 0.25f, 0.25f), new Color(1, 0.5f, 0.5f), new Color(1, 1, 1));
		//	_captureButton.transform.localScale = Vector3.zero;
		//	_captureButton.button.onClick.AddListener(() =>
		//	{
		//		try
		//		{
		//			RequestNextCaptureSet(1, CaptureRequest.MUTATION_AQUISITION_MODE_CAPTURE);
		//		}
		//		catch (Exception exc)
		//		{
		//			SuperController.LogError(exc.ToString());
		//		}
		//	});
		//	var positionTracker = new DragHelper();
		//	_positionTrackers.Add("CaptureButton", positionTracker);
		//	Action onStartDraggingEvent = () =>
		//	{
		//		positionTracker.IsIn3DSpace = !IsAnchoredOnHUD();
		//		positionTracker.XMultiplier = -1000f;
		//		positionTracker.YMultiplier = 1000f;
		//	};
		//	positionTracker.AddMouseDraggingToObject(_captureButton.button.gameObject, _captureButton.button.gameObject, true, true, onStartDraggingEvent);
		//}

		//private void CreateDynamicButton_GenerateCatalog()
		//{
		//	_generateButton = _controlsUi.CreateButton(_controlsUi.canvas.gameObject, _generateButtonInitText, 300, 100, 0, 150, new Color(1, 0.25f, 0.25f), new Color(1, 0.5f, 0.5f), new Color(1, 1, 1));
		//	_generateButton.transform.localScale = Vector3.zero;
		//	_generateButton.button.onClick.AddListener(() =>
		//	{
		//		try
		//		{
		//			RequestNextCaptureSet((int)_catalogRequestedEntriesCountJSON.val, CaptureRequest.MUTATION_AQUISITION_MODE_SEQUENCE);
		//		}
		//		catch (Exception exc)
		//		{
		//			SuperController.LogError(exc.Message + ": " + exc.StackTrace);
		//		}
		//	});

		//	var positionTracker = new DragHelper();
		//	_positionTrackers.Add("GenerateButton", positionTracker);
		//	Action onStartDraggingEvent = () =>
		//	{
		//		positionTracker.IsIn3DSpace = !IsAnchoredOnHUD();
		//		positionTracker.XMultiplier = -1000f;
		//		positionTracker.YMultiplier = 1000f;
		//	};
		//	positionTracker.AddMouseDraggingToObject(_generateButton.button.gameObject, _generateButton.button.gameObject, true, true, onStartDraggingEvent);

		//}

		//protected void FixedUpdate() { }

		//void CreateShape()
		//{
		//	_vertices = new Vector3[]
		//	{
		//		new Vector3 (0,0,0),
		//		new Vector3 (0,0,1),
		//		new Vector3 (1,0,0),
		//		new Vector3 (1,0,1),
		//	};
		//	_triangles = new int[]
		//	{
		//		0, 1, 2,
		//		1, 3, 2
		//	};
		//}

		//void UpdateMesh(Mesh mesh, Vector3[] vertices, int[] triangles = null)
		//{
		//	mesh.Clear();
		//	mesh.vertices = vertices;
		//	if (triangles != null) _mesh.triangles = _triangles;
		//	mesh.RecalculateNormals();
		//}
		// Update is called with each rendered frame by Unity
		void Update()
		{

			try
			{
				if (_atomType == ATOM_TYPE_PERSON) _mutationsService.Update();

				ManageScreenshotCaptureSequence();

				ManagePopupMessage();

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

		private void ManagePopupMessage()
		{
			if (_popupMessageFramesLeft < 0) return;
			if (--_popupMessageFramesLeft < 1) HidePopupMessage();
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

		private void GeneratingMutationsIsCompleteCallback()
		{

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
						_captureCamera.targetTexture = RenderTexture.GetTemporary(1000, 1000);

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
						GeneratingMutationsIsCompleteCallback();
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
			else if (_currentCaptureRequest.RequestModeEnum == CaptureRequest.MUTATION_AQUISITION_MODE_CAPTURE_ASSET)
			{
				mutation = _mutationsService.CaptureAtom(containingAtom);
			}
			else if (_currentCaptureRequest.RequestModeEnum == CaptureRequest.MUTATION_AQUISITION_MODE_CAPTURE_OBJECT)
			{
				mutation = _mutationsService.CaptureAtom(containingAtom);
			}
			else if (_currentCaptureRequest.RequestModeEnum == CaptureRequest.MUTATION_AQUISITION_MODE_CAPTURE_SESSION_OBJECT)
			{
				var selectedAtom = SuperController.singleton.GetSelectedAtom();
				if (selectedAtom == null) {
					SuperController.LogError("Please select an atom from the scene");
					_nextMutation = null;
					return;
				}
				mutation = _mutationsService.CaptureAtom(selectedAtom);
			}
			_nextMutation = mutation;
		}

		private void SelectCatalogEntry(CatalogEntry catalogEntry)
		{
			_catalog.Entries.ForEach(DeselectCatalogEntry);
			catalogEntry.Selected = true;
			_catalog.Entries.ForEach(UpdateCatalogEntryBorderColorBasedOnState);
			_currentCatalogEntry = catalogEntry;
			if (catalogEntry.SelectAction != null)
			{
				catalogEntry.SelectAction.Invoke(catalogEntry);
				return;
			}
			DefaultCatalogEntrySelectAction(catalogEntry);
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
				//Texture2D textureDxt = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.DXT1, false);

				float captureWidth = 960;
				float captureHeight = 960;
				float captureCropLeft = 150;
				float captureCropBottom = 40;
				Rect rect = new Rect(captureCropLeft, captureCropBottom, captureWidth, captureHeight);
				texture.ReadPixels(rect, 20, 20);
				//texture.Compress(true);
				texture.Apply();

				CatalogEntry newCatalogEntry = CreateEntryInCatalog(mutation, texture, customAction);

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

		private CatalogEntry CreateEntryInCatalog(Mutation mutation, Texture2D imageCapture, Action<CatalogEntry> customAction = null)
		{
			var catalogEntry = new CatalogEntry();
			catalogEntry.UniqueName = GetUniqueName();
			catalogEntry.CatalogMode = _catalogMode.val;
			catalogEntry.ImageAsTexture = imageCapture;
			catalogEntry.Mutation = mutation;
			BuildCatalogEntry(catalogEntry);
			return catalogEntry;
		}

		private CatalogEntry BuildCatalogEntry(CatalogEntry catalogEntry, Action<CatalogEntry> customAction = null)
		{
			try
			{
				catalogEntry.ImageAsEncodedString = EncodedImageFromTexture(catalogEntry.ImageAsTexture);
				// Create main image panel...
				GameObject catalogEntryPanel = _catalogUi.CreateImagePanel(_windowContainer, catalogEntry.ImageAsTexture, (int)_catalogEntryFrameSize.val, (int)_catalogEntryFrameSize.val, 0, 0);
				catalogEntry.UiCatalogEntryPanel = catalogEntryPanel;
				// Add indication borders...
				catalogEntry.UiCatalogBorder = _catalogUi.CreateBorders(catalogEntryPanel, (int)_catalogEntryFrameSize.val, (int)_catalogEntryFrameSize.val, 0, 0, defaultBorderWidth);

				_catalog.Entries.Add(catalogEntry);
				AddEntryToCatalog(catalogEntry, _catalog.Entries.Count - 1);

				// Apply button
				var normalColor = new Color(0.5f, 0.5f, 0.5f, 0f);
				var highlightedColor = new Color(0.0f, 0.0f, 0.5f, 0.2f);
				UIDynamicButton applyButton = _catalogUi.CreateClickablePanel(catalogEntryPanel, normalColor, highlightedColor, 10, (int)_catalogEntryFrameSize.val);
				catalogEntry.UiApplyButton = applyButton;
				catalogEntry.SelectAction = GetAppropriateApplyAction(catalogEntry, customAction);
				catalogEntry.UiApplyButton.button.onClick.AddListener(() => SelectCatalogEntry(catalogEntry));

				//Setup Drag-Scrolling...
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
				catalogEntry.PositionTracker.AddMouseDraggingToObject(catalogEntry.UiApplyButton.gameObject, _columnContainer, false, true, onStartDraggingEvent, null, onWhileDraggingEvent); // allow user to drag-scroll using this button aswell				

				int btnHeight = (int)_catalogEntryFrameSize.val / 5;
				int btnWidth = (int)((_catalogEntryFrameSize.val / 5) - _catalogEntryFrameSize.val / 50);

				var buttonGroup = CreateButtonRow(catalogEntryPanel, btnHeight);
				catalogEntry.UiButtonGroup = buttonGroup;

				// Favorite button
				var acceptColor = new Color(0.0f, 0.5f, 0.0f, 0.2f);
				var acceptHighlightedColor = new Color(0.0f, 0.5f, 0.0f, 0.5f);
				UIDynamicButton keepButton = _catalogUi.CreateClickablePanel(buttonGroup, acceptColor, acceptHighlightedColor, btnWidth, btnHeight);
				catalogEntry.UiKeepButton = keepButton;
				catalogEntry.UiKeepButton.buttonText.fontSize = 15;
				keepButton.button.onClick.AddListener(() =>
				{
					catalogEntry.Discarded = false;
					catalogEntry.Favorited += 1;
					catalogEntry.UiKeepButton.buttonText.text = "+" + catalogEntry.Favorited;
					UpdateCatalogEntryBorderColorBasedOnState(catalogEntry);
				});

				// Reject button
				var rejectColor = new Color(0.5f, 0.0f, 0.0f, 0.2f);
				var rejectHighlightedColor = new Color(0.5f, 0.0f, 0.0f, 0.5f);
				UIDynamicButton discardButton = _catalogUi.CreateClickablePanel(buttonGroup, rejectColor, rejectHighlightedColor, btnWidth, btnHeight);
				catalogEntry.UiDiscardButton = discardButton;
				discardButton.button.onClick.AddListener(() =>
				{
					catalogEntry.Discarded = !catalogEntry.Discarded;
					catalogEntry.Favorited = 0;
					catalogEntry.UiKeepButton.buttonText.text = "";
					UpdateCatalogEntryBorderColorBasedOnState(catalogEntry);
				});

				UIHelper.SetAnchors(catalogEntry.UiCatalogEntryPanel, catalogEntry.UiButtonGroup, "bottom");

				ResizeBackpanel();
				return catalogEntry;
			}
			catch (Exception e)
			{
				SuperController.LogError(e.ToString());
				throw e;
			}
		}

		private Action<CatalogEntry> GetAppropriateApplyAction(CatalogEntry catalogEntry, Action<CatalogEntry> customAction)
		{
			if (customAction != null) return customAction;
			if (catalogEntry.CatalogMode == CATALOG_MODE_ASSET) return (TheCatalogEntry) => CreateCustomUnityAsset(TheCatalogEntry, _catalogName.val);
			if (catalogEntry.CatalogMode == CATALOG_MODE_OBJECT) return (TheCatalogEntry) => SelectOrCreateAtomForCatalogEntry(TheCatalogEntry);
			if (catalogEntry.CatalogMode == CATALOG_MODE_SESSION) return (TheCatalogEntry) => SelectOrCreateAtomForCatalogEntry(TheCatalogEntry);
			if (catalogEntry.Mutation.ScenePathToOpen != null) return (TheCatalogEntry) => SuperController.singleton.Load(catalogEntry.Mutation.ScenePathToOpen);
			return (TheCatalogEntry) => DefaultCatalogEntrySelectAction(TheCatalogEntry);
		}

		public bool IsAnchoredOnHUD()
		{
			return _anchorOnHud.val;
		}

		private void ResizeBackpanel()
		{
			if (_anchorOnHud.val) return;
			var backPanelRect = _backPanel.GetComponent<RectTransform>();
			var width = (_catalogEntryFrameSize.val * (_catalog.Entries.Count > _catalogColumnsCountJSON.val ? _catalogColumnsCountJSON.val : _catalog.Entries.Count)) + 75; //(_catalogEntryFrameSize.val * _catalogColumnsCountJSON.val) + 100;
			var height = (float)(_catalogEntryFrameSize.val * Math.Ceiling(_catalog.Entries.Count / _catalogColumnsCountJSON.val)) + 75;
			backPanelRect.sizeDelta = new Vector2(width, height);
			UIHelper.SetAnchors(_windowContainer, _backPanel, "topleft", 0, 0);
		}

		private void UpdateCatalogEntryBorderColorBasedOnState(CatalogEntry newCatalogEntry)
		{
			if (newCatalogEntry.UiCatalogBorder == null) return;
			UIHelper.AddBorderColorToTexture(newCatalogEntry.UiCatalogBorder.texture, Color.magenta, 10);
			var color = Color.white;
			if (newCatalogEntry.Favorited > 0)
			{
				float greenValue = ((float)newCatalogEntry.Favorited + 3) / 10;
				color = new Color(0f, greenValue, 0f);
			}
			else if (newCatalogEntry.Discarded)
			{
				color = Color.red;
			}
			UIHelper.AddBorderColorToTexture(newCatalogEntry.UiCatalogBorder.texture, color);
			if (newCatalogEntry.Selected)
			{
				UIHelper.AddBorderColorToTexture(newCatalogEntry.UiCatalogBorder.texture, Color.magenta, 10);
			}
		}

		void DestroyCatalogEntry(CatalogEntry catalogEntry)
		{
			Destroy(catalogEntry.UiApplyButton);
			Destroy(catalogEntry.UiKeepButton);
			Destroy(catalogEntry.UiButtonGroup);
			Destroy(catalogEntry.UiCatalogEntryPanel);
			Destroy(catalogEntry.UiDiscardButton);
		}

		GameObject CreateButtonRow(GameObject parentPanel, int height)
		{
			var subPanel = _catalogUi.CreateUIPanel(parentPanel, 0, height, "bottom", 0, 0, Color.clear);
			_catalogUi.CreateHorizontalLayout(subPanel, 1, false, false, false, false);
			ContentSizeFitter psf = subPanel.AddComponent<ContentSizeFitter>();
			psf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
			psf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
			subPanel.transform.SetParent(parentPanel.transform, false);
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
			_rowContainer.transform.localPosition = new Vector3(_rowContainer.transform.localPosition.x, rowHeight, _rowContainer.transform.localPosition.z);
			_columnContainer.transform.localPosition = new Vector3(_columnContainer.transform.localPosition.x, rowHeight, _rowContainer.transform.localPosition.z);
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
			base.StartCoroutine(CreateAtom("CollisionTrigger", assignHandleUniqueName, handlesInitialPosition, Quaternion.identity, newAtom =>
			{
				newAtom.GetStorableByID("scale").SetFloatParamValue("scaleX", 0.08f);
				newAtom.GetStorableByID("scale").SetFloatParamValue("scaleY", 0.08f);
				newAtom.GetStorableByID("scale").SetFloatParamValue("scaleZ", 0.08f);
				newAtom.collisionEnabledJSON.SetVal(false);
				//newAtom.transform.position = handlesInitialPosition;
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
				atom.transform.position = position;
				atom.transform.rotation = rotation;
			}
			if (atom != null)
			{
				onAtomCreated(atom);
			}
		}

		private bool SelectAtomFromCatalogEntry(CatalogEntry catalogEntry)
		{
			var existingAtom = SuperController.singleton.GetAtomByUid(catalogEntry.Mutation.AtomName);
			var selectedAtom = SuperController.singleton.GetSelectedAtom();

			if (existingAtom == null || selectedAtom.name == catalogEntry.Mutation.AtomName)
			{
				// See if there is an atom by the same type, and select that instead...
				var atomType = catalogEntry.Mutation.AtomType;
				if (atomType == null) return false;
				var sceneAtoms = SuperController.singleton.GetAtoms();
				var otherAtom = sceneAtoms.FirstOrDefault(a => a.type == atomType);
				if (otherAtom == null) return false;
				SuperController.singleton.SelectController(existingAtom.mainController);
				return true;
			}

			if (selectedAtom == null) // Atom exits but is not selected
			{
				//... Select the atom...
				SuperController.singleton.SelectController(existingAtom.mainController);
				return true;
			}
			return false;
		}

		private void SelectOrCreateAtomForCatalogEntry(CatalogEntry catalogEntry)
		{
			try
			{
				var existingAtom = SuperController.singleton.GetAtomByUid(catalogEntry.Mutation.AtomName);
				var atomType = catalogEntry.Mutation.AtomType ?? "Cube";
				var selectedAtom = SuperController.singleton.GetSelectedAtom();
				if (existingAtom == null)
				{
					// Does not exist in scene...
					string newAtomName = GetNextAvailableName(catalogEntry.Mutation.AtomName, catalogEntry.Mutation.AtomType);
					CreateAtom(atomType, newAtomName, Vector3.zero, Quaternion.identity);
					return;
				}
				if (selectedAtom == null || selectedAtom.name == catalogEntry.Mutation.AtomName)
				{
					// Already exists in scene, and is already selected, create clone...
					string newAtomName = GetNextAvailableName(catalogEntry.Mutation.AtomName, catalogEntry.Mutation.AtomType);
					CreateAtom(atomType, newAtomName, existingAtom.transform.position, existingAtom.transform.rotation);
					return;
				}
				// Already exists in scene but is not selected. Select it.
				SuperController.singleton.SelectController(existingAtom.mainController);
			}
			catch (Exception e)
			{
				SuperController.LogError(e.ToString());
			}
		}

		private void CreateAtom(string atomType, string atomName, Vector3 position, Quaternion rotation)
		{
			this.StartCoroutine(this.CreateAtom(atomType, atomName, position, rotation, newAtom =>
			{
				newAtom.collisionEnabledJSON.SetVal(true);
				SuperController.singleton.SelectController(newAtom.mainController);
			}));
		}

		private void CreateCustomUnityAsset(CatalogEntry catalogEntry, string catalogName)
		{
			try
			{
				var existingAsset = SuperController.singleton.GetAtomByUid(catalogEntry.Mutation.AtomName);
				var selectedAtom = SuperController.singleton.GetSelectedAtom();
				// Create new atom...
				if (existingAsset == null || selectedAtom == null || selectedAtom.name == catalogEntry.Mutation.AtomName)
				{
					Vector3 position = (existingAsset != null) ? existingAsset.transform.position : Vector3.zero;
					Quaternion rotation = (existingAsset != null) ? existingAsset.transform.rotation : Quaternion.identity;
					string newAtomName = GetNextAvailableName(catalogEntry.Mutation.AtomName, catalogEntry.Mutation.AtomType);
					this.StartCoroutine(this.CreateAtom("CustomUnityAsset", newAtomName, position, rotation, newAtom =>
					{
						newAtom.GetStorableByID("asset").SetStringParamValue("assetUrl", catalogEntry.Mutation.AssetUrl);
						newAtom.GetStorableByID("asset").SetStringParamValue("assetName", catalogEntry.Mutation.AssetName);
						CustomUnityAssetLoader customAsset = newAtom.GetStorableByID("asset") as CustomUnityAssetLoader;
						customAsset.SetUrlParamValue("assetUrl", catalogEntry.Mutation.AssetUrl);
						customAsset.SetStringChooserParamValue("assetName", catalogEntry.Mutation.AssetName);
						newAtom.collisionEnabledJSON.SetVal(true);
						SuperController.singleton.SelectController(newAtom.mainController);
					}));
					return;
				}
				// Select existing Atom...
				SuperController.singleton.SelectController(existingAsset.mainController);
			}
			catch (Exception e)
			{
				SuperController.LogError(e.ToString());
			}
		}

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

		private void ResetPivotRotation(Atom atom)
		{
			var allJointsController = containingAtom.GetComponentInChildren<AllJointsController>();
			allJointsController.SetAllJointsControlOff();
			StartCoroutine(MoveControllerAndThenTurnOnJoints(atom));
		}

		IEnumerator MoveControllerAndThenTurnOnJoints(Atom atom)
		{
			var mainController = atom.mainController;
			mainController.transform.rotation = Quaternion.identity;
			yield return new WaitForFixedUpdate();
			var allJointsController = atom.GetComponentInChildren<AllJointsController>();
			allJointsController.SetAllJointsControlPositionAndRotation();
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
