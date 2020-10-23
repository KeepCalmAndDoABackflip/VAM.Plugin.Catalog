using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Events;

namespace juniperD.Models
{
	public class AwaiterRegistry
	{
		List<Awaiter> actions = new List<Awaiter>();
		UnityAction _onAllCompleteUnityAction;
		Awaiter _onAllCompleteAwaiter;
		public AwaiterRegistry(UnityAction whenAllAwaitersAreComplete = null)
		{
			_onAllCompleteUnityAction = whenAllAwaitersAreComplete;
		}

		public AwaiterRegistry(Awaiter whenAllAwaitersAreComplete = null)
		{
			_onAllCompleteAwaiter = whenAllAwaitersAreComplete;
		}

		public IEnumerable<string> GetTicketStubs()
		{
			return actions.Select(a => a.UniqueKey).ToList();
		}

		public Awaiter GetTicket(UnityAction<string> ticketCallback = null)
		{
			var awaiterUniqueKey = Guid.NewGuid().ToString();
			Awaiter newAwaiter = new Awaiter(() =>
			{
				try
				{
					if (actions.Any(a => a.UniqueKey == awaiterUniqueKey)) actions.Remove(actions.First(a => a.UniqueKey == awaiterUniqueKey));
					if (ticketCallback != null) ticketCallback.Invoke(awaiterUniqueKey);
					if (!actions.Any())
					{
						if (_onAllCompleteUnityAction != null) _onAllCompleteUnityAction.Invoke();
						if (_onAllCompleteAwaiter != null) _onAllCompleteAwaiter.Complete();
					}
				}
				catch (Exception e) { SuperController.LogError(e.ToString()); }
			}, awaiterUniqueKey);
			actions.Add(newAwaiter);
			return newAwaiter;
		}

	}
}
