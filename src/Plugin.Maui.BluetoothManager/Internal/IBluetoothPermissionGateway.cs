namespace Plugin.Maui.BluetoothManager;

internal interface IBluetoothPermissionGateway
{
	Task<BluetoothPermissionStatus> CheckAsync();

	Task<BluetoothPermissionStatus> RequestAsync();
}
