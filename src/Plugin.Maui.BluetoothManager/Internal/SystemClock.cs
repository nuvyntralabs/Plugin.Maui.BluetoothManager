namespace Plugin.Maui.BluetoothManager;

sealed class SystemClock : IClock
{
	public static readonly SystemClock Instance = new();

	public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

	public Task Delay(TimeSpan delay, CancellationToken cancellationToken) =>
		Task.Delay(delay, cancellationToken);
}
