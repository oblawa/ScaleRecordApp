using CommunityToolkit.Mvvm.Messaging;
using ScaleRecordApp.Helpers;
using ScaleRecordApp.ViewModels;

namespace ScaleRecordApp.Views;

public partial class WeighingRecordPage : ContentPage
{
	private readonly WeighingRecordVM _vm;
	bool isDataLoaded;
	public WeighingRecordPage(WeighingRecordVM vm)
	{
		InitializeComponent();
		BindingContext = _vm = vm;
	}
    protected override async void OnAppearing()
    {
		if (isDataLoaded)
			return;
		isDataLoaded = true;

        base.OnAppearing();
        await _vm.LoadDataAsync();

        WeakReferenceMessenger.Default.Register<VehiclesUpdatedMessage>(this, async (r, m) =>
        {
            await _vm.LoadDataAsync();
        });
        WeakReferenceMessenger.Default.Register<CargoTypesUpdatedMessage>(this, async (r, m) =>
        {
            await _vm.LoadDataAsync();
        });
    }
}