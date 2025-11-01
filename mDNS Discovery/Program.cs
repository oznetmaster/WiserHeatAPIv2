using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace WiserHubDiscovery;

public class WiserDiscoveredHub (IPAddress ipAddress, int port = 80)
	{
	public IPAddress IpAddress { get; } = ipAddress;
	public int Port { get; } = port;
	public string Url => Port == 80 ? $"http://{IpAddress}" : $"http://{IpAddress}:{Port}";
	public DateTime DiscoveredAt { get; } = DateTime.Now;

	public override string ToString () => $"{Url} (discovered at {DiscoveredAt:HH:mm:ss})";
	}

public class WiserDiscoveryOptions
	{
	public int TimeoutSeconds { get; set; } = 30;
	public int MaxConcurrency { get; set; } = 50;
	public int PingTimeout { get; set; } = 500;
	public int HttpTimeout { get; set; } = 1500;
	public bool ShowProgress { get; set; } = false;
	public bool ShowDebug { get; set; } = false;
	}

public class NetworkInfo
	{
	private static readonly byte[] _zeroBitCountTable =
		[.. Enumerable.Range (0, 256).Select (b => (byte)(8 - Convert.ToString (b, 2).Count (c => c == '1')))];

	public IPAddress NetworkBase
		{
		get;
		}
	public IPAddress NetworkAddress
		{
		get;
		}
	public IPAddress SubnetMask
		{
		get;
		}
	public IPAddress BroadcastAddress
		{
		get;
		}
	public int HostCount
		{
		get;
		}

	public NetworkInfo (IPAddress networkAddress, IPAddress subnetMask)
		{
		NetworkAddress = networkAddress;
		SubnetMask = subnetMask;
		BroadcastAddress = CalculateBroadcastAddress (networkAddress, subnetMask);

		var networkBytes = networkAddress.GetAddressBytes ();
		NetworkBase = new IPAddress ([.. networkBytes[0..3], 0]);

		// Calculate number of host addresses
		var maskBytes = subnetMask.GetAddressBytes ();
		var hostBits = 0;
		foreach (var maskByte in maskBytes)
			{
			hostBits += CountZeroBits (maskByte);
			}

		HostCount = (1 << hostBits) - 2; // Subtract network and broadcast addresses
		}

	private static IPAddress CalculateBroadcastAddress (IPAddress networkAddress, IPAddress subnetMask)
		{
		var networkBytes = networkAddress.GetAddressBytes ();
		var maskBytes = subnetMask.GetAddressBytes ();
		var broadcastBytes = new byte[4];

		for (var i = 0; i < 4; i++)
			{
			broadcastBytes[i] = (byte)(networkBytes[i] | (~maskBytes[i]));
			}

		return new IPAddress (broadcastBytes);
		}

	private static int CountZeroBits (byte value) => _zeroBitCountTable[value];
	}

