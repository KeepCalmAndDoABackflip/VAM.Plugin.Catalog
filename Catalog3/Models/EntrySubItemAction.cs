
using UnityEngine;
using UnityEngine.Events;

namespace juniperD.Models
{
	public class EntrySubItemAction
	{
		public string IconName { get;set;} = null;// ...name of icon in the "Resources" folder
		public string Tooltip { get;set; } = "";
		public Color ButtonColor { get;set;} = Color.red;
		public Color TextColor { get; set; } = Color.white;
		public string Text { get;set;} = "";
		public UnityAction<EntrySubItem> ClickAction{ get; set; }
		public int ButtonWidth { get; set; } = 20;
	}
}
