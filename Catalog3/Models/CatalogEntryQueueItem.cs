

using juniperD.StatefullServices;
using UnityEngine;
using UnityEngine.Events;

namespace juniperD.Models
{
	public class CatalogEntryQueueItem
	{

		public string EntryKey { get; private set; }
		public UnityAction ApplyCatalogEntryAction { get; private set; }
		public float Timeout { get; private set; } = 1;
		public bool Cancelled { get; private set; } = false;
		public bool Complete { get; private set; } = false;
		public bool Busy { get; private set; } = false;


		public CatalogEntryQueueItem(string entryKey, float timeout, UnityAction applyCatalogEntryAction)
		{
			EntryKey = entryKey;
			ApplyCatalogEntryAction = applyCatalogEntryAction;
			Timeout = timeout;
		}
		public void MarkAsCancelled()
		{
			Cancelled = true;
		}

		public void Start(CatalogPlugin pluginContext)
		{
			if (Cancelled) return;
			Complete = false;
			Busy = true;
			ApplyCatalogEntryAction.Invoke();
			pluginContext.StartCoroutine(WaitForTimeout());
		}

		System.Collections.IEnumerator WaitForTimeout()
		{
			yield return new WaitForSeconds(Timeout);
			if (!Complete)
			{
				SuperController.LogError("ERROR: Entry timed out. Cancelled");
				Cancelled = true;
				Busy = false;
			}
		}

		public void MarkAsCompleted()
		{
			Complete = true;
			Cancelled = true;
			Busy = false;
		}

	}
}
