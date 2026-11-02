// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace WiserHeatApiV2;

/// <summary>
/// Represents an Underfloor Heating (UFH) controller device and its relays.
/// </summary>
public class WiserUFHController : WiserDevice
	{
	/// <summary>Create a UFH controller wrapper from hub device data.</summary>
	/// <param name="wiserRestController">The controller used to communicate with the hub.</param>
	/// <param name="data">The hub data for this device or channel.</param>
	/// <param name="deviceTypeData">The device-type-specific data returned by the hub.</param>
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
	/// <value>The current measured temperature in user units.</value>
	public double CurrentTemperature => WiserTemperatureFunctions.FromWiserTemp (
		 DeviceTypeData.TryGetValue ("MeasuredTemperature", out var temp) ? temp : Constants.TEMP_OFF, "current", WiserRestController.Units);

	/// <summary>Gets whether dew is currently detected, if available.</summary>
	/// <value>Whether dew is currently detected, if available.</value>
	public bool? DewDetected => DeviceTypeData.TryGetValue ("DewDetected", out var detected) ? ConvertInvariant.ToBoolean (detected) : null;

	/// <summary>Gets whether interlock is active, if available.</summary>
	/// <value>Whether interlock is active, if available.</value>
	public bool? InterlockActive => DeviceTypeData.TryGetValue ("InterlockActive", out var active) ? ConvertInvariant.ToBoolean (active) : null;

	/// <summary>Gets whether the controller is a full strip, if available.</summary>
	/// <value>Whether the controller is a full strip, if available.</value>
	public bool? IsFullStrip => DeviceTypeData.TryGetValue ("IsFullStrip", out var fullStrip) ? ConvertInvariant.ToBoolean (fullStrip) : null;

	/// <summary>Gets the maximum floor temperature limit.</summary>
	/// <value>The maximum floor temperature limit.</value>
	public int MaxFloorTemperature => DeviceTypeData.TryGetValue ("MaxHeatFloorTemperature", out var temp) ? ConvertInvariant.ToInt32 (temp) : Constants.TEMP_MAXIMUM;

	/// <summary>Gets the minimum floor temperature limit.</summary>
	/// <value>The minimum floor temperature limit.</value>
	public int MinFloorTemperature => DeviceTypeData.TryGetValue ("MinHeatFloorTemperature", out var temp) ? ConvertInvariant.ToInt32 (temp) : Constants.TEMP_OFF;

	/// <summary>Gets the UFH output type description.</summary>
	/// <value>The UFH output type description.</value>
	public string? OutputType => DeviceTypeData.TryGetValue ("OutputType", out var type) ? type.ToString () : Constants.TEXT_UNKNOWN;

	/// <summary>Gets the list of relay channels for this controller.</summary>
	/// <value>The list of relay channels for this controller.</value>
	public List<WiserUFHRelay> Relays { get; } = [];
	}

/// <summary>
/// Collection helper for UFH controllers.
/// </summary>
public class WiserUFHControllers
	{
	/// <summary>Gets all UFH controllers.</summary>
	/// <value>All UFH controllers.</value>
	public List<WiserUFHController> All { get; } = [];

	/// <summary>Gets the number of UFH controllers.</summary>
	/// <value>The number of UFH controllers.</value>
	public int Count => All.Count;

	/// <summary>Finds a UFH controller by device id.</summary>
	/// <param name="id">The identifier to look up.</param>
	/// <returns>The first matching underfloor-heating controller, or <see langword="null"/> if none matches.</returns>
	public WiserUFHController? GetById (int id) => All.FirstOrDefault (controller => controller.Id == id);
	}

/// <summary>Represents one UFH relay channel configuration/state.</summary>
/// <param name="relayData">The hub data describing this underfloor-heating relay.</param>
public class WiserUFHRelay (Dictionary<string, object> relayData)
	{
	/// <summary>Gets the demand percentage for this relay (0..100).</summary>
	/// <value>The demand percentage for this relay (0..100).</value>
	public int DemandPercentage
		{
		get;
		} = relayData.TryGetValue ("DemandPercentage", out var demand) ? ConvertInvariant.ToInt32 (demand) : 0;
	/// <summary>Gets the relay polarity (true for active-high).</summary>
	/// <value>The relay polarity (true for active-high).</value>
	public bool Polarity
		{
		get;
		} = relayData.TryGetValue ("Polarity", out var polarity) && ConvertInvariant.ToBoolean (polarity);
	/// <summary>Gets the relay id.</summary>
	/// <value>The relay id.</value>
	public int Id
		{
		get;
		} = relayData.TryGetValue ("id", out var id) ? ConvertInvariant.ToInt32 (id) : 0;
	}