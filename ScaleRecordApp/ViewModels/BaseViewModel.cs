using CommunityToolkit.Mvvm;
using CommunityToolkit.Mvvm.ComponentModel;
namespace ScaleRecordApp.ViewModels
{
    public partial class BaseViewModel : ObservableObject
    {
        [ObservableProperty]
        private bool isBusy;

        [ObservableProperty]
        private string title;
    }
}
