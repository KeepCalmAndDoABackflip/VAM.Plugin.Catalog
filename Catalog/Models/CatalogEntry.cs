using juniperD.StatefullServices;
using juniperD.Models;
using System;
using UnityEngine;
using System.Collections.Generic;

namespace juniperD.Models
{
	public class CatalogEntry
	{
		
		// Serialized...
		public Mutation Mutation { get; set; }
		public string ImageFormat { get;set; }
		public string CatalogMode { get; set; }
		public string UniqueName { get; set; }
		public ImageInfo ImageInfo { get; set; }

		//public string ImageAsEncodedString { get; set; }


		// Non-Serialized...
		public bool Selected { get; set; }
		public int Favorited { get; set; }
		public bool Discarded { get; set; }
		public GameObject UiCatalogEntryPanel { get; set; }
		public Texture2D ImageAsTexture { get; set; } // ...Deprecated
		public UIDynamicButton UiApplyButton { get;set; }
		public GameObject UiBottomButtonGroup { get; set; }
		public UIDynamicButton UiKeepButton { get; set; }
		public UIDynamicButton UiDiscardButton { get; set; }
		public UIDynamicButton UiSelectButton { get; set; }
		public GameObject UiParentCatalogRow { get;set; }
		public GameObject UiParentCatalogColumn { get; set; }
		public DragHelper PositionTracker {get; set; }
		public Action<CatalogEntry> ApplyAction {get; set; }
		public Border UiCatalogBorder { get; set; }
		public List<UiCatalogSubItem> InfoToggles { get;set;} = new List<UiCatalogSubItem>();
	}
}
