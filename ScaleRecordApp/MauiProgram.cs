//using Microsoft.Extensions.Logging;

//namespace ScaleRecordApp
//{
//    public static class MauiProgram
//    {
//        public static MauiApp CreateMauiApp()
//        {
//            var builder = MauiApp.CreateBuilder();
//            builder
//                .UseMauiApp<App>()
//                .ConfigureFonts(fonts =>
//                {
//                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
//                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
//                });

//#if DEBUG
//    		builder.Logging.AddDebug();
//#endif

//            return builder.Build();
//        }
//    }
//}]
using CommunityToolkit.Maui;
using ScaleRecordApp.Services;
using ScaleRecordApp.ViewModels;
using ScaleRecordApp.Views;
using SQLite;
using System.IO;

namespace ScaleRecordApp
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            // берем токен из Preferences, при первом запуске можно положить дефолтный токен:
            var defaultToken = Preferences.Get("TelegramBotToken", "8365295280:AAG9lYhr-VTTr2exPD5BsFPNmtJzYOcOJ0s");

            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    
                });

            // DB path
            string dbPath = Path.Combine(FileSystem.AppDataDirectory, "scalerecord.db3");
            builder.Services.AddSingleton(new DatabaseService(dbPath));
            builder.Services.AddSingleton(new SQLiteAsyncConnection(dbPath));
            builder.Services.AddTransient<AddVehicleVM>();
            builder.Services.AddTransient<AddVehiclePage>();

            builder.Services.AddTransient<WeighingRecordPage>();
            builder.Services.AddTransient<WeighingRecordVM>();

            builder.Services.AddTransient<AddCargoPage>();
            builder.Services.AddTransient<AddCargoVM>();


            // using Microsoft.Maui.Storage; если нужно
            // Зарегистрируем Excel/Telegram и планировщик
            builder.Services.AddSingleton<ExcelService>();



            builder.Services.AddSingleton(sp => new TelegramService(defaultToken, new HttpClient()));




            builder.Services.AddTransient<HistoryPage>();
            builder.Services.AddTransient<HistoryVM>();



            // Core services
            builder.Services.AddSingleton<ISettingsService, PreferencesSettingsService>();
            builder.Services.AddSingleton<IHistoryRepository, HistoryRepository>();



            builder.Services.AddSingleton<IBackupService, BackupService>();

            // VM + Pages
            builder.Services.AddTransient<BackupSettingsViewModel>();
            builder.Services.AddTransient<BackupSettingsPage>();










            // register pages/viewmodels as needed
            //builder.Services.AddTransient<Views.WeighingRecordPage>();
            //builder.Services.AddTransient<ViewModels.WeighingRecordViewModel>();





            var app = builder.Build();

            var backupService = app.Services.GetRequiredService<IBackupService>();
            backupService.Start();
            return app;
        }
    }
}
