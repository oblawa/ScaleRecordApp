using CommunityToolkit.Mvvm.Messaging;
using ScaleRecordApp.Helpers;
using ScaleRecordApp.ViewModels;

namespace ScaleRecordApp.Views;

public partial class AddVehiclePage : ContentPage
{
	private readonly AddVehicleVM _vm;
	public AddVehiclePage(AddVehicleVM vm)
	{
		InitializeComponent();
		BindingContext = _vm = vm;
	}
	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await _vm.LoadVehiclesAsync();
	}
    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        WeakReferenceMessenger.Default.Send(new VehiclesUpdatedMessage());
    }

}