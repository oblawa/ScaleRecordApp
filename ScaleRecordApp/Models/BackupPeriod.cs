namespace ScaleRecordApp.Models;


public enum BackupPeriod
{
    Hourly,
    TwelveHours,
    Daily,
    Weekly
}


public static class BackupPeriodExtensions
{
    public static TimeSpan ToTimeSpan(this BackupPeriod p) => p switch
    {
        BackupPeriod.Hourly => TimeSpan.FromHours(1),
        BackupPeriod.TwelveHours => TimeSpan.FromHours(12),
        BackupPeriod.Daily => TimeSpan.FromDays(1),
        BackupPeriod.Weekly => TimeSpan.FromDays(7),
        _ => TimeSpan.FromDays(1)
    };


    public static string ToDisplay(this BackupPeriod p) => p switch
    {
        BackupPeriod.Hourly => "Каждый час",
        BackupPeriod.TwelveHours => "Каждые 12 часов",
        BackupPeriod.Daily => "Каждые 24 часа",
        BackupPeriod.Weekly => "Каждую неделю",
        _ => p.ToString()
    };
}