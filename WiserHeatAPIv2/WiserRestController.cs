// Copyright © 2026 Neil Colvin.
// Adapted from the Python implementation Copyright © 2021 Mark Parker
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using log4net;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using static WiserHeatApiV2.RestConstants;

namespace WiserHeatApiV2;

/// <summary>
/// REST constants and URL formats for the Wiser hub API.
/// </summary>
/// <remarks>
/// URLs are composed using the <c>WiserHubUrl</c> base and type-specific segments. For example:
/// <list type="bullet">
/// <item><description>Domain API: <c>string.Format(WiserHubDomain, host)</c> + relative path (e.g., <c>System</c>)</description></item>
/// <item><description>Schedules API: <c>string.Format(WiserHubSchedules, host)</c> + <c>{ScheduleType}/{Id}</c> or <c>Assign</c></description></item>
/// </list>
/// Timeouts and retry defaults (e.g., <see cref="REST_BACKOFF_FACTOR"/>, <see cref="REST_RETRIES"/>, <see cref="REST_TIMEOUT"/>) are used by
/// <see cref="WiserRestController"/> to implement simple resiliency for transient errors.
/// </remarks>
public static class RestConstants
	{
	/// <summary>Base factor for retry backoff.</summary>
	public const double REST_BACKOFF_FACTOR = 0.5;
	/// <summary>Number of retries for REST operations.</summary>
	public const int REST_RETRIES = 3;
	/// <summary>HTTP timeout in seconds.</summary>
	public const int REST_TIMEOUT = 30;
	// Wiser Hub Rest Api URL Constants
	/// <summary>Format string for the hub data base URL.</summary>
	public const string WISER_HUB_URL = "http://{0}/data/v2/";
	/// <summary>Format string for the domain endpoint URL.</summary>
	public const string WISER_HUB_DOMAIN = WISER_HUB_URL + "domain/";
	/// <summary>Format string for the network endpoint URL.</summary>
	public const string WISER_HUB_NETWORK = WISER_HUB_URL + "network/";
	/// <summary>Format string for the schedules endpoint URL.</summary>
	public const string WISER_HUB_SCHEDULES = WISER_HUB_URL + "schedules/";
	/// <summary>Format string for the OpenTherm endpoint URL.</summary>
	public const string WISER_HUB_OPENTHERM = WISER_HUB_URL + "opentherm/";
	/// <summary>REST path for system commands.</summary>
	public const string WISER_REST_SYSTEM = "System";
	/// <summary>REST path for device commands.</summary>
	public const string WISER_REST_DEVICE = "Device/{0}";
	/// <summary>REST path for hot water commands.</summary>
	public const string WISER_REST_HOT_WATER = "HotWater/{0}";
	/// <summary>REST path for room commands.</summary>
	public const string WISER_REST_ROOM = "Room/{0}";
	/// <summary>REST path for smart valve commands.</summary>
	public const string WISER_REST_SMART_VALVE = "SmartValve/{0}";
	/// <summary>REST path for room stat commands.</summary>
	public const string WISER_REST_ROOM_STAT = "RoomStat/{0}";
	/// <summary>REST path for smart plug commands.</summary>
	public const string WISER_REST_SMART_PLUG = "SmartPlug/{0}";
#if HEATACTUATOR
	/// <summary>REST path for heating actuator commands.</summary>
	public const string WISER_REST_HEATING_ACTUATOR = "HeatingActuator/{0}";
#endif
	/// <summary>REST path for underfloor heating controller commands.</summary>
	public const string WISER_REST_UFH_CONTROLLER = "UnderFloorHeating/{0}";
#if SHUTTER
	/// <summary>REST path for shutter commands.</summary>
	public const string WISER_REST_SHUTTER = "Shutter/{0}";
#endif
#if LIGHT
	/// <summary>REST path for light commands.</summary>
	public const string WISER_REST_LIGHT = "Light/{0}";
#endif
	}

