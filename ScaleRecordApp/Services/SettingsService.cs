using ScaleRecordApp.Helpers;
using System.Text.Json;

public static class SettingsService
{
    private const string Key = "ReportSettings";

    public static ReportSettings Load()
    {
        var json = Preferences.Get(Key, "");
        if (string.IsNullOrWhiteSpace(json))
        {
            // дефолтные настройки
            return new ReportSettings
            {
                AutoReportEnabled = true,
                AutoReportHour = 7,
                ChatIds = new List<string> { "798875980" },
                TelegramBotToken = "8365295280:AAG9lYhr-VTTr2exPD5BsFPNmtJzYOcOJ0s"
            };
        }

        return JsonSerializer.Deserialize<ReportSettings>(json) ?? new ReportSettings();
    }

    public static void Save(ReportSettings settings)
    {
        var json = JsonSerializer.Serialize(settings);
        Preferences.Set(Key, json);
    }
}
