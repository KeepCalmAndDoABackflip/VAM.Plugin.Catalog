
namespace juniperD.Models
{
	public class PoseTransition
	{
		public string UniqueKey { get; set; }
		public string AtomName { get; set; }
		public string ControllerName { get; set; }
		public string CurrentIteration { get; set; }
		public string TotalIterations { get; set; }
		public bool XRotationEnabled { get; internal set; }
		public bool YRotationEnabled { get; internal set; }
		public bool ZRotationEnabled { get; internal set; }
		public bool XPositionEnabled { get; internal set; }
		public bool YPositionEnabled { get; internal set; }
		public bool ZPositionEnabled { get; internal set; }

		public PoseTransition(string uniqueKey)
		{
			UniqueKey = uniqueKey;
		}
	}
}
