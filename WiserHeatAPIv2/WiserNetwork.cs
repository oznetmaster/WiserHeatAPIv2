// Copyright © 2025 Nivloc Enterprises Ltd.
// Adapted from the Python implementation Copyright © 2021 Mark Parker
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

// WiserHeatApiV2.cs
namespace WiserHeatApiV2
	{
	public class WiserNetwork
		{
		private readonly Dictionary<string, object> _data;
		private readonly Dictionary<string, object> _dhcpStatus;
		private readonly Dictionary<string, object> _networkInterface;

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

		public List<WiserDetectedNetwork> DetectedAccessPoints { get; } = [];

		public string DhcpMode => _networkInterface.TryGetValue ("DhcpMode", out var mode) ? mode.ToString () : Constants.TextUnknown;

		public string Hostname => _networkInterface.TryGetValue ("HostName", out var hostname) ? hostname.ToString () : Constants.TextUnknown;

		public string IpAddress
			{
			get
				{
				if (DhcpMode == "Client")
					{
					return _dhcpStatus.TryGetValue ("IPv4Address", out var address) ? address.ToString () : Constants.TextUnknown;
					}
				else
					{
					return _networkInterface.TryGetValue ("IPv4HostAddress", out var address) ? address.ToString () : Constants.TextUnknown;
					}
				}
			}

		public string IpSubnetMask
			{
			get
				{
				if (DhcpMode == "Client")
					{
					return _dhcpStatus.TryGetValue ("IPv4SubnetMask", out var mask) ? mask.ToString () : Constants.TextUnknown;
					}
				else
					{
					return _networkInterface.TryGetValue ("IPv4SubnetMask", out var mask) ? mask.ToString () : Constants.TextUnknown;
					}
				}
			}

		public string IpGateway
			{
			get
				{
				if (DhcpMode == "Client")
					{
					return _dhcpStatus.TryGetValue ("IPv4DefaultGateway", out var gateway) ? gateway.ToString () : Constants.TextUnknown;
					}
				else
					{
					return _networkInterface.TryGetValue ("IPv4DefaultGateway", out var gateway) ? gateway.ToString () : Constants.TextUnknown;
					}
				}
			}

		public string IpPrimaryDNS
			{
			get
				{
				if (DhcpMode == "Client")
					{
					return _dhcpStatus.TryGetValue ("IPv4PrimaryDNS", out var dns) ? dns.ToString () : Constants.TextUnknown;
					}
				else
					{
					return _networkInterface.TryGetValue ("IPv4PrimaryDNS", out var dns) ? dns.ToString () : Constants.TextUnknown;
					}
				}
			}

		public string IpSecondaryDNS
			{
			get
				{
				if (DhcpMode == "Client")
					{
					return _dhcpStatus.TryGetValue ("IPv4SecondaryDNS", out var dns) ? dns.ToString () : Constants.TextUnknown;
					}
				else
					{
					return _networkInterface.TryGetValue ("IPv4SecondaryDNS", out var dns) ? dns.ToString () : Constants.TextUnknown;
					}
				}
			}

		public string MacAddress => _data.TryGetValue ("MacAddress", out var mac) ? mac.ToString () : Constants.TextUnknown;

		public int SignalPercent => _data.TryGetValue ("RSSI", out var rssi) && rssi is Dictionary<string, object> rssiDict && rssiDict.TryGetValue ("Current", out var current)
					? Math.Min (100, 2 * (ConvertInvariant.ToInt32 (current) + 100))
					: 0;

		public int SignalRssi => _data.TryGetValue ("RSSI", out var rssi) && rssi is Dictionary<string, object> rssiDict && rssiDict.TryGetValue ("Current", out var current)
					? ConvertInvariant.ToInt32 (current)
					: 0;

		public int SignalRssiMin => _data.TryGetValue ("RSSI", out var rssi) && rssi is Dictionary<string, object> rssiDict && rssiDict.TryGetValue ("Min", out var min)
					? ConvertInvariant.ToInt32 (min)
					: 0;

		public int SignalRssiMax => _data.TryGetValue ("RSSI", out var rssi) && rssi is Dictionary<string, object> rssiDict && rssiDict.TryGetValue ("Max", out var max)
					? ConvertInvariant.ToInt32 (max)
					: 0;

		public string SecurityMode => _data.TryGetValue ("SecurityMode", out var mode) ? mode.ToString () : Constants.TextUnknown;

		public string SSID => _data.TryGetValue ("SSID", out var ssid) ? ssid.ToString () : Constants.TextUnknown;
		}

	public class WiserDetectedNetwork (Dictionary<string, object> data)
		{
		public string? SSID => data.TryGetValue ("SSID", out var ssid) ? ssid.ToString () : null;

		public int? Channel => data.TryGetValue ("Channel", out var channel) ? ConvertInvariant.ToInt32 (channel) : (int?)null;

		public string? SecurityMode => data.TryGetValue ("SecurityMode", out var mode) ? mode.ToString () : null;

		public int? RSSI => data.TryGetValue ("RSSI", out var rssi) ? ConvertInvariant.ToInt32 (rssi) : (int?)null;
		}
	}

// -----

