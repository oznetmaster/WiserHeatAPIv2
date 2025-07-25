// Copyright © 2025 Nivloc Enterprises Ltd.
// Adapted from the Python implementation Copyright © 2021 Mark Parker
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Threading.Tasks;

namespace WiserHeatApiV2
	{
	public class WiserMoment
		{
		private readonly WiserRestController _wiserRestController;
		private readonly IDictionary<string, object> _momentData;

		public WiserMoment (WiserRestController wiserRestController, IDictionary<string, object> momentData)
			{
			_wiserRestController = wiserRestController;
			_momentData = momentData;
			}

		private Task<bool> SendCommandAsync (object cmd, System.Threading.CancellationToken cancellationToken = default)
			{
			return _wiserRestController.SendCommandAsync (RestConstants.WiserSystem, cmd, cancellationToken: cancellationToken);
			}

		public int Id => _momentData.TryGetValue ("id", out var id) ? Convert.ToInt32 (id, CultureInfo.InvariantCulture) : 0;

		public string Name => _momentData.TryGetValue ("Name", out var name) ? name.ToString () : Constants.TextUnknown;

		public Task<bool> ActivateAsync (System.Threading.CancellationToken cancellationToken = default)
			{
			return SendCommandAsync (new
				{
				TriggerMoment = Id
				}, cancellationToken);
			}
		}

	public class WiserMoments
		{
		private readonly List<WiserMoment> _moments = new List<WiserMoment> ();
		private readonly WiserRestController _wiserRestController;

		public WiserMoments (WiserRestController wiserRestController, List<Dictionary<string, object>> momentsData)
			{
			_wiserRestController = wiserRestController;
			foreach (var moment in momentsData)
				{
				_moments.Add (new WiserMoment (wiserRestController, moment));
				}
			}

		public void Update (List<Dictionary<string, object>> momentsData)
			{
			_moments.Clear ();
			foreach (var moment in momentsData)
				{
				_moments.Add (new WiserMoment (_wiserRestController, moment));
				}
			}

		public List<WiserMoment> All => _moments;
		public int Count => _moments.Count;
		public WiserMoment GetById (int id)
			{
			return _moments.FirstOrDefault (moment => moment.Id == id);
			}
		}
	}
