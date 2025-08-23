// Copyright © 2025 Nivloc Enterprises Ltd.
// Adapted from the Python implementation Copyright © 2021 Mark Parker
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using log4net;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace WiserHeatApiV2
	{
	public static class RestConstants
		{
		public const double RestBackoffFactor = 0.5;
		public const int RestRetries = 3;
		public const int RestTimeout = 30;
		// Wiser Hub Rest Api URL Constants
		public const string WiserHubUrl = "http://{0}/data/v2/";
		public const string WiserHubDomain = WiserHubUrl + "domain/";
		public const string WiserHubNetwork = WiserHubUrl + "network/";
		public const string WiserHubSchedules = WiserHubUrl + "schedules/";
		public const string WiserHubOpentherm = WiserHubUrl + "opentherm/";
		public const string WiserSystem = "System";
		public const string WiserDevice = "Device/{0}";
		public const string WiserHotWater = "HotWater/{0}";
		public const string WiserRoom = "Room/{0}";
		public const string WiserSmartValve = "SmartValve/{0}";
		public const string WiserRoomStat = "RoomStat/{0}";
		public const string WiserSmartPlug = "SmartPlug/{0}";
#if HEATACTUATOR
		public const string WiserHeatingActuator = "HeatingActuator/{0}";
#endif
		public const string WiserUfhController = "UnderFloorHeating/{0}";
#if SHUTTER
		public const string WiserShutter = "Shutter/{0}";
#endif
#if LIGHT
		public const string WiserLight = "Light/{0}";
#endif
		}

	// Custom Exceptions
	public class WiserHubAuthenticationException (string message) : Exception (message)
		{
		}

	public class WiserHubConnectionException (string message) : Exception (message)
		{
		}

	public class WiserHubRESTException (string message) : Exception (message)
		{
		}

	// Connection Info Class
	public class WiserConnection (string? host, string? secret)
		{
		public string Host
			{
			get; set;
			} = host ?? throw new ArgumentNullException (nameof (host));
		public string Secret
			{
			get; set;
			} = secret ?? throw new ArgumentNullException (nameof (secret));
		public WiserUnits Units { get; set; } = WiserUnits.Metric; // Default to Metric
		}

	// Enums
	public enum WiserRestAction
		{
		GET,
		POST,
		PATCH,
		DELETE
		}

	public class WiserRestController : IDisposable
		{
		private readonly WiserConnection _wiserConnection;
		private HttpClient? _httpClient;
		private static readonly ILog _logger = log4net.LogManager.GetLogger (typeof (WiserRestController));

		public WiserRestController (WiserConnection wiserConnection)
			{
			var logger = (log4net.Repository.Hierarchy.Logger)((log4net.Core.LogImpl)_logger).Logger;
#if DEBUG
			logger.Level = log4net.Core.Level.Debug;
#else
			logger.Level = log4net.Core.Level.Error;
#endif
			_wiserConnection = wiserConnection ?? throw new ArgumentNullException (nameof (wiserConnection));

			ServicePointManager.Expect100Continue = false;    // even though GET has no body, this avoids edge cases
			ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;  // harmless for HTTP; needed for HTTPS
			ServicePointManager.DefaultConnectionLimit = 10;

			var handler = new HttpClientHandler
				{
				AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
				UseProxy = false,
				Proxy = null,
				AllowAutoRedirect = false
				// leave AllowAutoRedirect = true (default) — redirects still work later
				// If you’re hitting HTTPS directly and it’s self-signed, TEMP ONLY:
				// ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
				};       
			
			// Configure HttpClient with retry logic (simplified for example)
			_httpClient = new HttpClient (handler)
				{
				Timeout = TimeSpan.FromSeconds (RestConstants.RestTimeout)
				};
			_httpClient.DefaultRequestHeaders.Add ("SECRET", _wiserConnection.Secret);
			_httpClient.DefaultRequestHeaders.Accept.Add (new MediaTypeWithQualityHeaderValue ("application/json"));
			_httpClient.DefaultRequestHeaders.UserAgent.Clear ();
			_httpClient.DefaultRequestHeaders.UserAgent.Add (
				 new ProductInfoHeaderValue ("WiserRestController", "2.0"));
			//_httpClient.DefaultRequestHeaders.ExpectContinue = false; // global default (harmless for GETs)
			//_httpClient.DefaultRequestHeaders.ConnectionClose = true; // Equivalent to "Connection": "close"
			}

		// Helper method for HTTP calls (switch expression, non-async)
		private async Task<HttpResponseMessage> SendHttpRequestAsync (WiserRestAction action, string url, StringContent? data, CancellationToken cancellationToken)
			{
			HttpMethod method = action switch
				{
					WiserRestAction.GET => HttpMethod.Get,
					WiserRestAction.POST => HttpMethod.Post,
					WiserRestAction.PATCH => new HttpMethod ("PATCH"),
					WiserRestAction.DELETE => HttpMethod.Delete,
					_ => throw new ArgumentOutOfRangeException (nameof (action), action, "Invalid WiserRestAction"),
					};

			using (var req = new HttpRequestMessage (method, url))
				{
				if (data != null && method != HttpMethod.Get)
					req.Content = data;
				// v2 firmware quirk: schedules endpoint is more reliable with HTTP/1.0
				if (url.IndexOf ("/schedules/", StringComparison.OrdinalIgnoreCase) >= 0)
					req.Version = new Version (1, 0);

				HttpResponseMessage resp = await _httpClient!.SendAsync (req, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait (false);

				// Follow HTTP->HTTPS redirects (301/302/307/308)
				// Replace the problematic line with the following, which uses pattern matching for defined values
				// and an explicit integer check for 308 (Permanent Redirect):

				if (resp.StatusCode is HttpStatusCode.MovedPermanently
					 or HttpStatusCode.Found
					 or HttpStatusCode.TemporaryRedirect
					 || (int)resp.StatusCode == 308) // Permanent Redirect (not defined in .NET Framework 4.7.2)
					{
					resp.Dispose ();
					var httpsUrl = url.StartsWith ("http://", StringComparison.OrdinalIgnoreCase) ? "https://" + url[7..] : url;
					using (var req2 = new HttpRequestMessage (method, httpsUrl))
						{
						if (data != null && method != HttpMethod.Get)
							req2.Content = data;
						if (httpsUrl.IndexOf ("/schedules/", StringComparison.OrdinalIgnoreCase) >= 0)
							req2.Version = new Version (1, 0);
						resp = await _httpClient!.SendAsync (req2, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait (false);
						}
					}

				return resp;
				}
			}

		public async Task<HttpResponseMessage?> ExecuteHttpRequestAsync (
			 WiserRestAction action,
			 string url,
			 StringContent? data = null,
			 CancellationToken cancellationToken = default)
			{
			var retryCount = RestConstants.RestRetries;
			var delay = TimeSpan.FromSeconds (1);
			var backoffFactor = 2.0;
			HttpResponseMessage? response = null;

			while (retryCount >= 0)
				{
				try
					{
					response = await SendHttpRequestAsync (action, url, data, cancellationToken).ConfigureAwait (false);

					if (response.IsSuccessStatusCode)
						{
						break; // Success, exit loop
						}

					// Retryable errors
					if (response.StatusCode is HttpStatusCode.RequestEntityTooLarge or
														  HttpStatusCode.InternalServerError or
														  HttpStatusCode.BadGateway or
														  HttpStatusCode.ServiceUnavailable or
														  HttpStatusCode.GatewayTimeout)
						{
						retryCount--;
						if (retryCount >= 0)
							{
							await Task.Delay (delay, cancellationToken).ConfigureAwait (false);
							delay = TimeSpan.FromSeconds (delay.TotalSeconds * backoffFactor);
							}
						}
					else
						{
						// Non-retryable error, exit loop
						break;
						}
					}
				catch (HttpRequestException ex)
					{
					_logger.Error ("HTTP Request Exception", ex);
					throw new WiserHubConnectionException (
						 $"Connection error trying to communicate with Wiser Hub {_wiserConnection.Host}. Error is {ex.Message}");
					}
				catch (TaskCanceledException ex)
					{
					_logger.Error ("Task Canceled Exception", ex);
					throw new WiserHubConnectionException (
						 $"Timeout error trying to communicate with Wiser Hub {_wiserConnection.Host}. Error is {ex.Message}");
					}
				}

			// Return the last response (success or final failure)
			return response;
			}

		private async Task<bool> DoHubActionAsync (WiserRestAction action, string url, object? data = null, bool raiseForEndpointError = true, CancellationToken cancellationToken = default)
			{
			StringContent? jsonContent = null;
			if (data != null)
				{
				var jsonData = JsonConvert.SerializeObject (data);
				jsonContent = new StringContent (jsonData, Encoding.UTF8, "application/json");
				}

			try
				{
				using HttpResponseMessage? response = await ExecuteHttpRequestAsync (action, url, jsonContent, cancellationToken).ConfigureAwait (false);

				if (response == null)
					{
					_logger.Error ("Response from Wiser Hub is null.");
					throw new WiserHubConnectionException ("Response from Wiser Hub is null.");
					}

				if (!response.IsSuccessStatusCode)
					{
					await ProcessNokResponseAsync (response, raiseForEndpointError).ConfigureAwait (false);
					return false; // Return empty object on failure
					}
				else
					{
					return true;
					}
				}
			catch (WiserHubConnectionException)
				{
				throw; // Re-throw custom exception
				}
			catch (Exception ex)
				{
				// Catch any other unexpected exceptions
				_logger.Error ("An unexpected error occurred in _DoHubActionAsync.", ex);
				throw new WiserHubConnectionException ($"An unexpected error occurred: {ex.Message}");
				}
			}

		public async Task<Dictionary<string, object>> GetHubDataAsync (string url, object? data = null, bool raiseForEndpointError = true, CancellationToken cancellationToken = default)
			{
			StringContent? jsonContent = null;
			if (data != null)
				{
				var jsonData = JsonConvert.SerializeObject (data);
				jsonContent = new StringContent (jsonData, Encoding.UTF8, "application/json");
				}

			try
				{
				using HttpResponseMessage? response = await ExecuteHttpRequestAsync (WiserRestAction.GET, url, jsonContent, cancellationToken).ConfigureAwait (false);

				if (response == null)
					{
					_logger.Error ("Response from Wiser Hub is null.");
					throw new WiserHubConnectionException ("Response from Wiser Hub is null.");
					}

				if (!response.IsSuccessStatusCode)
					{
					await ProcessNokResponseAsync (response, raiseForEndpointError).ConfigureAwait (false);
					return []; // Return empty object on failure
					}
				else
					{
					var content = await response.Content.ReadAsByteArrayAsync ().ConfigureAwait (false);
					if (content.Length > 0)
						{
						// Remove non-ASCII characters (equivalent to the Python regex)
						var cleanedContent = Regex.Replace (Encoding.UTF8.GetString (content), @"[^\u0020-\u007F]+", string.Empty);
						JToken? cleaned = JsonConvert.DeserializeObject<JToken> (cleanedContent);
						if (cleaned != null)
							return (Dictionary<string, object>?)ConvertJTokenToObject (cleaned) ?? [];
						}

					return [];
					}
				}
			catch (WiserHubConnectionException)
				{
				throw; // Re-throw custom exception
				}
			catch (Exception ex)
				{
				// Catch any other unexpected exceptions
				_logger.Error ("An unexpected error occurred in _DoHubActionAsync.", ex);
				throw new WiserHubConnectionException ($"An unexpected error occurred: {ex.Message}");
				}
			}

		//public Dictionary<string, object> GetConnectionPools()
		//{
		//  // This is a placeholder as replicating connection pooling exactly might be complex.
		//  // Depending on your needs, you might use HttpClientFactory for connection pooling.
		//  // This example returns an empty dictionary.
		//  return new Dictionary<string, object>();
		//}

		private async Task ProcessNokResponseAsync (HttpResponseMessage response, bool raiseForEndpointError = true)
			{
			var errorMessage = await response.Content.ReadAsStringAsync ().ConfigureAwait (false);
			if (response.StatusCode == HttpStatusCode.Unauthorized)
				{
				throw new WiserHubAuthenticationException ($"Error authenticating to Wiser Hub {_wiserConnection.Host}. Check your secret key.  Message: {errorMessage}");
				}
			else if (response.StatusCode == HttpStatusCode.NotFound && raiseForEndpointError)
				{
				throw new WiserHubRESTException ($"Rest endpoint not found on Wiser Hub {_wiserConnection.Host}. Message: {errorMessage}");
				}
			else if (response.StatusCode == HttpStatusCode.RequestTimeout)
				{
				throw new WiserHubConnectionException ($"Connection timed out trying to communicate with Wiser Hub {_wiserConnection.Host}. Message: {errorMessage}");
				}
			else if (raiseForEndpointError)
				{
				throw new WiserHubRESTException ($"Unknown error communicating with Wiser Hub {_wiserConnection.Host}. Error code is: {response.StatusCode}. Message: {errorMessage}");
				}
			}

		public Task<bool> SendCommandAsync (string url, object? commandData, WiserRestAction method = WiserRestAction.PATCH, CancellationToken cancellationToken = default)
			{
			var fullUrl = $"{string.Format (CultureInfo.InvariantCulture, RestConstants.WiserHubDomain, _wiserConnection.Host)}{url}";
			_logger.DebugFormat ("Sending command to url: {0} with parameters {1}", fullUrl, JsonConvert.SerializeObject (commandData));

			return DoHubActionAsync (method, fullUrl, commandData, cancellationToken: cancellationToken);
			}

		private Task<bool> DoScheduleActionAsync (WiserRestAction action, string url, object? scheduleData = null, CancellationToken cancellationToken = default)
			{
			var fullUrl = $"{string.Format (CultureInfo.InvariantCulture, RestConstants.WiserHubSchedules, _wiserConnection.Host)}{url}";
			_logger.DebugFormat ("Actioning schedule to url: {0} with action {1} and _data {2}", fullUrl, action.ToString (), JsonConvert.SerializeObject (scheduleData));

			return DoHubActionAsync (action, fullUrl, scheduleData, cancellationToken: cancellationToken);
			}

		public Task<bool> SendScheduleCommandAsync (string action, object? scheduleData, int id = 0, string? scheduleType = null, CancellationToken cancellationToken = default)
			{
			switch (action.ToUpper (CultureInfo.InvariantCulture))
				{
				case "UPDATE":
					return DoScheduleActionAsync (
						 WiserRestAction.PATCH,
						 $"{scheduleType}/{id}",
						 scheduleData, cancellationToken);

				case "CREATE":
					return DoScheduleActionAsync (
						 WiserRestAction.POST,
						 "Assign",
						 scheduleData, cancellationToken);

				case "ASSIGN":
					return DoScheduleActionAsync (
						 WiserRestAction.PATCH,
						 "Assign",
						 scheduleData, cancellationToken);

				case "DELETE":
					return DoScheduleActionAsync (
						 WiserRestAction.DELETE,
						 $"{scheduleType}/{id}",
						 scheduleData, cancellationToken);

				default:
					_logger.ErrorFormat ("Invalid schedule action: {}", action);
					return Task.FromResult (false);
				}
			}

		public static object? ConvertJTokenToObject (JToken token) =>
			token.Type switch
				{
					JTokenType.Object => token.Children<JProperty> ()
														.ToDictionary (prop => prop.Name, prop => ConvertJTokenToObject (prop.Value)),
					JTokenType.Array => token.First is JValue
											? token.Children<JValue> ().Select (c => c.Value).ToList ()
											: token.Select (ConvertJTokenToObject).Cast<Dictionary<string, object>> ().ToList (),
					_ => ((JValue)token).Value,
					};

		public void Dispose ()
			{
			if (_httpClient != null)
				{
				_httpClient.Dispose ();
				_httpClient = null;
				}

			GC.SuppressFinalize (this);
			}

		public string GetHost () => _wiserConnection.Host;
		}
	}
