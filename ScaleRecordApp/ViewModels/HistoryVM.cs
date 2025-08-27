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

















//// ViewModels/HistoryVM.cs
//using CommunityToolkit.Mvvm.ComponentModel;
//using CommunityToolkit.Mvvm.Input;
//using ScaleRecordApp.Models;
//using ScaleRecordApp.Services;
//using System.Collections.ObjectModel;
//using System.Linq;
//using Microsoft.Maui.Storage;
//using System.Threading;
//using System.Threading.Tasks;

//namespace ScaleRecordApp.ViewModels
//{
//    public enum TimeRangeKind
//    {
//        AllTime,
//        LastWeek,
//        Last3Days,
//        Last24Hours,
//        Last12Hours,
//        TodayWindow,       // сегодня 06:00 -> завтра 03:00
//        YesterdayWindow,   // вчера 06:00 -> сегодня 03:00
//        Custom
//    }

//    public class RangeOption
//    {
//        public TimeRangeKind Kind { get; set; }
//        public string Title { get; set; } = "";
//    }

//    public partial class HistoryVM : BaseViewModel
//    {
//        private const string PrefKey_TelegramRecipient = "History.TelegramRecipient";

//        private readonly DatabaseService _db;
//        private readonly ExcelService _excel;
//        private readonly TelegramService _telegram;

//        private readonly SemaphoreSlim _filterSemaphore = new(1, 1);
//        private bool _suppressAutoApply;

//        public ObservableCollection<HistoryRecordDto> Records { get; } = new();
//        public ObservableCollection<Vehicle> Vehicles { get; } = new();
//        public ObservableCollection<CargoType> CargoTypes { get; } = new();
//        public ObservableCollection<RangeOption> RangeOptions { get; } = new();

//        [ObservableProperty] private Vehicle? selectedVehicle;
//        [ObservableProperty] private CargoType? selectedCargoType;

//        [ObservableProperty] private DateTime filterFromDate = DateTime.Today.AddDays(-1);
//        [ObservableProperty] private TimeSpan filterFromTime = TimeSpan.Zero;
//        [ObservableProperty] private DateTime filterToDate = DateTime.Today;
//        [ObservableProperty] private TimeSpan filterToTime = new(23, 59, 59);

//        [ObservableProperty] private RangeOption? selectedRangeOption;

//        // Активный сезон (скрытый фильтр)
//        [ObservableProperty] private Season? activeSeason;

//        public bool IsCustomRange => SelectedRangeOption?.Kind == TimeRangeKind.Custom;

//        // сортировка
//        [ObservableProperty] private string currentSortField = "Timestamp";
//        partial void OnCurrentSortFieldChanged(string value) => UpdateHeaderTexts();

//        [ObservableProperty] private bool sortAscending = false;
//        partial void OnSortAscendingChanged(bool value) => UpdateHeaderTexts();

//        // подписи заголовков
//        [ObservableProperty] private string dateHeaderText = "Дата";
//        [ObservableProperty] private string vehicleHeaderText = "Машина";
//        [ObservableProperty] private string cargoHeaderText = "Груз";
//        [ObservableProperty] private string netWeightHeaderText = "Нетто (кг)";
//        [ObservableProperty] private string fromHeaderText = "Откуда";
//        [ObservableProperty] private string toHeaderText = "Куда";
//        [ObservableProperty] private string sourceHeaderText = "Источник";
//        [ObservableProperty] private string commentHeaderText = "Комментарий";

//        // Telegram
//        [ObservableProperty] private string telegramRecipient = "";
//        partial void OnIsSendingChanged(bool value) => OnPropertyChanged(nameof(IsNotSending));
//        public bool IsNotSending => !IsSending;

//        [ObservableProperty] private bool isSending;

//        // --- реакция на изменения скрытого сезона ---
//        partial void OnActiveSeasonChanged(Season? value)
//        {
//            if (_suppressAutoApply) return;
//            _ = ApplyFilterAsync();
//        }

//        // --- персист TelegramRecipient в Preferences ---
//        partial void OnTelegramRecipientChanged(string value)
//        {
//            try { Preferences.Set(PrefKey_TelegramRecipient, value ?? string.Empty); }
//            catch { /* игнор: в крайнем случае просто не сохраним */ }
//        }

//        public double TotalNetTons => Records.Sum(r => r.NetWeight) / 1000.0;

//        public HistoryVM()
//        {
//            _db = Application.Current.Windows[0].Page.Handler.MauiContext.Services.GetService<DatabaseService>()!;
//            _excel = Application.Current.Windows[0].Page.Handler.MauiContext.Services.GetService<ExcelService>()!;
//            _telegram = Application.Current.Windows[0].Page.Handler.MauiContext.Services.GetService<TelegramService>()!;

//            // пресеты периодов
//            RangeOptions.Add(new RangeOption { Kind = TimeRangeKind.AllTime, Title = "За всё время" });
//            RangeOptions.Add(new RangeOption { Kind = TimeRangeKind.LastWeek, Title = "За последнюю неделю" });
//            RangeOptions.Add(new RangeOption { Kind = TimeRangeKind.Last3Days, Title = "За последние 3 дня" });
//            RangeOptions.Add(new RangeOption { Kind = TimeRangeKind.Last24Hours, Title = "За последние 24 часа" });
//            RangeOptions.Add(new RangeOption { Kind = TimeRangeKind.Last12Hours, Title = "За последние 12 часов" });
//            RangeOptions.Add(new RangeOption { Kind = TimeRangeKind.TodayWindow, Title = "За сегодня (06:00 → 03:00)" });
//            RangeOptions.Add(new RangeOption { Kind = TimeRangeKind.YesterdayWindow, Title = "За вчерашний день (06:00 → 03:00)" });
//            RangeOptions.Add(new RangeOption { Kind = TimeRangeKind.Custom, Title = "За выбранный промежуток времени" });

//            SelectedRangeOption = RangeOptions.First(ro => ro.Kind == TimeRangeKind.Last24Hours); // дефолт по времени

//            // восстановим Telegram адресата
//            TelegramRecipient = Preferences.Get(PrefKey_TelegramRecipient, "");

//            UpdateHeaderTexts();
//        }

//        private void UpdateHeaderTexts()
//        {
//            string arrow = SortAscending ? " ▲" : " ▼";

//            DateHeaderText = CurrentSortField == "Timestamp" ? $"Дата{arrow}" : "Дата ▲▼";
//            VehicleHeaderText = CurrentSortField == "Vehicle" ? $"Машина{arrow}" : "Машина ▲▼";
//            CargoHeaderText = CurrentSortField == "Cargo" ? $"Груз{arrow}" : "Груз ▲▼";
//            NetWeightHeaderText = CurrentSortField == "NetWeight" ? $"Нетто (кг) {arrow}" : "Нетто (кг) ▲▼";
//            FromHeaderText = CurrentSortField == "From" ? $"Откуда{arrow}" : "Откуда ▲▼";
//            ToHeaderText = CurrentSortField == "To" ? $"Куда{arrow}" : "Куда ▲▼";
//            SourceHeaderText = CurrentSortField == "Source" ? $"Источник{arrow}" : "Источник ▲▼";
//            CommentHeaderText = CurrentSortField == "Comment" ? $"Комментарий{arrow}" : "Комментарий ▲▼";
//        }

//        [RelayCommand]
//        public async Task LoadAsync()
//        {
//            // Справочники
//            var vehicles = await _db.GetAllAsync<Vehicle>();
//            Vehicles.Clear();
//            foreach (var v in vehicles) Vehicles.Add(v);

//            var cargos = await _db.GetAllAsync<CargoType>();
//            CargoTypes.Clear();
//            foreach (var c in cargos) CargoTypes.Add(c);

//            // Сезоны (скрыто): выбираем последний по CreatedAt
//            var seasons = await _db.GetAllAsync<Season>();
//            var latestSeason = seasons
//                .OrderBy(s => s.CreatedAt)
//                .LastOrDefault();

//            _suppressAutoApply = true;
//            try
//            {
//                ActiveSeason = latestSeason; // если null — фильтр по сезону не применяется
//            }
//            finally
//            {
//                _suppressAutoApply = false;
//            }

//            await ApplySelectedRangeAndFilterAsync();
//        }

//        // --- автообновление при изменении видимых фильтров ---
//        partial void OnSelectedVehicleChanged(Vehicle? value)
//        {
//            if (_suppressAutoApply) return;
//            _ = ApplyFilterAsync();
//        }

//        partial void OnSelectedCargoTypeChanged(CargoType? value)
//        {
//            if (_suppressAutoApply) return;
//            _ = ApplyFilterAsync();
//        }

//        partial void OnFilterFromDateChanged(DateTime value)
//        {
//            if (_suppressAutoApply) return;
//            if (IsCustomRange) _ = ApplyFilterAsync();
//        }
//        partial void OnFilterToDateChanged(DateTime value)
//        {
//            if (_suppressAutoApply) return;
//            if (IsCustomRange) _ = ApplyFilterAsync();
//        }
//        partial void OnFilterFromTimeChanged(TimeSpan value)
//        {
//            if (_suppressAutoApply) return;
//            if (IsCustomRange) _ = ApplyFilterAsync();
//        }
//        partial void OnFilterToTimeChanged(TimeSpan value)
//        {
//            if (_suppressAutoApply) return;
//            if (IsCustomRange) _ = ApplyFilterAsync();
//        }

//        partial void OnSelectedRangeOptionChanged(RangeOption? value)
//        {
//            if (_suppressAutoApply)
//            {
//                OnPropertyChanged(nameof(IsCustomRange));
//                return;
//            }

//            _ = ApplySelectedRangeAndFilterAsync();
//            OnPropertyChanged(nameof(IsCustomRange));
//        }

//        public async Task ApplySelectedRangeAndFilterAsync()
//        {
//            if (SelectedRangeOption == null)
//            {
//                await ApplyFilterAsync();
//                return;
//            }

//            var now = DateTime.Now;
//            DateTime from, to;

//            switch (SelectedRangeOption.Kind)
//            {
//                case TimeRangeKind.AllTime:
//                    {
//                        var all = await _db.GetAllAsync<WeighingRecord>();
//                        var scoped = ActiveSeason != null
//                            ? all.Where(r => r.SeasonId == ActiveSeason.Id)
//                            : all;

