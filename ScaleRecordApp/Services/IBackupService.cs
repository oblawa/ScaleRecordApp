using Microsoft.Extensions.Logging;
using ScaleRecordApp.Models;
using ScaleRecordApp.ViewModels;

namespace ScaleRecordApp.Services;

public interface IBackupService
{
    void Start();            // запускает фоновую проверку (в рамках работы приложения)
    void Stop();
    Task CheckNowAsync(bool force = false); // ручная проверка/отправка (например, сразу после сохранения настроек)
}

public class BackupService : IBackupService, IDisposable
{
    private readonly ISettingsService _settings;
    private readonly IHistoryRepository _historyRepo;
    private readonly ExcelService _excel;
    private readonly TelegramService _telegram;
    private readonly ILogger<BackupService>? _log;

    private CancellationTokenSource? _cts;
    private Task? _loopTask;

    public BackupService(ISettingsService settings, IHistoryRepository historyRepo, ExcelService excel, TelegramService telegram, ILogger<BackupService>? log = null)
    {
        _settings = settings;
        _historyRepo = historyRepo;
        _excel = excel;
        _telegram = telegram;
        _log = log;
    }

    public void Start()
    {
        Stop();
        _cts = new CancellationTokenSource();
        _loopTask = Task.Run(() => LoopAsync(_cts.Token));
    }

    public void Stop()
    {
        try { _cts?.Cancel(); } catch { }
        _cts = null;
        _loopTask = null;
    }

    public async Task CheckNowAsync(bool force = false)
    {
        try
        {
            await CheckAndSendAsync(force);
        }
        catch (Exception ex)
        {
            _log?.LogError(ex, "Backup CheckNow failed");
        }
    }

    private async Task LoopAsync(CancellationToken ct)
    {
        // Небольшой цикл: проверяем раз в минуту, сам чек учитывает период
        var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                await CheckAndSendAsync(force: false);
            }
        }
        catch (OperationCanceledException) { }
    }

    private async Task CheckAndSendAsync(bool force)
    {
        var settings = await _settings.LoadBackupSettingsAsync();
        if (!settings.Enabled)
            return;

        var now = DateTime.Now; // локально, как и Timestamp
        var period = settings.Period.ToTimeSpan();
        var last = settings.LastSentLocal ?? DateTime.MinValue;
        var dueAt = last + period;

        if (!force && now < dueAt)
            return; // ещё не пора

        // Берём только новые записи после последней отправки
        var from = last;
        var to = now;
        var records = await _historyRepo.GetRangeAsync(from, to);
        if (records.Count == 0)
        {
            // Нет новых записей — ничего не отправляем и не сдвигаем маркер
            return;
        }

        string filePath = string.Empty;
        try
        {
            string fileName = $"Backup_{now:yyyyMMdd_HHmmss}.xlsx";
            filePath = _excel.CreateExcelFromHistory(records, fileName);

            var fromTs = records.Min(r => r.Timestamp);
            var toTs = records.Max(r => r.Timestamp);
            string caption = $"Резервная копия ({fromTs:dd.MM.yyyy HH:mm} — {toTs:dd.MM.yyyy HH:mm})";

            await _telegram.SendDocumentAsync(settings.ChatIdOrUsername.Trim(), filePath, caption);

            // Сдвигаем маркер последней отправки на последний отправленный Timestamp
            settings.LastSentLocal = toTs;
            await _settings.SaveBackupSettingsAsync(settings);
        }
        finally
        {
            try { if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath)) File.Delete(filePath); } catch { }
        }
    }

    public void Dispose() => Stop();
}