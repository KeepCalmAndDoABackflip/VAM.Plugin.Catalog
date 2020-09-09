using System.Collections.Generic;
using UnityEngine.Events;

namespace juniperD.Models
{
	public class DynamicListBox
	{
		public UIDynamicButton label { get; set;}
		public Dictionary<UIDynamicButton, string> itemButtons { get;set;} = new Dictionary<UIDynamicButton, string>();
		public UnityAction<string> onSelectedOption { get; set; }
		public UnityAction onRefresh { get;set;}
		public List<string> items { get; set; }
	}
}