//                        if (scoped.Any())
//                        {
//                            from = scoped.Min(r => r.Timestamp).Date;
//                            to = scoped.Max(r => r.Timestamp);
//                        }
//                        else
//                        {
//                            from = DateTime.Today.AddYears(-1);
//                            to = DateTime.Today.AddDays(1).AddSeconds(-1);
//                        }
//                        break;
//                    }
//                case TimeRangeKind.LastWeek:
//                    from = now.AddDays(-7); to = now; break;
//                case TimeRangeKind.Last3Days:
//                    from = now.AddDays(-3); to = now; break;
//                case TimeRangeKind.Last24Hours:
//                    from = now.AddHours(-24); to = now; break;
//                case TimeRangeKind.Last12Hours:
//                    from = now.AddHours(-12); to = now; break;
//                case TimeRangeKind.TodayWindow:
//                    {
//                        var baseDay = DateTime.Today;
//                        from = baseDay.AddHours(6);
//                        to = baseDay.AddDays(1).AddHours(3);
//                        break;
//                    }
//                case TimeRangeKind.YesterdayWindow:
//                    {
//                        var baseDay = DateTime.Today.AddDays(-1);
//                        from = baseDay.AddHours(6);
//                        to = baseDay.AddDays(1).AddHours(3);
//                        break;
//                    }
//                case TimeRangeKind.Custom:
//                default:
//                    await ApplyFilterAsync();
//                    return;
//            }

//            _suppressAutoApply = true;
//            try
//            {
//                FilterFromDate = from.Date;
//                FilterFromTime = from.TimeOfDay;
//                FilterToDate = to.Date;
//                FilterToTime = to.TimeOfDay;
//            }
//            finally
//            {
//                _suppressAutoApply = false;
//            }

//            await ApplyFilterAsync();
//        }

//        [RelayCommand]
//        private async Task ApplyFilterAsync()
//        {
//            await _filterSemaphore.WaitAsync();
//            try
//            {
//                Records.Clear();

//                var from = FilterFromDate.Date + FilterFromTime;
//                var to = FilterToDate.Date + FilterToTime;

//                var all = await _db.GetAllAsync<WeighingRecord>();

//                var filtered = all
//                    .Where(r => r.Timestamp >= from && r.Timestamp <= to)
//                    .Where(r => ActiveSeason == null || r.SeasonId == ActiveSeason.Id)    // <<< фильтр по сезону
//                    .Where(r => SelectedVehicle == null || r.VehicleId == SelectedVehicle.Id)
//                    .Where(r => SelectedCargoType == null || r.CargoTypeId == SelectedCargoType.Id);

//                // Справочники
//                var vehicles = await _db.GetAllAsync<Vehicle>();
//                var cargos = await _db.GetAllAsync<CargoType>();
//                var destinations = await _db.GetAllAsync<Destination>();
//                var sources = await _db.GetAllAsync<Source>();

//                var list = filtered.Select(r =>
//                {
//                    var v = vehicles.FirstOrDefault(x => x.Id == r.VehicleId);
//                    var c = cargos.FirstOrDefault(x => x.Id == r.CargoTypeId);
//                    var fromDest = destinations.FirstOrDefault(x => x.Id == r.FromId);
//                    var toDest = destinations.FirstOrDefault(x => x.Id == r.ToId);
//                    var s = r.SourceId.HasValue ? sources.FirstOrDefault(x => x.Id == r.SourceId.Value) : null;

//                    string cargoDisplay = c?.Name ?? "";
//                    if (!string.IsNullOrEmpty(c?.Kind) || !string.IsNullOrEmpty(c?.Variety))
//                        cargoDisplay = $"{c?.Name} | {c?.Kind} | {c?.Variety}";

//                    return new HistoryRecordDto
//                    {
//                        Id = r.Id,
//                        Timestamp = r.Timestamp,
//                        VehicleName = v?.Name ?? "",
//                        CargoDisplay = cargoDisplay,
//                        NetWeight = (int)r.NetWeight,
//                        FromName = fromDest?.Name ?? "",
//                        ToName = toDest?.Name ?? "",
//                        SourceName = s?.Name ?? "",
//                        Comment = r.Comment ?? ""
//                    };
//                });

//                list = SortAscending
//                    ? list.OrderBy(x => x.GetField(CurrentSortField))
//                    : list.OrderByDescending(x => x.GetField(CurrentSortField));

//                foreach (var item in list)
//                    Records.Add(item);

//                OnPropertyChanged(nameof(TotalNetTons));
//            }
//            finally
//            {
//                _filterSemaphore.Release();
//            }
//        }

//        [RelayCommand]
//        private async Task SortAsync(string field)
//        {
//            if (CurrentSortField == field)
//                SortAscending = !SortAscending;
//            else
//            {
//                CurrentSortField = field;
//                SortAscending = true;
//            }

//            await ApplyFilterAsync();
//        }

//        // Сбросы
//        [RelayCommand]
//        private Task ResetDateFilterAsync()
//        {
//            SelectedRangeOption = RangeOptions.First(ro => ro.Kind == TimeRangeKind.Last24Hours);
//            return Task.CompletedTask;
//        }

//        [RelayCommand]
//        private Task ResetVehicleFilterAsync()
//        {
//            SelectedVehicle = null;
//            return Task.CompletedTask;
//        }

//        [RelayCommand]
//        private Task ResetCargoFilterAsync()
//        {
//            SelectedCargoType = null;
//            return Task.CompletedTask;
//        }

//        [RelayCommand]
//        private async Task ResetAllFiltersAsync()
//        {
//            _suppressAutoApply = true;
//            try
//            {
//                SelectedVehicle = null;
//                SelectedCargoType = null;
//                SelectedRangeOption = RangeOptions.First(ro => ro.Kind == TimeRangeKind.Last24Hours);
//                // ActiveSeason НЕ трогаем — остаётся последний сезон
//            }
//            finally
//            {
//                _suppressAutoApply = false;
//            }

//            await ApplySelectedRangeAndFilterAsync();
//        }

//        // Показ подробностей записи
//        [RelayCommand]
//        private async Task ShowDetailsAsync(HistoryRecordDto dto)
//        {
//            string msg =
//                $"Дата/время: {dto.Timestamp:dd.MM.yyyy HH:mm}\n" +
//                $"Машина: {dto.VehicleName}\n" +
//                $"Груз: {dto.CargoDisplay}\n" +
//                $"Нетто (кг): {dto.NetWeight:F0}\n" +
//                $"Откуда: {dto.FromName}\n" +
//                $"Куда: {dto.ToName}\n" +
//                $"Источник: {dto.SourceName}\n" +
//                $"Комментарий: {dto.Comment}";

//            await Application.Current.MainPage.DisplayAlert("Подробности", msg, "OK");
//        }

//        // Отправка отчёта
//        [RelayCommand]
//        private async Task SendFilteredReportAsync()
//        {
//            if (string.IsNullOrWhiteSpace(TelegramRecipient))
//            {
//                await Application.Current.MainPage.DisplayAlert("Ошибка", "Введите Telegram username или chat id.", "OK");
//                return;
//            }

//            if (!Records.Any())
//            {
//                await Application.Current.MainPage.DisplayAlert("Инфо", "Нет записей для отправки.", "OK");
//                return;
//            }

//            IsSending = true;
//            string filePath = "";
//            try
//            {
//                string fileName = $"Report_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
//                filePath = _excel.CreateExcelFromHistory(Records, fileName);

//                var fromTs = Records.Min(r => r.Timestamp);
//                var toTs = Records.Max(r => r.Timestamp);
//                string caption = $"Отчёт ({fromTs:dd.MM.yyyy HH:mm} — {toTs:dd.MM.yyyy HH:mm})";

//                await _telegram.SendDocumentAsync(TelegramRecipient.Trim(), filePath, caption);

//                await Application.Current.MainPage.DisplayAlert("Готово", "Отчёт отправлен.", "OK");
//            }
//            catch (Exception ex)
//            {
//                await Application.Current.MainPage.DisplayAlert("Ошибка при отправке", ex.Message, "OK");
//            }
//            finally
//            {
//                IsSending = false;
//                try { if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath)) File.Delete(filePath); } catch { }
//            }
//        }
//    }

//    // DTO без изменений
//    public class HistoryRecordDto
//    {
//        public Guid Id { get; set; }
//        public DateTime Timestamp { get; set; }
//        public string VehicleName { get; set; } = "";
//        public string CargoDisplay { get; set; } = "";
//        public int NetWeight { get; set; }
//        public string FromName { get; set; } = "";
//        public string ToName { get; set; } = "";
//        public string SourceName { get; set; } = "";
//        public string Comment { get; set; } = "";

//        public object? GetField(string field) => field switch
//        {
//            "Timestamp" => Timestamp,
//            "Vehicle" => VehicleName,
//            "Cargo" => CargoDisplay,
//            "NetWeight" => NetWeight,
//            "From" => FromName,
//            "To" => ToName,
//            "Source" => SourceName,
//            "Comment" => Comment,
//            _ => null
//        };
//    }
//}






























//using CommunityToolkit.Mvvm.ComponentModel;
//using CommunityToolkit.Mvvm.Input;
//using ScaleRecordApp.Models;
//using ScaleRecordApp.Services;
//using System.Collections.ObjectModel;
//using System.Threading; // ← добавлено

//namespace ScaleRecordApp.ViewModels
//{
//    public enum TimeRangeKind
//    {
//        AllTime,
//        LastWeek,
//        Last3Days,
//        Last24Hours,
//        Last12Hours,
//        TodayWindow,       // сегодня 06:00 -> завтра 03:00
//        YesterdayWindow,   // вчера 06:00 -> сегодня 03:00
//        Custom
//    }

//    public class RangeOption
//    {
//        public TimeRangeKind Kind { get; set; }
//        public string Title { get; set; } = "";
//    }

//    public partial class HistoryVM : BaseViewModel
//    {
//        private readonly DatabaseService _db;
//        private readonly ExcelService _excel;
//        private readonly TelegramService _telegram;

//        // сериализация фильтрации, чтобы не было гонок
//        private readonly SemaphoreSlim _filterSemaphore = new(1, 1);

//        // флаг для пакетных изменений (чтобы не триггерить автоприменение на каждый сет)
//        private bool _suppressAutoApply;

//        public ObservableCollection<HistoryRecordDto> Records { get; } = new();
//        public ObservableCollection<Vehicle> Vehicles { get; } = new();
//        public ObservableCollection<CargoType> CargoTypes { get; } = new();

//        public ObservableCollection<RangeOption> RangeOptions { get; } = new();

//        [ObservableProperty] private Vehicle? selectedVehicle;
//        [ObservableProperty] private CargoType? selectedCargoType;

//        [ObservableProperty] private DateTime filterFromDate = DateTime.Today.AddDays(-1);
//        [ObservableProperty] private TimeSpan filterFromTime = TimeSpan.Zero;
//        [ObservableProperty] private DateTime filterToDate = DateTime.Today;
//        [ObservableProperty] private TimeSpan filterToTime = new(23, 59, 59);

//        [ObservableProperty] private RangeOption? selectedRangeOption;

//        public bool IsCustomRange => SelectedRangeOption?.Kind == TimeRangeKind.Custom;

