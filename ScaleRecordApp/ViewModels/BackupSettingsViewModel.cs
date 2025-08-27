using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DocumentFormat.OpenXml.Wordprocessing;
using ScaleRecordApp.Models;
using ScaleRecordApp.Services;

namespace ScaleRecordApp.ViewModels;

public partial class BackupSettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settings;
    private readonly IBackupService _backup;

    public record PeriodItem(BackupPeriod Value, string Title);

    public IReadOnlyList<PeriodItem> Periods { get; } = new[]
    {
        new PeriodItem(BackupPeriod.Hourly, "Каждый час"),
        new PeriodItem(BackupPeriod.TwelveHours, "Каждые 12 часов"),
        new PeriodItem(BackupPeriod.Daily, "Каждые 24 часа"),
        new PeriodItem(BackupPeriod.Weekly, "Каждую неделю"),
    };

    [ObservableProperty]
    private bool isEnabled;

    [ObservableProperty]
    private PeriodItem? selectedPeriod;

    [ObservableProperty]
    private string chatId = string.Empty;

    [ObservableProperty]
    private string? chatIdError;

    [ObservableProperty]
    private string? periodError;

    public bool CanSave => !IsEnabled || (string.IsNullOrEmpty(ChatIdError) && string.IsNullOrEmpty(PeriodError));

    public BackupSettingsViewModel(ISettingsService settings, IBackupService backup)
    {
        _settings = settings;
        _backup = backup;
    }

    public async Task InitializeAsync()
    {
        var s = await _settings.LoadBackupSettingsAsync();
        IsEnabled = s.Enabled;
        ChatId = s.ChatIdOrUsername;
        SelectedPeriod = Periods.FirstOrDefault(p => p.Value == s.Period) ?? Periods[2];
        Validate();
    }

    partial void OnIsEnabledChanged(bool value) => Validate();
    partial void OnChatIdChanged(string value) => Validate();
    partial void OnSelectedPeriodChanged(PeriodItem? value) => Validate();

    private void Validate()
    {
        ChatIdError = null;
        PeriodError = null;

        if (IsEnabled)
        {
            if (string.IsNullOrWhiteSpace(ChatId))
                ChatIdError = "Введите chat id или @username";
            else if (!ChatIdValidator.IsValid(ChatId))
                ChatIdError = "Неверный формат. Допустимо: числовой chat_id или @username (5–32 символа).";

            if (SelectedPeriod is null)
                PeriodError = "Выберите период";
        }
        OnPropertyChanged(nameof(CanSave));
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        Validate();
        if (!CanSave)
            return;

        var s = await _settings.LoadBackupSettingsAsync();
        s.Enabled = IsEnabled;
        s.ChatIdOrUsername = ChatId.Trim();
        s.Period = (SelectedPeriod?.Value) ?? BackupPeriod.Daily;
        // LastSentLocal не трогаем — маркер сдвигается только после успешной отправки
        await _settings.SaveBackupSettingsAsync(s);

        // Запускаем/останавливаем фоновую проверку
        if (s.Enabled)
            await _backup.CheckNowAsync(force: false); // проверка на всякий случай
        // (Loop уже запущен в App при старте)

        await Application.Current.MainPage.DisplayAlert("Сохранено", s.Enabled ? "Резервное копирование включено." : "Резервное копирование выключено.", "OK");
    }
}

internal static class ChatIdValidator
{
    private static readonly System.Text.RegularExpressions.Regex _regex =
        new(@"^(@[A-Za-z0-9_]{5,32}|-?\d+)$", System.Text.RegularExpressions.RegexOptions.Compiled);

    public static bool IsValid(string input) => _regex.IsMatch(input.Trim());
}