using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace WiserHeatApiV2
	{
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
		public bool ShowProgress { get; set; }
		public bool ShowDebug { get; set; }
		public int MaxResults { get; set; }  // 0 means unlimited
		}

	public class NetworkInfo
		{
		private static readonly byte[] _zeroBitCountTable =
			 [.. Enumerable.Range (0, 256).Select (b => (byte)(8 - Convert.ToString (b, 2).Count (c => c == '1')))];

		public IPAddress NetworkBase { get; }
		public IPAddress NetworkAddress { get; }
		public IPAddress SubnetMask { get; }
		public IPAddress BroadcastAddress { get; }
		public int HostCount { get; }

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
				broadcastBytes[i] = (byte)(networkBytes[i] | ~maskBytes[i]);
				}

			return new IPAddress (broadcastBytes);
			}

		private static int CountZeroBits (byte value) => _zeroBitCountTable[value];
		}

	public class WiserHubDiscovery
		{
		private static readonly HttpClient _sharedHttpClient = new () { Timeout = TimeSpan.FromMilliseconds (3000) };
		private static readonly IPAddress _subnetMask = new ([255, 255, 255, 0]);
		private static readonly IPAddress[] _fallbackRanges = [new ([192, 168, 1, 0]), new ([192, 168, 0, 0]), new ([192, 168, 8, 0]), new ([10, 0, 0, 0]), new ([172, 16, 0, 0])];
		private static readonly IPAddress _fallbackMask = new ([255, 255, 255, 0]);

		/// <summary>
		/// Discover Wiser Hubs as they are found (streaming results)
		/// </summary>
		public static async IAsyncEnumerable<WiserDiscoveredHub> DiscoverHubsAsyncEnumerable (
			 WiserDiscoveryOptions? options = null,
			 [EnumeratorCancellation] CancellationToken cancellationToken = default)
			{
			options ??= new WiserDiscoveryOptions ();

			List<NetworkInfo> networkInfos = GetLocalNetworkInfos (options.ShowDebug);

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

			foreach (NetworkInfo networkInfo in networkInfos)
				{
				if (cts.Token.IsCancellationRequested)
					yield break;

				await foreach (WiserDiscoveredHub? hub in ScanNetworkAsyncEnumerable (networkInfo, options, cts.Token).ConfigureAwait (false))
					{
					yield return hub;
					}
				}
			}

		/// <summary>
		/// Scan a single network and stream discovered hubs
		/// </summary>
		private static async IAsyncEnumerable<WiserDiscoveredHub> ScanNetworkAsyncEnumerable (
			 NetworkInfo networkInfo,
			 WiserDiscoveryOptions options,
			 [EnumeratorCancellation] CancellationToken cancellationToken)
			{
			if (options.ShowProgress)
				Console.WriteLine ($"  Streaming scan {networkInfo.NetworkBase}.*...");

			var semaphore = new SemaphoreSlim (options.MaxConcurrency);
			var httpTasks = new List<Task<WiserDiscoveredHub?>> ();
			var hubsReturned = 0;
			var maxResults = options.MaxResults > 0 ? options.MaxResults : int.MaxValue;

			await foreach (IPAddress aliveIP in PingNetworkAsyncEnumerable (networkInfo, options, cancellationToken).ConfigureAwait (false))
				{
				if (hubsReturned >= maxResults)
					break;
				await semaphore.WaitAsync (cancellationToken).ConfigureAwait (false);
				Task<WiserDiscoveredHub?> task = Task.Run (async () =>
					{
					try
						{
						if (options.ShowDebug)
							Console.WriteLine ($"    HTTP test: {aliveIP}");

						using var cts = new CancellationTokenSource (options.HttpTimeout);
						if (await IsWiserHubAsync (aliveIP).ConfigureAwait (false))
							{
							var hub = new WiserDiscoveredHub (aliveIP);
							if (options.ShowProgress)
								Console.WriteLine ($"    ✅ Found Wiser Hub: {hub.Url}");
							return hub;
							}
						}
					catch (Exception ex)
						{
						if (options.ShowDebug)
							Console.WriteLine ($"    ❌ HTTP test failed for {aliveIP}: {ex.Message}");
						}
					finally
						{
							_ = semaphore.Release ();
						}

					return null;
					}, cancellationToken);
				httpTasks.Add (task);
				}

			// Stream results as soon as each HTTP test completes
			while (httpTasks.Count > 0 && hubsReturned < maxResults)
				{
				Task<WiserDiscoveredHub?> finished = await Task.WhenAny (httpTasks).ConfigureAwait (false);
				_ = httpTasks.Remove (finished);

				WiserDiscoveredHub? hub = null;
				try
					{
					hub = await finished.ConfigureAwait (false);
					}
				catch (Exception ex)
					{
					if (options.ShowDebug)
						Console.WriteLine ($"    ❌ HTTP task exception: {ex.Message}");
					}

				if (hub != null)
					{
					yield return hub;
					hubsReturned++;
					if (hubsReturned >= maxResults)
						break;
					}
				}

			if (options.ShowProgress)
				Console.WriteLine ($"  ✅ {networkInfo.NetworkBase}.* streaming scan complete");
			}

		/// <summary>
		/// Ping network and stream alive IPs as they respond (simplified, reliable)
		/// </summary>
		private static async IAsyncEnumerable<IPAddress> PingNetworkAsyncEnumerable (
			 NetworkInfo networkInfo,
			 WiserDiscoveryOptions options,
			 [EnumeratorCancellation] CancellationToken cancellationToken)
			{
			HashSet<IPAddress> gatewayIPs = GetGatewayIPs (networkInfo.NetworkAddress, options.ShowDebug);
			var semaphore = new SemaphoreSlim (options.MaxConcurrency);

			if (options.ShowDebug)
				{
				Console.WriteLine ($"    Network: {networkInfo.NetworkAddress}");
				Console.WriteLine ($"    Subnet Mask: {networkInfo.SubnetMask}");
				Console.WriteLine ($"    Broadcast: {networkInfo.BroadcastAddress}");
				Console.WriteLine ($"    Host Count: {networkInfo.HostCount}");
				}

			var ipsToScan = GetIPsToScan (networkInfo, gatewayIPs, options).ToList ();
			var pingTasks = ipsToScan.Select (ip =>
				 PingIPSimpleAsync (ip, semaphore, options.PingTimeout, cancellationToken)).ToList ();

			var remainingTasks = new List<Task<IPAddress?>> (pingTasks);
			while (remainingTasks.Count > 0)
				{
				cancellationToken.ThrowIfCancellationRequested ();
				Task<IPAddress?> completedTask = await Task.WhenAny (remainingTasks).ConfigureAwait (false);
				_ = remainingTasks.Remove (completedTask);
				if (completedTask.IsCanceled || completedTask.IsFaulted)
					continue;
				IPAddress? result = await completedTask.ConfigureAwait (false);
				if (result != null)
					yield return result;
				}
			}

		/// <summary>
		/// Ping an IP and return it if alive, null if not (simplified)
		/// </summary>
		private static async Task<IPAddress?> PingIPSimpleAsync (
			 IPAddress ip,
			 SemaphoreSlim semaphore,
			 int timeout,
			 CancellationToken cancellationToken)
			{
			await semaphore.WaitAsync (cancellationToken).ConfigureAwait (false);
			try
				{
				using var ping = new Ping ();
				PingReply reply = await ping.SendPingAsync (ip, timeout).ConfigureAwait (false);

				return reply.Status == IPStatus.Success ? ip : null;
				}
			catch (OperationCanceledException)
				{
				// Propagate cancellation
				throw;
				}
			catch
				{
				return null;
				}
			finally
				{
				_ = semaphore.Release ();
				}
			}

		/// <summary>
		/// Get all IPs to scan for a network
		/// </summary>
		private static IEnumerable<IPAddress> GetIPsToScan (
			 NetworkInfo networkInfo,
			 HashSet<IPAddress> gatewayIPs,
			 WiserDiscoveryOptions options)
			{
			var networkBytes = networkInfo.NetworkAddress.GetAddressBytes ();
			var broadcastBytes = networkInfo.BroadcastAddress.GetAddressBytes ();
			var startLastOctet = networkBytes[3];
			var endLastOctet = broadcastBytes[3];

			if (options.ShowDebug)
				Console.WriteLine ($"    Scanning: {networkBytes[0]}.{networkBytes[1]}.{networkBytes[2]}.{startLastOctet}-{endLastOctet}");

			for (int i = startLastOctet; i <= endLastOctet; i++)
				{
				var ip = new IPAddress ([.. networkBytes[0..3], (byte)i]);

				if (!ShouldSkipIP (ip, networkInfo, gatewayIPs))
					{
					yield return ip;
					}
				else if (options.ShowDebug)
					{
					if (ip == networkInfo.NetworkAddress)
						Console.WriteLine ($"    ⏭️ Skipping network address: {ip}");
					else if (ip == networkInfo.BroadcastAddress)
						Console.WriteLine ($"    ⏭️ Skipping broadcast address: {ip}");
					else if (gatewayIPs.Contains (ip))
						Console.WriteLine ($"    ⏭️ Skipping gateway: {ip}");
					}
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
							var ipInt = ipBytes[0] << 24 | ipBytes[1] << 16 | ipBytes[2] << 8 | ipBytes[3];
							var maskInt = maskBytes[0] << 24 | maskBytes[1] << 16 | maskBytes[2] << 8 | maskBytes[3];
							var networkInt = ipInt & maskInt;
							byte[] networkBytes = [(byte)(networkInt >> 24 & 0xFF), (byte)(networkInt >> 16 & 0xFF), (byte)(networkInt >> 8 & 0xFF), (byte)(networkInt & 0xFF)];

							var networkAddress = new IPAddress (networkBytes);
							var networkInfo = new NetworkInfo (networkAddress, addr.IPv4Mask);

							// Avoid duplicates
							if (!networkInfos.Any (ni => ni.NetworkAddress.Equals (networkAddress) && ni.SubnetMask.Equals (addr.IPv4Mask)))
								{
								networkInfos.Add (networkInfo);
								if (showDebug)
									Console.WriteLine ($"  Added network: {networkAddress}/{addr.IPv4Mask} ({networkInfo.HostCount} hosts)");
								}
							}
						}
					}
				}
			catch (Exception ex)
				{
				if (showDebug)
					Console.WriteLine ($"Error getting network information: {ex.Message}");
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
					Console.WriteLine ($"  🔍 Detecting gateways for network {networkBase}.*");

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
										Console.WriteLine ($"    Interface '{ni.Name}' is on target network: {networkBase}");

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
									Console.WriteLine ($"    ✅ Found gateway: {gatewayIP}");
								}
							}
						}
					}
				}
			catch (Exception ex)
				{
				if (showDebug)
					Console.WriteLine ($"    ❌ Error detecting gateways: {ex.Message}");
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

		/// <summary>
		/// Tests if a specific IP address is a Wiser Hub
		/// </summary>
		public static async Task<bool> IsWiserHubAsync (IPAddress ipAddress, int port = 80)
			{
			try
				{
				var baseUrl = $"http://{ipAddress}:{port}";
				var endpoint = "/data/v2/domain/";

				using var cts = new CancellationTokenSource (1500); // Use options.HttpTimeout if available
				HttpResponseMessage response = await _sharedHttpClient.GetAsync (baseUrl + endpoint, cts.Token).ConfigureAwait (false);

				// 401 Unauthorized is the Wiser Hub signature
				return response.StatusCode == HttpStatusCode.Unauthorized;
				}
			catch
				{
				return false;
				}
			}

		/// <summary>
		/// Backward compatible method that collects all results
		/// </summary>
		public static async Task<ConcurrentBag<WiserDiscoveredHub>> DiscoverHubsAsync (
			 WiserDiscoveryOptions? options = null,
			 CancellationToken cancellationToken = default)
			{
			var discoveredHubs = new ConcurrentBag<WiserDiscoveredHub> ();

			await foreach (WiserDiscoveredHub hub in DiscoverHubsAsyncEnumerable (options, cancellationToken).ConfigureAwait (false))
				{
				discoveredHubs.Add (hub);
				}

			return discoveredHubs;
			}

		/// <summary>
		/// Streaming version of QuickScanRangeAsync
		/// </summary>
		public static async IAsyncEnumerable<WiserDiscoveredHub> QuickScanRangeAsyncEnumerable (
			 IPAddress networkBase,
			 int startIp = 0,
			 int endIp = 254,
			 WiserDiscoveryOptions? options = null,
			 [EnumeratorCancellation] CancellationToken cancellationToken = default)
			{
			options ??= new WiserDiscoveryOptions { ShowProgress = true };
			var networkInfo = new NetworkInfo (networkBase, _subnetMask);

			await foreach (WiserDiscoveredHub hub in ScanNetworkAsyncEnumerable (networkInfo, options, cancellationToken).ConfigureAwait (false))
				{
				yield return hub;
				}
			}

		/// <summary>
		/// Backward compatible QuickScanRangeAsync
		/// </summary>
		public static async Task<ConcurrentBag<WiserDiscoveredHub>> QuickScanRangeAsync (
			 IPAddress networkBase,
			 int startIp = 0,
			 int endIp = 254,
			 WiserDiscoveryOptions? options = null)
			{
			var discoveredHubs = new ConcurrentBag<WiserDiscoveredHub> ();

			await foreach (WiserDiscoveredHub hub in QuickScanRangeAsyncEnumerable (networkBase, startIp, endIp, options))
				{
				discoveredHubs.Add (hub);
				}

			return discoveredHubs;
			}
		}
	}