//        // сортировка
//        [ObservableProperty] private string currentSortField = "Timestamp";
//        partial void OnCurrentSortFieldChanged(string value) => UpdateHeaderTexts();

//        [ObservableProperty] private bool sortAscending = false;
//        partial void OnSortAscendingChanged(bool value) => UpdateHeaderTexts();

//        // подписи заголовков
//        [ObservableProperty] private string dateHeaderText = "Дата";
//        [ObservableProperty] private string vehicleHeaderText = "Машина";
//        [ObservableProperty] private string cargoHeaderText = "Груз";
//        [ObservableProperty] private string netWeightHeaderText = "Нетто (кг)";
//        [ObservableProperty] private string fromHeaderText = "Откуда";
//        [ObservableProperty] private string toHeaderText = "Куда";
//        [ObservableProperty] private string sourceHeaderText = "Источник";
//        [ObservableProperty] private string commentHeaderText = "Комментарий";

//        // Telegram
//        [ObservableProperty] private string telegramRecipient = "";
//        [ObservableProperty] private bool isSending;
//        partial void OnIsSendingChanged(bool value) => OnPropertyChanged(nameof(IsNotSending));
//        public bool IsNotSending => !IsSending;

//        public double TotalNetTons => Records.Sum(r => r.NetWeight) / 1000.0;

//        public HistoryVM()
//        {
//            _db = Application.Current.Windows[0].Page.Handler.MauiContext.Services.GetService<DatabaseService>()!;
//            _excel = Application.Current.Windows[0].Page.Handler.MauiContext.Services.GetService<ExcelService>()!;
//            _telegram = Application.Current.Windows[0].Page.Handler.MauiContext.Services.GetService<TelegramService>()!;

//            // пресеты периодов
//            RangeOptions.Add(new RangeOption { Kind = TimeRangeKind.AllTime, Title = "За всё время" });
//            RangeOptions.Add(new RangeOption { Kind = TimeRangeKind.LastWeek, Title = "За последнюю неделю" });
//            RangeOptions.Add(new RangeOption { Kind = TimeRangeKind.Last3Days, Title = "За последние 3 дня" });
//            RangeOptions.Add(new RangeOption { Kind = TimeRangeKind.Last24Hours, Title = "За последние 24 часа" });
//            RangeOptions.Add(new RangeOption { Kind = TimeRangeKind.Last12Hours, Title = "За последние 12 часов" });
//            RangeOptions.Add(new RangeOption { Kind = TimeRangeKind.TodayWindow, Title = "За сегодня (06:00 → 03:00)" });
//            RangeOptions.Add(new RangeOption { Kind = TimeRangeKind.YesterdayWindow, Title = "За вчерашний день (06:00 → 03:00)" });
//            RangeOptions.Add(new RangeOption { Kind = TimeRangeKind.Custom, Title = "За выбранный промежуток времени" });

//            SelectedRangeOption = RangeOptions.First(ro => ro.Kind == TimeRangeKind.Last24Hours); // дефолт
//            UpdateHeaderTexts();
//        }

//        private void UpdateHeaderTexts()
//        {
//            string arrow = SortAscending ? " ▲" : " ▼";

//            DateHeaderText = CurrentSortField == "Timestamp" ? $"Дата{arrow}" : "Дата ▲▼";
//            VehicleHeaderText = CurrentSortField == "Vehicle" ? $"Машина{arrow}" : "Машина ▲▼";
//            CargoHeaderText = CurrentSortField == "Cargo" ? $"Груз{arrow}" : "Груз ▲▼";
//            NetWeightHeaderText = CurrentSortField == "NetWeight" ? $"Нетто (кг) {arrow}" : "Нетто (кг) ▲▼";
//            FromHeaderText = CurrentSortField == "From" ? $"Откуда{arrow}" : "Откуда ▲▼";
//            ToHeaderText = CurrentSortField == "To" ? $"Куда{arrow}" : "Куда ▲▼";
//            SourceHeaderText = CurrentSortField == "Source" ? $"Источник{arrow}" : "Источник ▲▼";
//            CommentHeaderText = CurrentSortField == "Comment" ? $"Комментарий{arrow}" : "Комментарий ▲▼";
//        }

//        [RelayCommand]
//        public async Task LoadAsync()
//        {
//            var vehicles = await _db.GetAllAsync<Vehicle>();
//            Vehicles.Clear();
//            foreach (var v in vehicles) Vehicles.Add(v);

//            var cargos = await _db.GetAllAsync<CargoType>();
//            CargoTypes.Clear();
//            foreach (var c in cargos) CargoTypes.Add(c);

//            await ApplyFilterAsync();
//        }

//        // --- автообновление при изменении фильтров ---
//        partial void OnSelectedVehicleChanged(Vehicle? value)
//        {
//            if (_suppressAutoApply) return;
//            _ = ApplyFilterAsync();
//        }

//        partial void OnSelectedCargoTypeChanged(CargoType? value)
//        {
//            if (_suppressAutoApply) return;
//            _ = ApplyFilterAsync();
//        }

//        partial void OnFilterFromDateChanged(DateTime value)
//        {
//            if (_suppressAutoApply) return;
//            if (IsCustomRange) _ = ApplyFilterAsync();
//        }
//        partial void OnFilterToDateChanged(DateTime value)
//        {
//            if (_suppressAutoApply) return;
//            if (IsCustomRange) _ = ApplyFilterAsync();
//        }
//        partial void OnFilterFromTimeChanged(TimeSpan value)
//        {
//            if (_suppressAutoApply) return;
//            if (IsCustomRange) _ = ApplyFilterAsync();
//        }
//        partial void OnFilterToTimeChanged(TimeSpan value)
//        {
//            if (_suppressAutoApply) return;
//            if (IsCustomRange) _ = ApplyFilterAsync();
//        }

//        partial void OnSelectedRangeOptionChanged(RangeOption? value)
//        {
//            if (_suppressAutoApply)
//            {
//                // только обновим зависимое свойство для UI
//                OnPropertyChanged(nameof(IsCustomRange));
//                return;
//            }

//            _ = ApplySelectedRangeAndFilterAsync();
//            OnPropertyChanged(nameof(IsCustomRange));
//        }

//        public async Task ApplySelectedRangeAndFilterAsync()
//        {
//            if (SelectedRangeOption == null) return;

//            var now = DateTime.Now;
//            DateTime from, to;

//            switch (SelectedRangeOption.Kind)
//            {
//                case TimeRangeKind.AllTime:
//                    {
//                        var all = await _db.GetAllAsync<WeighingRecord>();
//                        if (all.Any())
//                        {
//                            from = all.Min(r => r.Timestamp).Date;
//                            to = all.Max(r => r.Timestamp);
//                        }
//                        else
//                        {
//                            from = DateTime.Today.AddYears(-1);
//                            to = DateTime.Today.AddDays(1).AddSeconds(-1);
//                        }
//                        break;
//                    }
//                case TimeRangeKind.LastWeek:
//                    from = now.AddDays(-7); to = now; break;
//                case TimeRangeKind.Last3Days:
//                    from = now.AddDays(-3); to = now; break;
//                case TimeRangeKind.Last24Hours:
//                    from = now.AddHours(-24); to = now; break;
//                case TimeRangeKind.Last12Hours:
//                    from = now.AddHours(-12); to = now; break;
//                case TimeRangeKind.TodayWindow:
//                    {
//                        var baseDay = DateTime.Today;
//                        from = baseDay.AddHours(6);
//                        to = baseDay.AddDays(1).AddHours(3);
//                        break;
//                    }
//                case TimeRangeKind.YesterdayWindow:
//                    {
//                        var baseDay = DateTime.Today.AddDays(-1);
//                        from = baseDay.AddHours(6);
//                        to = baseDay.AddDays(1).AddHours(3);
//                        break;
//                    }
//                case TimeRangeKind.Custom:
//                default:
//                    await ApplyFilterAsync();
//                    return;
//            }

//            // изменяем интервал (эти сеттеры НЕ триггерят ApplyFilterAsync, т.к. IsCustomRange=false)
//            _suppressAutoApply = true;
//            try
//            {
//                FilterFromDate = from.Date;
//                FilterFromTime = from.TimeOfDay;
//                FilterToDate = to.Date;
//                FilterToTime = to.TimeOfDay;
//            }
//            finally
//            {
//                _suppressAutoApply = false;
//            }

//            await ApplyFilterAsync();
//        }

//        [RelayCommand]
//        private async Task ApplyFilterAsync()
//        {
//            await _filterSemaphore.WaitAsync();
//            try
//            {
//                Records.Clear();

//                var from = FilterFromDate.Date + FilterFromTime;
//                var to = FilterToDate.Date + FilterToTime;

//                var all = await _db.GetAllAsync<WeighingRecord>();

//                var filtered = all
//                    .Where(r => r.Timestamp >= from && r.Timestamp <= to)
//                    .Where(r => SelectedVehicle == null || r.VehicleId == SelectedVehicle.Id)
//                    .Where(r => SelectedCargoType == null || r.CargoTypeId == SelectedCargoType.Id);

//                // Справочники
//                var vehicles = await _db.GetAllAsync<Vehicle>();
//                var cargos = await _db.GetAllAsync<CargoType>();
//                var destinations = await _db.GetAllAsync<Destination>();
//                var sources = await _db.GetAllAsync<Source>();

//                var list = filtered.Select(r =>
//                {
//                    var v = vehicles.FirstOrDefault(x => x.Id == r.VehicleId);
//                    var c = cargos.FirstOrDefault(x => x.Id == r.CargoTypeId);
//                    var fromDest = destinations.FirstOrDefault(x => x.Id == r.FromId);
//                    var toDest = destinations.FirstOrDefault(x => x.Id == r.ToId);
//                    var s = r.SourceId.HasValue ? sources.FirstOrDefault(x => x.Id == r.SourceId.Value) : null;

//                    string cargoDisplay = c?.Name ?? "";
//                    if (!string.IsNullOrEmpty(c?.Kind) || !string.IsNullOrEmpty(c?.Variety))
//                        cargoDisplay = $"{c?.Name} | {c?.Kind} | {c?.Variety}";

//                    return new HistoryRecordDto
//                    {
//                        Id = r.Id,
//                        Timestamp = r.Timestamp,
//                        VehicleName = v?.Name ?? "",
//                        CargoDisplay = cargoDisplay,
//                        NetWeight = (int)r.NetWeight,
//                        FromName = fromDest?.Name ?? "",
//                        ToName = toDest?.Name ?? "",
//                        SourceName = s?.Name ?? "",
//                        Comment = r.Comment ?? ""
//                    };
//                });

//                list = SortAscending
//                    ? list.OrderBy(x => x.GetField(CurrentSortField))
//                    : list.OrderByDescending(x => x.GetField(CurrentSortField));

//                foreach (var item in list)
//                    Records.Add(item);

