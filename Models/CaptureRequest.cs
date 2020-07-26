using System;
using System.Collections.Generic;
using UnityEngine;

namespace CataloggerPlugin.Models
{
	public class CaptureRequest
	{
		public static int MUTATION_AQUISITION_MODE_CAPTURE = 0;
		public static int MUTATION_AQUISITION_MODE_SEQUENCE = 1;
		public static int MUTATION_AQUISITION_MODE_CAPTURE_ASSET = 2;
		public static int MUTATION_AQUISITION_MODE_CAPTURE_OBJECT = 3;
		public static int MUTATION_AQUISITION_MODE_CAPTURE_SESSION_OBJECT = 4;

		public int RequestModeEnum { get; set;} = MUTATION_AQUISITION_MODE_CAPTURE;
		public int LastRequestedCount { get; set; } = 0;
		public int CatalogEntriesStillLeftToCreate { get; set; } = 0;

		public Action BeforeMutationAction { get; set; }
		public Action AfterMutationAction { get; set; }

		public Func<List<Vector3>> VertexFetcher { get; set; }

	}
}
