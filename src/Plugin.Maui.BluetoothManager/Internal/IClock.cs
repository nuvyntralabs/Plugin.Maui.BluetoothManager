namespace Plugin.Maui.BluetoothManager;

internal interface IClock
{
	DateTimeOffset UtcNow { get; }

	Task Delay(TimeSpan delay, CancellationToken cancellationToken);
}
