using SimpleJSON;
using System.Collections.Generic;
using UnityEngine;

namespace juniperD.Models
{
	public class AnimationLink
	{
		public string Name { get; set; }
		public string SlaveAtom { get; set; }
		public string SlaveController { get; set; }
		public Vector3 SlaveAtomPosition { get; set; }
		public Quaternion SlaveAtomRotation { get; set; }
		public Vector3 SlaveControllerPosition { get; set; }
		public Quaternion SlaveControllerRotation { get; set; }
		public Vector3 AnimationAtomPosition { get; set; }
		public Quaternion AnimationAtomRotation { get; set; }
		public JSONClass AnimationPatternJSON { get; set; }
		public List<AnimationLinkStep> AnimationSteps { get; set; } = new List<AnimationLinkStep>();
	}

}
