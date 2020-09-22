
using System;
using System.Collections;

namespace juniperD.Models
{
	public class TransitionInProgress
	{
		public string GroupKey { get; }
		public string UniqueKey { get; } = Guid.NewGuid().ToString();
		public IEnumerator Transition { get; set;}
		public float Timeout {get; set;}

		public TransitionInProgress(string uniqueKey, string groupKey, IEnumerator transition, float timeout)
		{
			GroupKey = groupKey;
			UniqueKey = uniqueKey;
			Transition = transition;
			Timeout = timeout;
		}
	}
}
