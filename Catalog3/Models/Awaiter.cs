using System;
using UnityEngine.Events;

namespace juniperD.Models
{
	public class Awaiter
	{
		public string UniqueKey {get; private set;}
		public UnityAction OnComplete { get; private set; }
		public UnityAction OnCancel { get; private set; }

		public bool IsCancelled { get; private set;} = false;
		public bool IsAborted { get; private set; } = false;
		public bool IsCompleted { get; private set; } = false;

		public Awaiter(UnityAction onComplete, string useKey = null, UnityAction onCancel = null)
		{
			UniqueKey = useKey ?? Guid.NewGuid().ToString();
			OnComplete = onComplete;
		}

		public Awaiter GetCancellationAwaiter()
		{
			return new Awaiter(() => {
				Cancel();
			});
		}

		public void MarkAsCancelled()
		{
			IsCancelled = true;
		}

		public void MarkAsAborted()
		{
			IsAborted = true;
		}

		public void MarkAsCompleted()
		{
			IsCompleted = true;
		}

		public void Complete()
		{
			if (IsCompleted) //...already completed
			IsCompleted = true;
			if (OnComplete != null) OnComplete.Invoke();
		}

		public void Cancel()
		{
			if (IsCancelled) return; //...already cancelled
			IsCancelled = true;
			if (OnCancel != null) OnCancel.Invoke();
		}

	}
}
