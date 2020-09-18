﻿using juniperD.StatefullServices;
using juniperD.Models;
using System;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;

namespace juniperD.Models
{
	public class CatalogEntry
	{
		
		// Serialized...
		public Mutation Mutation { get; set; }
		public string ImageFormat { get;set; }
		public string CatalogEntryMode { get; set; }
		public string UniqueName { get; set; }
		public ImageInfo ImageInfo { get; set; }
		public string EntryType { get;set; }
		public bool Active { get; set; } = true;
		public float TransitionTimeInSeconds { get; set; } = 1;
		public float StartTimeRatio { get; set; } = 0;
		public float EndTimeRatio { get; set; } = 1;
		public List<CatalogEntry> ChildEntries { get;set;} = new List<CatalogEntry>();
		

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
		public Color CurrentBorderColor { get; set; }
		public List<EntrySubItem> EntrySubItemToggles { get;set;} = new List<EntrySubItem>();
		public UIDynamicButton UiShiftLeftButton { get; set; }
		public UIDynamicButton UiShiftRightButton { get; set; }
		public AnimatedItem AnimatedItem { get; internal set; }
		public GameObject UiAnimationPanel { get; internal set; }
		public GameObject UiAnimationInnerPanel { get; internal set; }
		public VerticalLayoutGroup UiAnimationVLayout { get; internal set; }
	}
}
