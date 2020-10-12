using System.Collections.Generic;
using UnityEngine.Events;

namespace juniperD.Models
{
	public class SelectListItem
	{
		public string Label { get;set;}
		public UnityAction OnClickAction { get;set;}
		public List<SelectListItem> ChildItems { get; set; } = new List<SelectListItem>();
		public SelectListItem(string label, UnityAction onClickAction)
		{
			OnClickAction = onClickAction;
			Label = label;
		}

	}

}