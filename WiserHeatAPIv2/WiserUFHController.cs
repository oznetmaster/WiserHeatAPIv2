// Copyright © 2025 Nivloc Enterprises Ltd.
// Adapted from the Python implementation Copyright © 2021 Mark Parker
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace WiserHeatApiV2
	{
	public class WiserUFHController : WiserDevice
		{
		private readonly List<WiserUFHRelay> _relays = new List<WiserUFHRelay> ();

		public WiserUFHController (WiserRestController wiserRestController, Dictionary<string, object> data, Dictionary<string, object> deviceTypeData)
			 : base (wiserRestController, data, deviceTypeData)
			{
			DeviceLockEnabled = false;

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
			 DeviceTypeData.TryGetValue ("MeasuredTemperature", out var temp) ? temp : Constants.TempOff, "current");

		public bool? DewDetected => DeviceTypeData.TryGetValue ("DewDetected", out var detected) ? (bool?)Convert.ToBoolean (detected, CultureInfo.InvariantCulture) : null;

		public bool? InterlockActive => DeviceTypeData.TryGetValue ("InterlockActive", out var active) ? (bool?)Convert.ToBoolean (active, CultureInfo.InvariantCulture) : null;

		public bool? IsFullStrip => DeviceTypeData.TryGetValue ("IsFullStrip", out var fullStrip) ? (bool?)Convert.ToBoolean (fullStrip, CultureInfo.InvariantCulture) : null;

		public int MaxFloorTemperature => DeviceTypeData.TryGetValue ("MaxHeatFloorTemperature", out var temp) ? Convert.ToInt32 (temp, CultureInfo.InvariantCulture) : Constants.TempMaximum;

		public int MinFloorTemperature => DeviceTypeData.TryGetValue ("MinHeatFloorTemperature", out var temp) ? Convert.ToInt32 (temp, CultureInfo.InvariantCulture) : Constants.TempOff;

		public string OutputType => DeviceTypeData.TryGetValue ("OutputType", out var type) ? type.ToString () : Constants.TextUnknown;

		public List<WiserUFHRelay> Relays => _relays;
		}

	public class WiserUFHControllers
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
			DemandPercentage = relayData.TryGetValue ("DemandPercentage", out var demand) ? Convert.ToInt32 (demand, CultureInfo.InvariantCulture) : 0;
			Polarity = relayData.TryGetValue ("Polarity", out var polarity) && Convert.ToBoolean (polarity, CultureInfo.InvariantCulture);
			Id = relayData.TryGetValue ("id", out var id) ? Convert.ToInt32 (id, CultureInfo.InvariantCulture) : 0;
			}
		}


	}
