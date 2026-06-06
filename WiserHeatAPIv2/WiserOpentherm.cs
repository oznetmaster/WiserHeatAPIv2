// Copyright © 2026 Neil Colvin.
// Adapted from the Python implementation Copyright © 2021 Mark Parker
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

// WiserHeatApiV2.cs
namespace WiserHeatApiV2;

#if OPENTHERM
/// <summary>
/// Represents OpenTherm configuration and live data from the hub.
/// </summary>
public class WiserOpentherm (IDictionary<string, object> data, string enabledStatus)
	{
	internal readonly IDictionary<string, object> _data = data;

	/// <summary>Gets the CH flow active lower setpoint temperature.</summary>
	public double ChFlowActiveLowerSetpoint => WiserTemperatureFunctions.FromWiserTemp (
		 _data.TryGetValue ("chFlowActiveLowerSetpoint", out var value) ? value : null, "current");

	/// <summary>Gets the CH flow active upper setpoint temperature.</summary>
	public double ChFlowActiveUpperSetpoint => WiserTemperatureFunctions.FromWiserTemp (
		 _data.TryGetValue ("chFlowActiveUpperSetpoint", out var value) ? value : null, "current");

	/// <summary>Gets a value indicating whether CH1 flow is enabled.</summary>
	public bool Ch1FlowEnabled => _data.TryGetValue ("ch1FlowEnable", out var value) && ConvertInvariant.ToBoolean (value);

	/// <summary>Gets the CH1 flow setpoint temperature.</summary>
	public double Ch1FlowSetpoint => WiserTemperatureFunctions.FromWiserTemp (
		 _data.TryGetValue ("ch1FlowSetpoint", out var value) ? value : null, "current");

	/// <summary>Gets a value indicating whether CH2 flow is enabled.</summary>
	public bool Ch2FlowEnabled => _data.TryGetValue ("ch2FlowEnable", out var value) && ConvertInvariant.ToBoolean (value);

	/// <summary>Gets the CH2 flow setpoint temperature.</summary>
	public double Ch2FlowSetpoint => WiserTemperatureFunctions.FromWiserTemp (
		 _data.TryGetValue ("ch2FlowSetpoint", out var value) ? value : null, "current");

	/// <summary>Gets the hub-provided connection status string.</summary>
	public string ConnectionStatus { get; } = enabledStatus;

	/// <summary>Gets a value indicating whether OpenTherm is enabled.</summary>
	public bool Enabled => _data.TryGetValue ("Enabled", out var value) && ConvertInvariant.ToBoolean (value);

	/// <summary>Gets a value indicating whether domestic hot water is enabled.</summary>
	public bool HwEnabled => _data.TryGetValue ("dhwEnable", out var value) && ConvertInvariant.ToBoolean (value);

	/// <summary>Gets the hot water flow setpoint temperature.</summary>
	public double HwFlowSetpoint => WiserTemperatureFunctions.FromWiserTemp (
		 _data.TryGetValue ("dhwFlowSetpoint", out var value) ? value : null, "current");

	/// <summary>Gets the operating mode string, if available.</summary>
	public string? OperatingMode => _data.TryGetValue ("operatingMode", out var value) ? value.ToString () : null;

	/// <summary>Gets detailed operational data.</summary>
	public WiserOpenThermOperationalData OperationalData => _data.TryGetValue ("operationalData", out var data) && data is Dictionary<string, object> dataDict
				? new WiserOpenThermOperationalData (dataDict)
				: new WiserOpenThermOperationalData ([]);

	/// <summary>Gets predefined remote boiler parameters.</summary>
	public WiserOpenThermBoilerParameters BoilerParameters => _data.TryGetValue ("preDefinedRemoteBoilerParameters", out var data) && data is Dictionary<string, object> dataDict
				? new WiserOpenThermBoilerParameters (dataDict)
				: new WiserOpenThermBoilerParameters ([]);

	/// <summary>Gets the room setpoint temperature.</summary>
	public double RoomSetpoint => WiserTemperatureFunctions.FromWiserTemp (
		 _data.TryGetValue ("roomSetpoint", out var value) ? value : null, "current");

	/// <summary>Gets the current room temperature.</summary>
	public double RoomTemperature => WiserTemperatureFunctions.FromWiserTemp (
		 _data.TryGetValue ("roomTemperature", out var value) ? value : null, "current");

	/// <summary>Gets the id of the tracked room, if any.</summary>
	public int? TrackedRoomId => _data.TryGetValue ("TrackedRoomId", out var value) ? (int?)ConvertInvariant.ToInt32 (value) : null;
	}

/// <summary>
/// Represents remote boiler parameter capabilities for OpenTherm.
/// </summary>
public class WiserOpenThermBoilerParameters (Dictionary<string, object> data)
	{
	/// <summary>Gets a value indicating whether hot water setpoint transfer is enabled.</summary>
	public bool? HwSetpointTransferEnable => data.TryGetValue ("dhwSetpointTransferEnable", out var value) ? (bool?)ConvertInvariant.ToBoolean (value) : null;

	/// <summary>Gets a value indicating whether max CH setpoint transfer is enabled.</summary>
	public bool? ChSetpointTransferEnable => data.TryGetValue ("maxChSetpointTransferEnable", out var value) ? (bool?)ConvertInvariant.ToBoolean (value) : null;

	/// <summary>Gets a value indicating whether the HW setpoint is read/write.</summary>
	public bool? HwSetpointReadWrite => data.TryGetValue ("dhwSetpointReadWrite", out var value) ? (bool?)ConvertInvariant.ToBoolean (value) : null;

	/// <summary>Gets a value indicating whether the max CH setpoint is read/write.</summary>
	public bool? ChSetpointReadWrite => data.TryGetValue ("maxChSetpointReadWrite", out var value) ? (bool?)ConvertInvariant.ToBoolean (value) : null;
	}

/// <summary>
/// Represents operational telemetry from the OpenTherm interface.
/// </summary>
public class WiserOpenThermOperationalData (Dictionary<string, object> data)
	{
	/// <summary>Gets the central heating pressure in bar.</summary>
	public double ChPressureBar => data.TryGetValue ("ChPressureBar", out var value) ? ConvertInvariant.ToDouble (value) / 10 : 0;

	/// <summary>Gets the CH flow temperature.</summary>
	public double ChFlowTemperature => WiserTemperatureFunctions.FromWiserTemp (
		 data.TryGetValue ("Ch1FlowTemperature", out var value) ? value : (int?)null, "current");

	/// <summary>Gets the CH return temperature.</summary>
	public double ChReturnTemperature => WiserTemperatureFunctions.FromWiserTemp (
		 data.TryGetValue ("ChReturnTemperature", out var value) ? value : (int?)null, "current");

	/// <summary>Gets the hot water temperature.</summary>
	public double HwTemperature => WiserTemperatureFunctions.FromWiserTemp (
		 data.TryGetValue ("Dhw1Temperature", out var value) ? value : (int?)null, "current");

	/// <summary>Gets the relative modulation level, if available.</summary>
	public int? RelativeModulationLevel => data.TryGetValue ("RelativeModulationLevel", out var value) ? (int?)ConvertInvariant.ToInt32 (value) : null;

	/// <summary>Gets the raw slave status flags, if available.</summary>
	public int? SlaveStatus => data.TryGetValue ("SlaveStatus", out var value) ? (int?)ConvertInvariant.ToInt32 (value) : null;
	}
#endif

// -----