//                OnPropertyChanged(nameof(TotalNetTons));
//            }
//            finally
//            {
//                _filterSemaphore.Release();
//            }
//        }

//        [RelayCommand]
//        private async Task SortAsync(string field)
//        {
//            if (CurrentSortField == field)
//                SortAscending = !SortAscending;
//            else
//            {
//                CurrentSortField = field;
//                SortAscending = true;
//            }

//            await ApplyFilterAsync();
//        }

//        // Сбросы
//        [RelayCommand]
//        private Task ResetDateFilterAsync()
//        {
//            // только меняем пресет — обработчик SelectedRangeOption сам вызовет ApplySelectedRangeAndFilterAsync()
//            SelectedRangeOption = RangeOptions.First(ro => ro.Kind == TimeRangeKind.Last24Hours);
//            return Task.CompletedTask;
//        }

//        [RelayCommand]
//        private Task ResetVehicleFilterAsync()
//        {
//            // меняем свойство — сработает OnSelectedVehicleChanged → ApplyFilterAsync()
//            SelectedVehicle = null;
//            return Task.CompletedTask;
//        }

//        [RelayCommand]
//        private Task ResetCargoFilterAsync()
//        {
//            SelectedCargoType = null;
//            return Task.CompletedTask;
//        }

//        [RelayCommand]
//        private async Task ResetAllFiltersAsync()
//        {
//            _suppressAutoApply = true;
//            try
//            {
//                SelectedVehicle = null;
//                SelectedCargoType = null;
//                SelectedRangeOption = RangeOptions.First(ro => ro.Kind == TimeRangeKind.Last24Hours);
//            }
//            finally
//            {
//                _suppressAutoApply = false;
//            }

//            // один-единственный пересчёт диапазона и фильтра
//            await ApplySelectedRangeAndFilterAsync();
//        }

//        // Показ подробностей записи
//        [RelayCommand]
//        private async Task ShowDetailsAsync(HistoryRecordDto dto)
//        {
//            string msg =
//                $"Дата/время: {dto.Timestamp:dd.MM.yyyy HH:mm}\n" +
//                $"Машина: {dto.VehicleName}\n" +
//                $"Груз: {dto.CargoDisplay}\n" +
//                $"Нетто (кг): {dto.NetWeight:F0}\n" +
//                $"Откуда: {dto.FromName}\n" +
//                $"Куда: {dto.ToName}\n" +
//                $"Источник: {dto.SourceName}\n" +
//                $"Комментарий: {dto.Comment}";

//            await Application.Current.MainPage.DisplayAlert("Подробности", msg, "OK");
//        }

//        // Отправка отчёта
//        [RelayCommand]
//        private async Task SendFilteredReportAsync()
//        {
//            if (string.IsNullOrWhiteSpace(TelegramRecipient))
//            {
//                await Application.Current.MainPage.DisplayAlert("Ошибка", "Введите Telegram username или chat id.", "OK");
//                return;
//            }

//            if (!Records.Any())
//            {
//                await Application.Current.MainPage.DisplayAlert("Инфо", "Нет записей для отправки.", "OK");
//                return;
//            }

//            IsSending = true;
//            string filePath = "";
//            try
//            {
//                string fileName = $"Report_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
//                filePath = _excel.CreateExcelFromHistory(Records, fileName);

//                string caption = $"Отчёт ({Records.First().Timestamp:dd.MM.yyyy HH:mm} — {Records.Last().Timestamp:dd.MM.yyyy HH:mm})";
//                await _telegram.SendDocumentAsync(TelegramRecipient.Trim(), filePath, caption);

//                await Application.Current.MainPage.DisplayAlert("Готово", "Отчёт отправлен.", "OK");
//            }
//            catch (Exception ex)
//            {
//                await Application.Current.MainPage.DisplayAlert("Ошибка при отправке", ex.Message, "OK");
//            }
//            finally
//            {
//                IsSending = false;
//                try { if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath)) File.Delete(filePath); } catch { }
//            }
//        }
//    }

//    // DTO без изменений
//    public class HistoryRecordDto
//    {
//        public Guid Id { get; set; }
//        public DateTime Timestamp { get; set; }
//        public string VehicleName { get; set; } = "";
//        public string CargoDisplay { get; set; } = "";
//        public int NetWeight { get; set; }
//        public string FromName { get; set; } = "";
//        public string ToName { get; set; } = "";
//        public string SourceName { get; set; } = "";
//        public string Comment { get; set; } = "";

//        public object? GetField(string field) => field switch
//        {
//            "Timestamp" => Timestamp,
//            "Vehicle" => VehicleName,
//            "Cargo" => CargoDisplay,
//            "NetWeight" => NetWeight,
//            "From" => FromName,
//            "To" => ToName,
//            "Source" => SourceName,
//            "Comment" => Comment,
//            _ => null
//        };
//    }
//}



































//using CommunityToolkit.Mvvm.ComponentModel;
//using CommunityToolkit.Mvvm.Input;
//using ScaleRecordApp.Models;
//using ScaleRecordApp.Services;
//using System.Collections.ObjectModel;

//namespace ScaleRecordApp.ViewModels;

//public enum TimeRangeKind
//{
//    AllTime,
//    LastWeek,
//    Last3Days,
//    Last24Hours,
//    Last12Hours,
//    TodayWindow,       // сегодня 06:00 -> завтра 03:00
//    YesterdayWindow,   // вчера 06:00 -> сегодня 03:00
//    Custom
//}

//public class RangeOption
//{
//    public TimeRangeKind Kind { get; set; }
//    public string Title { get; set; } = "";
//}

//public partial class HistoryVM : BaseViewModel
//{
//    private readonly DatabaseService _db;
//    private readonly ExcelService _excel;
//    private readonly TelegramService _telegram;

//    public ObservableCollection<HistoryRecordDto> Records { get; } = new();
//    public ObservableCollection<Vehicle> Vehicles { get; } = new();
//    public ObservableCollection<CargoType> CargoTypes { get; } = new();

//    public ObservableCollection<RangeOption> RangeOptions { get; } = new();

//    [ObservableProperty] private Vehicle? selectedVehicle;
//    [ObservableProperty] private CargoType? selectedCargoType;

//    [ObservableProperty] private DateTime filterFromDate = DateTime.Today.AddDays(-1);
//    [ObservableProperty] private TimeSpan filterFromTime = TimeSpan.Zero;
//    [ObservableProperty] private DateTime filterToDate = DateTime.Today;
//    [ObservableProperty] private TimeSpan filterToTime = new(23, 59, 59);

//    [ObservableProperty] private RangeOption? selectedRangeOption;

//    public bool IsCustomRange => SelectedRangeOption?.Kind == TimeRangeKind.Custom;

//    // сортировка
//    [ObservableProperty] private string currentSortField = "Timestamp";
//    partial void OnCurrentSortFieldChanged(string value) => UpdateHeaderTexts();

//    [ObservableProperty] private bool sortAscending = false;
//    partial void OnSortAscendingChanged(bool value) => UpdateHeaderTexts();

//    // подписи заголовков
//    [ObservableProperty] private string dateHeaderText = "Дата";
//    [ObservableProperty] private string vehicleHeaderText = "Машина";
//    [ObservableProperty] private string cargoHeaderText = "Груз";
//    [ObservableProperty] private string netWeightHeaderText = "Нетто (кг)";
//    [ObservableProperty] private string fromHeaderText = "Откуда";
//    [ObservableProperty] private string toHeaderText = "Куда";
//    [ObservableProperty] private string sourceHeaderText = "Источник";
//    [ObservableProperty] private string commentHeaderText = "Комментарий";

//    // Telegram
//    [ObservableProperty] private string telegramRecipient = "";
//    [ObservableProperty] private bool isSending;
//    partial void OnIsSendingChanged(bool value) => OnPropertyChanged(nameof(IsNotSending));
//    public bool IsNotSending => !IsSending;

//    public double TotalNetTons => Records.Sum(r => r.NetWeight) / 1000.0;

//    public HistoryVM()
//    {
//        _db = Application.Current.Windows[0].Page.Handler.MauiContext.Services.GetService<DatabaseService>()!;
//        _excel = Application.Current.Windows[0].Page.Handler.MauiContext.Services.GetService<ExcelService>()!;
//        _telegram = Application.Current.Windows[0].Page.Handler.MauiContext.Services.GetService<TelegramService>()!;

//        // заполним пресеты периода
//        RangeOptions.Add(new RangeOption { Kind = TimeRangeKind.AllTime, Title = "За всё время" });
//        RangeOptions.Add(new RangeOption { Kind = TimeRangeKind.LastWeek, Title = "За последнюю неделю" });
//        RangeOptions.Add(new RangeOption { Kind = TimeRangeKind.Last3Days, Title = "За последние 3 дня" });
//        RangeOptions.Add(new RangeOption { Kind = TimeRangeKind.Last24Hours, Title = "За последние 24 часа" });
//        RangeOptions.Add(new RangeOption { Kind = TimeRangeKind.Last12Hours, Title = "За последние 12 часов" });
//        RangeOptions.Add(new RangeOption { Kind = TimeRangeKind.TodayWindow, Title = "За сегодня (06:00 → 03:00)" });
//        RangeOptions.Add(new RangeOption { Kind = TimeRangeKind.YesterdayWindow, Title = "За вчерашний день (06:00 → 03:00)" });
//        RangeOptions.Add(new RangeOption { Kind = TimeRangeKind.Custom, Title = "За выбранный промежуток времени" });

//        SelectedRangeOption = RangeOptions.First(ro => ro.Kind == TimeRangeKind.Last24Hours); // дефолт
//        UpdateHeaderTexts();

//        //Task.Run(async () =>
//        //{
//        //    await LoadAsync();
//        //    await ApplySelectedRangeAndFilterAsync(); // применим дефолтный период
//        //});
//    }

//    private void UpdateHeaderTexts()
//    {
//        string arrow = SortAscending ? " ▲" : " ▼";

//        DateHeaderText = CurrentSortField == "Timestamp" ? $"Дата{arrow}" : "Дата ▲▼";
//        VehicleHeaderText = CurrentSortField == "Vehicle" ? $"Машина{arrow}" : "Машина ▲▼";
//        CargoHeaderText = CurrentSortField == "Cargo" ? $"Груз{arrow}" : "Груз ▲▼";
//        NetWeightHeaderText = CurrentSortField == "NetWeight" ? $"Нетто (кг) {arrow}" : "Нетто (кг) ▲▼";
//        FromHeaderText = CurrentSortField == "From" ? $"Откуда{arrow}" : "Откуда ▲▼";
//        ToHeaderText = CurrentSortField == "To" ? $"Куда{arrow}" : "Куда ▲▼";
//        SourceHeaderText = CurrentSortField == "Source" ? $"Источник{arrow}" : "Источник ▲▼";
//        CommentHeaderText = CurrentSortField == "Comment" ? $"Комментарий{arrow}" : "Комментарий ▲▼";
//    }

