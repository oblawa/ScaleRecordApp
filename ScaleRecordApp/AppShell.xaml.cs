using ScaleRecordApp.ViewModels;
using ScaleRecordApp.Views;

namespace ScaleRecordApp
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
            Routing.RegisterRoute(nameof(AddVehiclePage), typeof(AddVehiclePage));
            Routing.RegisterRoute(nameof(WeighingRecordPage), typeof(WeighingRecordPage));
            Routing.RegisterRoute(nameof(AddCargoPage), typeof(AddCargoPage));
            Routing.RegisterRoute(nameof(HistoryPage), typeof(HistoryPage));
            Routing.RegisterRoute(nameof(BackupSettingsPage), typeof(BackupSettingsPage));
        }
    }
}
