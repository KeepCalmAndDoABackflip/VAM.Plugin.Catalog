using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace juniperD.Models
{
	public class DynamicDropdownField
	{
		

		public UIDynamicButton label { get; set;}
		public Dictionary<UIDynamicButton, string> options { get;set;} = new Dictionary<UIDynamicButton, string>();
		public UIDynamicTextField infoBox { get; set; }
		public string selectedValue { get; set; }
		public UIDynamicButton selectedOption { get; set; }
		public GameObject backpanel { get; set; }
		public bool isOpen { get; set; } = false;
		public UnityAction<string> onSelectedOption {get;set; }
		public UnityAction onRefresh { get; set; }
		public List<string> items { get; set; }
		public bool Minimized {get;set;}

	}
}