//    [RelayCommand]
//    public async Task LoadAsync()
//    {
//        var vehicles = await _db.GetAllAsync<Vehicle>();
//        Vehicles.Clear();
//        foreach (var v in vehicles) Vehicles.Add(v);

//        var cargos = await _db.GetAllAsync<CargoType>();
//        CargoTypes.Clear();
//        foreach (var c in cargos) CargoTypes.Add(c);

//        await ApplyFilterAsync();
//    }

//    // --- автообновление при изменении фильтров ---
//    partial void OnSelectedVehicleChanged(Vehicle? value) => _ = ApplyFilterAsync();
//    partial void OnSelectedCargoTypeChanged(CargoType? value) => _ = ApplyFilterAsync();

//    partial void OnFilterFromDateChanged(DateTime value)
//    {
//        if (IsCustomRange) _ = ApplyFilterAsync();
//    }
//    partial void OnFilterToDateChanged(DateTime value)
//    {
//        if (IsCustomRange) _ = ApplyFilterAsync();
//    }
//    partial void OnFilterFromTimeChanged(TimeSpan value)
//    {
//        if (IsCustomRange) _ = ApplyFilterAsync();
//    }
//    partial void OnFilterToTimeChanged(TimeSpan value)
//    {
//        if (IsCustomRange) _ = ApplyFilterAsync();
//    }

//    partial void OnSelectedRangeOptionChanged(RangeOption? value)
//    {
//        _ = ApplySelectedRangeAndFilterAsync();
//        OnPropertyChanged(nameof(IsCustomRange));
//    }

//    public async Task ApplySelectedRangeAndFilterAsync()
//    {
//        if (SelectedRangeOption == null) return;

//        var now = DateTime.Now;
//        DateTime from, to;

//        switch (SelectedRangeOption.Kind)
//        {
//            case TimeRangeKind.AllTime:
//                {
//                    var all = await _db.GetAllAsync<WeighingRecord>();
//                    if (all.Any())
//                    {
//                        from = all.Min(r => r.Timestamp).Date;
//                        to = all.Max(r => r.Timestamp);
//                    }
//                    else
//                    {
//                        from = DateTime.Today.AddYears(-1);
//                        to = DateTime.Today.AddDays(1).AddSeconds(-1);
//                    }
//                    break;
//                }
//            case TimeRangeKind.LastWeek:
//                from = now.AddDays(-7);
//                to = now;
//                break;
//            case TimeRangeKind.Last3Days:
//                from = now.AddDays(-3);
//                to = now;
//                break;
//            case TimeRangeKind.Last24Hours:
//                from = now.AddHours(-24);
//                to = now;
//                break;
//            case TimeRangeKind.Last12Hours:
//                from = now.AddHours(-12);
//                to = now;
//                break;
//            case TimeRangeKind.TodayWindow:
//                {
//                    var baseDay = DateTime.Today;
//                    from = baseDay.AddHours(6);                 // сегодня 06:00
//                    to = baseDay.AddDays(1).AddHours(3);      // завтра 03:00
//                    break;
//                }
//            case TimeRangeKind.YesterdayWindow:
//                {
//                    var baseDay = DateTime.Today.AddDays(-1);
//                    from = baseDay.AddHours(6);                 // вчера 06:00
//                    to = baseDay.AddDays(1).AddHours(3);      // сегодня 03:00
//                    break;
//                }
//            case TimeRangeKind.Custom:
//            default:
//                // не трогаем даты — пользователь сам правит
//                await ApplyFilterAsync();
//                return;
//        }

//        FilterFromDate = from.Date;
//        FilterFromTime = from.TimeOfDay;
//        FilterToDate = to.Date;
//        FilterToTime = to.TimeOfDay;

//        await ApplyFilterAsync();
//    }

//    [RelayCommand]
//    private async Task ApplyFilterAsync()
//    {
//        Records.Clear();
//        var from = FilterFromDate.Date + FilterFromTime;
//        var to = FilterToDate.Date + FilterToTime;

//        var all = await _db.GetAllAsync<WeighingRecord>();

//        var filtered = all
//            .Where(r => r.Timestamp >= from && r.Timestamp <= to)
//            .Where(r => SelectedVehicle == null || r.VehicleId == SelectedVehicle.Id)
//            .Where(r => SelectedCargoType == null || r.CargoTypeId == SelectedCargoType.Id);

//        // Справочники
//        var vehicles = await _db.GetAllAsync<Vehicle>();
//        var cargos = await _db.GetAllAsync<CargoType>();
//        var destinations = await _db.GetAllAsync<Destination>();
//        var sources = await _db.GetAllAsync<Source>();

//        var list = filtered.Select(r =>
//        {
//            var v = vehicles.FirstOrDefault(x => x.Id == r.VehicleId);
//            var c = cargos.FirstOrDefault(x => x.Id == r.CargoTypeId);
//            var fromDest = destinations.FirstOrDefault(x => x.Id == r.FromId);
//            var toDest = destinations.FirstOrDefault(x => x.Id == r.ToId);
//            var s = r.SourceId.HasValue ? sources.FirstOrDefault(x => x.Id == r.SourceId.Value) : null;

//            string cargoDisplay = c?.Name ?? "";
//            if (!string.IsNullOrEmpty(c?.Kind) || !string.IsNullOrEmpty(c?.Variety))
//                cargoDisplay = $"{c?.Name} | {c?.Kind} | {c?.Variety}";

//            return new HistoryRecordDto
//            {
//                Id = r.Id,
//                Timestamp = r.Timestamp,
//                VehicleName = v?.Name ?? "",
//                CargoDisplay = cargoDisplay,
//                NetWeight = (int)r.NetWeight,
//                FromName = fromDest?.Name ?? "",
//                ToName = toDest?.Name ?? "",
//                SourceName = s?.Name ?? "",
//                Comment = r.Comment ?? ""
//            };
//        });

//        list = SortAscending
//            ? list.OrderBy(x => x.GetField(CurrentSortField))
//            : list.OrderByDescending(x => x.GetField(CurrentSortField));

//        foreach (var item in list)
//            Records.Add(item);

//        OnPropertyChanged(nameof(TotalNetTons));
//    }

//    [RelayCommand]
//    private async Task SortAsync(string field)
//    {
//        if (CurrentSortField == field)
//            SortAscending = !SortAscending;
//        else
//        {
//            CurrentSortField = field;
//            SortAscending = true;
//        }

//        await ApplyFilterAsync();
//    }

//    // Сбросы
//    [RelayCommand]
//    private async Task ResetDateFilterAsync()
//    {
//        SelectedRangeOption = RangeOptions.First(ro => ro.Kind == TimeRangeKind.Last24Hours);
//        await ApplySelectedRangeAndFilterAsync();
//    }

//    [RelayCommand]
//    private async Task ResetVehicleFilterAsync()
//    {
//        SelectedVehicle = null;
//        await ApplyFilterAsync();
//    }

//    [RelayCommand]
//    private async Task ResetCargoFilterAsync()
//    {
//        SelectedCargoType = null;
//        await ApplyFilterAsync();
//    }

//    [RelayCommand]
//    private async Task ResetAllFiltersAsync()
//    {
//        SelectedVehicle = null;
//        SelectedCargoType = null;
//        SelectedRangeOption = RangeOptions.First(ro => ro.Kind == TimeRangeKind.Last24Hours);
//        await ApplySelectedRangeAndFilterAsync();
//    }

//    // Показ подробностей записи
//    [RelayCommand]
//    private async Task ShowDetailsAsync(HistoryRecordDto dto)
//    {
//        string msg =
//            $"Дата/время: {dto.Timestamp:dd.MM.yyyy HH:mm}\n" +
//            $"Машина: {dto.VehicleName}\n" +
//            $"Груз: {dto.CargoDisplay}\n" +
//            $"Нетто (кг): {dto.NetWeight:F0}\n" +
//            $"Откуда: {dto.FromName}\n" +
//            $"Куда: {dto.ToName}\n" +
//            $"Источник: {dto.SourceName}\n" +
//            $"Комментарий: {dto.Comment}";

//        await Application.Current.MainPage.DisplayAlert("Подробности", msg, "OK");
//    }

//    // Отправка отчёта
//    [RelayCommand]
//    private async Task SendFilteredReportAsync()
//    {
//        if (string.IsNullOrWhiteSpace(TelegramRecipient))
//        {
//            await Application.Current.MainPage.DisplayAlert("Ошибка", "Введите Telegram username или chat id.", "OK");
//            return;
//        }

//        if (!Records.Any())
//        {
//            await Application.Current.MainPage.DisplayAlert("Инфо", "Нет записей для отправки.", "OK");
//            return;
//        }

//        IsSending = true;
//        string filePath = "";
//        try
//        {
//            string fileName = $"Report_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
//            filePath = _excel.CreateExcelFromHistory(Records, fileName);

//            string caption = $"Отчёт ({Records.First().Timestamp:dd.MM.yyyy HH:mm} — {Records.Last().Timestamp:dd.MM.yyyy HH:mm})";
//            await _telegram.SendDocumentAsync(TelegramRecipient.Trim(), filePath, caption);

//            await Application.Current.MainPage.DisplayAlert("Готово", "Отчёт отправлен.", "OK");
//        }
//        catch (Exception ex)
//        {
//            await Application.Current.MainPage.DisplayAlert("Ошибка при отправке", ex.Message, "OK");
//        }
//        finally
//        {
//            IsSending = false;
//            try { if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath)) File.Delete(filePath); } catch { }
//        }
//    }
//}

//// DTO без изменений
//public class HistoryRecordDto
//{
//    public Guid Id { get; set; }
//    public DateTime Timestamp { get; set; }
//    public string VehicleName { get; set; } = "";
//    public string CargoDisplay { get; set; } = "";
//    public int NetWeight { get; set; }
//    public string FromName { get; set; } = "";
//    public string ToName { get; set; } = "";
//    public string SourceName { get; set; } = "";
//    public string Comment { get; set; } = "";

//    public object? GetField(string field) => field switch
//    {
//        "Timestamp" => Timestamp,
//        "Vehicle" => VehicleName,
//        "Cargo" => CargoDisplay,
//        "NetWeight" => NetWeight,
//        "From" => FromName,
//        "To" => ToName,
//        "Source" => SourceName,
//        "Comment" => Comment,
//        _ => null
//    };
//}
















































//using CommunityToolkit.Mvvm.ComponentModel;
//using CommunityToolkit.Mvvm.Input;
//using ScaleRecordApp.Models;
//using ScaleRecordApp.Services;
//using System.Collections.ObjectModel;

