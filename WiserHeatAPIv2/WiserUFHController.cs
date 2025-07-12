// Copyright © 2025 Nivloc Enterprises Ltd.
// Adapted from the Python implementation Copyright © 2021 Mark Parker
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace WiserHeatApiV2
	{
	public class WiserUFHController : WiserDevice
		{
		private readonly List<WiserUFHRelay> _relays = new List<WiserUFHRelay> ();

		public WiserUFHController (WiserRestController wiserRestController, Dictionary<string, object> data, Dictionary<string, object> deviceTypeData)
			 : base (wiserRestController, data, deviceTypeData)
			{
			_deviceLockEnabled = false;

			if (deviceTypeData.TryGetValue ("Relays", out var relays) && relays is List<object> relaysList)
				{
				foreach (var relay in relaysList)
					{
					if (relay is Dictionary<string, object> relayDict)
						{
						_relays.Add (new WiserUFHRelay (relayDict));
						}
					}
				}
			}
		public double CurrentTemperature => WiserTemperatureFunctions.FromWiserTemp (
			 _deviceTypeData.TryGetValue ("MeasuredTemperature", out var temp) ? temp : Constants.TEMP_OFF, "current");

		public bool? DewDetected => _deviceTypeData.TryGetValue ("DewDetected", out var detected) ? (bool?)Convert.ToBoolean (detected) : null;

		public bool? InterlockActive => _deviceTypeData.TryGetValue ("InterlockActive", out var active) ? (bool?)Convert.ToBoolean (active) : null;

		public bool? IsFullStrip => _deviceTypeData.TryGetValue ("IsFullStrip", out var fullStrip) ? (bool?)Convert.ToBoolean (fullStrip) : null;

		public int MaxFloorTemperature => _deviceTypeData.TryGetValue ("MaxHeatFloorTemperature", out var temp) ? Convert.ToInt32 (temp) : Constants.TEMP_MAXIMUM;

		public int MinFloorTemperature => _deviceTypeData.TryGetValue ("MinHeatFloorTemperature", out var temp) ? Convert.ToInt32 (temp) : Constants.TEMP_OFF;

		public string OutputType => _deviceTypeData.TryGetValue ("OutputType", out var type) ? type.ToString () : Constants.TEXT_UNKNOWN;

		public List<WiserUFHRelay> Relays => _relays;
		}

	public class WiserUFHControllerCollection
		{
		private readonly List<WiserUFHController> _ufhControllers = new List<WiserUFHController> ();

		public List<WiserUFHController> All => _ufhControllers;

		public int Count => _ufhControllers.Count;

		public WiserUFHController GetById (int id)
			{
			return _ufhControllers.FirstOrDefault (controller => controller.Id == id);
			}
		}
	public class WiserUFHRelay
		{
		public int DemandPercentage
			{
			get;
			}
		public bool Polarity
			{
			get;
			}
		public int Id
			{
			get;
			}

		public WiserUFHRelay (Dictionary<string, object> relayData)
			{
			DemandPercentage = relayData.TryGetValue ("DemandPercentage", out var demand) ? Convert.ToInt32 (demand) : 0;
			Polarity = relayData.TryGetValue ("Polarity", out var polarity) && Convert.ToBoolean (polarity);
			Id = relayData.TryGetValue ("id", out var id) ? Convert.ToInt32 (id) : 0;
			}
		}


	}
