// Copyright © 2025 Nivloc Enterprises Ltd.
// Adapted from the Python implementation Copyright © 2021 Mark Parker
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace WiserHeatApiV2
	{
	public class WiserUFHController : WiserDevice
		{
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
						Relays.Add (new WiserUFHRelay (relayDict));
						}
					}
				}
			}
		public double CurrentTemperature => WiserTemperatureFunctions.FromWiserTemp (
			 DeviceTypeData.TryGetValue ("MeasuredTemperature", out var temp) ? temp : Constants.TempOff, "current");

		public bool? DewDetected => DeviceTypeData.TryGetValue ("DewDetected", out var detected) ? ConvertInvariant.ToBoolean (detected) : null;

		public bool? InterlockActive => DeviceTypeData.TryGetValue ("InterlockActive", out var active) ? ConvertInvariant.ToBoolean (active) : null;

		public bool? IsFullStrip => DeviceTypeData.TryGetValue ("IsFullStrip", out var fullStrip) ? ConvertInvariant.ToBoolean (fullStrip) : null;

		public int MaxFloorTemperature => DeviceTypeData.TryGetValue ("MaxHeatFloorTemperature", out var temp) ? ConvertInvariant.ToInt32 (temp) : Constants.TempMaximum;

		public int MinFloorTemperature => DeviceTypeData.TryGetValue ("MinHeatFloorTemperature", out var temp) ? ConvertInvariant.ToInt32 (temp) : Constants.TempOff;

		public string OutputType => DeviceTypeData.TryGetValue ("OutputType", out var type) ? type.ToString () : Constants.TextUnknown;

		public List<WiserUFHRelay> Relays { get; } = [];
		}

	public class WiserUFHControllers
		{
		public List<WiserUFHController> All { get; } = [];

		public int Count => All.Count;

		public WiserUFHController GetById (int id) => All.FirstOrDefault (controller => controller.Id == id);
		}
	public class WiserUFHRelay (Dictionary<string, object> relayData)
		{
		public int DemandPercentage
			{
			get;
			} = relayData.TryGetValue ("DemandPercentage", out var demand) ? ConvertInvariant.ToInt32 (demand) : 0;
		public bool Polarity
			{
			get;
			} = relayData.TryGetValue ("Polarity", out var polarity) && ConvertInvariant.ToBoolean (polarity);
		public int Id
			{
			get;
			} = relayData.TryGetValue ("id", out var id) ? ConvertInvariant.ToInt32 (id) : 0;
		}
	}
