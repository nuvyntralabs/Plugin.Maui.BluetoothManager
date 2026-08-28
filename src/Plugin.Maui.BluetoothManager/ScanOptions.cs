namespace Plugin.Maui.BluetoothManager;

/// <summary>
/// Filters and timing applied to a single scan.
/// </summary>
public sealed class ScanOptions
{
	/// <summary>
	/// Gets or sets how long the scan runs. When <c>null</c>, <see cref="BluetoothManagerOptions.DefaultScanDuration"/> is used.
	/// </summary>
	public TimeSpan? Duration { get; set; }

	/// <summary>
	/// Gets or sets advertised service UUIDs to match. Devices that do not advertise any of these are dropped.
	/// </summary>
	public IReadOnlyList<Guid>? ServiceUuids { get; set; }

	/// <summary>
	/// Gets or sets a case-insensitive substring that must appear in the device name.
	/// Unnamed devices are excluded when this is set.
	/// </summary>
	public string? NameContains { get; set; }

	/// <summary>
	/// Gets or sets a minimum RSSI in dBm. Weaker advertisements are ignored.
	/// </summary>
	public int? MinimumRssi { get; set; }

	/// <summary>
	/// Gets or sets an additional predicate applied after built-in filters.
	/// </summary>
	public Func<BleDevice, bool>? Filter { get; set; }

	/// <summary>
	/// Gets or sets whether duplicate advertisements should raise <see cref="IBluetoothManager.DeviceDiscovered"/> again
	/// so RSSI can be updated. Default is <c>false</c>.
	/// </summary>
	public bool AllowDuplicates { get; set; }
}
