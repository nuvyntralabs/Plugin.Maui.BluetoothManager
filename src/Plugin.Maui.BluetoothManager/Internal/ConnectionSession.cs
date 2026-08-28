namespace Plugin.Maui.BluetoothManager;

sealed class ConnectionSession
{
	public ConnectionSession(BleDevice device, ConnectOptions options)
	{
		Device = device;
		Options = options;
		State = BluetoothConnectionState.Disconnected;
		Services = [];
	}

	public string DeviceId => Device.Id;

	public BleDevice Device { get; set; }

	public ConnectOptions Options { get; }

	public BluetoothConnectionState State { get; set; }

	public IReadOnlyList<BleService> Services { get; set; }

	public int ReconnectAttempt { get; set; }

	public bool UserRequestedDisconnect { get; set; }

	public CancellationTokenSource? ReconnectCts { get; set; }

	public void CancelReconnect()
	{
		try
		{
			ReconnectCts?.Cancel();
		}
		catch (ObjectDisposedException)
		{
		}

		ReconnectCts?.Dispose();
		ReconnectCts = null;
	}
}
