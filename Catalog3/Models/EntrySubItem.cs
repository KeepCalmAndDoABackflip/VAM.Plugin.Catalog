using System.Collections.Generic;
using UnityEngine;

namespace juniperD.Models
{
	public class EntrySubItem
	{
		// Data...
		public string ItemName { get;set; }
		public string Label { get; set; }
		public bool CheckState { get; set;}

		// Control...
		public UIDynamicButton ItemActiveCheckbox { get;set; }
		public UIDynamicButton StopTrackingItemButton { get; set; }
		public List<UIDynamicButton> ExtraButtons { get;set; } = new List<UIDynamicButton>();
		public GameObject ButtonRow { get; set; }

	}

	public class FrameOptionItem
	{
		// Data...
		public string ItemName { get; set; }
		public string Label { get; set; }
		public bool CheckState { get; set; }

		// Control...
		public UIDynamicButton ItemActiveCheckbox { get; set; }
		public UIDynamicButton StopTrackingItemButton { get; set; }
		public List<UIDynamicButton> ExtraButtons { get; set; } = new List<UIDynamicButton>();
		public GameObject ButtonRow { get; set; }

	}
}
