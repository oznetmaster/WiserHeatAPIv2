// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace WiserHeatAPIv2.Tests;

// An unexpected request fails immediately; this handler never contacts a real hub.
internal sealed class ScriptedHub : HttpMessageHandler
	{
	private readonly Queue<Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>> _responses = new ();
	internal List<(string Method, string Url, Version Version, string Body, string Secret)> Requests { get; } = [];
	internal bool Disposed
		{
		get; private set;
		}

	internal void Reply (string json = "{}", HttpStatusCode status = HttpStatusCode.OK) =>
		Respond ((_, _) => Task.FromResult (new HttpResponseMessage (status)
			{
			Content = new StringContent (json, System.Text.Encoding.UTF8, "application/json")
			}));

	internal void Respond (Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> response) => _responses.Enqueue (response);

	protected override async Task<HttpResponseMessage> SendAsync (HttpRequestMessage request, CancellationToken cancellationToken)
		{
		cancellationToken.ThrowIfCancellationRequested ();
		Requests.Add ((request.Method.Method, request.RequestUri!.AbsoluteUri, request.Version,
			request.Content == null ? "" : await request.Content.ReadAsStringAsync (),
			string.Join (",", request.Headers.GetValues ("SECRET"))));
		Assert.That (_responses, Is.Not.Empty, "Unexpected HTTP request: " + request.RequestUri);
		return await _responses.Dequeue () (request, cancellationToken);
		}

	internal void AssertComplete () => Assert.That (_responses, Is.Empty, "Expected hub requests were not sent.");

	protected override void Dispose (bool disposing)
		{
		Disposed = true;
		base.Dispose (disposing);
		}

	internal static Dictionary<string, object> Data (string json) =>
		(Dictionary<string, object>)WiserRestController.ConvertJTokenToObject (JToken.Parse (json))!;
	}