using System;
using System.Collections.Generic;
using UnityEngine;

namespace juniperD.Models
{
	public class CaptureRequest
	{
		public static int MUTATION_AQUISITION_MODE_CAPTURE { get; } = 0;
		public static int MUTATION_AQUISITION_MODE_SEQUENCE { get; } = 1;
		public static int MUTATION_AQUISITION_MODE_CAPTURE_ASSET { get; } = 2;
		public static int MUTATION_AQUISITION_MODE_CAPTURE_OBJECT { get; } = 3;
		public static int MUTATION_AQUISITION_MODE_CAPTURE_SESSION_OBJECT { get; } = 4;
		public static int MUTATION_AQUISITION_MODE_CAPTURE_ADDITIONAL_SESSION_OBJECT { get; } = 4;
		public static int MUTATION_AQUISITION_MODE_CAPTURE_EMPTY {get; } = 5;

		public int RequestModeEnum { get; set;} = MUTATION_AQUISITION_MODE_CAPTURE;
		public int LastRequestedCount { get; set; } = 0;
		public int CatalogEntriesStillLeftToCreate { get; set; } = 0;

		public Action BeforeMutationAction { get; set; }
		public Action AfterMutationAction { get; set; }

		public Func<List<Vector3>> VertexFetcher { get; set; }

	}
}
