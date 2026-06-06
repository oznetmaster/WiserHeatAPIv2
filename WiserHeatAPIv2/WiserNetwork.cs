// Copyright © 2026 Neil Colvin.
// Adapted from the Python implementation Copyright © 2021 Mark Parker
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

// WiserHeatApiV2.cs
namespace WiserHeatApiV2;

/// <summary>
/// Represents network configuration and Wi‑Fi signal details for the hub.
/// </summary>
/// <remarks>
/// Values are taken directly from the hub payload. When a field is missing, string properties return
/// <see cref="Constants.TEXT_UNKNOWN"/> and numeric properties return 0. IPv4 details are exposed;
/// DHCP client versus static addressing is handled by selecting values from the appropriate payload section.
/// The detected access points list may be empty.
/// </remarks>
public class WiserNetwork
	{
	private readonly Dictionary<string, object> _data;
	private readonly Dictionary<string, object> _dhcpStatus;
	private readonly Dictionary<string, object> _networkInterface;

	/// <summary>
	/// Initializes a new instance of the <see cref="WiserNetwork"/> class from a hub network payload.
	/// </summary>
	/// <param name="data">Raw network payload from the hub's "Network" endpoint.</param>
	/// <remarks>
	/// Parses DHCP status, network interface details, and detected access points if present in the payload.
	/// </remarks>
	public WiserNetwork (Dictionary<string, object> data)
		{
		_data = data;
		_dhcpStatus = _data.TryGetValue ("DhcpStatus", out var dhcpStatus) && dhcpStatus is Dictionary<string, object> dhcpDict
			 ? dhcpDict : [];
		_networkInterface = _data.TryGetValue ("NetworkInterface", out var networkInterface) && networkInterface is Dictionary<string, object> networkDict
			 ? networkDict : [];

		if (_data.TryGetValue ("DetectedAccessPoints", out var accessPoints) && accessPoints is List<object> accessPointsList)
			{
			foreach (var ap in accessPointsList)
				{
				if (ap is Dictionary<string, object> apDict)
					DetectedAccessPoints.Add (new WiserDetectedNetwork (apDict));
				}
			}
		}

	/// <summary>
	/// Gets the list of detected Wi‑Fi access points.
	/// </summary>
	/// <value>
	/// A list of <see cref="WiserDetectedNetwork"/> instances discovered by the hub during its last scan,
	/// or an empty list if none were reported.
	/// </value>
	public List<WiserDetectedNetwork> DetectedAccessPoints { get; } = [];

	/// <summary>
	/// Gets the DHCP mode of the network interface.
	/// </summary>
	/// <value>
	/// A string such as "Client" (DHCP) or "Static". Returns <see cref="Constants.TEXT_UNKNOWN"/> if not provided.
	/// </value>
	public string? DhcpMode => _networkInterface.TryGetValue ("DhcpMode", out var mode) ? mode.ToString () : Constants.TEXT_UNKNOWN;

	/// <summary>
	/// Gets the hostname reported by the hub.
	/// </summary>
	/// <value>
	/// The hub hostname, or <see cref="Constants.TEXT_UNKNOWN"/> if not provided.
	/// </value>
	public string Hostname => _networkInterface.GetStringOr ("HostName");

	/// <summary>
	/// Gets the IPv4 address.
	/// </summary>
	/// <value>
	/// The IPv4 address string. When <see cref="DhcpMode"/> is "Client", the value is read from DHCP status;
	/// otherwise, it is read from the static interface configuration. Returns <see cref="Constants.TEXT_UNKNOWN"/> if unavailable.
	/// </value>
	public string IpAddress => DhcpMode == "Client" ? _dhcpStatus.GetStringOr ("IPv4Address") : _networkInterface.GetStringOr ("IPv4HostAddress");

	/// <summary>
	/// Gets the IPv4 subnet mask.
	/// </summary>
	/// <value>
	/// The subnet mask string. When <see cref="DhcpMode"/> is "Client", the value is read from DHCP status;
	/// otherwise, it is read from the static interface configuration. Returns <see cref="Constants.TEXT_UNKNOWN"/> if unavailable.
	/// </value>
	public string IpSubnetMask => DhcpMode == "Client" ? _dhcpStatus.GetStringOr ("IPv4SubnetMask") : _networkInterface.GetStringOr ("IPv4SubnetMask");

	/// <summary>
	/// Gets the IPv4 default gateway.
	/// </summary>
	/// <value>
	/// The default gateway string. When <see cref="DhcpMode"/> is "Client", the value is read from DHCP status;
	/// otherwise, it is read from the static interface configuration. Returns <see cref="Constants.TEXT_UNKNOWN"/> if unavailable.
	/// </value>
	public string IpGateway => DhcpMode == "Client" ? _dhcpStatus.GetStringOr ("IPv4DefaultGateway") : _networkInterface.GetStringOr ("IPv4DefaultGateway");

	/// <summary>
	/// Gets the IPv4 primary DNS server.
	/// </summary>
	/// <value>
	/// The primary DNS server string. When <see cref="DhcpMode"/> is "Client", the value is read from DHCP status;
	/// otherwise, it is read from the static interface configuration. Returns <see cref="Constants.TEXT_UNKNOWN"/> if unavailable.
	/// </value>
	public string IpPrimaryDNS => DhcpMode == "Client" ? _dhcpStatus.GetStringOr ("IPv4PrimaryDNS") : _networkInterface.GetStringOr ("IPv4PrimaryDNS");

	/// <summary>
	/// Gets the IPv4 secondary DNS server.
	/// </summary>
	/// <value>
	/// The secondary DNS server string. When <see cref="DhcpMode"/> is "Client", the value is read from DHCP status;
	/// otherwise, it is read from the static interface configuration. Returns <see cref="Constants.TEXT_UNKNOWN"/> if unavailable.
	/// </value>
	public string IpSecondaryDNS => DhcpMode == "Client" ? _dhcpStatus.GetStringOr ("IPv4SecondaryDNS") : _networkInterface.GetStringOr ("IPv4SecondaryDNS");

	/// <summary>
	/// Gets the MAC address.
	/// </summary>
	/// <value>
	/// The MAC address string reported by the hub, or <see cref="Constants.TEXT_UNKNOWN"/> if not provided.
	/// </value>
	public string MacAddress => _data.GetStringOr ("MacAddress");

	/// <summary>
	/// Gets a derived Wi‑Fi signal strength in percent (0–100) from RSSI.
	/// </summary>
	/// <value>
	/// An integer from 0 to 100 computed as <c>min(100, 2 × (RSSI + 100))</c>, or 0 if no RSSI is available.
	/// </value>
	/// <remarks>
	/// This is a heuristic mapping of RSSI (dBm) to a percentage for display purposes.
	/// </remarks>
	public int SignalPercent => _data.TryGetValue ("RSSI", out var rssi) && rssi is Dictionary<string, object> rssiDict && rssiDict.TryGetValue ("Current", out var current)
				? Math.Min (100, 2 * (ConvertInvariant.ToInt32 (current) + 100))
				: 0;

	/// <summary>
	/// Gets the current RSSI value (dBm).
	/// </summary>
	/// <value>
	/// The current RSSI as an integer (typically negative dBm), or 0 if not available.
	/// </value>
	public int SignalRssi => _data.TryGetValue ("RSSI", out var rssi) && rssi is Dictionary<string, object> rssiDict && rssiDict.TryGetValue ("Current", out var current)
				? ConvertInvariant.ToInt32 (current)
				: 0;

	/// <summary>
	/// Gets the minimum RSSI value (dBm) observed over the reporting interval.
	/// </summary>
	/// <value>
	/// The minimum RSSI as an integer, or 0 if not available.
	/// </value>
	public int SignalRssiMin => _data.TryGetValue ("RSSI", out var rssi) && rssi is Dictionary<string, object> rssiDict && rssiDict.TryGetValue ("Min", out var min)
				? ConvertInvariant.ToInt32 (min)
				: 0;

	/// <summary>
	/// Gets the maximum RSSI value (dBm) observed over the reporting interval.
	/// </summary>
	/// <value>
	/// The maximum RSSI as an integer, or 0 if not available.
	/// </value>
	public int SignalRssiMax => _data.TryGetValue ("RSSI", out var rssi) && rssi is Dictionary<string, object> rssiDict && rssiDict.TryGetValue ("Max", out var max)
				? ConvertInvariant.ToInt32 (max)
				: 0;

	/// <summary>
	/// Gets the Wi‑Fi security mode.
	/// </summary>
	/// <value>
	/// The security mode string (for example, "WPA2"), or <see cref="Constants.TEXT_UNKNOWN"/> if not provided.
	/// </value>
	public string SecurityMode => _data.GetStringOr ("SecurityMode");

	/// <summary>
	/// Gets the SSID of the connected network.
	/// </summary>
	/// <value>
	/// The SSID string, or <see cref="Constants.TEXT_UNKNOWN"/> if not provided.
	/// </value>
	public string SSID => _data.GetStringOr ("SSID");
	}

