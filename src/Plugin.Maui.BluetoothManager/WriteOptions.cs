namespace Plugin.Maui.BluetoothManager;

/// <summary>
/// Per-write overrides for GATT write type and retry.
/// </summary>
public sealed class WriteOptions
{
	/// <summary>
	/// Gets or sets the write mode. Default chooses WithResponse when the characteristic supports it.
	/// </summary>
	public BleWriteType WriteType { get; set; } = BleWriteType.Default;

	/// <summary>
	/// Gets or sets extra retries for this write. When <c>null</c>, <see cref="BluetoothManagerOptions.OperationRetryCount"/> is used.
	/// </summary>
	public int? RetryCount { get; set; }
}
