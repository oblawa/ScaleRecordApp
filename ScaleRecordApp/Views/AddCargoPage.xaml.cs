using CommunityToolkit.Mvvm.Messaging;
using ScaleRecordApp.Helpers;
using ScaleRecordApp.ViewModels;

namespace ScaleRecordApp.Views;

public partial class AddCargoPage : ContentPage
{
	private readonly AddCargoVM _vm;
	public AddCargoPage(AddCargoVM vm)
	{
		InitializeComponent();
		BindingContext = _vm = vm;
	}
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadCargoAsync();
    }
    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        WeakReferenceMessenger.Default.Send(new CargoTypesUpdatedMessage());
    }
}