
namespace juniperD.Models
{
	public class CancellationToken
	{
		public static string ENUM_CANCEL_WITH_ROLLBACK_TO_PREVIOUS = "reverse any current transitions to the previously known state";
		public static string ENUM_CANCEL_WITH_ROLLBACK_TO_BASE = "reverse any current transitions to a known base state";
		public static string ENUM_CANCEL_WITH_ABORT = "stop any current transitions without any cleanup routine";

		public bool IsCancelled { get; private set; } = false;
		public string EnumCanceOption { get; private set; } = ENUM_CANCEL_WITH_ROLLBACK_TO_PREVIOUS;

		public void Cancel(string ENUM_CANCEL)
		{
			IsCancelled = true;
			EnumCanceOption = ENUM_CANCEL ?? ENUM_CANCEL_WITH_ROLLBACK_TO_PREVIOUS;
		}

	}
}
