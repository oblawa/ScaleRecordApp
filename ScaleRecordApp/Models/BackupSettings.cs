using System.Text.Json.Serialization;


namespace ScaleRecordApp.Models;


public class BackupSettings
{
    public bool Enabled { get; set; }
    public string ChatIdOrUsername { get; set; } = string.Empty; // "123456789" или "@username"
    public BackupPeriod Period { get; set; } = BackupPeriod.Daily;


    // Важно: используем локальное время, чтобы совпадало с Timestamp записей
    public DateTime? LastSentLocal { get; set; } // момент последней успешной отправки (max Timestamp из отправленного отчёта)
}