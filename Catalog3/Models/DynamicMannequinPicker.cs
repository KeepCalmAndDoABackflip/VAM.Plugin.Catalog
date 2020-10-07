using System.Collections.Generic;
using UnityEngine;

namespace juniperD.Models
{
	public class DynamicMannequinPicker
	{
		public string SelectedAtomName { get; set; }
		public string SelectedControllerName { get; set; }
		public string SelectedPointAction { get; set; }
		public bool Minimized { get;set;} = false;
		public GameObject Window { get;set;}
		public GameObject BackPanel { get;set;}
		public GameObject MannequinOverlay { get; set; }
		public UIDynamicButton CloseButton { get; set;}
		public UIDynamicButton MinimizeButton { get; set; }
		public UIDynamicButton RefreshButton { get; set; }

		public UIDynamicButton AtomMiniLabel { get; set; }
		public UIDynamicButton ControllerMiniLabel { get; set; }
		public UIDynamicButton MiniOverlay { get; set; }

		public DynamicJointPoint SelectedJoint { get; set; }
		public List<DynamicJointPoint> JointPoints { get; set; } = new List<DynamicJointPoint>();
		public UIDynamicButton ButtonSelectionHalo { get; set; }
		public DynamicDropdownField AtomSelector { get;set; }
		public DynamicDropdownField PointSelector { get; set; }
		public DynamicDropdownField PointActionSelector { get;set; }
		public DynamicDropdownField AddFeatureSelector { get; set; }
		public DynamicDropdownField MasterLinkedAtomSelector { get;set; }
		public DynamicDropdownField SlaveLinkedAtomSelector{ get; set; }


	}
}