/// <summary>
/// Authentication error communicating with the Wiser hub.
/// </summary>
/// <param name="message">Details about the authentication failure.</param>
public class WiserHubAuthenticationException (string message) : Exception (message)
	{
	}

/// <summary>
/// Connection error communicating with the Wiser hub.
/// </summary>
/// <param name="message">Details about the connection or timeout failure.</param>
public class WiserHubConnectionException (string message) : Exception (message)
	{
	}

/// <summary>
/// General REST error communicating with the Wiser hub.
/// </summary>
/// <param name="message">Details about the REST error returned by the hub.</param>
public class WiserHubRESTException (string message) : Exception (message)
	{
	}

/// <summary>
/// Encapsulates hub connection information used by the REST client.
/// </summary>
/// <param name="host">The hub host name or IP address.</param>
/// <param name="secret">The hub secret used for authentication.</param>
/// <remarks>
/// The <see cref="Host"/> and <see cref="Secret"/> properties are initialized from the primary constructor
/// and are required for all hub communications.
/// </remarks>
/// <exception cref="ArgumentNullException">
/// Thrown when <paramref name="host"/> or <paramref name="secret"/> is <see langword="null"/>.
/// </exception>
public class WiserConnection (string? host, string? secret)
	{
	/// <summary>
	/// Gets or sets the Wiser hub host name or IP address.
	/// </summary>
	/// <value>The hub host (DNS name or IPv4/IPv6 literal) without scheme.</value>
	/// <remarks>
	/// Used to compose request URLs via <see cref="RestConstants.WISER_HUB_URL"/>. This value is not validated
	/// when set; it must resolve and be reachable by <see cref="WiserRestController"/>.
	/// </remarks>
	public string Host
		{
		get; set;
		} = host ?? throw new ArgumentNullException (nameof (host));
	/// <summary>
	/// Gets or sets the Wiser hub authentication secret.
	/// </summary>
	/// <value>The opaque secret sent in the "SECRET" HTTP header on each request.</value>
	/// <remarks>
	/// The constructor enforces non-null initialization. Changing this value after a controller is created
	/// affects only subsequent requests. Avoid logging this value.
	/// </remarks>
	public string Secret
		{
		get; set;
		} = secret ?? throw new ArgumentNullException (nameof (secret));
	/// <summary>
	/// Gets or sets the preferred unit system for temperature conversions.
	/// </summary>
	/// <value>The unit system used by client-side helpers (default is <see cref="WiserUnits.Metric"/>).</value>
	/// <remarks>
	/// This setting does not change hub behavior; it controls how values are interpreted and formatted
	/// by API consumers.
	/// </remarks>
	public WiserUnits Units { get; set; } = WiserUnits.Metric; // Default to Metric
	}

// Enums
/// <summary>HTTP verb used for REST actions.</summary>
public enum WiserRestAction
	{
	/// <summary>
	/// HTTP GET request.
	/// </summary>
	GET,
	/// <summary>
	/// HTTP POST request to create a new resource.
	/// </summary>
	POST,
	/// <summary>
	/// HTTP PATCH request to partially update an existing resource.
	/// </summary>
	PATCH,
	/// <summary>
	/// HTTP DELETE request to remove a resource.
	/// </summary>
	DELETE
	}

