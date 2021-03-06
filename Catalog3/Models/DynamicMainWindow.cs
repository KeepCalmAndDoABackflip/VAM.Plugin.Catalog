﻿using juniperD.StatefullServices;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace juniperD.Models
{
	public class DynamicMainWindow
	{
		public CatalogEntry CurrentCatalogEntry { get; set; }
		public bool Minimized { get; set;} = false;
		public int WindowWidth { get; set; } = 320;
		public int WindowHeight { get; set; } = 220;

		public bool ControlPanelMinimized { get; set; } = false;
		public bool SubItemPanelMinimized { get; set; } = true;
		public float ControlPanelMinimizedWidth { get; set; } = 150;
		public float ControlPanelMinimizedHeight { get; set; } = 50;

		public CatalogUiHelper ParentUiHelper { get; set; }
		public GameObject ParentWindowContainer { get; set; }
		public GameObject SubWindow { get; internal set; }

		// Panels...
		public GameObject PanelBackground { get; set; }
		public GameObject SubPanelMode { get; set; }
		public GameObject SubPanelFileMenu { get; set; }
		public GameObject SubPanelCapture { get; set; }
		public GameObject SubSubPanelCaptureOptions { get; set; }
		public GameObject SubPanelManageCatalog { get; set; }
		public GameObject SubPanelSceneTools { get; set; }
		public GameObject MiniPanelBackground { get; set; }
		public GameObject MiniSubPanelShortcut { get; set; }

		// Panel stacks
		public HorizontalLayoutGroup SubPanelHorizontalStack { get; set; }
		public GameObject SubPanelVerticalStack { get; set; }

		// Catalog File buttons
		public UIDynamicButton ButtonOpenCatalog { get; set; }
		public UIDynamicButton ButtonOpenCatalogShortcut { get; set; }
		public UIDynamicButton ButtonSaveCatalog { get; set; }
		public UIDynamicButton ButtonQuickload { get; set; }
		public UIDynamicButton ButtonQuickloadShortcut { get; set; }
		public UIDynamicButton ButtonMinimizeControlPanel { get; set; }
		public UIDynamicButton ButtonMaximizeControlPanel { get; set; }

		// Catalog Management buttons
		public UIDynamicButton ButtonResetCatalog { get; set; }
		public UIDynamicButton ButtonZoomIn { get; set; }
		public UIDynamicButton ButtonZoomOut { get; set; }
		public UIDynamicButton ButtonScrollUp { get; set; }
		public UIDynamicButton ButtonScrollDown { get; set; }
		public UIDynamicButton ButtonSort { get; set; }
		public UIDynamicButton ButtonShowDebug { get; set; }

		// Scene action buttons...
		public UIDynamicButton ButtonSelectSceneAtom { get; set; }
		public UIDynamicButton ButtonResetPivot { get; set; }
		public UIDynamicButton ButtonCreateMannequinPicker { get; set; }

		// Capture Buttons...
		public UIDynamicButton ButtonCapture { get; set; }
		public UIDynamicButton ButtonAddAtomToCapture { get; set; }
		public UIDynamicButton ButtonAddActionToCapture { get; set; }
		public UIDynamicButton ButtonSelectScenesFolder { get; set; }
		public UIDynamicButton ToggleButtonCaptureMorphs { get; set; }
		public UIDynamicButton ToggleButtonCaptureClothes { get; set; }
		public UIDynamicButton ToggleButtonCaptureHair { get; set; }
		
		// Info boxes...
		public Dictionary<string, UIDynamicButton> ModeButtons { get; set; } = new Dictionary<string, UIDynamicButton>();
		public Sprite MainCaptureButtonIcon { get; set; }
		public Sprite IconForCapturePerson { get; set; }
		public Sprite IconForCaptureScenes { get; set; }
		public Sprite IconForCaptureObject { get; set; }
		public Sprite IconForCaptureSelectedObject { get; set; }
		public Sprite IconForCaptureNone { get; set; }

		public GameObject CatalogRowContainer { get; set; }
		public List<GameObject> CatalogColumnContainers { get; set; } = new List<GameObject>();
		public VerticalLayoutGroup CatalogRowsVLayout { get; set; }
		public HorizontalLayoutGroup CatalogColumnsHLayout { get; set; }
		public List<GameObject> CatalogRows { get; set; } = new List<GameObject>();
		public List<GameObject> CatalogColumns { get; set; } = new List<GameObject>();

		public UIDynamicTextField TextToolTip { get; set; }
		public UIDynamicTextField TextDebugPanelText { get; set; }
		public UIDynamicButton ButtonNameLabel { get; set; }
		public UIDynamicButton ButtonPopupMessageLabel { get; set; }

		public GameObject DynamicInfoPanel { get; set; }
		public UIDynamicTextField InfoLabel { get; set; }
		public VerticalLayoutGroup UiSubItemsPanelLayout { get; set; }
		public UIDynamicButton ButtonRemoveAllClothing { get; set; }
		public UIDynamicButton ButtonRemoveAllHair { get; set; }
		public GameObject DebugPanel { get; set; }
		public UIDynamicButton DebugDynamicButton1 { get; set; }
		public UIDynamicButton DebugDynamicButton2 { get; set; }
		public UIDynamicButton DebugDynamicButton3 { get; set; }
		public UIDynamicButton DebugDynamicButton4 { get; set; }

		public UIDynamicButton ButtonClean { get; set; }
		public UIDynamicButton ToggleButtonCapturePose { get; set; }
		public UIDynamicButton ButtonHideCatalogShortcut { get; set; }
		public UIDynamicButton ButtonPlayStop { get; set; }
		public UIDynamicButton ButtonShowSubItemsPanel { get; set; }
		public UIDynamicButton ButtonCreateMergedEntrySequenceGroup { get; set; }
		public UIDynamicButton ButtonToggleAnimFeatures { get; set; }
		public GameObject ActiveCatalogContainer { get; set; }
		public UIDynamicButton ButtonRemoveAllFavorites { get; set; }
		public UIDynamicButton ButtonLoopFavorited { get; set; }
		public VerticalLayoutGroup InfoTogglesVLayout { get; set; }
		public UIDynamicButton ButtonShowAnimationsPanel { get; set; }
		public UIDynamicButton ButtonShowOptionsPanel { get; set; }
		public GameObject DynamicInfoTogglesPanel { get; set; }
		public GameObject UiAnimationPanel { get; set; }
		//public GameObject UiAnimationInnerPanel { get; set; }
		public VerticalLayoutGroup UiAnimationPanelLayout { get; set; }
		public List<GameObject> UiAnimationItems { get; set; } = new List<GameObject>();

		public string SelectedInfoPanel { get; set; }
		public GameObject UiSubItemsPanel { get;  set; }
		public GameObject UiFrameOptionsPanel { get;  set; }
		public VerticalLayoutGroup UiFrameOptionsPanelLayout { get; set; }
		public UIDynamicButton ButtonMergeEntries { get;  set; }
		public UIDynamicButton ButtonCaptureEmpty { get;  set; }

		public DynamicMainWindow(CatalogUiHelper uiHelper)
		{
			ParentUiHelper = uiHelper;
		}


	}
}
