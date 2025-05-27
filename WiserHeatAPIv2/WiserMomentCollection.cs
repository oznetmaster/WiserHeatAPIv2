// Copyright © 2025 Nivloc Enterprises Ltd.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WiserHeatApiV2
	{
	public class WiserMomentCollection
		{
		private readonly List<WiserMoment> _moments = new List<WiserMoment> ();
		private readonly WiserRestController _wiserRestController;

		public WiserMomentCollection (WiserRestController wiserRestController, List<Dictionary<string, object>> momentsData)
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
