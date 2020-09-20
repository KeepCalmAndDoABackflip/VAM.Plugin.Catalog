
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

		// Serialized...
		public string Name { get; set; }
		public AnimationElement MasterElement { get; set; }
		public List<AnimatedGroup> AnimatedState { get; set; } = new List<AnimatedGroup>();
		public List<AnimationElement> AnimatedElements { get; set; } = new List<AnimationElement>();


	}

	public class AnimatedGroup
	{
		// Serialized...
		public List<AnimationElement> AnimatedAxes { get; set; } = new List<AnimationElement>();
	}

	public class AnimationElement
	{
		// Serialized...
		public string Name { get; set;}
		public float TransitionTimeInSeconds { get; set; }
		public string Curve { get; set;}
		public bool DirectionFlipped { get; set;}
		public float SourceValue { get; set; }
		public float TargetValue { get; set; }
		public float StartAtRatio { get; set;}
		public float EndAtRatio { get; set; }

		public List<AnimationElement> ChildElements { get;set; } = new List<AnimationElement>();
		public List<AnimationElement> SubElements { get; set; } = new List<AnimationElement>();

		// UI
		public UIDynamicButton UiRightHandle { get; internal set; }
		public UIDynamicButton UiLeftHandle { get; internal set; }
		public UIDynamicButton UiActiveAreaBar { get; internal set; }
		public UIDynamicButton UiLabel { get; internal set; }
		public bool OnDisplay { get; internal set; }
	}


}