//namespace ScaleRecordApp.ViewModels;

//public partial class HistoryVM : BaseViewModel
//{
//    private readonly DatabaseService _db;
//    private readonly ExcelService _excel;
//    private readonly TelegramService _telegram;

//    public ObservableCollection<HistoryRecordDto> Records { get; } = new();
//    public ObservableCollection<Vehicle> Vehicles { get; } = new();
//    public ObservableCollection<CargoType> CargoTypes { get; } = new();

//    [ObservableProperty] private Vehicle? selectedVehicle;
//    [ObservableProperty] private CargoType? selectedCargoType;

//    [ObservableProperty] private DateTime filterFromDate = DateTime.Today.AddDays(-1);
//    [ObservableProperty] private TimeSpan filterFromTime = TimeSpan.Zero;
//    [ObservableProperty] private DateTime filterToDate = DateTime.Today;
//    [ObservableProperty] private TimeSpan filterToTime = new(23, 59, 59);

//    // сортировка
//    [ObservableProperty] private string currentSortField = "Timestamp";
//    partial void OnCurrentSortFieldChanged(string value) => UpdateHeaderTexts();

//    [ObservableProperty] private bool sortAscending = false;
//    partial void OnSortAscendingChanged(bool value) => UpdateHeaderTexts();

//    // подписи заголовков
//    [ObservableProperty] private string dateHeaderText = "Дата";
//    [ObservableProperty] private string vehicleHeaderText = "Машина";
//    [ObservableProperty] private string cargoHeaderText = "Груз";
//    [ObservableProperty] private string netWeightHeaderText = "Нетто (кг)";
//    [ObservableProperty] private string fromHeaderText = "Откуда";
//    [ObservableProperty] private string toHeaderText = "Куда";
//    [ObservableProperty] private string sourceHeaderText = "Источник";
//    [ObservableProperty] private string commentHeaderText = "Комментарий";

//    // --- Новые поля для отправки в Telegram ---
//    [ObservableProperty] private string telegramRecipient = "";
//    [ObservableProperty] private bool isSending;
//    partial void OnIsSendingChanged(bool value) => OnPropertyChanged(nameof(IsNotSending));
//    public bool IsNotSending => !IsSending; // bind to button

//    public double TotalNetTons => Records.Sum(r => r.NetWeight) / 1000.0;

//    public HistoryVM()
//    {
//        _db = Application.Current.Windows[0].Page.Handler.MauiContext.Services.GetService<DatabaseService>()!;
//        _excel = Application.Current.Windows[0].Page.Handler.MauiContext.Services.GetService<ExcelService>()!;
//        _telegram = Application.Current.Windows[0].Page.Handler.MauiContext.Services.GetService<TelegramService>()!;
//        UpdateHeaderTexts();
//        Task.Run(LoadAsync);
//    }

//    private void UpdateHeaderTexts()
//    {
//        string arrow = SortAscending ? " ▲" : " ▼";

//        DateHeaderText = CurrentSortField == "Timestamp" ? $"Дата{arrow}" : "Дата ▲▼";
//        VehicleHeaderText = CurrentSortField == "Vehicle" ? $"Машина{arrow}" : "Машина ▲▼";
//        CargoHeaderText = CurrentSortField == "Cargo" ? $"Груз{arrow}" : "Груз ▲▼";
//        NetWeightHeaderText = CurrentSortField == "NetWeight" ? $"Нетто (кг) {arrow}" : "Нетто (кг) ▲▼";
//        FromHeaderText = CurrentSortField == "From" ? $"Откуда{arrow}" : "Откуда ▲▼";
//        ToHeaderText = CurrentSortField == "To" ? $"Куда{arrow}" : "Куда ▲▼";
//        SourceHeaderText = CurrentSortField == "Source" ? $"Источник{arrow}" : "Источник ▲▼";
//        CommentHeaderText = CurrentSortField == "Comment" ? $"Комментарий{arrow}" : "Комментарий ▲▼";
//    }

//    [RelayCommand]
//    public async Task LoadAsync()
//    {
//        var vehicles = await _db.GetAllAsync<Vehicle>();
//        Vehicles.Clear();
//        foreach (var v in vehicles) Vehicles.Add(v);

//        var cargos = await _db.GetAllAsync<CargoType>();
//        CargoTypes.Clear();
//        foreach (var c in cargos) CargoTypes.Add(c);

//        await ApplyFilterAsync();
//    }

//    [RelayCommand]
//    private async Task ApplyFilterAsync()
//    {
//        Records.Clear();
//        var from = FilterFromDate.Date + FilterFromTime;
//        var to = FilterToDate.Date + FilterToTime;

//        var all = await _db.GetAllAsync<WeighingRecord>();

//        var filtered = all
//            .Where(r => r.Timestamp >= from && r.Timestamp <= to)
//            .Where(r => SelectedVehicle == null || r.VehicleId == SelectedVehicle.Id)
//            .Where(r => SelectedCargoType == null || r.CargoTypeId == SelectedCargoType.Id);

//        // Подгрузка справочников
//        var vehicles = await _db.GetAllAsync<Vehicle>();
//        var cargos = await _db.GetAllAsync<CargoType>();
//        var destinations = await _db.GetAllAsync<Destination>();
//        var sources = await _db.GetAllAsync<Source>();

//        var list = filtered.Select(r =>
//        {
//            var v = vehicles.FirstOrDefault(x => x.Id == r.VehicleId);
//            var c = cargos.FirstOrDefault(x => x.Id == r.CargoTypeId);
//            var fromDest = destinations.FirstOrDefault(x => x.Id == r.FromId);
//            var toDest = destinations.FirstOrDefault(x => x.Id == r.ToId);
//            var s = r.SourceId.HasValue ? sources.FirstOrDefault(x => x.Id == r.SourceId.Value) : null;

//            string cargoDisplay = c?.Name ?? "";
//            if (!string.IsNullOrEmpty(c?.Kind) || !string.IsNullOrEmpty(c?.Variety))
//                cargoDisplay = $"{c?.Name} | {c?.Kind} | {c?.Variety}";

//            return new HistoryRecordDto
//            {
//                Id = r.Id,
//                Timestamp = r.Timestamp,
//                VehicleName = v?.Name ?? "",
//                CargoDisplay = cargoDisplay,
//                NetWeight = (int)r.NetWeight,
//                FromName = fromDest?.Name ?? "",
//                ToName = toDest?.Name ?? "",
//                SourceName = s?.Name ?? "",
//                Comment = r.Comment ?? ""
//            };
//        });

//        // сортировка
//        list = SortAscending
//            ? list.OrderBy(x => x.GetField(CurrentSortField))
//            : list.OrderByDescending(x => x.GetField(CurrentSortField));

//        foreach (var item in list)
//            Records.Add(item);

//        OnPropertyChanged(nameof(TotalNetTons));
//    }

//    [RelayCommand]
//    private async Task SortAsync(string field)
//    {
//        if (CurrentSortField == field)
//            SortAscending = !SortAscending;
//        else
//        {
//            CurrentSortField = field;
//            SortAscending = true;
//        }

//        await ApplyFilterAsync();
//    }

//    // --- Команды для сброса фильтров (как было) ---
//    [RelayCommand]
//    private async Task ResetDateFilterAsync()
//    {
//        FilterFromDate = DateTime.Today.AddDays(-1);
//        FilterFromTime = TimeSpan.Zero;
//        FilterToDate = DateTime.Today;
//        FilterToTime = new TimeSpan(23, 59, 59);
//        await ApplyFilterAsync();
//    }

//    [RelayCommand]
//    private async Task ResetVehicleFilterAsync()
//    {
//        SelectedVehicle = null;
//        await ApplyFilterAsync();
//    }

//    [RelayCommand]
//    private async Task ResetCargoFilterAsync()
//    {
//        SelectedCargoType = null;
//        await ApplyFilterAsync();
//    }

//    [RelayCommand]
//    private async Task ResetAllFiltersAsync()
//    {
//        SelectedVehicle = null;
//        SelectedCargoType = null;
//        FilterFromDate = DateTime.Today.AddDays(-1);
//        FilterFromTime = TimeSpan.Zero;
//        FilterToDate = DateTime.Today;
//        FilterToTime = new TimeSpan(23, 59, 59);
//        await ApplyFilterAsync();
//    }

//    // ----------------- Отправка текущего (отфильтрованного) отчёта вручную -----------------
//    [RelayCommand]
//    private async Task SendFilteredReportAsync()
//    {
//        if (string.IsNullOrWhiteSpace(TelegramRecipient))
//        {
//            await Application.Current.MainPage.DisplayAlert("Ошибка", "Введите Telegram username или chat id.", "OK");
//            return;
//        }

//        if (!Records.Any())
//        {
//            await Application.Current.MainPage.DisplayAlert("Инфо", "Нет записей для отправки.", "OK");
//            return;
//        }

//        IsSending = true;
//        string filePath = "";
//        try
//        {
//            string fileName = $"Report_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
//            filePath = _excel.CreateExcelFromHistory(Records, fileName);

//            string caption = $"Отчёт ({Records.First().Timestamp:dd.MM.yyyy HH:mm} — {Records.Last().Timestamp:dd.MM.yyyy HH:mm})";
//            await _telegram.SendDocumentAsync(TelegramRecipient.Trim(), filePath, caption);

//            await Application.Current.MainPage.DisplayAlert("Готово", "Отчёт отправлен.", "OK");
//        }
//        catch (Exception ex)
//        {
//            await Application.Current.MainPage.DisplayAlert("Ошибка при отправке", ex.Message, "OK");
//        }
//        finally
//        {
//            IsSending = false;
//            try { if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath)) File.Delete(filePath); } catch { }
//        }
//    }
//}

//// DTO остался без изменений
//public class HistoryRecordDto
//{
//    public Guid Id { get; set; }
//    public DateTime Timestamp { get; set; }
//    public string VehicleName { get; set; } = "";
//    public string CargoDisplay { get; set; } = "";
//    public int NetWeight { get; set; }
//    public string FromName { get; set; } = "";
//    public string ToName { get; set; } = "";
//    public string SourceName { get; set; } = "";
//    public string Comment { get; set; } = "";

//    public object? GetField(string field) => field switch
//    {
//        "Timestamp" => Timestamp,
//        "Vehicle" => VehicleName,
//        "Cargo" => CargoDisplay,
//        "NetWeight" => NetWeight,
//        "From" => FromName,
//        "To" => ToName,
//        "Source" => SourceName,
//        "Comment" => Comment,
//        _ => null
//    };
//}




















//using CommunityToolkit.Mvvm.ComponentModel;
//using CommunityToolkit.Mvvm.Input;
//using ScaleRecordApp.Models;
//using ScaleRecordApp.Services;
//using System.Collections.ObjectModel;

//namespace ScaleRecordApp.ViewModels;

