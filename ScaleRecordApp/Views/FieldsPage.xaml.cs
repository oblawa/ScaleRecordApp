using ScaleRecordApp.ViewModels;

namespace ScaleRecordApp.Views;

public partial class FieldsPage : ContentPage
{
    private FieldsVM Vm => BindingContext as FieldsVM;

    public FieldsPage(FieldsVM vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (Vm != null)
            await Vm.LoadFieldsAsync();
    }
}