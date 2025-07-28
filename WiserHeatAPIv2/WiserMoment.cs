// Copyright © 2025 Nivloc Enterprises Ltd.
// Adapted from the Python implementation Copyright © 2021 Mark Parker
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Threading.Tasks;

namespace WiserHeatApiV2
	{
	public class WiserMoment (WiserRestController wiserRestController, IDictionary<string, object> momentData)
		{
		private Task<bool> SendCommandAsync (object cmd, System.Threading.CancellationToken cancellationToken = default) =>
			wiserRestController.SendCommandAsync (RestConstants.WiserSystem, cmd, cancellationToken: cancellationToken);

		public int Id => momentData.TryGetValue ("id", out var id) ? Convert.ToInt32 (id, CultureInfo.InvariantCulture) : 0;

		public string Name => momentData.TryGetValue ("Name", out var name) ? name.ToString () : Constants.TextUnknown;

		public Task<bool> ActivateAsync (System.Threading.CancellationToken cancellationToken = default) =>
			SendCommandAsync (new
				{
				TriggerMoment = Id
				}, cancellationToken);
		}

	public class WiserMoments
		{
		private readonly WiserRestController _wiserRestController;

		public WiserMoments (WiserRestController wiserRestController, List<Dictionary<string, object>> momentsData)
			{
			_wiserRestController = wiserRestController;
			foreach (Dictionary<string, object> moment in momentsData)
				{
				All.Add (new WiserMoment (wiserRestController, moment));
				}
			}

		public void Update (List<Dictionary<string, object>> momentsData)
			{
			All.Clear ();
			foreach (Dictionary<string, object> moment in momentsData)
				{
				All.Add (new WiserMoment (_wiserRestController, moment));
				}
			}

		public List<WiserMoment> All { get; } = [];
		public int Count => All.Count;
		public WiserMoment GetById (int id) => All.FirstOrDefault (moment => moment.Id == id);
		}
	}
