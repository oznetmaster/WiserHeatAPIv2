// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.IO;
using System.Net.Sockets;
using System.Text;

namespace WiserHeatAPIv2.Tests;

[TestFixture]
public sealed class ResponseBodyCancellationTests
	{
	[TestCase (false, 200)]
	[TestCase (true, 200)]
	[TestCase (true, 401)]
	public async Task Cancellation_StopsAStalledHttpConnection (bool sendHeaders, int status)
		{
		var listener = new TcpListener (IPAddress.Loopback, 0);
		listener.Start ();
		using var lifetime = new CancellationTokenSource (TimeSpan.FromSeconds (10));
		using var stopListener = lifetime.Token.Register (listener.Stop);
		TcpClient? peer = null;
		Task? pending = null;
		try
			{
			int port = ((IPEndPoint)listener.LocalEndpoint).Port;
			using var controller = new WiserRestController (new WiserConnection ("127.0.0.1", "test-secret"));
			using var cancellation = new CancellationTokenSource ();
			var accepted = listener.AcceptTcpClientAsync ();
			pending = controller.GetHubDataAsync ($"http://127.0.0.1:{port}/data/v2/domain/", cancellationToken: cancellation.Token);
			peer = await accepted;
			using var stopPeer = lifetime.Token.Register (peer.Dispose);
			var stream = peer.GetStream ();
			var request = new StringBuilder ();
			var buffer = new byte[1];
			while (!request.ToString ().EndsWith ("\r\n\r\n", StringComparison.Ordinal))
				{
				Assert.That (await stream.ReadAsync (buffer, 0, 1, lifetime.Token), Is.EqualTo (1));
				request.Append ((char)buffer[0]);
				Assert.That (request.Length, Is.LessThan (16384));
				}
			if (sendHeaders)
				{
				byte[] response = Encoding.ASCII.GetBytes ($"HTTP/1.1 {status} Test\r\nContent-Type: application/json\r\nContent-Length: 100\r\nConnection: close\r\n\r\n{{");
				await stream.WriteAsync (response, 0, response.Length, lifetime.Token);
				await stream.FlushAsync (lifetime.Token);
				}
			await Task.Delay (150, lifetime.Token);
			Assert.That (pending.IsCompleted, Is.False, "The server must still be withholding the response.");
			cancellation.Cancel ();
			Assert.That (await Task.WhenAny (pending, Task.Delay (TimeSpan.FromSeconds (2))), Is.SameAs (pending), "Cancellation must stop the real HTTP transport while its peer remains connected.");
			Assert.CatchAsync<OperationCanceledException> (async () => await pending);
			}
		finally
			{
			peer?.Dispose ();
			listener.Stop ();
			if (pending != null)
				try { await pending; } catch (Exception) { }
			}
		}

	[TestCase (HttpStatusCode.OK, false)]
	[TestCase (HttpStatusCode.Unauthorized, false)]
	[TestCase (HttpStatusCode.Unauthorized, true)]
	public async Task CancellationAfterHeaders_StopsAStalledResponseBody (HttpStatusCode status, bool command)
		{
		using var hub = new ScriptedHub ();
		using var body = new StalledBody ();
		hub.Respond ((_, _) => Task.FromResult (new HttpResponseMessage (status) { Content = new StreamContent (body) }));
		using var controller = new WiserRestController (new WiserConnection ("hub.example", "test-secret"), hub);
		using var cancellation = new CancellationTokenSource ();
		Task pending = command
			? controller.SendCommandAsync ("System", new { AwayModeActive = true }, cancellationToken: cancellation.Token)
			: controller.GetHubDataAsync ("http://hub.example/data/v2/domain/", cancellationToken: cancellation.Token);
		try
			{
			if (await Task.WhenAny (body.ReadStarted.Task, pending, Task.Delay (TimeSpan.FromSeconds (3))) == pending)
				await pending;
			Assert.That (await Task.WhenAny (body.ReadStarted.Task, Task.Delay (TimeSpan.FromSeconds (3))), Is.SameAs (body.ReadStarted.Task), "Response headers were returned, but the body read did not begin.");
			cancellation.Cancel ();
			Assert.That (await Task.WhenAny (pending, Task.Delay (TimeSpan.FromSeconds (2))), Is.SameAs (pending), "Caller cancellation must end the body read, not wait for the network stream to recover.");
			Assert.CatchAsync<OperationCanceledException> (async () => await pending);
			Assert.That (body.Disposed, Is.True, "Cancellation must release the response stream.");
			hub.Reply ("{\"ok\":true}");
			var recovered = await controller.GetHubDataAsync ("http://hub.example/data/v2/domain/");
			Assert.That (recovered["ok"], Is.EqualTo (true), "Aborting one response must not dispose the reusable client.");
			hub.AssertComplete ();
			}
		finally
			{
			body.Dispose ();
			try { await pending; } catch (Exception) { }
			}
		}

	private sealed class StalledBody : Stream
		{
		internal TaskCompletionSource<bool> ReadStarted { get; } = new (TaskCreationOptions.RunContinuationsAsynchronously);
		internal bool Disposed { get; private set; }
		private readonly TaskCompletionSource<int> _release = new (TaskCreationOptions.RunContinuationsAsynchronously);
		public override bool CanRead => true;
		public override bool CanSeek => false;
		public override bool CanWrite => false;
		public override long Length => throw new NotSupportedException ();
		public override long Position { get => throw new NotSupportedException (); set => throw new NotSupportedException (); }
		public override async Task<int> ReadAsync (byte[] buffer, int offset, int count, CancellationToken cancellationToken)
			{
			ReadStarted.TrySetResult (true);
			using var cancelled = cancellationToken.Register (() => _release.TrySetCanceled ());
			return await _release.Task;
			}
		public override int Read (byte[] buffer, int offset, int count) => ReadAsync (buffer, offset, count, CancellationToken.None).GetAwaiter ().GetResult ();
		public override void Flush () => throw new NotSupportedException ();
		public override long Seek (long offset, SeekOrigin origin) => throw new NotSupportedException ();
		public override void SetLength (long value) => throw new NotSupportedException ();
		public override void Write (byte[] buffer, int offset, int count) => throw new NotSupportedException ();
		protected override void Dispose (bool disposing)
			{
			if (disposing)
				{
				Disposed = true;
				_release.TrySetCanceled ();
				}
			base.Dispose (disposing);
			}
		}
	}