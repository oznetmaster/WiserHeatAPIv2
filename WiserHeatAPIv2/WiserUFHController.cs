// Copyright © 2025 Nivloc Enterprises Ltd.
// Adapted from the Python implementation Copyright © 2021 Mark Parker
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace WiserHeatApiV2
	{
	/// <summary>
	/// Represents an Underfloor Heating (UFH) controller device and its relays.
	/// </summary>
	public class WiserUFHController : WiserDevice
		{
		/// <summary>Create a UFH controller wrapper from hub device data.</summary>
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
		/// <summary>Gets the current measured temperature in user units.</summary>
		public double CurrentTemperature => WiserTemperatureFunctions.FromWiserTemp (
			 DeviceTypeData.TryGetValue ("MeasuredTemperature", out var temp) ? temp : Constants.TempOff, "current");

		/// <summary>Gets whether dew is currently detected, if available.</summary>
		public bool? DewDetected => DeviceTypeData.TryGetValue ("DewDetected", out var detected) ? ConvertInvariant.ToBoolean (detected) : null;

		/// <summary>Gets whether interlock is active, if available.</summary>
		public bool? InterlockActive => DeviceTypeData.TryGetValue ("InterlockActive", out var active) ? ConvertInvariant.ToBoolean (active) : null;

		/// <summary>Gets whether the controller is a full strip, if available.</summary>
		public bool? IsFullStrip => DeviceTypeData.TryGetValue ("IsFullStrip", out var fullStrip) ? ConvertInvariant.ToBoolean (fullStrip) : null;

		/// <summary>Gets the maximum floor temperature limit.</summary>
		public int MaxFloorTemperature => DeviceTypeData.TryGetValue ("MaxHeatFloorTemperature", out var temp) ? ConvertInvariant.ToInt32 (temp) : Constants.TempMaximum;

		/// <summary>Gets the minimum floor temperature limit.</summary>
		public int MinFloorTemperature => DeviceTypeData.TryGetValue ("MinHeatFloorTemperature", out var temp) ? ConvertInvariant.ToInt32 (temp) : Constants.TempOff;

		/// <summary>Gets the UFH output type description.</summary>
		public string OutputType => DeviceTypeData.TryGetValue ("OutputType", out var type) ? type.ToString () : Constants.TextUnknown;

		/// <summary>Gets the list of relay channels for this controller.</summary>
		public List<WiserUFHRelay> Relays { get; } = [];
		}

	/// <summary>
	/// Collection helper for UFH controllers.
	/// </summary>
	public class WiserUFHControllers
		{
		/// <summary>Gets all UFH controllers.</summary>
		public List<WiserUFHController> All { get; } = [];

		/// <summary>Gets the number of UFH controllers.</summary>
		public int Count => All.Count;

		/// <summary>Finds a UFH controller by device id.</summary>
		public WiserUFHController GetById (int id) => All.FirstOrDefault (controller => controller.Id == id);
		}

	/// <summary>Represents one UFH relay channel configuration/state.</summary>
	public class WiserUFHRelay (Dictionary<string, object> relayData)
		{
		/// <summary>Gets the demand percentage for this relay (0..100).</summary>
		public int DemandPercentage
			{
			get;
			} = relayData.TryGetValue ("DemandPercentage", out var demand) ? ConvertInvariant.ToInt32 (demand) : 0;
		/// <summary>Gets the relay polarity (true for active-high).</summary>
		public bool Polarity
			{
			get;
			} = relayData.TryGetValue ("Polarity", out var polarity) && ConvertInvariant.ToBoolean (polarity);
		/// <summary>Gets the relay id.</summary>
		public int Id
			{
			get;
			} = relayData.TryGetValue ("id", out var id) ? ConvertInvariant.ToInt32 (id) : 0;
		}
	}
