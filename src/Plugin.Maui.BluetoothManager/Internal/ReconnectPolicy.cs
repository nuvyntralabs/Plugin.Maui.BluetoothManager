namespace Plugin.Maui.BluetoothManager;

static class ReconnectPolicy
{
	public static TimeSpan DelayForAttempt(int attempt, TimeSpan initial, TimeSpan max)
	{
		if (attempt < 0)
			attempt = 0;

		var milliseconds = initial.TotalMilliseconds * Math.Pow(2, attempt);
		var capped = Math.Min(milliseconds, max.TotalMilliseconds);
		return TimeSpan.FromMilliseconds(capped);
	}

	public static bool ShouldReconnect(ConnectOptions options, BluetoothManagerOptions defaults, int attempt)
	{
		var enabled = options.AutoReconnect ?? defaults.AutoReconnect;
		if (!enabled)
			return false;

		var max = options.MaxReconnectAttempts ?? defaults.MaxReconnectAttempts;
		return attempt < max;
	}
}
