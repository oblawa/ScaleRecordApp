using ScaleRecordApp.ViewModels;

namespace ScaleRecordApp.Views;

public partial class BackupSettingsPage : ContentPage
{
    private readonly BackupSettingsViewModel _vm;

    public BackupSettingsPage(BackupSettingsViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.InitializeAsync();
    }
}
