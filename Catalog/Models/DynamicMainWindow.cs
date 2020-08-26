using juniperD.StatefullServices;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace juniperD.Models
{
	public class DynamicMainWindow
	{
		public CatalogEntry CurrentCatalogEntry { get; set; }
		public bool Minimized { get; set;} = false;
		public int _windowWidth { get; set; } = 320;
		public int _windowHeight { get; set; } = 220;

		public bool ControlPanelMinimized { get; set; } = true;
		public float ControlPanelMinimizedWidth { get; set; } = 320;
		public float ControlPanelMinimizedHeight { get; set; } = 50;

		public CatalogUiHelper ParentUiHelper { get; set; }
		public GameObject ParentWindowContainer { get; set; }
		public GameObject BackPanel { get; set; }

		public GameObject ModeSubPanel { get; set; }
		public GameObject CaptureSubPanel { get; set; }
		public GameObject ManageSubPanel { get; set; }
		public GameObject SceneToolsSubPanel { get; set; }

		public GameObject MainWindow { get; set; }

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

		// Capture Buttons
		public UIDynamicButton ButtonCapture { get; set; }
		public UIDynamicButton ButtonAddAtomToCapture { get; set; }
		public UIDynamicButton ButtonSelectScenesFolder { get; set; }
		public UIDynamicButton ToggleButtonCaptureMorphs { get; set; }
		public UIDynamicButton ToggleButtonCaptureClothes { get; set; }
		public UIDynamicButton ToggleButtonCaptureHair { get; set; }

		
		public GameObject CatalogRowContainer { get; set; }
		public GameObject CatalogColumnContainer { get; set; }
		public VerticalLayoutGroup CatalogRowsVLayout { get; set; }
		public HorizontalLayoutGroup CatalogColumnsHLayout { get; set; }

		public Dictionary<string, UIDynamicButton> ModeButtons { get; set; } = new Dictionary<string, UIDynamicButton>();
		public Sprite MainCaptureButtonIcon { get; set; }
		public Sprite IconForCapturePerson { get; set; }
		public Sprite IconForCaptureScenes { get; set; }
		public Sprite IconForCaptureObject { get; set; }
		public Sprite IconForCaptureSelectedObject { get; set; }
		public Sprite IconForCaptureNone { get; set; }

		public List<GameObject> CatalogRows { get; set; } = new List<GameObject>();
		public List<GameObject> CatalogColumns { get; set; } = new List<GameObject>();

		public UIDynamicTextField TextToolTip { get; set; }
		public UIDynamicTextField TextDebugPanel { get; set; }
		public UIDynamicButton ButtonNameLabel { get; set; }
		public UIDynamicButton ButtonPopupMessageLabel { get; set; }

		public DynamicMainWindow(CatalogUiHelper catalogUi, GameObject windowContainer)
		{
			ParentUiHelper = catalogUi;
			ParentWindowContainer = windowContainer;
		}

	}
}
