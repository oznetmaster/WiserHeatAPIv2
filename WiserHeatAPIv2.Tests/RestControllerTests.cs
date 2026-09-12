// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace WiserHeatAPIv2.Tests;

[TestFixture]
public sealed class RestControllerTests
	{
	private ScriptedHub _hub = null!;
	private WiserRestController _controller = null!;
	private WiserConnection _connection = null!;

	[SetUp]
	public void SetUp ()
		{
		_hub = new ScriptedHub ();
		_connection = new WiserConnection ("hub.example", "test-secret");
		_controller = new WiserRestController (_connection, _hub);
		}

	[TearDown]
	public void TearDown ()
		{
		_controller.Dispose ();
		_hub.Dispose ();
		}

	[TestCase (WiserRestAction.GET, "GET")]
	[TestCase (WiserRestAction.POST, "POST")]
	[TestCase (WiserRestAction.PATCH, "PATCH")]
	[TestCase (WiserRestAction.DELETE, "DELETE")]
	public async Task Request_UsesSelectedVerbAndAuthentication (WiserRestAction action, string verb)
		{
		_hub.Reply ();
		using var payload = new StringContent ("payload");
		using var response = await _controller.ExecuteHttpRequestAsync (action, "http://hub.example/data/v2/domain/", payload);
		Assert.That (_hub.Requests.Single ().Method, Is.EqualTo (verb));
		Assert.That (_hub.Requests.Single ().Secret, Is.EqualTo ("test-secret"));
		Assert.That (_hub.Requests.Single ().Body, Is.EqualTo (action == WiserRestAction.GET ? "" : "payload"));
		_hub.AssertComplete ();
		}

	[Test]
	public async Task ChangedSecret_IsUsedOnTheNextRequest ()
		{
		_connection.Secret = "replacement-secret";
		_hub.Reply ();
		await _controller.GetHubDataAsync ("http://hub.example/data/v2/domain/");
		Assert.That (_hub.Requests.Single ().Secret, Is.EqualTo ("replacement-secret"));
		}

	[TestCase ("UPDATE", "PATCH", "Heating/7")]
	[TestCase ("create", "POST", "Assign")]
	[TestCase ("ASSIGN", "PATCH", "Assign")]
	[TestCase ("DELETE", "DELETE", "Heating/7")]
	public async Task ScheduleCommand_UsesScheduleEndpointAndHttp10 (string action, string verb, string path)
		{
		_hub.Reply ();
		Assert.That (await _controller.SendScheduleCommandAsync (action, new
			{
			Name = "Weekdays"
			}, 7, "Heating"), Is.True);
		var request = _hub.Requests.Single ();
		Assert.That (request.Url, Is.EqualTo ("http://hub.example/data/v2/schedules/" + path));
		Assert.That (request.Method, Is.EqualTo (verb));
		Assert.That (request.Version, Is.EqualTo (new Version (1, 0)));
		Assert.That (JObject.Parse (request.Body)["Name"]!.Value<string> (), Is.EqualTo ("Weekdays"));
		}

	[Test]
	public async Task InvalidScheduleAction_DoesNotSendARequest ()
		{
		Assert.That (await _controller.SendScheduleCommandAsync ("unknown", null), Is.False);
		Assert.That (_hub.Requests, Is.Empty);
		}

	[TestCase (301)]
	[TestCase (302)]
	[TestCase (307)]
	[TestCase (308)]
	public async Task HttpRedirect_ResendsCommandOverHttps (int status)
		{
		_hub.Reply (status: (HttpStatusCode)status);
		_hub.Reply ();
		Assert.That (await _controller.SendCommandAsync ("Room/4", new
			{
			Name = "Study"
			}), Is.True);
		Assert.That (_hub.Requests[1].Url, Is.EqualTo ("https://hub.example/data/v2/domain/Room/4"));
		Assert.That (_hub.Requests[1].Body, Is.EqualTo (_hub.Requests[0].Body));
		_hub.AssertComplete ();
		}

	[TestCase (413)]
	[TestCase (500)]
	[TestCase (502)]
	[TestCase (503)]
	[TestCase (504)]
	public async Task TransientFailure_RetriesWithTheOriginalCommandBody (int status)
		{
		_hub.Reply (status: (HttpStatusCode)status);
		_hub.Reply ();
		Assert.That (await _controller.SendCommandAsync ("Room/4", new
			{
			Name = "Study"
			}), Is.True);
		Assert.That (_hub.Requests, Has.Count.EqualTo (2));
		Assert.That (_hub.Requests[1].Body, Is.EqualTo (_hub.Requests[0].Body));
		_hub.AssertComplete ();
		}

	[TestCase (401, typeof (WiserHubAuthenticationException))]
	[TestCase (404, typeof (WiserHubRESTException))]
	[TestCase (400, typeof (WiserHubRESTException))]
	[TestCase (408, typeof (WiserHubConnectionException))]
	public void FailedRead_PreservesTheDocumentedExceptionType (int status, Type exceptionType)
		{
		_hub.Reply ("denied", (HttpStatusCode)status);
		Assert.ThrowsAsync (exceptionType, async () => await _controller.GetHubDataAsync ("http://hub.example/data/v2/domain/"));
		Assert.That (_hub.Requests, Has.Count.EqualTo (1));
		}

	[TestCase (401, typeof (WiserHubAuthenticationException))]
	[TestCase (404, typeof (WiserHubRESTException))]
	public void FailedCommand_PreservesTheDocumentedExceptionType (int status, Type exceptionType)
		{
		_hub.Reply ("denied", (HttpStatusCode)status);
		Assert.ThrowsAsync (exceptionType, async () => await _controller.SendCommandAsync ("Room/4", new { Name = "Study" }));
		}

	[Test]
	public async Task MissingOptionalEndpoint_ReturnsEmptyData ()
		{
		_hub.Reply ("not available", HttpStatusCode.NotFound);
		Assert.That (await _controller.GetHubDataAsync ("http://hub.example/data/v2/opentherm/", raiseForEndpointError: false), Is.Empty);
		}

	[TestCase ("read")]
	[TestCase ("command")]
	public void CallerCancellation_RemainsCancellation (string operation)
		{
		using var cancellation = new CancellationTokenSource ();
		_hub.Respond ((_, token) =>
			{
				cancellation.Cancel ();
				token.ThrowIfCancellationRequested ();
				throw new InvalidOperationException ("Cancellation was not forwarded.");
			});
		Assert.CatchAsync<OperationCanceledException> (async () =>
			{
				if (operation == "read")
					await _controller.GetHubDataAsync ("http://hub.example/data/v2/domain/", cancellationToken: cancellation.Token);
				else
					await _controller.SendCommandAsync ("Room/4", null, cancellationToken: cancellation.Token);
			});
		}

	[Test]
	public void TransportFailure_IsReportedAsAConnectionError ()
		{
		_hub.Respond ((_, _) => throw new HttpRequestException ("connection unavailable"));
		var error = Assert.ThrowsAsync<WiserHubConnectionException> (async () => await _controller.GetHubDataAsync ("http://hub.example/data/v2/domain/"));
		Assert.That (error!.Message, Does.Contain ("hub.example"));
		}

	[Test]
	public async Task Response_PreservesUnicodeNamesAndNestedDeviceLists ()
		{
		_hub.Reply ("""{"Room":[{"id":1,"Name":"Büro 温度","SmartValveIds":[2,3]}]}""");
		var data = await _controller.GetHubDataAsync ("http://hub.example/data/v2/domain/");
		var room = ((List<Dictionary<string, object>>)data["Room"]).Single ();
		Assert.That (room["Name"], Is.EqualTo ("Büro 温度"));
		Assert.That ((List<object>)room["SmartValveIds"], Is.EqualTo (new object[] { 2L, 3L }));
		}

	[TestCase ("")]
	[TestCase ("null")]
	[TestCase ("{}")]
	public async Task EmptyResponse_ReturnsEmptyData (string body)
		{
		_hub.Reply (body);
		Assert.That (await _controller.GetHubDataAsync ("http://hub.example/data/v2/domain/"), Is.Empty);
		}

	[Test]
	public void MalformedResponse_IsReportedAsAConnectionError ()
		{
		_hub.Reply ("not json");
		Assert.ThrowsAsync<WiserHubConnectionException> (async () => await _controller.GetHubDataAsync ("http://hub.example/data/v2/domain/"));
		}

	[Test]
	public void Dispose_ReleasesTransportAndCanBeRepeated ()
		{
		_controller.Dispose ();
		Assert.That (_hub.Disposed, Is.True);
		Assert.DoesNotThrow (_controller.Dispose);
		}

	[Test]
	public void TransportTimeout_StillProducesAConnectionError ()
		{
		_hub.Respond ((_, _) => throw new TaskCanceledException ("simulated transport timeout"));
		var error = Assert.ThrowsAsync<WiserHubConnectionException> (async () =>
			await _controller.GetHubDataAsync ("http://hub.example/data/v2/domain/"));
		Assert.That (error!.Message, Does.Contain ("Timeout"));
		}

	[Test]
	public async Task RetryLimit_ReturnsLastFailureAndDisposesEarlierResponses ()
		{
		var contents = new List<TrackingContent> ();
		for (var attempt = 0; attempt < 4; attempt++)
			{
			var content = new TrackingContent ();
			contents.Add (content);
			_hub.Respond ((_, _) => Task.FromResult (new HttpResponseMessage (HttpStatusCode.ServiceUnavailable) { Content = content }));
			}
		using var response = await _controller.ExecuteHttpRequestAsync (WiserRestAction.GET, "http://hub.example/data/v2/domain/");
		Assert.That (response!.StatusCode, Is.EqualTo (HttpStatusCode.ServiceUnavailable));
		Assert.That (_hub.Requests, Has.Count.EqualTo (4));
		Assert.That (contents.Take (3).All (c => c.Disposed), Is.True);
		Assert.That (contents[3].Disposed, Is.False, "The final response belongs to the caller.");
		response.Dispose ();
		Assert.That (contents[3].Disposed, Is.True);
		_hub.AssertComplete ();
		}

	[Test]
	public void CancellationDuringBackoff_DisposesResponseAndDoesNotRetry ()
		{
		using var cancellation = new CancellationTokenSource ();
		using var content = new TrackingContent ();
		_hub.Respond ((_, _) =>
			{
				cancellation.Cancel ();
				return Task.FromResult (new HttpResponseMessage (HttpStatusCode.ServiceUnavailable) { Content = content });
			});
		Assert.CatchAsync<OperationCanceledException> (async () =>
			await _controller.GetHubDataAsync ("http://hub.example/data/v2/domain/", cancellationToken: cancellation.Token));
		Assert.That (_hub.Requests, Has.Count.EqualTo (1));
		Assert.That (content.Disposed, Is.True);
		}

	[Test]
	public async Task RedirectedSchedule_KeepsHttp10AndJsonContentHeaders ()
		{
		_hub.Reply (status: HttpStatusCode.TemporaryRedirect);
		_hub.Respond ((request, _) =>
			{
				Assert.That (request.Version, Is.EqualTo (new Version (1, 0)));
#if NET10_0_OR_GREATER
			Assert.That (request.VersionPolicy, Is.EqualTo (HttpVersionPolicy.RequestVersionExact));
#endif
				Assert.That (request.Content!.Headers.ContentType!.MediaType, Is.EqualTo ("application/json"));
				Assert.That (request.Content.Headers.ContentType.CharSet, Is.EqualTo ("utf-8"));
				return Task.FromResult (new HttpResponseMessage (HttpStatusCode.OK));
			});
		Assert.That (await _controller.SendScheduleCommandAsync ("UPDATE", new
			{
			Name = "Schedule"
			}, 7, "Heating"), Is.True);
		_hub.AssertComplete ();
		}

	[Test]
	public async Task Response_AcceptsBomAndLegacyInvalidControlCharacters ()
		{
		_hub.Reply ("\uFEFF{\n\"Name\":\"Büro\"}\0");
		var result = await _controller.GetHubDataAsync ("http://hub.example/data/v2/domain/");
		Assert.That (result["Name"], Is.EqualTo ("Büro"));
		}

	private sealed class TrackingContent : StringContent
		{
		internal TrackingContent () : base ("{}")
			{
			}

		internal bool Disposed
			{
			get; private set;
			}

		protected override void Dispose (bool disposing)
			{
			Disposed = true;
			base.Dispose (disposing);
			}
		}
	}