//public partial class HistoryVM : BaseViewModel
//{
//    private readonly DatabaseService _db;

//    public ObservableCollection<HistoryRecordDto> Records { get; } = new();
//    public ObservableCollection<Vehicle> Vehicles { get; } = new();
//    public ObservableCollection<CargoType> CargoTypes { get; } = new();

//    [ObservableProperty] private Vehicle? selectedVehicle;
//    [ObservableProperty] private CargoType? selectedCargoType;

//    [ObservableProperty] private DateTime filterFromDate = DateTime.Today.AddDays(-1);
//    [ObservableProperty] private TimeSpan filterFromTime = TimeSpan.Zero;
//    [ObservableProperty] private DateTime filterToDate = DateTime.Today;
//    [ObservableProperty] private TimeSpan filterToTime = new(23, 59, 59);

//    // сортировка: поле и направление
//    [ObservableProperty] private string currentSortField = "Timestamp";
//    partial void OnCurrentSortFieldChanged(string value) => UpdateHeaderTexts();

//    [ObservableProperty] private bool sortAscending = false;
//    partial void OnSortAscendingChanged(bool value) => UpdateHeaderTexts();

//    // подписи заголовков (для биндинга в XAML)
//    [ObservableProperty] private string dateHeaderText = "Дата";
//    [ObservableProperty] private string vehicleHeaderText = "Машина";
//    [ObservableProperty] private string cargoHeaderText = "Груз";
//    [ObservableProperty] private string netWeightHeaderText = "Нетто";
//    [ObservableProperty] private string fromHeaderText = "Откуда";
//    [ObservableProperty] private string toHeaderText = "Куда";
//    [ObservableProperty] private string sourceHeaderText = "Источник";
//    [ObservableProperty] private string commentHeaderText = "Комментарий";

//    public double TotalNetTons => Records.Sum(r => r.NetWeight) / 1000.0;

//    public HistoryVM()
//    {
//        _db = Application.Current.Windows[0].Page.Handler.MauiContext.Services.GetService<DatabaseService>()!;
//        UpdateHeaderTexts();
//        Task.Run(LoadAsync);
//    }

//    // Обновление отображаемых текстов заголовков в зависимости от CurrentSortField и SortAscending
//    private void UpdateHeaderTexts()
//    {
//        string arrow = SortAscending ? " ▲" : " ▼";

//        DateHeaderText = CurrentSortField == "Timestamp" ? $"Дата{arrow}" : "Дата ▲▼";
//        VehicleHeaderText = CurrentSortField == "Vehicle" ? $"Машина{arrow}" : "Машина ▲▼";
//        CargoHeaderText = CurrentSortField == "Cargo" ? $"Груз{arrow}" : "Груз ▲▼";
//        NetWeightHeaderText = CurrentSortField == "NetWeight" ? $"Нетто{arrow}" : "Нетто ▲▼";
//        FromHeaderText = CurrentSortField == "From" ? $"Откуда{arrow}" : "Откуда ▲▼";
//        ToHeaderText = CurrentSortField == "To" ? $"Куда{arrow}" : "Куда ▲▼";
//        SourceHeaderText = CurrentSortField == "Source" ? $"Источник{arrow}" : "Источник ▲▼";
//        CommentHeaderText = CurrentSortField == "Comment" ? $"Комментарий{arrow}" : "Комментарий ▲▼";
//    }

//    [RelayCommand]
//    public async Task LoadAsync()
//    {
//        var vehicles = await _db.GetAllAsync<Vehicle>();
//        Vehicles.Clear();
//        foreach (var v in vehicles) Vehicles.Add(v);

//        var cargos = await _db.GetAllAsync<CargoType>();
//        CargoTypes.Clear();
//        foreach (var c in cargos) CargoTypes.Add(c);

//        await ApplyFilterAsync();
//    }

//    [RelayCommand]
//    private async Task ApplyFilterAsync()
//    {
//        Records.Clear();
//        var from = FilterFromDate.Date + FilterFromTime;
//        var to = FilterToDate.Date + FilterToTime;

//        var all = await _db.GetAllAsync<WeighingRecord>();

//        var filtered = all
//            .Where(r => r.Timestamp >= from && r.Timestamp <= to)
//            .Where(r => SelectedVehicle == null || r.VehicleId == SelectedVehicle.Id)
//            .Where(r => SelectedCargoType == null || r.CargoTypeId == SelectedCargoType.Id);

//        // Подгрузка справочников
//        var vehicles = await _db.GetAllAsync<Vehicle>();
//        var cargos = await _db.GetAllAsync<CargoType>();
//        var destinations = await _db.GetAllAsync<Destination>();
//        var sources = await _db.GetAllAsync<Source>();

//        var list = filtered.Select(r =>
//        {
//            var v = vehicles.FirstOrDefault(x => x.Id == r.VehicleId);
//            var c = cargos.FirstOrDefault(x => x.Id == r.CargoTypeId);
//            var fromDest = destinations.FirstOrDefault(x => x.Id == r.FromId);
//            var toDest = destinations.FirstOrDefault(x => x.Id == r.ToId);
//            var s = r.SourceId.HasValue ? sources.FirstOrDefault(x => x.Id == r.SourceId.Value) : null;

//            string cargoDisplay = c?.Name ?? "";
//            if (!string.IsNullOrEmpty(c?.Kind) || !string.IsNullOrEmpty(c?.Variety))
//                cargoDisplay = $"{c?.Name} | {c?.Kind} | {c?.Variety}";

//            return new HistoryRecordDto
//            {
//                Id = r.Id,
//                Timestamp = r.Timestamp,
//                VehicleName = v?.Name ?? "",
//                CargoDisplay = cargoDisplay,
//                NetWeight = (int)r.NetWeight,
//                FromName = fromDest?.Name ?? "",
//                ToName = toDest?.Name ?? "",
//                SourceName = s?.Name ?? "",
//                Comment = r.Comment ?? ""
//            };
//        });

//        // сортировка
//        list = SortAscending
//            ? list.OrderBy(x => x.GetField(CurrentSortField))
//            : list.OrderByDescending(x => x.GetField(CurrentSortField));

//        foreach (var item in list)
//            Records.Add(item);

//        OnPropertyChanged(nameof(TotalNetTons));
//    }

//    [RelayCommand]
//    private async Task SortAsync(string field)
//    {
//        if (CurrentSortField == field)
//            SortAscending = !SortAscending;
//        else
//        {
//            CurrentSortField = field;
//            SortAscending = true;
//        }

//        // подписи заголовков обновятся через partial On...Changed
//        await ApplyFilterAsync();
//    }

//    // --- Команды для сброса фильтров ---
//    [RelayCommand]
//    private async Task ResetDateFilterAsync()
//    {
//        FilterFromDate = DateTime.Today.AddDays(-1);
//        FilterFromTime = TimeSpan.Zero;
//        FilterToDate = DateTime.Today;
//        FilterToTime = new TimeSpan(23, 59, 59);
//        await ApplyFilterAsync();
//    }

//    [RelayCommand]
//    private async Task ResetVehicleFilterAsync()
//    {
//        SelectedVehicle = null;
//        await ApplyFilterAsync();
//    }

//    [RelayCommand]
//    private async Task ResetCargoFilterAsync()
//    {
//        SelectedCargoType = null;
//        await ApplyFilterAsync();
//    }

//    [RelayCommand]
//    private async Task ResetAllFiltersAsync()
//    {
//        SelectedVehicle = null;
//        SelectedCargoType = null;
//        FilterFromDate = DateTime.Today.AddDays(-1);
//        FilterFromTime = TimeSpan.Zero;
//        FilterToDate = DateTime.Today;
//        FilterToTime = new TimeSpan(23, 59, 59);
//        await ApplyFilterAsync();
//    }
//}

//public class HistoryRecordDto
//{
//    public Guid Id { get; set; }
//    public DateTime Timestamp { get; set; }
//    public string VehicleName { get; set; } = "";
//    public string CargoDisplay { get; set; } = "";
//    public int NetWeight { get; set; }
//    public string FromName { get; set; } = "";
//    public string ToName { get; set; } = "";
//    public string SourceName { get; set; } = "";
//    public string Comment { get; set; } = "";

//    public object? GetField(string field) => field switch
//    {
//        "Timestamp" => Timestamp,
//        "Vehicle" => VehicleName,
//        "Cargo" => CargoDisplay,
//        "NetWeight" => NetWeight,
//        "From" => FromName,
//        "To" => ToName,
//        "Source" => SourceName,
//        "Comment" => Comment,
//        _ => null
//    };
//}

























//using System.Collections.ObjectModel;
//using CommunityToolkit.Mvvm.ComponentModel;
//using CommunityToolkit.Mvvm.Input;
//using ScaleRecordApp.Models;
//using ScaleRecordApp.Services;

//namespace ScaleRecordApp.ViewModels;

//public partial class HistoryVM : BaseViewModel
//{
//    private readonly DatabaseService _db;

//    public ObservableCollection<HistoryRecordDto> Records { get; } = new();
//    public ObservableCollection<Vehicle> Vehicles { get; } = new();
//    public ObservableCollection<CargoType> CargoTypes { get; } = new();

//    [ObservableProperty] private Vehicle? selectedVehicle;
//    [ObservableProperty] private CargoType? selectedCargoType;

//    [ObservableProperty] private DateTime filterFromDate = DateTime.Today.AddDays(-1);
//    [ObservableProperty] private TimeSpan filterFromTime = TimeSpan.Zero;
//    [ObservableProperty] private DateTime filterToDate = DateTime.Today;
//    [ObservableProperty] private TimeSpan filterToTime = new(23, 59, 59);

//    [ObservableProperty] private string currentSortField = "Timestamp";
//    [ObservableProperty] private bool sortAscending = false;

//    public double TotalNetTons => Records.Sum(r => r.NetWeight) / 1000.0;

//    public HistoryVM()
//    {
//        _db = Application.Current.Windows[0].Page.Handler.MauiContext.Services.GetService<DatabaseService>()!;
//        Task.Run(LoadAsync);
//    }

//    [RelayCommand]
//    public async Task LoadAsync()
//    {
//        var vehicles = await _db.GetAllAsync<Vehicle>();
//        Vehicles.Clear();
//        foreach (var v in vehicles) Vehicles.Add(v);

//        var cargos = await _db.GetAllAsync<CargoType>();
//        CargoTypes.Clear();
//        foreach (var c in cargos) CargoTypes.Add(c);

//        await ApplyFilterAsync();
//    }

//    [RelayCommand]
//    private async Task ApplyFilterAsync()
//    {
//        Records.Clear();
//        var from = FilterFromDate.Date + FilterFromTime;
//        var to = FilterToDate.Date + FilterToTime;

//        var all = await _db.GetAllAsync<WeighingRecord>();