/// <summary>
/// Represents a detected Wi‑Fi network.
/// </summary>
/// <remarks>
/// Values are populated from an individual entry in the hub's DetectedAccessPoints list. Properties return <see langword="null"/>
/// when the corresponding field is not present in the payload.
/// </remarks>
public class WiserDetectedNetwork (Dictionary<string, object> data)
	{
	/// <summary>
	/// Gets the SSID of the network, if provided.
	/// </summary>
	/// <value>
	/// The SSID string, or <see langword="null"/> if not present.
	/// </value>
	public string? SSID => data.TryGetValue ("SSID", out var ssid) ? ssid.ToString () : null;

	/// <summary>
	/// Gets the Wi‑Fi channel, if provided.
	/// </summary>
	/// <value>
	/// The channel number, or <see langword="null"/> if not present.
	/// </value>
	public int? Channel => data.TryGetValue ("Channel", out var channel) ? ConvertInvariant.ToInt32 (channel) : (int?)null;

	/// <summary>
	/// Gets the security mode, if provided.
	/// </summary>
	/// <value>
	/// The security mode string, or <see langword="null"/> if not present.
	/// </value>
	public string? SecurityMode => data.TryGetValue ("SecurityMode", out var mode) ? mode.ToString () : null;

	/// <summary>
	/// Gets the signal RSSI value (dBm), if provided.
	/// </summary>
	/// <value>
	/// The RSSI value as an integer, or <see langword="null"/> if not present.
	/// </value>
	public int? RSSI => data.TryGetValue ("RSSI", out var rssi) ? ConvertInvariant.ToInt32 (rssi) : (int?)null;
	}

// -----
