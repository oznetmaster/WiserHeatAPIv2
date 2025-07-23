// Copyright © 2025 Nivloc Enterprises Ltd.
// Adapted from the Python implementation Copyright © 2021 Mark Parker
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

// WiserHeatApiV2.cs
using System;
using System.Collections.Generic;

namespace WiserHeatApiV2
	{
	public class WiserNetwork
		{
		private readonly Dictionary<string, object> _data;
		private readonly Dictionary<string, object> _dhcpStatus;
		private readonly Dictionary<string, object> _networkInterface;
		private readonly List<WiserDetectedNetwork> _detectedAccessPoints = new List<WiserDetectedNetwork> ();

		public WiserNetwork (Dictionary<string, object> data)
			{
			_data = data;
			_dhcpStatus = _data.TryGetValue ("DhcpStatus", out var dhcpStatus) && dhcpStatus is Dictionary<string, object> dhcpDict
				 ? dhcpDict : new Dictionary<string, object> ();
			_networkInterface = _data.TryGetValue ("NetworkInterface", out var networkInterface) && networkInterface is Dictionary<string, object> networkDict
				 ? networkDict : new Dictionary<string, object> ();

			if (_data.TryGetValue ("DetectedAccessPoints", out var accessPoints) && accessPoints is List<object> accessPointsList)
				{
				foreach (var ap in accessPointsList)
					{
					if (ap is Dictionary<string, object> apDict)
						_detectedAccessPoints.Add (new WiserDetectedNetwork (apDict));
					}
				}
			}

		public List<WiserDetectedNetwork> DetectedAccessPoints => _detectedAccessPoints;

		public string DhcpMode => _networkInterface.TryGetValue ("DhcpMode", out var mode) ? mode.ToString () : Constants.TEXT_UNKNOWN;

		public string Hostname => _networkInterface.TryGetValue ("HostName", out var hostname) ? hostname.ToString () : Constants.TEXT_UNKNOWN;

		public string IpAddress
			{
			get
				{
				if (DhcpMode == "Client")
					return _dhcpStatus.TryGetValue ("IPv4Address", out var address) ? address.ToString () : Constants.TEXT_UNKNOWN;
				else
					{
					return _networkInterface.TryGetValue ("IPv4HostAddress", out var address) ? address.ToString () : Constants.TEXT_UNKNOWN;
					}
				}
			}

		public string IpSubnetMask
			{
			get
				{
				if (DhcpMode == "Client")
					return _dhcpStatus.TryGetValue ("IPv4SubnetMask", out var mask) ? mask.ToString () : Constants.TEXT_UNKNOWN;
				else
					{
					return _networkInterface.TryGetValue ("IPv4SubnetMask", out var mask) ? mask.ToString () : Constants.TEXT_UNKNOWN;
					}
				}
			}

		public string IpGateway
			{
			get
				{
				if (DhcpMode == "Client")
					return _dhcpStatus.TryGetValue ("IPv4DefaultGateway", out var gateway) ? gateway.ToString () : Constants.TEXT_UNKNOWN;
				else
					{
					return _networkInterface.TryGetValue ("IPv4DefaultGateway", out var gateway) ? gateway.ToString () : Constants.TEXT_UNKNOWN;
					}
				}
			}

		public string IpPrimaryDNS
			{
			get
				{
				if (DhcpMode == "Client")
					return _dhcpStatus.TryGetValue ("IPv4PrimaryDNS", out var dns) ? dns.ToString () : Constants.TEXT_UNKNOWN;
				else
					{
					return _networkInterface.TryGetValue ("IPv4PrimaryDNS", out var dns) ? dns.ToString () : Constants.TEXT_UNKNOWN;
					}
				}
			}

		public string IpSecondaryDNS
			{
			get
				{
				if (DhcpMode == "Client")
					return _dhcpStatus.TryGetValue ("IPv4SecondaryDNS", out var dns) ? dns.ToString () : Constants.TEXT_UNKNOWN;
				else
					{
					return _networkInterface.TryGetValue ("IPv4SecondaryDNS", out var dns) ? dns.ToString () : Constants.TEXT_UNKNOWN;
					}
				}
			}

		public string MacAddress => _data.TryGetValue ("MacAddress", out var mac) ? mac.ToString () : Constants.TEXT_UNKNOWN;

		public int SignalPercent
			{
			get
				{
				if (_data.TryGetValue ("RSSI", out var rssi) && rssi is Dictionary<string, object> rssiDict && rssiDict.TryGetValue ("Current", out var current))
					return Math.Min (100, 2 * (Convert.ToInt32 (current) + 100));
				return 0;
				}
			}

		public int SignalRssi
			{
			get
				{
				if (_data.TryGetValue ("RSSI", out var rssi) && rssi is Dictionary<string, object> rssiDict && rssiDict.TryGetValue ("Current", out var current))
					return Convert.ToInt32 (current);
				return 0;
				}
			}

		public int SignalRssiMin
			{
			get
				{
				if (_data.TryGetValue ("RSSI", out var rssi) && rssi is Dictionary<string, object> rssiDict && rssiDict.TryGetValue ("Min", out var min))
					return Convert.ToInt32 (min);
				return 0;
				}
			}

		public int SignalRssiMax
			{
			get
				{
				if (_data.TryGetValue ("RSSI", out var rssi) && rssi is Dictionary<string, object> rssiDict && rssiDict.TryGetValue ("Max", out var max))
					return Convert.ToInt32 (max);
				return 0;
				}
			}

		public string SecurityMode => _data.TryGetValue ("SecurityMode", out var mode) ? mode.ToString () : Constants.TEXT_UNKNOWN;

		public string SSID => _data.TryGetValue ("SSID", out var ssid) ? ssid.ToString () : Constants.TEXT_UNKNOWN;
		}

	public class WiserDetectedNetwork
		{
		private readonly Dictionary<string, object> _data;

		public WiserDetectedNetwork (Dictionary<string, object> data)
			{
			_data = data;
			}

		public string? SSID => _data.TryGetValue ("SSID", out var ssid) ? ssid.ToString () : null;

		public int? Channel => _data.TryGetValue ("Channel", out var channel) ? Convert.ToInt32 (channel) : (int?)null;

		public string? SecurityMode => _data.TryGetValue ("SecurityMode", out var mode) ? mode.ToString () : null;

		public int? RSSI => _data.TryGetValue ("RSSI", out var rssi) ? Convert.ToInt32 (rssi) : (int?)null;
		}
	}

// -----

