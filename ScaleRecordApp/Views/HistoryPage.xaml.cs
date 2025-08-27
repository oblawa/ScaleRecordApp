using ScaleRecordApp.ViewModels;

namespace ScaleRecordApp.Views;

public partial class HistoryPage : ContentPage
{
    bool _loaded;
    public HistoryPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_loaded) return;
        _loaded = true;

        if (BindingContext is HistoryVM vm)
            await vm.LoadAsync();
    }
}







//using ScaleRecordApp.ViewModels;

//namespace ScaleRecordApp.Views;

//public partial class HistoryPage : ContentPage
//{
//    public HistoryPage(HistoryVM vm)
//    {
//        InitializeComponent();
//        BindingContext = vm;
//    }
//    protected override async void OnAppearing()
//    {
//        base.OnAppearing();
//        if (BindingContext is HistoryVM vm)
//        {
//            await vm.LoadAsync();
//            await vm.ApplySelectedRangeAndFilterAsync(); // применяем дефолтный период
//        }
//    }
//}
