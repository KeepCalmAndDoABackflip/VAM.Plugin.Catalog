
using System;
using System.Collections;
using UnityEngine.Events;

namespace juniperD.Models
{
	public class TransitionInProgress
	{
		public string GroupKey { get; }
		public float StartDelay { get;set; }
		public float Duration { get; set; }
		public string UniqueKey { get; } = Guid.NewGuid().ToString();
		public IEnumerator Transition { get; set;}
		public float Timeout {get; set;}
		public string Description { get;set;}
		public bool IsCancelled { get; private set; } = false;

		public CancellationToken CancellationToken { get; private set; }

		public TransitionInProgress(string uniqueKey, string groupKey, IEnumerator transition, float startDelay, float duration, float timeout = 0, CancellationToken cancellationToken = null)
		{
			GroupKey = groupKey;
			UniqueKey = uniqueKey;
			Transition = transition;
			StartDelay = startDelay;
			Duration = duration;
			var finalTimeout = timeout;
			if (finalTimeout == 0) finalTimeout = startDelay + (duration * 2);
			if (finalTimeout == 0) finalTimeout = 1;
			Timeout = finalTimeout;
			CancellationToken = cancellationToken;
		}

	}
}