//        var filtered = all
//            .Where(r => r.Timestamp >= from && r.Timestamp <= to)
//            .Where(r => SelectedVehicle == null || r.VehicleId == SelectedVehicle.Id)
//            .Where(r => SelectedCargoType == null || r.CargoTypeId == SelectedCargoType.Id);

//        // Подгрузка справочников
//        var vehicles = await _db.GetAllAsync<Vehicle>();
//        var cargos = await _db.GetAllAsync<CargoType>();
//        var destinations = await _db.GetAllAsync<Destination>();
//        var sources = await _db.GetAllAsync<Source>();

//        var list = filtered.Select(r =>
//        {
//            var v = vehicles.FirstOrDefault(x => x.Id == r.VehicleId);
//            var c = cargos.FirstOrDefault(x => x.Id == r.CargoTypeId);
//            var fromDest = destinations.FirstOrDefault(x => x.Id == r.FromId);
//            var toDest = destinations.FirstOrDefault(x => x.Id == r.ToId);
//            var s = r.SourceId.HasValue ? sources.FirstOrDefault(x => x.Id == r.SourceId.Value) : null;

//            string cargoDisplay = c?.Name ?? "";
//            if (!string.IsNullOrEmpty(c?.Kind) || !string.IsNullOrEmpty(c?.Variety))
//                cargoDisplay = $"{c?.Name} | {c?.Kind} | {c?.Variety}";

//            return new HistoryRecordDto
//            {
//                Id = r.Id,
//                Timestamp = r.Timestamp,
//                VehicleName = v?.Name ?? "",
//                CargoDisplay = cargoDisplay,
//                NetWeight = (int)r.NetWeight,
//                FromName = fromDest?.Name ?? "",
//                ToName = toDest?.Name ?? "",
//                SourceName = s?.Name ?? "",
//                Comment = r.Comment ?? ""
//            };
//        });

//        // сортировка
//        list = SortAscending
//            ? list.OrderBy(x => x.GetField(CurrentSortField))
//            : list.OrderByDescending(x => x.GetField(CurrentSortField));

//        foreach (var item in list)
//            Records.Add(item);

//        OnPropertyChanged(nameof(TotalNetTons));
//    }

//    [RelayCommand]
//    private async Task SortAsync(string field)
//    {
//        if (CurrentSortField == field)
//            SortAscending = !SortAscending;
//        else
//        {
//            CurrentSortField = field;
//            SortAscending = true;
//        }
//        await ApplyFilterAsync();
//    }
//}

//public class HistoryRecordDto
//{
//    public Guid Id { get; set; }
//    public DateTime Timestamp { get; set; }
//    public string VehicleName { get; set; } = "";
//    public string CargoDisplay { get; set; } = "";
//    public int NetWeight { get; set; }
//    public string FromName { get; set; } = "";
//    public string ToName { get; set; } = "";
//    public string SourceName { get; set; } = "";
//    public string Comment { get; set; } = "";

//    public object? GetField(string field) => field switch
//    {
//        "Timestamp" => Timestamp,
//        "Vehicle" => VehicleName,
//        "Cargo" => CargoDisplay,
//        "NetWeight" => NetWeight,
//        "From" => FromName,
//        "To" => ToName,
//        "Source" => SourceName,
//        "Comment" => Comment,
//        _ => null
//    };
//}










//using System;
//using System.Collections.Generic;
//using System.Collections.ObjectModel;
//using System.Linq;
//using System.Threading.Tasks;
//using CommunityToolkit.Mvvm.ComponentModel;
//using CommunityToolkit.Mvvm.Input;
//using Microsoft.Maui.Controls;
//using ScaleRecordApp.Models;
//using ScaleRecordApp.Services;

//namespace ScaleRecordApp.ViewModels;

//public partial class HistoryVM : BaseViewModel
//{
//    private readonly DatabaseService _db;

//    // DTO, который отображаем в таблице
//    public class WeighingRecordDisplay
//    {
//        public Guid Id { get; set; }
//        public DateTime Timestamp { get; set; }
//        public string Vehicle { get; set; } = "";
//        public string Cargo { get; set; } = "";
//        public float NetWeight { get; set; }
//        public string From { get; set; } = "";
//        public string To { get; set; } = "";
//        public string Source { get; set; } = "";
//        public string Comment { get; set; } = "";
//        // Оригинал (если понадобится)
//        public WeighingRecord? Original { get; set; }
//    }

//    public ObservableCollection<WeighingRecordDisplay> Records { get; } = new();

//    // Внутренний список для сортировки/фильтрации
//    private List<WeighingRecordDisplay> _allRecords = new();

//    [ObservableProperty] private string totalNetWeightDisplay = "Общая масса (нетто): 0.000";

//    // Сортировка
//    private string _sortColumn = "Timestamp";
//    private bool _sortAscending = false;

//    public HistoryVM(DatabaseService db)
//    {
//        _db = db;
//    }

//    /// <summary>
//    /// Загрузка данных (включая справочники)
//    /// </summary>
//    [RelayCommand]
//    public async Task LoadAsync()
//    {
//        try
//        {
//            var records = (await _db.GetAllAsync<WeighingRecord>()).ToList();
//            var vehicles = (await _db.GetAllAsync<Vehicle>()).ToDictionary(x => x.Id, x => x);
//            var cargos = (await _db.GetAllAsync<CargoType>()).ToDictionary(x => x.Id, x => x);
//            var destinations = (await _db.GetAllAsync<Destination>()).ToDictionary(x => x.Id, x => x);
//            var sources = (await _db.GetAllAsync<Source>()).ToDictionary(x => x.Id, x => x);

//            _allRecords = records.Select(r =>
//            {
//                string vehicleName = vehicles.TryGetValue(r.VehicleId, out var v) ? v.Name : "";
//                string cargoName = "";
//                if (cargos.TryGetValue(r.CargoTypeId, out var c))
//                {
//                    // Формат: Name | Kind | Variety, если есть
//                    var parts = new List<string>();
//                    if (!string.IsNullOrWhiteSpace(c.Name)) parts.Add(c.Name);
//                    // Попробуем безопасно взять возможные свойства Kind и Variety (если их нет — просто пропустим)
//                    if (!string.IsNullOrWhiteSpace(c.Kind)) parts.Add(c.Kind);
//                    if (!string.IsNullOrWhiteSpace(c.Variety)) parts.Add(c.Variety);
//                    cargoName = string.Join(" | ", parts);
//                }

//                string fromName = destinations.TryGetValue(r.FromId, out var fd) ? fd.Name : "";
//                string toName = destinations.TryGetValue(r.ToId, out var td) ? td.Name : "";
//                string sourceName = r.SourceId.HasValue && sources.TryGetValue(r.SourceId.Value, out var s) ? s.Name : "";

//                return new WeighingRecordDisplay
//                {
//                    Id = r.Id,
//                    Timestamp = r.Timestamp,
//                    Vehicle = vehicleName,
//                    Cargo = cargoName,
//                    NetWeight = r.NetWeight,
//                    From = fromName,
//                    To = toName,
//                    Source = sourceName,
//                    Comment = r.Comment ?? "",
//                    Original = r
//                };
//            }).ToList();

//            // Применить начальную сортировку (по времени по убыванию)
//            _sortColumn = "Timestamp";
//            _sortAscending = false;
//            ApplySortingAndPublish();
//        }
//        catch (Exception ex)
//        {
//            System.Diagnostics.Debug.WriteLine(ex);
//            await Application.Current.MainPage.DisplayAlert("Ошибка", "Не удалось загрузить историю", "Ок");
//        }
//    }

//    private void ApplySortingAndPublish()
//    {
//        IEnumerable<WeighingRecordDisplay> q = _allRecords.AsEnumerable();
//        q = _sortColumn switch
//        {
//            "Timestamp" => _sortAscending ? q.OrderBy(x => x.Timestamp) : q.OrderByDescending(x => x.Timestamp),
//            "Vehicle" => _sortAscending ? q.OrderBy(x => x.Vehicle) : q.OrderByDescending(x => x.Vehicle),
//            "Cargo" => _sortAscending ? q.OrderBy(x => x.Cargo) : q.OrderByDescending(x => x.Cargo),
//            "NetWeight" => _sortAscending ? q.OrderBy(x => x.NetWeight) : q.OrderByDescending(x => x.NetWeight),
//            "From" => _sortAscending ? q.OrderBy(x => x.From) : q.OrderByDescending(x => x.From),
//            "To" => _sortAscending ? q.OrderBy(x => x.To) : q.OrderByDescending(x => x.To),
//            "Source" => _sortAscending ? q.OrderBy(x => x.Source) : q.OrderByDescending(x => x.Source),
//            "Comment" => _sortAscending ? q.OrderBy(x => x.Comment) : q.OrderByDescending(x => x.Comment),
//            _ => q
//        };

//        Records.Clear();
//        foreach (var item in q) Records.Add(item);

//        UpdateTotalNetWeight();
//    }

//    private void UpdateTotalNetWeight()
//    {
//        var sum = Records.Sum(r => r.NetWeight);
//        TotalNetWeightDisplay = $"Общая масса (нетто): {sum:F3}";
//    }

//    /// <summary>
//    /// Сортировка по колонке. При повторном нажатии меняет направление.
//    /// </summary>
//    [RelayCommand]
//    public void Sort(string column)
//    {
//        if (string.Equals(_sortColumn, column, StringComparison.OrdinalIgnoreCase))
//            _sortAscending = !_sortAscending;
//        else
//        {
//            _sortColumn = column;
//            // для времени удобно по-умолчанию делать DESC
//            _sortAscending = column != "Timestamp";
//        }

//        ApplySortingAndPublish();
//    }

//    /// <summary>
//    /// Нажатие по строке — показываем полные данные во всплывающем окне (комментарий и др.)
//    /// </summary>
//    [RelayCommand]
//    public async Task RowTapped(WeighingRecordDisplay row)
//    {
//        if (row == null) return;

//        // Формируем подробный текст
//        var details =
//$@"Дата: {row.Timestamp:dd.MM.yyyy HH:mm}
//Машина: {row.Vehicle}
//Груз: {row.Cargo}
//Нетто: {row.NetWeight:F3}
//Откуда: {row.From}
//Куда: {row.To}
//Источник: {row.Source}
//Комментарий: {row.Comment}";

//        await Application.Current.MainPage.DisplayAlert("Детали записи", details, "Ок");
//    }

//    [RelayCommand]
//    public async Task RefreshAsync()
//    {
//        await LoadAsync();
//    }

//    // Заготовка для экспорта (пока не реализовано)
//    [RelayCommand]
//    public async Task ExportAsync()
//    {
//        await Application.Current.MainPage.DisplayAlert("Экспорт", "Экспорт пока не реализован", "Ок");
//    }
//}
