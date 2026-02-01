using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Storage;
using ScaleRecordApp.Models;
using ScaleRecordApp.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ScaleRecordApp.ViewModels
{
    public enum TimeRangeKind
    {
        AllTime,
        LastWeek,
        Last3Days,
        Last24Hours,
        Last12Hours,
        TodayWindow,
        YesterdayWindow,
        Custom
    }

    public class RangeOption
    {
        public TimeRangeKind Kind { get; set; }
        public string Title { get; set; } = "";
    }

    public partial class HistoryVM : BaseViewModel
    {
        private const string PrefKey_TelegramRecipient = "History.TelegramRecipient";

        private readonly DatabaseService _db;
        private readonly ExcelService _excel;
        private readonly TelegramService _telegram;

        private readonly SemaphoreSlim _filterSemaphore = new(1, 1);
        private bool _suppressAutoApply;

        public ObservableCollection<HistoryRecordDto> Records { get; } = new();
        public ObservableCollection<Vehicle> Vehicles { get; } = new();
        public ObservableCollection<CargoType> CargoTypes { get; } = new();
        public ObservableCollection<RangeOption> RangeOptions { get; } = new();


        public ObservableCollection<Destination> Destinations { get; } = new();

        [ObservableProperty] private Destination? selectedFrom;
        [ObservableProperty] private Destination? selectedTo;




        [ObservableProperty] private Vehicle? selectedVehicle;
        [ObservableProperty] private CargoType? selectedCargoType;

        [ObservableProperty] private DateTime filterFromDate = DateTime.Today.AddDays(-1);
        [ObservableProperty] private TimeSpan filterFromTime = TimeSpan.Zero;
        [ObservableProperty] private DateTime filterToDate = DateTime.Today;
        [ObservableProperty] private TimeSpan filterToTime = new(23, 59, 59);

        [ObservableProperty] private RangeOption? selectedRangeOption;

        // Скрытый фильтр по сезону
        [ObservableProperty] private Season? activeSeason;

        public bool IsCustomRange => SelectedRangeOption?.Kind == TimeRangeKind.Custom;

        // сортировка
        [ObservableProperty] private string currentSortField = "Timestamp";
        partial void OnCurrentSortFieldChanged(string value) => UpdateHeaderTexts();

        [ObservableProperty] private bool sortAscending = false;
        partial void OnSortAscendingChanged(bool value) => UpdateHeaderTexts();

        // подписи заголовков
        [ObservableProperty] private string dateHeaderText = "Дата";
        [ObservableProperty] private string vehicleHeaderText = "Машина";
        [ObservableProperty] private string cargoHeaderText = "Груз";
        [ObservableProperty] private string netWeightHeaderText = "Нетто (кг)";
        [ObservableProperty] private string fromHeaderText = "Откуда";
        [ObservableProperty] private string toHeaderText = "Куда";
        [ObservableProperty] private string sourceHeaderText = "Источник";
        [ObservableProperty] private string commentHeaderText = "Комментарий";

        // Telegram
        [ObservableProperty] private string telegramRecipient = "";
        [ObservableProperty] private bool isSending;
        public bool IsNotSending => !IsSending;
        partial void OnIsSendingChanged(bool value) => OnPropertyChanged(nameof(IsNotSending));

        // Текст для кнопки в тулбаре
        public string ActiveSeasonTitle => ActiveSeason != null ? $"Сезон: {ActiveSeason.Name}" : "Сезон: все";

        // Смена активного сезона
        partial void OnActiveSeasonChanged(Season? value)
        {
            OnPropertyChanged(nameof(ActiveSeasonTitle));
            if (_suppressAutoApply) return;

            // Пересчитываем не только фильтр, но и диапазон (важно для AllTime)
            _ = ApplySelectedRangeAndFilterAsync();
        }

        // Персист адресата Telegram
        partial void OnTelegramRecipientChanged(string value)
        {
            try { Preferences.Set(PrefKey_TelegramRecipient, value ?? string.Empty); }
            catch { /* no-op */ }
        }

        public double TotalNetTons => Records.Sum(r => r.NetWeight) / 1000.0;
        public string TotalNetTonsText => $"Общая масса :   {TotalNetTons.ToString("F3", CultureInfo.InvariantCulture)} тонн";

        public HistoryVM()
        {
            _db = Application.Current.Windows[0].Page.Handler.MauiContext.Services.GetService<DatabaseService>()!;
            _excel = Application.Current.Windows[0].Page.Handler.MauiContext.Services.GetService<ExcelService>()!;
            _telegram = Application.Current.Windows[0].Page.Handler.MauiContext.Services.GetService<TelegramService>()!;

            // пресеты периодов
            RangeOptions.Add(new RangeOption { Kind = TimeRangeKind.AllTime, Title = "За весь сезон" });
            RangeOptions.Add(new RangeOption { Kind = TimeRangeKind.LastWeek, Title = "Последняя неделя" });
            RangeOptions.Add(new RangeOption { Kind = TimeRangeKind.Last3Days, Title = "Последние 3 дня" });
            RangeOptions.Add(new RangeOption { Kind = TimeRangeKind.Last24Hours, Title = "Последние 24 часа" });
            RangeOptions.Add(new RangeOption { Kind = TimeRangeKind.Last12Hours, Title = "Последние 12 часов" });
            RangeOptions.Add(new RangeOption { Kind = TimeRangeKind.TodayWindow, Title = "Сегодняшняя смена  (06:00 → 03:00)" });
            RangeOptions.Add(new RangeOption { Kind = TimeRangeKind.YesterdayWindow, Title = "Вчерашняя смена (06:00 → 03:00)" });
            RangeOptions.Add(new RangeOption { Kind = TimeRangeKind.Custom, Title = "За выбранный промежуток времени" });

            SelectedRangeOption = RangeOptions.First(ro => ro.Kind == TimeRangeKind.Last24Hours);

            // Восстановим Telegram получателя
            TelegramRecipient = Preferences.Get(PrefKey_TelegramRecipient, "");

            UpdateHeaderTexts();
        }

        private void UpdateHeaderTexts()
        {
            string arrow = SortAscending ? " ▲" : " ▼";

            DateHeaderText = CurrentSortField == "Timestamp" ? $"Дата{arrow}" : "Дата ▲▼";
            VehicleHeaderText = CurrentSortField == "Vehicle" ? $"Машина{arrow}" : "Машина ▲▼";
            CargoHeaderText = CurrentSortField == "Cargo" ? $"Груз{arrow}" : "Груз ▲▼";
            NetWeightHeaderText = CurrentSortField == "NetWeight" ? $"Нетто (кг) {arrow}" : "Нетто (кг) ▲▼";
            FromHeaderText = CurrentSortField == "From" ? $"Откуда{arrow}" : "Откуда ▲▼";
            ToHeaderText = CurrentSortField == "To" ? $"Куда{arrow}" : "Куда ▲▼";
            SourceHeaderText = CurrentSortField == "Source" ? $"Источник{arrow}" : "Источник ▲▼";
            CommentHeaderText = CurrentSortField == "Comment" ? $"Комментарий{arrow}" : "Комментарий ▲▼";
        }

        [RelayCommand]
        public async Task LoadAsync()
        {
            // Справочники
            var vehicles = await _db.GetAllAsync<Vehicle>();
            Vehicles.Clear();
            foreach (var v in vehicles) Vehicles.Add(v);

            var cargos = await _db.GetAllAsync<CargoType>();
            CargoTypes.Clear();
            foreach (var c in cargos) CargoTypes.Add(c);

            // Сезоны — берём последний по CreatedAt
            var seasons = await _db.GetAllAsync<Season>();
            var latestSeason = seasons.OrderBy(s => s.CreatedAt).LastOrDefault();

            _suppressAutoApply = true;
            try
            {
                ActiveSeason = latestSeason; // если seasons пуст — будет null
            }
            finally
            {
                _suppressAutoApply = false;
            }



            var destinations = await _db.GetAllAsync<Destination>();
            Destinations.Clear();
            foreach (var d in destinations) Destinations.Add(d);



            await ApplySelectedRangeAndFilterAsync();
        }

        // Выбор сезона через ActionSheet (без отдельного Picker в UI)
        [RelayCommand]
        private async Task PickSeasonAsync()
        {
            var seasons = (await _db.GetAllAsync<Season>())
                .OrderBy(s => s.CreatedAt)
                .ToList();

            var cancel = "Отмена";
            var all = "Все сезоны";

            var buttons = new List<string> { all };
            buttons.AddRange(seasons.Select(s => s.Name));

            var chosen = await Application.Current.MainPage.DisplayActionSheet("Выберите сезон", cancel, null, buttons.ToArray());
            if (string.IsNullOrEmpty(chosen) || chosen == cancel) return;

            Season? selected = null;
            if (chosen != all)
                selected = seasons.FirstOrDefault(s => s.Name == chosen);

            _suppressAutoApply = true;
            try
            {
                ActiveSeason = selected; // null = все сезоны
            }
            finally
            {
                _suppressAutoApply = false;
            }

            await ApplySelectedRangeAndFilterAsync();
        }

        // --- автообновление при изменении видимых фильтров ---
        partial void OnSelectedFromChanged(Destination? value)
        {
            if (_suppressAutoApply) return;
            _ = ApplyFilterAsync();
        }
        partial void OnSelectedToChanged(Destination? value)
        {
            if (_suppressAutoApply) return;
            _ = ApplyFilterAsync();
        }
        partial void OnSelectedVehicleChanged(Vehicle? value)
        {
            if (_suppressAutoApply) return;
            _ = ApplyFilterAsync();
        }

        partial void OnSelectedCargoTypeChanged(CargoType? value)
        {
            if (_suppressAutoApply) return;
            _ = ApplyFilterAsync();
        }

        partial void OnFilterFromDateChanged(DateTime value)
        {
            if (_suppressAutoApply) return;
            if (IsCustomRange) _ = ApplyFilterAsync();
        }
        partial void OnFilterToDateChanged(DateTime value)
        {
            if (_suppressAutoApply) return;
            if (IsCustomRange) _ = ApplyFilterAsync();
        }
        partial void OnFilterFromTimeChanged(TimeSpan value)
        {
            if (_suppressAutoApply) return;
            if (IsCustomRange) _ = ApplyFilterAsync();
        }
        partial void OnFilterToTimeChanged(TimeSpan value)
        {
            if (_suppressAutoApply) return;
            if (IsCustomRange) _ = ApplyFilterAsync();
        }

        partial void OnSelectedRangeOptionChanged(RangeOption? value)
        {
            if (_suppressAutoApply)
            {
                OnPropertyChanged(nameof(IsCustomRange));
                return;
            }

            _ = ApplySelectedRangeAndFilterAsync();
            OnPropertyChanged(nameof(IsCustomRange));
        }

        public async Task ApplySelectedRangeAndFilterAsync()
        {
            // Когда меняется пресет/сезон — выставляем From/To и затем применяем фильтр.
            if (SelectedRangeOption == null)
            {
                await ApplyFilterAsync();
                return;
            }

            var now = DateTime.Now;
            DateTime from, to;

            switch (SelectedRangeOption.Kind)
            {
                case TimeRangeKind.AllTime:
                    {
                        var all = await _db.GetAllAsync<WeighingRecord>();
                        var scoped = ActiveSeason != null
                            ? all.Where(r => r.SeasonId == ActiveSeason.Id)
                            : all;

                        if (scoped.Any())
                        {
                            from = scoped.Min(r => r.Timestamp).Date;
                            to = scoped.Max(r => r.Timestamp);
                        }
                        else
                        {
                            from = DateTime.Today.AddYears(-1);
                            to = DateTime.Today.AddDays(1).AddSeconds(-1);
                        }
                        break;
                    }
                case TimeRangeKind.LastWeek:
                    from = now.AddDays(-7); to = now; break;
                case TimeRangeKind.Last3Days:
                    from = now.AddDays(-3); to = now; break;
                case TimeRangeKind.Last24Hours:
                    from = now.AddHours(-24); to = now; break;
                case TimeRangeKind.Last12Hours:
                    from = now.AddHours(-12); to = now; break;
                case TimeRangeKind.TodayWindow:
                    {
                        var baseDay = DateTime.Today;

                        // если текущее время меньше 6 утра — значит, рабочий "сегодня" ещё не наступил
                        if (now.Hour < 6)
                            baseDay = baseDay.AddDays(-1);

                        from = baseDay.AddHours(6);            // начало в 6:00
                        to = baseDay.AddDays(1).AddHours(3);   // конец в 03:00 следующего дня
                        break;
                    }
                case TimeRangeKind.YesterdayWindow:
                    {
                        var baseDay = DateTime.Today.AddDays(-1);

                        // если сейчас до 6 утра — смещаем "вчера" ещё на день назад
                        if (now.Hour < 6)
                            baseDay = baseDay.AddDays(-1);

                        from = baseDay.AddHours(6);
                        to = baseDay.AddDays(1).AddHours(3);
                        break;
                    }
                case TimeRangeKind.Custom:
                default:
                    await ApplyFilterAsync();
                    return;
            }

            _suppressAutoApply = true;
            try
            {
                FilterFromDate = from.Date;
                FilterFromTime = from.TimeOfDay;
                FilterToDate = to.Date;
                FilterToTime = to.TimeOfDay;
            }
            finally
            {
                _suppressAutoApply = false;
            }

            await ApplyFilterAsync();
        }

        [RelayCommand]
        private async Task ApplyFilterAsync()
        {
            await _filterSemaphore.WaitAsync();
            try
            {
                Records.Clear();

                var from = FilterFromDate.Date + FilterFromTime;
                var to = FilterToDate.Date + FilterToTime;

                var all = await _db.GetAllAsync<WeighingRecord>();

                var filtered = all
                    .Where(r => r.Timestamp >= from && r.Timestamp <= to)
                    .Where(r => ActiveSeason == null || r.SeasonId == ActiveSeason.Id)
                    .Where(r => SelectedVehicle == null || r.VehicleId == SelectedVehicle.Id)
                    .Where(r => SelectedCargoType == null || r.CargoTypeId == SelectedCargoType.Id)
                    .Where(r => SelectedFrom == null || r.FromId == SelectedFrom.Id)
                    .Where(r => SelectedTo == null || r.ToId == SelectedTo.Id);


                // Справочники
                var vehicles = await _db.GetAllAsync<Vehicle>();
                var cargos = await _db.GetAllAsync<CargoType>();
                var destinations = await _db.GetAllAsync<Destination>();
                var sources = await _db.GetAllAsync<Source>();

                var list = filtered.Select(r =>
                {
                    var v = vehicles.FirstOrDefault(x => x.Id == r.VehicleId);
                    var c = cargos.FirstOrDefault(x => x.Id == r.CargoTypeId);
                    var fromDest = destinations.FirstOrDefault(x => x.Id == r.FromId);
                    var toDest = destinations.FirstOrDefault(x => x.Id == r.ToId);
                    var s = r.SourceId.HasValue ? sources.FirstOrDefault(x => x.Id == r.SourceId.Value) : null;

                    string cargoDisplay = c != null ? c.DisplayName : "";
                    //if (!string.IsNullOrEmpty(c?.Kind) || !string.IsNullOrEmpty(c?.Variety))
                    //    cargoDisplay = $"{c?.Name} | {c?.Kind} | {c?.Variety}";

                    return new HistoryRecordDto
                    {
                        Id = r.Id,
                        Timestamp = r.Timestamp,
                        VehicleName = v?.Name ?? "",
                        CargoDisplay = cargoDisplay,
                        NetWeight = (int)r.NetWeight,
                        FromName = fromDest?.Name ?? "",
                        ToName = toDest?.Name ?? "",
                        SourceName = s?.Name ?? "",
                        Comment = r.Comment ?? ""
                    };
                });

                list = SortAscending
                    ? list.OrderBy(x => x.GetField(CurrentSortField))
                    : list.OrderByDescending(x => x.GetField(CurrentSortField));

                foreach (var item in list)
                    Records.Add(item);

                OnPropertyChanged(nameof(TotalNetTons));
                OnPropertyChanged(nameof(TotalNetTonsText));

            }
            finally
            {
                _filterSemaphore.Release();
            }
        }

        [RelayCommand]
        private async Task SortAsync(string field)
        {
            if (CurrentSortField == field)
                SortAscending = !SortAscending;
            else
            {
                CurrentSortField = field;
                SortAscending = true;
            }

            await ApplyFilterAsync();
        }

        // Сбросы
        [RelayCommand]
        private Task ResetDateFilterAsync()
        {
            Debug.WriteLine("RESEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEET");
            SelectedRangeOption = RangeOptions.First(ro => ro.Kind == TimeRangeKind.Last24Hours);
            return Task.CompletedTask;
        }

        [RelayCommand]
        private Task ResetVehicleFilterAsync()
        {
            SelectedVehicle = null;
            return Task.CompletedTask;
        }

        [RelayCommand]
        private Task ResetCargoFilterAsync()
        {
            SelectedCargoType = null;
            return Task.CompletedTask;
        }

        [RelayCommand]
        private async Task ResetAllFiltersAsync()
        {
            _suppressAutoApply = true;
            try
            {
                SelectedVehicle = null;
                SelectedCargoType = null;
                SelectedRangeOption = RangeOptions.First(ro => ro.Kind == TimeRangeKind.Last24Hours);
                SelectedFrom = null;
                SelectedTo = null;
                // ActiveSeason не трогаем — остаётся выбранный/последний
            }
            finally
            {
                _suppressAutoApply = false;
            }

            await ApplySelectedRangeAndFilterAsync();
        }

        [RelayCommand]
        private Task ResetFromFilterAsync()
        {
            SelectedFrom = null;
            return Task.CompletedTask;
        }

        [RelayCommand]
        private Task ResetToFilterAsync()
        {
            SelectedTo = null;
            return Task.CompletedTask;
        }



        // Показ подробностей записи
        [RelayCommand]
        private async Task ShowDetailsAsync(HistoryRecordDto dto)
        {
            string msg =
                $"Дата/время: {dto.Timestamp:dd.MM.yyyy HH:mm}\n" +
                $"Машина: {dto.VehicleName}\n" +
                $"Груз: {dto.CargoDisplay}\n" +
                $"Нетто (кг): {dto.NetWeight:F0}\n" +
                $"Откуда: {dto.FromName}\n" +
                $"Куда: {dto.ToName}\n" +
                $"Источник: {dto.SourceName}\n" +
                $"Комментарий: {dto.Comment}";

            // accept = Удалить, cancel = Закрыть
            bool delete = await Application.Current.MainPage.DisplayAlert(
                "Подробности", msg, "Удалить", "Закрыть");

            if (delete)
                await DeleteRecordAsync(dto);
        }
        [RelayCommand]
        private async Task DeleteRecordAsync(HistoryRecordDto dto)
        {
            try
            {
                // Находим реальную запись в БД
                var all = await _db.GetAllAsync<WeighingRecord>();
                var entity = all.FirstOrDefault(r => r.Id == dto.Id);

                if (entity == null)
                {
                    await Application.Current.MainPage.DisplayAlert(
                        "Не найдено", "Запись уже отсутствует в базе.", "OK");
                    // На всякий случай уберём её и из UI
                    Records.Remove(dto);
                    OnPropertyChanged(nameof(TotalNetTons));
                    OnPropertyChanged(nameof(TotalNetTonsText));
                    return;
                }

                // ВАЖНО: сюда передаём сам объект, а не Guid
                await _db.DeleteAsync(entity);

                // Синхронизируем UI
                Records.Remove(dto);
                OnPropertyChanged(nameof(TotalNetTons));
                OnPropertyChanged(nameof(TotalNetTonsText));
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert(
                    "Ошибка удаления", ex.Message, "OK");
            }
        }

        //[RelayCommand]
        //private async Task ShowDetailsAsync(HistoryRecordDto dto)
        //{
        //    string msg =
        //        $"Дата/время: {dto.Timestamp:dd.MM.yyyy HH:mm}\n" +
        //        $"Машина: {dto.VehicleName}\n" +
        //        $"Груз: {dto.CargoDisplay}\n" +
        //        $"Нетто (кг): {dto.NetWeight:F0}\n" +
        //        $"Откуда: {dto.FromName}\n" +
        //        $"Куда: {dto.ToName}\n" +
        //        $"Источник: {dto.SourceName}\n" +
        //        $"Комментарий: {dto.Comment}";

        //    await Application.Current.MainPage.DisplayAlert("Подробности", msg, "OK");
        //}

        // Отправка отчёта
        [RelayCommand]
        private async Task SendFilteredReportAsync()
        {
            if (string.IsNullOrWhiteSpace(TelegramRecipient))
            {
                await Application.Current.MainPage.DisplayAlert("Ошибка", "Введите Telegram username или chat id.", "OK");
                return;
            }

            if (!Records.Any())
            {
                await Application.Current.MainPage.DisplayAlert("Инфо", "Нет записей для отправки.", "OK");
                return;
            }

            IsSending = true;
            string filePath = "";
            try
            {
                string fileName = $"Report_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                filePath = _excel.CreateExcelFromHistory(Records, fileName);

                var fromTs = Records.Min(r => r.Timestamp);
                var toTs = Records.Max(r => r.Timestamp);
                string caption = $"Отчёт ({fromTs:dd.MM.yyyy HH:mm} — {toTs:dd.MM.yyyy HH:mm})";

                await _telegram.SendDocumentAsync(TelegramRecipient.Trim(), filePath, caption);

                await Application.Current.MainPage.DisplayAlert("Готово", "Отчёт отправлен.", "OK");
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Ошибка при отправке", ex.Message, "OK");
            }
            finally
            {
                IsSending = false;
                try { if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath)) File.Delete(filePath); } catch { }
            }
        }

    }

    public class HistoryRecordDto
    {
        public Guid Id { get; set; }
        public DateTime Timestamp { get; set; }
        public string VehicleName { get; set; } = "";
        public string CargoDisplay { get; set; } = "";
        public float NetWeight { get; set; }
        public string FromName { get; set; } = "";
        public string ToName { get; set; } = "";
        public string SourceName { get; set; } = "";
        public string Comment { get; set; } = "";

        public object? GetField(string field) => field switch
        {
            "Timestamp" => Timestamp,
            "Vehicle" => VehicleName,
            "Cargo" => CargoDisplay,
            "NetWeight" => NetWeight,
            "From" => FromName,
            "To" => ToName,
            "Source" => SourceName,
            "Comment" => Comment,
            _ => null
        };
    }
}
