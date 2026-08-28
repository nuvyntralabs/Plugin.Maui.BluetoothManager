namespace Plugin.Maui.BluetoothManager;

sealed class NativeConnectionEventArgs : EventArgs
{
	public NativeConnectionEventArgs(string deviceId, bool isConnected, string? errorMessage = null)
	{
		DeviceId = deviceId;
		IsConnected = isConnected;
		ErrorMessage = errorMessage;
	}

	public string DeviceId { get; }

	public bool IsConnected { get; }

	public string? ErrorMessage { get; }
}