public class WiserHubDiscovery
	{
	private static readonly HttpClient _sharedHttpClient = new () { Timeout = TimeSpan.FromMilliseconds (3000) };

	/// <summary>
	/// Smart discovery that adapts to network size with proper broadcast calculation
	/// </summary>
	public static async Task<ConcurrentBag<WiserDiscoveredHub>> DiscoverHubsAsync (
		 WiserDiscoveryOptions? options = null,
		 CancellationToken cancellationToken = default)
		{
		options ??= new WiserDiscoveryOptions ();

		List<NetworkInfo> networkInfos = GetLocalNetworkInfos (options.ShowDebug);
		var discoveredHubs = new ConcurrentBag<WiserDiscoveredHub> ();

		if (options.ShowProgress)
			{
			Console.WriteLine ($"Scanning {networkInfos.Count} networks:");
			foreach (NetworkInfo netInfo in networkInfos)
				{
				Console.WriteLine ($"  - {netInfo.NetworkAddress}/{netInfo.SubnetMask} ({netInfo.HostCount} hosts, broadcast: {netInfo.BroadcastAddress})");
				}
			}

		using var cts = CancellationTokenSource.CreateLinkedTokenSource (cancellationToken);
		cts.CancelAfter (TimeSpan.FromSeconds (options.TimeoutSeconds));

		try
			{
			foreach (NetworkInfo networkInfo in networkInfos)
				{
				if (cts.Token.IsCancellationRequested)
					break;

				if (networkInfo.HostCount > 1000) // Large network
					{
					if (options.ShowProgress)
						Console.WriteLine ($"Large network detected ({networkInfo.NetworkAddress}), using smart scanning...");
					ConcurrentBag<WiserDiscoveredHub> smartResults = await SmartScanLargeNetworkAsync (networkInfo, options, cts.Token);
					// Replace this line:
					// discoveredHubs.AddRange (smartResults);
					// With the following:
					foreach (WiserDiscoveredHub hub in smartResults)
						discoveredHubs.Add (hub);
					}
				else
					{
					// Use normal scanning for smaller networks
					ConcurrentBag<WiserDiscoveredHub> normalResults = await FastScanNetworkAsync (networkInfo, options, cts.Token);
					foreach (WiserDiscoveredHub hub in normalResults)
						discoveredHubs.Add (hub);
					}
				}
			}
		catch (OperationCanceledException)
			{
			if (options.ShowProgress)
				Console.WriteLine ("Discovery timed out");
			}

		return discoveredHubs;
		}

	/// <summary>
	/// Smart scanning for large networks - scan in /24 chunks
	/// </summary>
	private static async Task<ConcurrentBag<WiserDiscoveredHub>> SmartScanLargeNetworkAsync (
		 NetworkInfo networkInfo,
		 WiserDiscoveryOptions options,
		 CancellationToken cancellationToken)
		{
		var discoveredHubs = new ConcurrentBag<WiserDiscoveredHub> ();
		var networkBytes = networkInfo.NetworkAddress.GetAddressBytes ();

		// For /16 networks, focus on common IoT subnets
		if (networkInfo.SubnetMask.Equals (new IPAddress ([255, 255, 0, 0])))
			{
			var commonSubnets = new[] { 0, 1, 10, 20, 50, 100, 150, 200 };

			foreach (var subnet in commonSubnets)
				{
				if (cancellationToken.IsCancellationRequested)
					break;

				// Create /24 network info for this subnet
				var subnetNetwork = new IPAddress ([.. networkBytes[0..2], (byte)subnet, 0]);
				var subnetInfo = new NetworkInfo (subnetNetwork, _subnetMask);

				if (options.ShowProgress)
					Console.WriteLine ($"  Scanning IoT subnet: {subnetInfo.NetworkBase}.*");

				ConcurrentBag<WiserDiscoveredHub> results = await FastScanNetworkAsync (subnetInfo, options, cancellationToken);
				foreach (WiserDiscoveredHub hub in results)
					discoveredHubs.Add (hub);
				}
			}

		return discoveredHubs;
		}

	/// <summary>
	/// Fast scan of a network with proper broadcast address calculation
	/// </summary>
	private static async Task<ConcurrentBag<WiserDiscoveredHub>> FastScanNetworkAsync (
		 NetworkInfo networkInfo,
		 WiserDiscoveryOptions options,
		 CancellationToken cancellationToken)
		{
		var discoveredHubs = new ConcurrentBag<WiserDiscoveredHub> ();

		if (options.ShowProgress)
			Console.WriteLine ($"  Ping scanning {networkInfo.NetworkBase}.*...");

		// Phase 1: Ping scan to find alive IPs
		ConcurrentBag<IPAddress> aliveIPs = await PingScanNetworkAsync (networkInfo, options, cancellationToken);

		if (options.ShowProgress)
			{
			Console.WriteLine ($"  Found {aliveIPs.Count} alive IPs, checking for Wiser Hubs...");
			}

		// Phase 2: HTTP check only alive IPs
		if (!aliveIPs.IsEmpty)
			{
			var httpSemaphore = new SemaphoreSlim (Math.Min (20, aliveIPs.Count));
			Task[] httpTasks = [.. aliveIPs.Select (ip =>
				 CheckWiserHubAsync (ip, httpSemaphore, discoveredHubs, options, cancellationToken))];

			await Task.WhenAll (httpTasks);
			}

		if (options.ShowProgress)
			{
			Console.WriteLine ($"  ✅ {networkInfo.NetworkBase}.* scan complete - Found {discoveredHubs.Count} hubs");
			}

		return discoveredHubs;
		}

	/// <summary>
	/// Ping scan with proper subnet mask handling for ANY network size
	/// </summary>
	private static async Task<ConcurrentBag<IPAddress>> PingScanNetworkAsync (
		 NetworkInfo networkInfo,
		 WiserDiscoveryOptions options,
		 CancellationToken cancellationToken)
		{
		var aliveIPs = new ConcurrentBag<IPAddress> ();
		var semaphore = new SemaphoreSlim (options.MaxConcurrency);
		var tasks = new List<Task> ();

		// Get actual gateway IPs to skip them
		HashSet<IPAddress> gatewayIPs = GetGatewayIPs (networkInfo.NetworkAddress, options.ShowDebug);

		if (options.ShowDebug)
			{
			Console.WriteLine ($"    Network: {networkInfo.NetworkAddress}");
			Console.WriteLine ($"    Subnet Mask: {networkInfo.SubnetMask}");
			Console.WriteLine ($"    Broadcast: {networkInfo.BroadcastAddress}");
			Console.WriteLine ($"    Host Count: {networkInfo.HostCount}");
			}

		// Handle different network sizes appropriately
		if (networkInfo.HostCount <= 254) // Small networks (/24 and smaller)
			{
			await ScanSmallNetworkAsync (networkInfo, gatewayIPs, semaphore, aliveIPs, options, cancellationToken);
			}
		else if (networkInfo.HostCount <= 65534) // Medium networks (/16)
			{
			await ScanMediumNetworkAsync (networkInfo, gatewayIPs, semaphore, aliveIPs, options, cancellationToken);
			}
		else // Large networks (/8 and bigger)
			{
			await ScanLargeNetworkAsync (networkInfo, gatewayIPs, semaphore, aliveIPs, options, cancellationToken);
			}

		await Task.WhenAll (tasks);
		return aliveIPs;
		}

	/// <summary>
	/// Scan small networks (up to /24) by iterating through all valid host IPs
	/// </summary>
	private static async Task ScanSmallNetworkAsync (
		 NetworkInfo networkInfo,
		 HashSet<IPAddress> gatewayIPs,
		 SemaphoreSlim semaphore,
		 ConcurrentBag<IPAddress> aliveIPs,
		 WiserDiscoveryOptions options,
		 CancellationToken cancellationToken)
		{
		var networkBytes = networkInfo.NetworkAddress.GetAddressBytes ();
		var broadcastBytes = networkInfo.BroadcastAddress.GetAddressBytes ();
		var tasks = new List<Task> ();

		// Calculate the range of the last octet that can vary
		var startLastOctet = networkBytes[3];
		var endLastOctet = broadcastBytes[3];

		if (options.ShowDebug)
			{
			Console.WriteLine ($"    Scanning small network: {networkBytes[0]}.{networkBytes[1]}.{networkBytes[2]}.{startLastOctet}-{endLastOctet}");
			}

		for (int i = startLastOctet; i <= endLastOctet; i++)
			{
			if (cancellationToken.IsCancellationRequested)
				break;

			var ip = new IPAddress ([.. networkBytes[0..3], (byte)i]);

			// Skip network address, broadcast address, and gateways
			if (ShouldSkipIP (ip, networkInfo, gatewayIPs))
				{
				if (options.ShowDebug)
					{
					if (ip == networkInfo.NetworkAddress)
						Console.WriteLine ($"    ⏭️ Skipping network address: {ip}");
					else if (ip == networkInfo.BroadcastAddress)
						Console.WriteLine ($"    ⏭️ Skipping broadcast address: {ip}");
					else if (gatewayIPs.Contains (ip))
						Console.WriteLine ($"    ⏭️ Skipping gateway: {ip}");
					}

				continue;
				}

			tasks.Add (PingIPAsync (ip, semaphore, aliveIPs, options.PingTimeout, cancellationToken));
			}

		await Task.WhenAll (tasks);
		}

	// For /16 networks, scan common subnets to avoid overwhelming
	private static readonly byte[] _commonSubnets = [0, 1, 8, 10, 20, 50, 100, 150, 200, 254];

	/// <summary>
	/// Scan medium networks (/16) in /24 chunks to avoid overwhelming the network
	/// </summary>
	private static async Task ScanMediumNetworkAsync (
		 NetworkInfo networkInfo,
		 HashSet<IPAddress> gatewayIPs,
		 SemaphoreSlim semaphore,
		 ConcurrentBag<IPAddress> aliveIPs,
		 WiserDiscoveryOptions options,
		 CancellationToken cancellationToken)
		{
		var networkBytes = networkInfo.NetworkAddress.GetAddressBytes ();

		if (options.ShowDebug)
			{
			Console.WriteLine ($"    Scanning /16 network in /24 chunks: {networkBytes[0]}.{networkBytes[1]}.x.x");
			}

		foreach (var thirdOctet in _commonSubnets)
			{
			if (cancellationToken.IsCancellationRequested)
				break;

			// Create a /24 subnet to scan
			var subnetAddress = new IPAddress ([.. networkBytes[0..2], (byte)thirdOctet, 0]);
			//var subnetMask = IPAddress.Parse ("255.255.255.0");
			var subnetInfo = new NetworkInfo (subnetAddress, _subnetMask);

			if (options.ShowDebug)
				{
				Console.WriteLine ($"    Scanning /24 chunk: {networkBytes[0]}.{networkBytes[1]}.{thirdOctet}.*");
				}

			await ScanSmallNetworkAsync (subnetInfo, gatewayIPs, semaphore, aliveIPs, options, cancellationToken);
			}
		}

	/// <summary>
	/// Scan large networks (/8) with very limited scope
	/// </summary>
	private static async Task ScanLargeNetworkAsync (
		 NetworkInfo networkInfo,
		 HashSet<IPAddress> gatewayIPs,
		 SemaphoreSlim semaphore,
		 ConcurrentBag<IPAddress> aliveIPs,
		 WiserDiscoveryOptions options,
		 CancellationToken cancellationToken)
		{
		var networkBytes = networkInfo.NetworkAddress.GetAddressBytes ();

		if (options.ShowDebug)
			{
			Console.WriteLine ($"    Scanning /8 network with limited scope: {networkBytes[0]}.x.x.x");
			}

		// For /8 networks, only scan very common subnets
		IPAddress[] limitedRanges =
		[
			new IPAddress ([networkBytes[0], 0, 0, 0]),
			new IPAddress ([networkBytes[0], 0, 1, 0]),
			new IPAddress ([networkBytes[0], 1, 0, 0]),
			new IPAddress ([networkBytes[0], 1, 1, 0]),
 ];

		foreach (IPAddress range in limitedRanges)
			{
			if (cancellationToken.IsCancellationRequested)
				break;

			var subnetInfo = new NetworkInfo (range, _subnetMask);

			if (options.ShowDebug)
				{
				Console.WriteLine ($"    Scanning limited /8 chunk: {range}.*");
				}

			await ScanSmallNetworkAsync (subnetInfo, gatewayIPs, semaphore, aliveIPs, options, cancellationToken);
			}
		}
	/// <summary>
	/// Determine if an IP should be skipped based on actual network calculations
	/// </summary>
	private static bool ShouldSkipIP (IPAddress ip, NetworkInfo networkInfo, HashSet<IPAddress> gatewayIPs)
		{
		// Skip explicitly detected gateways
		if (gatewayIPs.Contains (ip))
			return true;

		// Skip actual network address
		if (ip == networkInfo.NetworkAddress)
			return true;

		// Skip actual broadcast address
		return ip == networkInfo.BroadcastAddress;
		}

	private static readonly IPAddress _subnetMask = new ([255, 255, 255, 0]);
	private static readonly IPAddress[] _fallbackRanges = [new ([192, 168, 1, 0]), new ([192, 168, 0, 0]), new ([192, 168, 8, 0]), new ([10, 0, 0, 0]), new ([172, 16, 0, 0])];
	private static readonly IPAddress _fallbackMask = new ([255, 255, 255, 0]);
	/// <summary>
	/// Get network information with proper subnet mask calculations
	/// </summary>
	private static List<NetworkInfo> GetLocalNetworkInfos (bool showDebug = false)
		{
		var networkInfos = new List<NetworkInfo> ();

		try
			{
			var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces ()
				 .Where (ni => ni.OperationalStatus == OperationalStatus.Up &&
								 ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
				 .ToList ();

			foreach (NetworkInterface? ni in networkInterfaces)
				{
				IPInterfaceProperties ipProps = ni.GetIPProperties ();
				foreach (UnicastIPAddressInformation addr in ipProps.UnicastAddresses)
					{
					if (addr.Address.AddressFamily == AddressFamily.InterNetwork && addr.IPv4Mask != null)
						{
						var ipBytes = addr.Address.GetAddressBytes ();
						var maskBytes = addr.IPv4Mask.GetAddressBytes ();
						// Use manual bit shifting for clarity and network byte order
						var ipInt = (ipBytes[0] << 24) | (ipBytes[1] << 16) | (ipBytes[2] << 8) | ipBytes[3];
						var maskInt = (maskBytes[0] << 24) | (maskBytes[1] << 16) | (maskBytes[2] << 8) | maskBytes[3];
						var networkInt = ipInt & maskInt;
						byte[] networkBytes = [(byte)((networkInt >> 24) & 0xFF), (byte)((networkInt >> 16) & 0xFF), (byte)((networkInt >> 8) & 0xFF), (byte)(networkInt & 0xFF)];

						var networkAddress = new IPAddress (networkBytes);
						var networkInfo = new NetworkInfo (networkAddress, addr.IPv4Mask);

						// Avoid duplicates
						if (!networkInfos.Any (ni => ni.NetworkAddress.Equals (networkAddress) && ni.SubnetMask.Equals (addr.IPv4Mask)))
							{
							networkInfos.Add (networkInfo);
							if (showDebug)
								{
								Console.WriteLine ($"  Added network: {networkAddress}/{addr.IPv4Mask} ({networkInfo.HostCount} hosts)");
								}
							}
						}
					}
				}
			}
		catch (Exception ex)
			{
			if (showDebug)
				{
				Console.WriteLine ($"Error getting network information: {ex.Message}");
				}
			// Fallback to common private ranges as /24 networks

			foreach (IPAddress range in _fallbackRanges)
				{
				networkInfos.Add (new NetworkInfo (range, _fallbackMask));
				}
			}

		return networkInfos;
		}

	/// <summary>
	/// Get actual gateway IPs from network interfaces with debugging
	/// </summary>
	private static HashSet<IPAddress> GetGatewayIPs (IPAddress networkBase, bool showDebug = false)
		{
		var gatewayIPs = new HashSet<IPAddress> ();

		try
			{
			IEnumerable<NetworkInterface> networkInterfaces = NetworkInterface.GetAllNetworkInterfaces ()
				 .Where (ni => ni.OperationalStatus == OperationalStatus.Up);

			if (showDebug)
				{
				Console.WriteLine ($"  🔍 Detecting gateways for network {networkBase}.*");
				}

			foreach (NetworkInterface? ni in networkInterfaces)
				{
				IPInterfaceProperties ipProps = ni.GetIPProperties ();

				// Check if this interface is on the target network
				var isTargetNetwork = false;
				foreach (UnicastIPAddressInformation addr in ipProps.UnicastAddresses)
					{
					if (addr.Address.AddressFamily == AddressFamily.InterNetwork)
						{
						var parts = addr.Address.GetAddressBytes ();
						if (parts.Length == 4)
							{
							var interfaceNetworkBase = new IPAddress ([.. parts[0..3], 0]);
							if (interfaceNetworkBase.Equals (networkBase))
								{
								isTargetNetwork = true;
								if (showDebug)
									{
									Console.WriteLine ($"    Interface '{ni.Name}' is on target network: {networkBase}");
									}

								break;
								}
							}
						}
					}

				// Only get gateways for interfaces on the target network
				if (isTargetNetwork)
					{
					foreach (GatewayIPAddressInformation gateway in ipProps.GatewayAddresses)
						{
						if (gateway.Address.AddressFamily == AddressFamily.InterNetwork)
							{
							IPAddress gatewayIP = gateway.Address;
							_ = gatewayIPs.Add (gatewayIP);

							if (showDebug)
								{
								Console.WriteLine ($"    ✅ Found gateway: {gatewayIP}");
								}
							}
						}
					}
				}
			}
		catch (Exception ex)
			{
			if (showDebug)
				{
				Console.WriteLine ($"    ❌ Error detecting gateways: {ex.Message}");
				}
			}

		if (showDebug)
			{
			if (gatewayIPs.Count > 0)
				{
				Console.WriteLine ($"    🎯 Total gateways to skip: {string.Join (", ", gatewayIPs)}");
				}
			else
				{
				Console.WriteLine ($"    ℹ️ No gateways detected for {networkBase}.*");
				}
			}

		return gatewayIPs;
		}

	private static async Task PingIPAsync (
		 IPAddress ip,
		 SemaphoreSlim semaphore,
		 ConcurrentBag<IPAddress> aliveIPs,
		 int timeout,
		 CancellationToken cancellationToken)
		{
		await semaphore.WaitAsync (cancellationToken);
		try
			{
			using var ping = new Ping ();
			PingReply reply = await ping.SendPingAsync (ip, timeout);

			if (reply.Status == IPStatus.Success)
				// If ping is successful, add to alive IPs
				aliveIPs.Add (ip);
			}
		catch
			{
			// Ping failed, ignore
			}
		finally
			{
			_ = semaphore.Release ();
			}
		}

	/// <summary>
	/// Check for Wiser Hub using individual HttpClient with improved error handling
	/// </summary>
	private static async Task CheckWiserHubAsync (
		 IPAddress ip,
		 SemaphoreSlim semaphore,
		 ConcurrentBag<WiserDiscoveredHub> discoveredHubs,
		 WiserDiscoveryOptions options,
		 CancellationToken cancellationToken)
		{
		await semaphore.WaitAsync (cancellationToken);
		try
			{
			// Use longer timeout and better configuration
			using var client = new HttpClient (new HttpClientHandler ()
				{
				ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
				})
				{
				Timeout = TimeSpan.FromMilliseconds (options.HttpTimeout),
				DefaultRequestHeaders = { ConnectionClose = true }
				};

			if (await IsWiserHubWithClientAsync (ip, client))
				{
				var hub = new WiserDiscoveredHub (ip);
				discoveredHubs.Add (hub);
				if (options.ShowProgress)
					Console.WriteLine ($"    ✅ Found Wiser Hub: {hub.Url}");
				}
			}
		catch (Exception ex) when (options.ShowDebug)
			{
			Console.WriteLine ($"    ❌ Error checking {ip}: {ex.Message}");
			}
		finally
			{
			_ = semaphore.Release ();
			}
		}
	/// <summary>
	/// Test if IP is a Wiser Hub using provided HttpClient
	/// </summary>
	private static async Task<bool> IsWiserHubWithClientAsync (IPAddress ipAddress, HttpClient client, int port = 80)
		{
		try
			{
			var baseUrl = $"http://{ipAddress}:{port}";
			var endpoint = "/data/v2/domain/";

			HttpResponseMessage response = await client.GetAsync (baseUrl + endpoint);

			// 401 Unauthorized is the Wiser Hub signature
			return response.StatusCode == HttpStatusCode.Unauthorized;
			}
		catch
			{
			return false;
			}
		}

	/// <summary>
	/// Tests if a specific IP address is a Wiser Hub
	/// </summary>
	public static async Task<bool> IsWiserHubAsync (IPAddress ipAddress, int port = 80)
		{
		try
			{
			var baseUrl = $"http://{ipAddress}:{port}";
			var endpoint = "/data/v2/domain/";

			HttpResponseMessage response = await _sharedHttpClient.GetAsync (baseUrl + endpoint);

			// 401 Unauthorized is the Wiser Hub signature
			return response.StatusCode == HttpStatusCode.Unauthorized;
			}
		catch
			{
			return false;
			}
		}

	/// <summary>
	/// Quick scan of specific IP range with proper broadcast calculation
	/// </summary>
	public static async Task<ConcurrentBag<WiserDiscoveredHub>> QuickScanRangeAsync (
		 IPAddress networkBase,
		 int startIp = 0,
		 int endIp = 254,
		 WiserDiscoveryOptions? options = null)
		{
		options ??= new WiserDiscoveryOptions { ShowProgress = true };

		// Create a /24 network info for the range
		//var networkAddress = IPAddress.Parse ($"{networkBase.ToString().Substring(0, networkBase.ToString().LastIndexOf('.'))}.0");
		//var subnetMask = IPAddress.Parse ("255.255.255.0");
		//var networkInfo = new NetworkInfo (networkAddress, subnetMask);
		var networkInfo = new NetworkInfo (networkBase, _subnetMask);

		var aliveIPs = new ConcurrentBag<IPAddress> ();
		var pingSemaphore = new SemaphoreSlim (100);
		var pingTasks = new List<Task> ();

		// Get gateway IPs to skip them
		HashSet<IPAddress> gatewayIPs = GetGatewayIPs (networkBase, options.ShowDebug);

		for (var i = startIp; i <= endIp; i++)
			{
			var baseBytes = networkBase.GetAddressBytes ();
			var ip = new IPAddress ([.. baseBytes[0..3], (byte)i]); //{ baseBytes[0], baseBytes[1], baseBytes[2], (byte)i });

			// Apply proper network-aware skipping logic
			if (ShouldSkipIP (ip, networkInfo, gatewayIPs))
				continue;

			pingTasks.Add (PingIPAsync (ip, pingSemaphore, aliveIPs, options.PingTimeout, CancellationToken.None));
			}

		await Task.WhenAll (pingTasks);

		var discoveredHubs = new ConcurrentBag<WiserDiscoveredHub> ();
		var httpSemaphore = new SemaphoreSlim (20);
		Task[] httpTasks = [.. aliveIPs.Select (ip =>
			 CheckWiserHubAsync (ip, httpSemaphore, discoveredHubs, options, CancellationToken.None))];

		await Task.WhenAll (httpTasks);
		return discoveredHubs;
		}

	/// <summary>
	/// Test the known hub IP directly for debugging
	/// </summary>
	public static async Task<string> DiagnoseKnownHubAsync (string ipAddress = "192.168.8.196")
		{
		var results = new List<string>
			{
			$"=== Diagnosing {ipAddress} ==="
			};

		// Test 1: Ping
		try
			{
			using var ping = new Ping ();
			PingReply reply = await ping.SendPingAsync (ipAddress, 500);
			results.Add ($"Ping: {reply.Status} ({reply.RoundtripTime}ms)");
			}
		catch (Exception ex)
			{
			results.Add ($"Ping failed: {ex.Message}");
			}

		// Test 2: Shared HttpClient
		try
			{
			HttpResponseMessage response = await _sharedHttpClient.GetAsync ($"http://{ipAddress}/data/v2/domain/");
			results.Add ($"Shared HttpClient: {response.StatusCode}");
			}
		catch (Exception ex)
			{
			results.Add ($"Shared HttpClient failed: {ex.Message}");
			}

		// Test 3: Individual HttpClient
		try
			{
			using var client = new HttpClient { Timeout = TimeSpan.FromMilliseconds (1500) };
			HttpResponseMessage response = await client.GetAsync ($"http://{ipAddress}/data/v2/domain/");
			results.Add ($"Individual HttpClient: {response.StatusCode}");
			}
		catch (Exception ex)
			{
			results.Add ($"Individual HttpClient failed: {ex.Message}");
			}

		return string.Join ("\n", results);
		}

	/// <summary>
	/// Diagnose network and gateway detection for debugging
	/// </summary>
	public static void DiagnoseNetworks ()
		{
		Console.WriteLine ("=== Network Detection Diagnosis ===");

		List<NetworkInfo> networkInfos = GetLocalNetworkInfos (true);
		foreach (NetworkInfo networkInfo in networkInfos)
			{
			Console.WriteLine ($"\nNetwork: {networkInfo.NetworkAddress}/{networkInfo.SubnetMask}");
			Console.WriteLine ($"  Base: {networkInfo.NetworkBase}.*");
			Console.WriteLine ($"  Broadcast: {networkInfo.BroadcastAddress}");
			Console.WriteLine ($"  Host count: {networkInfo.HostCount}");

			HashSet<IPAddress> gateways = GetGatewayIPs (networkInfo.NetworkAddress, true);
			Console.WriteLine ($"  Gateways: {(gateways.Count != 0 ? string.Join (", ", gateways) : "None detected")}");
			}
		}
	}

// Example usage program
class Program
	{
	static async Task Main ()
		{
		Console.WriteLine ("🚀 Wiser Hub Network Discovery (Broadcast-Aware Version)");
		Console.WriteLine ("========================================================");

		// Optional: Diagnose network detection
		//Console.WriteLine("\n--- Network Diagnosis ---");
		//WiserHubDiscovery.DiagnoseNetworks();

		var options = new WiserDiscoveryOptions
			{
			ShowProgress = true,
			ShowDebug = false,
			TimeoutSeconds = 30,
			HttpTimeout = 5000,  // Increase from 1500ms to 5000ms
			PingTimeout = 1000   // Also increase ping timeout slightly
			};

		// Add this before the main discovery
		Console.WriteLine ("\n--- Testing Known Hub ---");
		var knownHubTest = await WiserHubDiscovery.IsWiserHubAsync (IPAddress.Parse ("192.168.8.196"));
		Console.WriteLine ($"Known hub test result: {knownHubTest}");

		DateTime startTime = DateTime.Now;
		ConcurrentBag<WiserDiscoveredHub> hubs = await WiserHubDiscovery.DiscoverHubsAsync (options);
		TimeSpan elapsed = DateTime.Now - startTime;

		Console.WriteLine ($"\n🔍 Discovery completed in {elapsed.TotalSeconds:F1}s");

		if (!hubs.IsEmpty)
			{
			Console.WriteLine ($"✅ Found {hubs.Count} Wiser Hub(s):");
			foreach (WiserDiscoveredHub hub in hubs)
				{
				Console.WriteLine ($"  - {hub}");
				}
			}
		else
			{
			Console.WriteLine ("❌ No Wiser Hubs found on the network");
			}

		Console.WriteLine ("\nPress any key to exit...");
		_ = Console.ReadKey ();
		}
	}