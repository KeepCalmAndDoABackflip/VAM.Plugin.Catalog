using juniperD.Models;
using System.Collections.Generic;
using UnityEngine;

namespace juniperD.StatefullServices
{
	public static class DynamicUiExtensionMethods
	{
		public static void SetItems(this DynamicListBox listBox, List<string> items)
		{
			listBox.items = items;
			listBox.onRefresh.Invoke();
		}

		public static void Refresh(this DynamicListBox listBox)
		{
			listBox.onRefresh.Invoke();
		}

		public static void SetItems(this DynamicDropdownField listBox, List<string> items)
		{
			listBox.items = items;
			listBox.onRefresh.Invoke();
		}

		public static void Refresh(this DynamicDropdownField listBox)
		{
			listBox.onRefresh.Invoke();
		}

		public static void Destroy(this DynamicDropdownField listBox, MVRScript context)
		{
			listBox.ClearDropdownList(context);
			if (listBox.label != null) context.RemoveButton(listBox.label);
			if (listBox.infoBox != null) context.RemoveTextField(listBox.infoBox);
			if (listBox.selectedOption != null) context.RemoveButton(listBox.selectedOption);
		}

		public static void ClearDropdownList(this DynamicDropdownField dynamicDropdown, MVRScript context)
		{
			foreach (var prevOption in dynamicDropdown.options)
			{
				if (prevOption.Key == null) return;
				context.RemoveButton(prevOption.Key);
			}
			GameObject.Destroy(dynamicDropdown.backpanel);
			dynamicDropdown.backpanel = null;
		}

		public static void MinimizeDynamicDropdown(this DynamicDropdownField dropdown, MVRScript context, bool minimize = true)
		{
			dropdown.ClearDropdownList(context);
			if (dropdown.backpanel != null) dropdown.backpanel.transform.localScale = minimize ? Vector3.zero: Vector3.one;
			if (dropdown.infoBox != null) dropdown.infoBox.transform.localScale = minimize ? Vector3.zero : Vector3.one;
			if (dropdown.label != null) dropdown.label.transform.localScale = minimize ? Vector3.zero : Vector3.one;
			if (dropdown.selectedOption != null) dropdown.selectedOption.transform.localScale = minimize ? Vector3.zero : Vector3.one;
		}

	}


}
