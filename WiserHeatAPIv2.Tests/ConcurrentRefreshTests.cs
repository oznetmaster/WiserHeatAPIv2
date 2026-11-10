// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Threading;

namespace WiserHeatAPIv2.Tests;

[TestFixture]
public sealed class ConcurrentRefreshTests
	{
	[Test]
	public async Task OverlappingRefreshesCompleteInOrderAndPreserveNewerRoomState ()
		{
		using var handler = new Hub ();
		using var api = new WiserAPI ("audit.invalid", "fake", WiserUnits.Metric, handler);
		Task<bool> first = api.ReadHubDataAsync ();
		try
			{
			await handler.Started.Task.WaitAsync (TimeSpan.FromSeconds (10));
			Task<bool> second = api.ReadHubDataAsync ();
			Assert.That (handler.DomainRequests, Is.EqualTo (1));
			Assert.That (second.IsCompleted, Is.False);
			_ = handler.Release.TrySetResult (true);
			Assert.That (await first, Is.True);
			Assert.That (await second, Is.True);
			Assert.That (api.Rooms!.GetById (4)!.Name, Is.EqualTo ("Newer"));
			Assert.That (handler.DomainRequests, Is.EqualTo (2));
			}
		finally { _ = handler.Release.TrySetResult (true); }
		}
	[Test]
	public async Task DisposalRejectsLatePublicationAndWaitsBeforeDisposingTransport ()
		{
		using var handler = new Hub ();
		var api = new WiserAPI ("audit.invalid", "fake", WiserUnits.Metric, handler);
		Task<bool> first = api.ReadHubDataAsync ();
		try
			{
			await handler.Started.Task.WaitAsync (TimeSpan.FromSeconds (10));
			Task<bool> queued = api.ReadHubDataAsync ();
			api.Dispose ();
			Assert.That (handler.Disposed, Is.False);
			_ = handler.Release.TrySetResult (true);
			Assert.That (await first, Is.False);
			Assert.That (await queued, Is.False);
			Assert.That (api.Rooms, Is.Null);
			Assert.That (handler.Disposed, Is.True);
			}
		finally { _ = handler.Release.TrySetResult (true); api.Dispose (); }
		}
	private sealed class Hub : HttpMessageHandler
		{
		internal int DomainRequests;
		internal bool Disposed;
		internal TaskCompletionSource<bool> Started { get; } = new (TaskCreationOptions.RunContinuationsAsynchronously);
		internal TaskCompletionSource<bool> Release { get; } = new (TaskCreationOptions.RunContinuationsAsynchronously);
		protected override async Task<HttpResponseMessage> SendAsync (HttpRequestMessage request, CancellationToken cancellationToken)
			{
			string path = request.RequestUri!.AbsolutePath;
			if (path.Contains ("/domain/"))
				{
				bool first = Interlocked.Increment (ref DomainRequests) == 1;
				if (first)
					{
					_ = Started.TrySetResult (true);
					await Release.Task;
					}
				return Reply ($$"""{"System":{},"Device":[],"Room":[{"id":4,"Name":"{{(first ? "Older" : "Newer")}}","CurrentSetPoint":205}],"HeatingChannel":[],"Moment":[]}""");
				}
			if (path.Contains ("/network/"))
				return Reply ("{\"Station\":{}}");
			if (path.Contains ("/schedules/"))
				return Reply ("{\"Heating\":[]}");
			return Reply ("not supported", HttpStatusCode.NotFound);
			}
		private static HttpResponseMessage Reply (string text, HttpStatusCode status = HttpStatusCode.OK) => new (status) { Content = new StringContent (text) };
		protected override void Dispose (bool disposing)
			{
			Disposed = true;
			base.Dispose (disposing);
			}
		}
	}