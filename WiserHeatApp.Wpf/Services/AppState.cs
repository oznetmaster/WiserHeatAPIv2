using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

using WiserHeatApiV2;

namespace WiserHeatApp.Wpf.Services;

public class AppState
	{
	public static AppState Current { get; } = new ();

	private AppState ()
		{
		}

	public WiserAPI? Api
		{
		get; private set;
		}
	public bool IsConnected => Api != null;

	private CancellationTokenSource? _pollCts;

	public async Task<bool> ConnectAsync (string host, string secret, CancellationToken ct = default)
		{
		Api = new WiserAPI (host, secret);
		await Api.InitializeAsync (ct).ConfigureAwait (false);
		return true;
		}

	public void StartPolling (TimeSpan interval)
		{
		StopPolling ();
		if (Api == null)
			return;
		_pollCts = new CancellationTokenSource ();
		_ = Task.Run (async () =>
		{
			while (!_pollCts.IsCancellationRequested)
				{
				try
					{
					_ = await Api!.ReadHubDataAsync (_pollCts.Token).ConfigureAwait (false);
					}
				catch { /* ignore poll errors */ }

				await Task.Delay (interval, _pollCts.Token).ConfigureAwait (false);
				}
		});
		}

	public void StopPolling ()
		{
		try
			{
			_pollCts?.Cancel ();
			}
		catch { }

		_pollCts = null;
		}
	}