/// <summary>
/// REST controller for communicating with the Wiser hub.
/// </summary>
/// <remarks>
/// This controller wraps an <see cref="HttpClient"/> configured for JSON, gzip/deflate, and a default timeout
/// via <see cref="RestConstants.REST_TIMEOUT"/>. Methods are safe for concurrent use; a single instance can be reused
/// for multiple requests. The controller is <see cref="IDisposable"/> and should be disposed when no longer needed
/// to release underlying HTTP resources.
/// </remarks>
public partial class WiserRestController : IDisposable
	{
	private readonly WiserConnection _wiserConnection;
	private HttpClient? _httpClient;
	private static readonly ILog _logger = log4net.LogManager.GetLogger (typeof (WiserRestController));

	/// <summary>
	/// Creates a new REST controller using the given connection.
	/// </summary>
	/// <param name="wiserConnection">The connection settings (host, secret, and units).</param>
	/// <remarks>
	/// The controller configures an <see cref="HttpClient"/> with JSON defaults, optional gzip/deflate
	/// decompression, and a default timeout of <see cref="RestConstants.REST_TIMEOUT"/> seconds.
	/// The hub secret is sent via the "SECRET" HTTP header on every request.
	/// </remarks>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="wiserConnection"/> is <see langword="null"/>.
	/// </exception>
	public WiserRestController (WiserConnection wiserConnection)
		{
		var logger = (log4net.Repository.Hierarchy.Logger)((log4net.Core.LogImpl)_logger).Logger;
#if DEBUG
		logger.Level = log4net.Core.Level.Debug;
#else
		logger.Level = log4net.Core.Level.Error;
#endif
		_wiserConnection = wiserConnection ?? throw new ArgumentNullException (nameof (wiserConnection));

#if NETFRAMEWORK
		ServicePointManager.Expect100Continue = false;    // even though GET has no body, this avoids edge cases
		ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;  // harmless for HTTP; needed for HTTPS
		ServicePointManager.DefaultConnectionLimit = 10;
#endif

		var handler = new HttpClientHandler
			{
			AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
			UseProxy = false,
			Proxy = null,
			AllowAutoRedirect = false
			// leave AllowAutoRedirect = true (default) � redirects still work later
			// If you�re hitting HTTPS directly and it�s self-signed, TEMP ONLY:
			// ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
			};

		// Configure HttpClient with retry logic (simplified for example)
		_httpClient = new HttpClient (handler)
			{
			Timeout = TimeSpan.FromSeconds (REST_TIMEOUT)
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

		using var req = new HttpRequestMessage (method, url);
		if (data != null && method != HttpMethod.Get)
			req.Content = data;
		// v2 firmware quirk: schedules endpoint is more reliable with HTTP/1.0
		if (url.Contains ("/schedules/", StringComparison.OrdinalIgnoreCase))
			{
			req.Version = new Version (1, 0);
#if !NETFRAMEWORK
			req.VersionPolicy = HttpVersionPolicy.RequestVersionExact;
#endif
			}

		HttpResponseMessage resp = await _httpClient!.SendAsync (req, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait (false);

		// Follow HTTP->HTTPS redirects (301/302/307/308)
#if NETFRAMEWORK
		if (resp.StatusCode is HttpStatusCode.MovedPermanently
				 or HttpStatusCode.Found
				 or HttpStatusCode.TemporaryRedirect
				 || (int)resp.StatusCode == 308) // Permanent Redirect (not defined in .NET Framework 4.7.2)
#else
		if (resp.StatusCode is HttpStatusCode.MovedPermanently
				 or HttpStatusCode.Found
				 or HttpStatusCode.TemporaryRedirect
				 or HttpStatusCode.PermanentRedirect)
#endif
			{
			resp.Dispose ();
			var httpsUrl = url.StartsWith ("http://", StringComparison.OrdinalIgnoreCase) ? "https://" + url[7..] : url;
			using var req2 = new HttpRequestMessage (method, httpsUrl);
			if (data != null && method != HttpMethod.Get)
				req2.Content = data;
			if (httpsUrl.Contains ("/schedules/", StringComparison.OrdinalIgnoreCase))
				req2.Version = new Version (1, 0);
			resp = await _httpClient!.SendAsync (req2, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait (false);
			}

		return resp;
		}
	/// <summary>
	/// Executes an HTTP request with basic retry handling.
	/// </summary>
	/// <param name="action">The HTTP verb to use.</param>
	/// <param name="url">The absolute request URL.</param>
	/// <param name="data">Optional JSON content to send for non-GET requests.</param>
	/// <param name="cancellationToken">Token to cancel the request and any retries.</param>
	/// <returns>
	/// The final <see cref="HttpResponseMessage"/> returned by the hub, or <see langword="null"/> if
	/// no response could be obtained (rare; typically an exception is thrown instead).
	/// </returns>
	/// <remarks>
	/// On transient 5xx/408/413 responses the call is retried up to <see cref="RestConstants.REST_RETRIES"/> times
	/// with exponential backoff. Non-success responses are still returned to the caller for inspection.
	/// </remarks>
	/// <exception cref="WiserHubConnectionException">
	/// Thrown when a connectivity issue occurs (e.g., DNS, refused connection) or the request times out.
	/// </exception>
	public async Task<HttpResponseMessage?> ExecuteHttpRequestAsync (
		 WiserRestAction action,
		 string url,
		 StringContent? data = null,
		 CancellationToken cancellationToken = default)
		{
		var retryCount = REST_RETRIES;
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

	private static readonly Regex _nonAscii = ValidAsciiRegex ();

	/// <summary>
	/// Gets a hub data payload as a dictionary.
	/// </summary>
	/// <param name="url">The absolute request URL (typically under the domain endpoint).</param>
	/// <param name="data">Optional JSON payload for the request body (rare for GET).</param>
	/// <param name="raiseForEndpointError">
	/// If <see langword="true"/>, 404 and other REST errors raise exceptions; if <see langword="false"/>, an empty dictionary is returned.
	/// </param>
	/// <param name="cancellationToken">Token to cancel the request.</param>
	/// <returns>
	/// A dictionary representing the JSON payload returned by the hub, or an empty dictionary on failure
	/// (when <paramref name="raiseForEndpointError"/> is <see langword="false"/>).
	/// </returns>
	/// <remarks>
	/// The response body is sanitized to remove non-ASCII characters before JSON parsing to mirror
	/// the behavior of the original Python implementation.
	/// </remarks>
	/// <exception cref="WiserHubAuthenticationException">The hub rejected the provided secret.</exception>
	/// <exception cref="WiserHubRESTException">The hub returned a non-success status and errors are not suppressed.</exception>
	/// <exception cref="WiserHubConnectionException">A connection or timeout error occurred.</exception>
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
#if NETFRAMEWORK
				var content = await response.Content.ReadAsByteArrayAsync ().ConfigureAwait (false);
#else
				var content = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false); 
#endif
				if (content.Length > 0)
					{
					// Remove non-ASCII characters (equivalent to the Python regex)
					var text = Encoding.UTF8.GetString (content);
					var cleanedContent = _nonAscii.Replace (text, string.Empty);
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

	/// <summary>
	/// Sends a command payload to a domain endpoint on the hub.
	/// </summary>
	/// <param name="url">Domain-relative path (e.g., <c>System</c>, <c>Room/1</c>).</param>
	/// <param name="commandData">The JSON-serializable payload to send.</param>
	/// <param name="method">HTTP verb to use (default is PATCH).</param>
	/// <param name="cancellationToken">Token to cancel the request.</param>
	/// <returns><see langword="true"/> if the hub returned a success status code; otherwise <see langword="false"/>.</returns>
	/// <remarks>
	/// The full URL is composed using <see cref="RestConstants.WISER_HUB_DOMAIN"/>. On non-success status codes,
	/// detailed exceptions may be thrown depending on the hub response.
	/// </remarks>
	/// <exception cref="WiserHubAuthenticationException">The hub rejected the provided secret.</exception>
	/// <exception cref="WiserHubRESTException">The hub returned a non-success status.</exception>
	/// <exception cref="WiserHubConnectionException">A connection or timeout error occurred.</exception>
	public Task<bool> SendCommandAsync (string url, object? commandData, WiserRestAction method = WiserRestAction.PATCH, CancellationToken cancellationToken = default)
		{
		var fullUrl = $"{WISER_HUB_DOMAIN.FormatInvariant (_wiserConnection.Host)}{url}";
		_logger.DebugFormat ("Sending command to url: {0} with parameters {1}", fullUrl, JsonConvert.SerializeObject (commandData));

		return DoHubActionAsync (method, fullUrl, commandData, cancellationToken: cancellationToken);
		}

	private Task<bool> DoScheduleActionAsync (WiserRestAction action, string url, object? scheduleData = null, CancellationToken cancellationToken = default)
		{
		var fullUrl = $"{WISER_HUB_SCHEDULES.FormatInvariant (_wiserConnection.Host)}{url}";
		_logger.DebugFormat ("Actioning schedule to url: {0} with action {1} and _data {2}", fullUrl, action.ToString (), JsonConvert.SerializeObject (scheduleData));

		return DoHubActionAsync (action, fullUrl, scheduleData, cancellationToken: cancellationToken);
		}

	/// <summary>
	/// Sends a schedule command to the schedules endpoint.
	/// </summary>
	/// <param name="action">
	/// The schedule action to perform. One of <c>UPDATE</c>, <c>CREATE</c>, <c>ASSIGN</c>, or <c>DELETE</c>.
	/// </param>
	/// <param name="scheduleData">The JSON-serializable schedule payload.</param>
	/// <param name="id">The target schedule id (required for UPDATE/DELETE).</param>
	/// <param name="scheduleType">The schedule type segment (e.g., <c>Heating</c>, <c>OnOff</c>).</param>
	/// <param name="cancellationToken">Token to cancel the request.</param>
	/// <returns><see langword="true"/> if the hub returned a success status code; otherwise <see langword="false"/>.</returns>
	/// <remarks>
	/// The correct HTTP verb and relative path are chosen based on <paramref name="action"/>.
	/// If an invalid action is provided, the method logs an error and returns <see langword="false"/>.
	/// </remarks>
	/// <exception cref="WiserHubAuthenticationException">The hub rejected the provided secret.</exception>
	/// <exception cref="WiserHubRESTException">The hub returned a non-success status.</exception>
	/// <exception cref="WiserHubConnectionException">A connection or timeout error occurred.</exception>
	public Task<bool> SendScheduleCommandAsync (string action, object? scheduleData, int id = 0, string? scheduleType = null, CancellationToken cancellationToken = default)
		{
		switch (action.ToUpperInvariant ())
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

	/// <summary>
	/// Recursively converts a JSON token into native .NET types.
	/// </summary>
	/// <param name="token">The JSON token to convert.</param>
	/// <returns>
	/// A native object graph consisting of dictionaries, lists, and scalar values corresponding to the JSON structure.
	/// </returns>
	/// <remarks>
	/// Objects become <see cref="Dictionary{TKey, TValue}"/> instances with string keys; arrays become <see cref="List{T}"/>.
	/// Scalar values are unwrapped to their underlying CLR types.
	/// </remarks>
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

	/// <summary>
	/// Releases underlying HTTP resources.
	/// </summary>
	/// <remarks>
	/// Disposes the internal <see cref="HttpClient"/> instance (if any) and suppresses finalization.
	/// The controller should not be used after disposal.
	/// </remarks>
	public void Dispose ()
		{
		_httpClient?.Dispose ();
		_httpClient = null;

		GC.SuppressFinalize (this);
		}

	/// <summary>
	/// Gets the configured hub host string.
	/// </summary>
	/// <returns>The host name or IP address used by this controller.</returns>
	public string GetHost () => _wiserConnection.Host;

#if NETFRAMEWORK
	// .NET Framework 4.7.2 does not support GeneratedRegexAttribute; provide a normal method.
	private static Regex ValidAsciiRegex () =>
		new(@"[^\u0020-\u007F]+", RegexOptions.Compiled | RegexOptions.CultureInvariant);
#else
	[GeneratedRegex (@"[^\u0020-\u007F]+", RegexOptions.Compiled | RegexOptions.CultureInvariant)]
	private static partial Regex ValidAsciiRegex ();
#endif
	}
