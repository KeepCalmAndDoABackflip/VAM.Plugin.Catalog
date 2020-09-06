using SimpleJSON;
using UnityEngine;

namespace juniperD.Models
{
	public class AnimationLinkStep
	{
		public string Name { get; set; }
		public Vector3 StepPosition { get; set; }
		public Quaternion StepRotation { get; set; }
		public JSONClass AnimationStepJSON { get; set; }
	}

}
