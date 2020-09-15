
using System.Collections.Generic;
using UnityEngine.UI;

namespace juniperD.Models
{
	public class EntryAnimation
	{
		// Serialized...
		public List<AnimatedItem> AnimatedController { get; set; } = new List<AnimatedItem>();

		// UI
		public UIDynamicButton FramePanel { get;set; }
		public VerticalLayoutGroup Stack { get;set;}

	}

	public class AnimatedItem
	{

		public string Name { get;set;}
		public AnimatedElement MasterElement { get; set;}
		// Serialized...
		public List<AnimatedGroup> AnimatedState { get; set; } = new List<AnimatedGroup>();
		public List<AnimatedElement> AnimatedElements { get; set; } = new List<AnimatedElement>();
		public UIDynamicButton UiRightHandle { get; internal set; }
		public UIDynamicButton UiLeftHandle { get; internal set; }
		public UIDynamicButton UiItemBar { get; internal set; }
		public UIDynamicButton UiItemRow { get; internal set; }
	}

	public class AnimatedGroup
	{
		// Serialized...
		public List<AnimatedElement> AnimatedAxes { get; set; } = new List<AnimatedElement>();
	}

	public class AnimatedElement
	{
		// Serialized...
		public string Name { get; set;}
		public string Curve { get; set;}
		public bool DirectionFlipped { get; set;}
		public float SourceValue { get; set; }
		public float TargetValue { get; set; }
		public float StartAtRatio { get; set;}
		public float EndAtRatio { get; set; }
	}


}
