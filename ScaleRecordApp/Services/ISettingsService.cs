using Microsoft.Maui.Storage;
using ScaleRecordApp.Models;
using System.Text.Json;


namespace ScaleRecordApp.Services;


public interface ISettingsService
{
    Task<BackupSettings> LoadBackupSettingsAsync();
    Task SaveBackupSettingsAsync(BackupSettings settings);
}


public class PreferencesSettingsService : ISettingsService
{
    private const string Key = "backup_settings_v1";


    public Task<BackupSettings> LoadBackupSettingsAsync()
    {
        var json = Preferences.Get(Key, string.Empty);
        if (string.IsNullOrWhiteSpace(json))
            return Task.FromResult(new BackupSettings());


        var settings = JsonSerializer.Deserialize<BackupSettings>(json) ?? new BackupSettings();
        return Task.FromResult(settings);
    }


    public Task SaveBackupSettingsAsync(BackupSettings settings)
    {
        var json = JsonSerializer.Serialize(settings);
        Preferences.Set(Key, json);
        return Task.CompletedTask;
    }
}