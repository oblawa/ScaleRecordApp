using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScaleRecordApp.Models;
using ScaleRecordApp.Services;
using ScaleRecordApp.Views;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace ScaleRecordApp.ViewModels
{
    public partial class WeighingRecordVM : BaseViewModel
    {
        private readonly DatabaseService _db;

        // Preferences keys
        private const string Pref_LastCargoType = "Weighing.LastCargoTypeId";
        private const string Pref_LastFrom = "Weighing.LastFromId";
        private const string Pref_LastTo = "Weighing.LastToId"; 
        private const string Pref_LastSource = "Weighing.LastSourceId";
        private const string Pref_LastSeason = "Weighing.LastSeasonId";
        private const string Pref_LastComment = "Weighing.LastComment";

        // Флаг, чтобы НЕ сохранять в Preferences во время программного восстановления
        private bool _suppressPersistence = false;

        // Коллекции для Picker'ов
        public ObservableCollection<Vehicle> Vehicles { get; } = new();
        public ObservableCollection<CargoType> CargoTypes { get; } = new();
        public ObservableCollection<Destination> Destinations { get; } = new();
        public ObservableCollection<Source> Sources { get; } = new();
        public ObservableCollection<Season> Seasons { get; } = new();

        public ObservableCollection<Destination> FilteredDestinations { get; } = new();




        // Выбранные элементы
        [ObservableProperty] private Vehicle? selectedVehicle;
        [ObservableProperty] private CargoType? selectedCargoType;
        [ObservableProperty] private Destination? selectedFrom;
        [ObservableProperty] private Destination? selectedTo;
        [ObservableProperty] private Source? selectedSource;
        [ObservableProperty] private Season? selectedSeason;

        // Веса
        [ObservableProperty] private float grossWeight;
        [ObservableProperty] private float tareWeight;
        [ObservableProperty] private float netWeight;

        // Комментарий
        [ObservableProperty] private string? comment;

        public WeighingRecordVM(DatabaseService db)
        {
            _db = db;
        }

        partial void OnSelectedVehicleChanged(Vehicle? value)
        {
            Debug.WriteLine($"VM.SelectedVehicle changed -> {value?.Id} / {value?.Name}");
            if (value != null)
                TareWeight = value.TareWeight; // авто-подстановка тары
        }

        partial void OnGrossWeightChanged(float value) => RecalculateNet();
        partial void OnTareWeightChanged(float value) => RecalculateNet();

        private void RecalculateNet() => NetWeight = GrossWeight - TareWeight;

        // --- автоматическое сохранение выбранных справочников/комментария в Preferences ---
        partial void OnSelectedCargoTypeChanged(CargoType? value)
        {
            if (_suppressPersistence) return;
            try { Preferences.Set(Pref_LastCargoType, value?.Id.ToString() ?? string.Empty); } catch { }
        }

        partial void OnSelectedFromChanged(Destination? value)
        {
            if (_suppressPersistence) return;
            try { Preferences.Set(Pref_LastFrom, value?.Id.ToString() ?? string.Empty); } catch { }
        }

        partial void OnSelectedToChanged(Destination? value)
        {
            if (_suppressPersistence) return;
            try { Preferences.Set(Pref_LastTo, value?.Id.ToString() ?? string.Empty); } catch { }
        }

        partial void OnSelectedSourceChanged(Source? value)
        {
            if (_suppressPersistence) return;
            try { Preferences.Set(Pref_LastSource, value?.Id.ToString() ?? string.Empty); } catch { }
        }

        partial void OnSelectedSeasonChanged(Season? value)
        {
            if (_suppressPersistence) return;
            try { Preferences.Set(Pref_LastSeason, value?.Id.ToString() ?? string.Empty); } catch { }
        }

        partial void OnCommentChanged(string? value)
        {
            if (_suppressPersistence) return;
            try { Preferences.Set(Pref_LastComment, value ?? string.Empty); } catch { }
        }

        //[RelayCommand]
        //public async Task LoadDataAsync()
        //{
        //    try
        //    {
        //        var vehicles = await _db.GetAllAsync<Vehicle>();
        //        var cargos = await _db.GetAllAsync<CargoType>();
        //        var destinations = await _db.GetAllAsync<Destination>();
        //        var sources = await _db.GetAllAsync<Source>();
        //        var seasons = await _db.GetAllAsync<Season>();

        //        Vehicles.Clear(); foreach (var v in vehicles) Vehicles.Add(v);
        //        CargoTypes.Clear(); foreach (var c in cargos) CargoTypes.Add(c);
        //        Sources.Clear(); foreach (var s in sources) Sources.Add(s);
        //        Seasons.Clear(); foreach (var s in seasons) Seasons.Add(s);
        //        // Откуда/куда — общий список
        //        Destinations.Clear();
        //        foreach (var d in destinations.OrderBy(x => x.Name))
        //            Destinations.Add(d);

        //        // Для "Куда" — отдельный отфильтрованный список (если используешь FilteredDestinations)
        //        FilteredDestinations.Clear();
        //        foreach (var d in destinations
        //                            .Where(x => !x.Name.Contains("поле", StringComparison.OrdinalIgnoreCase))
        //                            .OrderBy(x => x.Name))
        //            FilteredDestinations.Add(d);



        //        // Восстановление сохранённых значений (только если соответствующие элементы есть в коллекциях)
        //        _suppressPersistence = true;
        //        try
        //        {
        //            // CargoType
        //            var cargoStr = Preferences.Get(Pref_LastCargoType, "");
        //            if (Guid.TryParse(cargoStr, out var cargoId))
        //            {
        //                var cargo = CargoTypes.FirstOrDefault(c => c.Id == cargoId);
        //                if (cargo != null) SelectedCargoType = cargo;
        //            }

        //            // From
        //            var fromStr = Preferences.Get(Pref_LastFrom, "");
        //            if (Guid.TryParse(fromStr, out var fromId))
        //            {
        //                var from = Destinations.FirstOrDefault(d => d.Id == fromId);
        //                if (from != null) SelectedFrom = from;
        //            }

        //            // To
        //            var toStr = Preferences.Get(Pref_LastTo, "");
        //            if (Guid.TryParse(toStr, out var toId))
        //            {
        //                var to = Destinations.FirstOrDefault(d => d.Id == toId);
        //                if (to != null) SelectedTo = to;
        //            }

        //            // Source
        //            var srcStr = Preferences.Get(Pref_LastSource, "");
        //            if (Guid.TryParse(srcStr, out var srcId))
        //            {
        //                var src = Sources.FirstOrDefault(s => s.Id == srcId);
        //                if (src != null) SelectedSource = src;
        //            }

        //            // Season
        //            var seasonStr = Preferences.Get(Pref_LastSeason, "");
        //            if (Guid.TryParse(seasonStr, out var seasonId))
        //            {
        //                var season = Seasons.FirstOrDefault(s => s.Id == seasonId);
        //                if (season != null) SelectedSeason = season;
        //            }

        //            // Если сезона нет и список не пуст — сохранить прежнее поведение: первый элемент
        //            if (SelectedSeason is null && Seasons.Count > 0)
        //                SelectedSeason = Seasons
        //                                    .OrderByDescending(s => s.CreatedAt)
        //                                    .FirstOrDefault();


        //            // Комментарий
        //            Comment = Preferences.Get(Pref_LastComment, Comment ?? string.Empty);
        //        }
        //        finally
        //        {
        //            _suppressPersistence = false;
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        Debug.WriteLine(ex);
        //        await Application.Current.MainPage.DisplayAlert("Ошибка", "Не удалось загрузить данные", "Ок");
        //    }
        //}

        [RelayCommand]
        public async Task SaveAsync()
        {
            // Обязательные поля
            if (SelectedVehicle == null || SelectedCargoType == null ||
                SelectedFrom == null || SelectedTo == null || SelectedSeason == null)
            {
                await Application.Current.MainPage.DisplayAlert("Ошибка", "Заполните все обязательные поля", "Ок");
                return;
            }

            // Простая валидация весов
            if (GrossWeight <= 0)
            {
                await Application.Current.MainPage.DisplayAlert("Ошибка", "Вес брутто должен быть больше нуля", "Ок");
                return;
            }
            if (TareWeight < 0)
            {
                await Application.Current.MainPage.DisplayAlert("Ошибка", "Вес тары не может быть отрицательным", "Ок");
                return;
            }
            if (NetWeight < 0)
            {
                await Application.Current.MainPage.DisplayAlert("Ошибка", "Вес нетто не может быть отрицательным (проверьте брутто/тару)", "Ок");
                return;
            }

            var record = new WeighingRecord
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTime.Now,
                VehicleId = SelectedVehicle.Id,
                CargoTypeId = SelectedCargoType.Id,
                FromId = SelectedFrom.Id,
                ToId = SelectedTo.Id,
                SourceId = SelectedSource?.Id,
                SeasonId = SelectedSeason.Id,
                GrossWeight = GrossWeight,
                TareWeight = TareWeight,
                NetWeight = NetWeight,
                Comment = Comment
            };

            try
            {
                await _db.InsertAsync(record);
                await Application.Current.MainPage.DisplayAlert("Успех", "Запись сохранена", "Ок");

                // Лёгкий сброс полей (оставляю выбранные справочники и комментарий — они должны сохраняться)
                GrossWeight = 0;
                NetWeight = 0;
                // Comment = string.Empty; // убираем очистку комментария — он должен сохраняться
                SelectedVehicle = null;
                // Если хочешь сохранять авто-тару от машины — закомментируй следующую строку
                TareWeight = 0;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                await Application.Current.MainPage.DisplayAlert("Ошибка", "Не удалось сохранить запись", "Ок");
            }
        }

        [RelayCommand]
        public async Task AddVehicleAsync()
        {
            await Application.Current.MainPage.Navigation.PushAsync(new AddVehiclePage(new AddVehicleVM(_db)));
        }

        [RelayCommand]
        public async Task AddCargoAsync()
        {
            await Application.Current.MainPage.Navigation.PushAsync(new AddCargoPage(new AddCargoVM(_db)));
        }

        // Разделённые команды добавления направлений, чтобы корректно выбирать From/To
        // --- метод обновления фильтрованного списка ---
        private void RefreshFilteredDestinations()
        {
            FilteredDestinations.Clear();
            foreach (var d in Destinations
                                .Where(x => !x.Name.Contains("поле", StringComparison.OrdinalIgnoreCase))
                                .OrderBy(x => x.Name))
            {
                FilteredDestinations.Add(d);
            }
        }
        [RelayCommand]
        public async Task LoadDataAsync()
        {
            try
            {
                var vehicles = await _db.GetAllAsync<Vehicle>();
                var cargos = await _db.GetAllAsync<CargoType>();
                var destinations = await _db.GetAllAsync<Destination>();
                var sources = await _db.GetAllAsync<Source>();
                var seasons = await _db.GetAllAsync<Season>();

                Vehicles.Clear(); foreach (var v in vehicles) Vehicles.Add(v);
                CargoTypes.Clear(); foreach (var c in cargos) CargoTypes.Add(c);
                Sources.Clear(); foreach (var s in sources) Sources.Add(s);
                Seasons.Clear(); foreach (var s in seasons) Seasons.Add(s);

                // Откуда/куда — общий список
                Destinations.Clear();
                foreach (var d in destinations.OrderBy(x => x.Name))
                    Destinations.Add(d);

                // Обновляем фильтрованный список
                RefreshFilteredDestinations();

                // Восстановление сохранённых значений
                _suppressPersistence = true;
                try
                {
                    // CargoType
                    var cargoStr = Preferences.Get(Pref_LastCargoType, "");
                    if (Guid.TryParse(cargoStr, out var cargoId))
                    {
                        var cargo = CargoTypes.FirstOrDefault(c => c.Id == cargoId);
                        if (cargo != null) SelectedCargoType = cargo;
                    }

                    // From
                    var fromStr = Preferences.Get(Pref_LastFrom, "");
                    if (Guid.TryParse(fromStr, out var fromId))
                    {
                        var from = Destinations.FirstOrDefault(d => d.Id == fromId);
                        if (from != null) SelectedFrom = from;
                    }

                    // To
                    var toStr = Preferences.Get(Pref_LastTo, "");
                    if (Guid.TryParse(toStr, out var toId))
                    {
                        var to = Destinations.FirstOrDefault(d => d.Id == toId);
                        if (to != null) SelectedTo = to;
                    }

                    // Source
                    var srcStr = Preferences.Get(Pref_LastSource, "");
                    if (Guid.TryParse(srcStr, out var srcId))
                    {
                        var src = Sources.FirstOrDefault(s => s.Id == srcId);
                        if (src != null) SelectedSource = src;
                    }

                    // Season
                    var seasonStr = Preferences.Get(Pref_LastSeason, "");
                    if (Guid.TryParse(seasonStr, out var seasonId))
                    {
                        var season = Seasons.FirstOrDefault(s => s.Id == seasonId);
                        if (season != null) SelectedSeason = season;
                    }

                    // Если сезона нет и список не пуст — взять последний по CreatedAt
                    if (SelectedSeason is null && Seasons.Count > 0)
                        SelectedSeason = Seasons
                                            .OrderByDescending(s => s.CreatedAt)
                                            .FirstOrDefault();

                    // Комментарий
                    Comment = Preferences.Get(Pref_LastComment, Comment ?? string.Empty);
                }
                finally
                {
                    _suppressPersistence = false;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                await Application.Current.MainPage.DisplayAlert("Ошибка", "Не удалось загрузить данные", "Ок");
            }
        }

        [RelayCommand]
        public async Task AddFromDestinationAsync()
        {
            var name = await Application.Current.MainPage.DisplayPromptAsync("Добавить место (Откуда)", "Введите название");
            if (!string.IsNullOrWhiteSpace(name))
            {
                var dest = new Destination { Id = Guid.NewGuid(), Name = name };
                await _db.InsertAsync(dest);
                Destinations.Add(dest);

                // обновляем фильтрованный список
                RefreshFilteredDestinations();

                SelectedFrom = dest;
            }
        }

        [RelayCommand]
        public async Task AddToDestinationAsync()
        {
            var name = await Application.Current.MainPage.DisplayPromptAsync("Добавить место (Куда)", "Введите название");
            if (!string.IsNullOrWhiteSpace(name))
            {
                var dest = new Destination { Id = Guid.NewGuid(), Name = name };
                await _db.InsertAsync(dest);
                Destinations.Add(dest);

                // обновляем фильтрованный список
                RefreshFilteredDestinations();

                SelectedTo = dest;
            }
        }
        //[RelayCommand]
        //public async Task AddFromDestinationAsync()
        //{
        //    var name = await Application.Current.MainPage.DisplayPromptAsync("Добавить место (Откуда)", "Введите название");
        //    if (!string.IsNullOrWhiteSpace(name))
        //    {
        //        var dest = new Destination { Id = Guid.NewGuid(), Name = name };
        //        await _db.InsertAsync(dest);
        //        Destinations.Add(dest);
        //        SelectedFrom = dest;
        //    }
        //}

        //[RelayCommand]
        //public async Task AddToDestinationAsync()
        //{
        //    var name = await Application.Current.MainPage.DisplayPromptAsync("Добавить место (Куда)", "Введите название");
        //    if (!string.IsNullOrWhiteSpace(name))
        //    {
        //        var dest = new Destination { Id = Guid.NewGuid(), Name = name };
        //        await _db.InsertAsync(dest);
        //        Destinations.Add(dest);
        //        SelectedTo = dest;
        //    }
        //}

        [RelayCommand]
        public async Task AddSourceAsync()
        {
            var name = await Application.Current.MainPage.DisplayPromptAsync("Добавить источник", "Введите название");
            if (!string.IsNullOrWhiteSpace(name))
            {
                var src = new Source { Id = Guid.NewGuid(), Name = name };
                await _db.InsertAsync(src);
                Sources.Add(src);
                SelectedSource = src;
            }
        }

        [RelayCommand]
        public async Task AddSeasonAsync()
        {
            var name = await Application.Current.MainPage.DisplayPromptAsync("Добавить сезон", "Введите название сезона");
            if (!string.IsNullOrWhiteSpace(name))
            {
                var season = new Season { Id = Guid.NewGuid(), Name = name };
                await _db.InsertAsync(season);
                Seasons.Add(season);
                SelectedSeason = season;
            }
        }
    }
}





























//using System;
//using System.Collections.ObjectModel;
//using System.Diagnostics;
//using System.Threading.Tasks;
//using CommunityToolkit.Mvvm.ComponentModel;
//using CommunityToolkit.Mvvm.Input;
//using Microsoft.Maui.Controls;
//using ScaleRecordApp.Models;
//using ScaleRecordApp.Services;
//using ScaleRecordApp.Views;

//namespace ScaleRecordApp.ViewModels
//{
//    public partial class WeighingRecordVM : BaseViewModel
//    {
//        private readonly DatabaseService _db;

//        // Коллекции для Picker'ов
//        public ObservableCollection<Vehicle> Vehicles { get; } = new();
//        public ObservableCollection<CargoType> CargoTypes { get; } = new();
//        public ObservableCollection<Destination> Destinations { get; } = new();
//        public ObservableCollection<Source> Sources { get; } = new();
//        public ObservableCollection<Season> Seasons { get; } = new();

//        // Выбранные элементы
//        [ObservableProperty] private Vehicle? selectedVehicle;
//        [ObservableProperty] private CargoType? selectedCargoType;
//        [ObservableProperty] private Destination? selectedFrom;
//        [ObservableProperty] private Destination? selectedTo;
//        [ObservableProperty] private Source? selectedSource;
//        [ObservableProperty] private Season? selectedSeason;

//        // Веса
//        [ObservableProperty] private float grossWeight;
//        [ObservableProperty] private float tareWeight;
//        [ObservableProperty] private float netWeight;

//        // Комментарий
//        [ObservableProperty] private string? comment;

//        public WeighingRecordVM(DatabaseService db)
//        {
//            _db = db;
//        }

//        partial void OnSelectedVehicleChanged(Vehicle? value)
//        {
//            Debug.WriteLine($"VM.SelectedVehicle changed -> {value?.Id} / {value?.Name}");
//            if (value != null)
//                TareWeight = value.TareWeight; // авто-подстановка тары
//        }

//        partial void OnGrossWeightChanged(float value) => RecalculateNet();
//        partial void OnTareWeightChanged(float value) => RecalculateNet();

//        private void RecalculateNet() => NetWeight = GrossWeight - TareWeight;

//        [RelayCommand]
//        public async Task LoadDataAsync()
//        {
//            try
//            {
//                var vehicles = await _db.GetAllAsync<Vehicle>();
//                var cargos = await _db.GetAllAsync<CargoType>();
//                var destinations = await _db.GetAllAsync<Destination>();
//                var sources = await _db.GetAllAsync<Source>();
//                var seasons = await _db.GetAllAsync<Season>();

//                Vehicles.Clear(); foreach (var v in vehicles) Vehicles.Add(v);
//                CargoTypes.Clear(); foreach (var c in cargos) CargoTypes.Add(c);
//                Destinations.Clear(); foreach (var d in destinations) Destinations.Add(d);
//                Sources.Clear(); foreach (var s in sources) Sources.Add(s);
//                Seasons.Clear(); foreach (var s in seasons) Seasons.Add(s);

//                if (SelectedSeason is null && Seasons.Count > 0)
//                    SelectedSeason = Seasons[0];
//            }
//            catch (Exception ex)
//            {
//                Debug.WriteLine(ex);
//                await Application.Current.MainPage.DisplayAlert("Ошибка", "Не удалось загрузить данные", "Ок");
//            }
//        }

//        [RelayCommand]
//        public async Task SaveAsync()
//        {
//            // Обязательные поля
//            if (SelectedVehicle == null || SelectedCargoType == null ||
//                SelectedFrom == null || SelectedTo == null || SelectedSeason == null)
//            {
//                await Application.Current.MainPage.DisplayAlert("Ошибка", "Заполните все обязательные поля", "Ок");
//                return;
//            }

//            // Простая валидация весов
//            if (GrossWeight <= 0)
//            {
//                await Application.Current.MainPage.DisplayAlert("Ошибка", "Вес брутто должен быть больше нуля", "Ок");
//                return;
//            }
//            if (TareWeight < 0)
//            {
//                await Application.Current.MainPage.DisplayAlert("Ошибка", "Вес тары не может быть отрицательным", "Ок");
//                return;
//            }
//            if (NetWeight < 0)
//            {
//                await Application.Current.MainPage.DisplayAlert("Ошибка", "Вес нетто не может быть отрицательным (проверьте брутто/тару)", "Ок");
//                return;
//            }

//            var record = new WeighingRecord
//            {
//                Id = Guid.NewGuid(),
//                Timestamp = DateTime.Now,
//                VehicleId = SelectedVehicle.Id,
//                CargoTypeId = SelectedCargoType.Id,
//                FromId = SelectedFrom.Id,
//                ToId = SelectedTo.Id,
//                SourceId = SelectedSource?.Id,
//                SeasonId = SelectedSeason.Id,
//                GrossWeight = GrossWeight,
//                TareWeight = TareWeight,
//                NetWeight = NetWeight,
//                Comment = Comment
//            };

//            try
//            {
//                await _db.InsertAsync(record);
//                await Application.Current.MainPage.DisplayAlert("Успех", "Запись сохранена", "Ок");

//                // Лёгкий сброс полей (оставляю выбранные справочники)
//                GrossWeight = 0;
//                NetWeight = 0;
//                Comment = string.Empty;
//                SelectedVehicle = null;
//                // Если хочешь сохранять авто-тару от машины — закомментируй следующую строку
//                // TareWeight = 0;
//            }
//            catch (Exception ex)
//            {
//                Debug.WriteLine(ex);
//                await Application.Current.MainPage.DisplayAlert("Ошибка", "Не удалось сохранить запись", "Ок");
//            }
//        }

//        [RelayCommand]
//        public async Task AddVehicleAsync()
//        {
//            await Application.Current.MainPage.Navigation.PushAsync(new AddVehiclePage(new AddVehicleVM(_db)));
//        }

//        [RelayCommand]
//        public async Task AddCargoAsync()
//        {
//            await Application.Current.MainPage.Navigation.PushAsync(new AddCargoPage(new AddCargoVM(_db)));
//        }

//        // Разделённые команды добавления направлений, чтобы корректно выбирать From/To
//        [RelayCommand]
//        public async Task AddFromDestinationAsync()
//        {
//            var name = await Application.Current.MainPage.DisplayPromptAsync("Добавить место (Откуда)", "Введите название");
//            if (!string.IsNullOrWhiteSpace(name))
//            {
//                var dest = new Destination { Id = Guid.NewGuid(), Name = name };
//                await _db.InsertAsync(dest);
//                Destinations.Add(dest);
//                SelectedFrom = dest;
//            }
//        }

//        [RelayCommand]
//        public async Task AddToDestinationAsync()
//        {
//            var name = await Application.Current.MainPage.DisplayPromptAsync("Добавить место (Куда)", "Введите название");
//            if (!string.IsNullOrWhiteSpace(name))
//            {
//                var dest = new Destination { Id = Guid.NewGuid(), Name = name };
//                await _db.InsertAsync(dest);
//                Destinations.Add(dest);
//                SelectedTo = dest;
//            }
//        }

//        [RelayCommand]
//        public async Task AddSourceAsync()
//        {
//            var name = await Application.Current.MainPage.DisplayPromptAsync("Добавить источник", "Введите название");
//            if (!string.IsNullOrWhiteSpace(name))
//            {
//                var src = new Source { Id = Guid.NewGuid(), Name = name };
//                await _db.InsertAsync(src);
//                Sources.Add(src);
//                SelectedSource = src;
//            }
//        }

//        [RelayCommand]
//        public async Task AddSeasonAsync()
//        {
//            var name = await Application.Current.MainPage.DisplayPromptAsync("Добавить сезон", "Введите название сезона");
//            if (!string.IsNullOrWhiteSpace(name))
//            {
//                var season = new Season { Id = Guid.NewGuid(), Name = name };
//                await _db.InsertAsync(season);
//                Seasons.Add(season);
//                SelectedSeason = season;
//            }
//        }
//    }
//}


































//using CommunityToolkit.Mvvm.ComponentModel;
//using CommunityToolkit.Mvvm.Input;
//using ScaleRecordApp.Models;
//using ScaleRecordApp.Services;
//using ScaleRecordApp.Views;
//using System;
//using System.Collections.Generic;
//using System.Collections.ObjectModel;
//using System.Diagnostics;
//using System.Linq;
//using System.Text;
//using System.Text.RegularExpressions;
//using System.Threading.Tasks;

//namespace ScaleRecordApp.ViewModels
//{
//    public partial class WeighingRecordVM : BaseViewModel
//    {
//        private readonly DatabaseService _db;

//        // Коллекции для Picker'ов
//        public ObservableCollection<Vehicle> Vehicles { get; } = new();
//        public ObservableCollection<CargoType> CargoTypes { get; } = new();
//        public ObservableCollection<Destination> Destinations { get; } = new();
//        public ObservableCollection<Source> Sources { get; } = new();
//        public ObservableCollection<Season> Seasons { get; } = new();

//        // Выбранные элементы
//        [ObservableProperty] private Vehicle? selectedVehicle;
//        [ObservableProperty] private CargoType? selectedCargoType;
//        [ObservableProperty] private Destination? selectedFrom;
//        [ObservableProperty] private Destination? selectedTo;
//        [ObservableProperty] private Source? selectedSource;
//        [ObservableProperty] private Season? selectedSeason;

//        // Веса
//        [ObservableProperty] private float grossWeight;
//        [ObservableProperty] private float tareWeight;
//        [ObservableProperty] private float netWeight;

//        // Комментарий
//        [ObservableProperty] private string? comment;

//        public WeighingRecordVM(DatabaseService db)
//        {
//            _db = db;
//        }

//        partial void OnSelectedVehicleChanged(Vehicle? value)
//        {
//            Debug.WriteLine($"VM.SelectedVehicle changed -> {value?.Id} / {value?.Name}");
//            if (value != null)
//                TareWeight = value.TareWeight; // авто-подстановка тары
//        }

//        partial void OnGrossWeightChanged(float value) => RecalculateNet();
//        partial void OnTareWeightChanged(float value) => RecalculateNet();

//        private void RecalculateNet() => NetWeight = GrossWeight - TareWeight;

//        [RelayCommand]
//        public async Task LoadDataAsync()
//        {
//            var vehicles = await _db.GetAllAsync<Vehicle>();
//            var cargos = await _db.GetAllAsync<CargoType>();
//            var destinations = await _db.GetAllAsync<Destination>();
//            var sources = await _db.GetAllAsync<Source>();
//            var seasons = await _db.GetAllAsync<Season>();

//            Vehicles.Clear(); foreach (var v in vehicles) Vehicles.Add(v);
//            CargoTypes.Clear(); foreach (var c in cargos) CargoTypes.Add(c);
//            Destinations.Clear(); foreach (var d in destinations) Destinations.Add(d);
//            Sources.Clear(); foreach (var s in sources) Sources.Add(s);
//            Seasons.Clear(); foreach (var s in seasons) Seasons.Add(s);
//        }

//        [RelayCommand]
//        public async Task SaveAsync()
//        {
//            if (SelectedVehicle == null || SelectedCargoType == null ||
//                SelectedFrom == null || SelectedTo == null || SelectedSeason == null)
//            {
//                await Application.Current.MainPage.DisplayAlert("Ошибка", "Заполните все обязательные поля", "Ок");
//                return;
//            }

//            var record = new WeighingRecord
//            {
//                Id = Guid.NewGuid(),
//                VehicleId = SelectedVehicle.Id,
//                CargoTypeId = SelectedCargoType.Id,
//                FromId = SelectedFrom.Id,
//                ToId = SelectedTo.Id,
//                SourceId = SelectedSource?.Id,
//                SeasonId = SelectedSeason.Id,
//                GrossWeight = GrossWeight,
//                TareWeight = TareWeight,
//                NetWeight = NetWeight,
//                Comment = Comment
//            };

//            await _db.InsertAsync(record);
//            await Application.Current.MainPage.DisplayAlert("Успех", "Запись сохранена", "Ок");
//        }

//        [RelayCommand]
//        public async Task AddVehicleAsync()
//        {
//            await Application.Current.MainPage.Navigation.PushAsync(new AddVehiclePage(new AddVehicleVM(_db)));
//        }

//        [RelayCommand]
//        public async Task AddCargoAsync()
//        {
//            await Application.Current.MainPage.Navigation.PushAsync(new AddCargoPage(new AddCargoVM(_db)));
//        }

//        [RelayCommand]
//        public async Task AddDestinationAsync()
//        {
//            var name = await Application.Current.MainPage.DisplayPromptAsync("Добавить место", "Введите название");
//            if (!string.IsNullOrWhiteSpace(name))
//            {
//                var dest = new Destination { Id = Guid.NewGuid(), Name = name };
//                await _db.InsertAsync(dest);
//                Destinations.Add(dest);
//                SelectedTo = dest;
//            }
//        }

//        [RelayCommand]
//        public async Task AddSourceAsync()
//        {
//            var name = await Application.Current.MainPage.DisplayPromptAsync("Добавить источник", "Введите название");
//            if (!string.IsNullOrWhiteSpace(name))
//            {
//                var src = new Source { Id = Guid.NewGuid(), Name = name };
//                await _db.InsertAsync(src);
//                Sources.Add(src);
//                SelectedSource = src;
//            }
//        }
//    }



//public partial class WeighingRecordVM : BaseViewModel
//{
//    private readonly DatabaseService _db;

//    // Коллекции для Picker'ов
//    public ObservableCollection<Vehicle> Vehicles { get; } = new();
//    public ObservableCollection<CargoType> CargoTypes { get; } = new();
//    public ObservableCollection<Destination> Destinations { get; } = new();
//    public ObservableCollection<Source> Sources { get; } = new();
//    public ObservableCollection<Season> Seasons { get; } = new();

//    // Выбранные элементы
//    [ObservableProperty] private Vehicle? selectedVehicle;
//    [ObservableProperty] private CargoType? selectedCargoType;
//    [ObservableProperty] private Destination? selectedFrom;
//    [ObservableProperty] private Destination? selectedTo;
//    [ObservableProperty] private Source? selectedSource;
//    [ObservableProperty] private Season? selectedSeason;

//    // Веса
//    [ObservableProperty] private float grossWeight;
//    [ObservableProperty] private float tareWeight;
//    [ObservableProperty] private float netWeight;

//    // Комментарий
//    [ObservableProperty] private string? comment;

//    public WeighingRecordVM(DatabaseService db)
//    {
//        _db = db;
//    }

//    [RelayCommand]
//    public async Task LoadDataAsync()
//    {
//        // Загружаем данные из БД
//        var vehicles = await _db.GetAllAsync<Vehicle>();
//        var cargos = await _db.GetAllAsync<CargoType>();
//        var destinations = await _db.GetAllAsync<Destination>();
//        var sources = await _db.GetAllAsync<Source>();
//        var seasons = await _db.GetAllAsync<Season>();

//        // Обновляем коллекции
//        Vehicles.Clear(); foreach (var v in vehicles) Vehicles.Add(v);
//        CargoTypes.Clear(); foreach (var c in cargos) CargoTypes.Add(c);
//        Destinations.Clear(); foreach (var d in destinations) Destinations.Add(d);
//        Sources.Clear(); foreach (var s in sources) Sources.Add(s);
//        Seasons.Clear(); foreach (var s in seasons) Seasons.Add(s);
//    }

//    [RelayCommand]
//    public async Task SaveAsync()
//    {
//        if (SelectedVehicle == null || SelectedCargoType == null ||
//            SelectedFrom == null || SelectedTo == null || SelectedSeason == null)
//        {
//            await Application.Current.MainPage.DisplayAlert("Ошибка", "Заполните все обязательные поля", "Ок");
//            return;
//        }

//        var record = new WeighingRecord
//        {
//            Id = Guid.NewGuid(),
//            VehicleId = SelectedVehicle.Id,
//            CargoTypeId = SelectedCargoType.Id,
//            FromId = SelectedFrom.Id,
//            ToId = SelectedTo.Id,
//            SourceId = SelectedSource?.Id,
//            SeasonId = SelectedSeason.Id,
//            GrossWeight = GrossWeight,
//            TareWeight = TareWeight,
//            NetWeight = GrossWeight - TareWeight,
//            Comment = Comment
//        };

//        await _db.InsertAsync(record);
//        await Application.Current.MainPage.DisplayAlert("Успех", "Запись сохранена", "Ок");
//    }

//    [RelayCommand]
//    public async Task AddDestinationAsync()
//    {
//        var name = await Application.Current.MainPage.DisplayPromptAsync("Добавить место", "Введите название");
//        if (!string.IsNullOrWhiteSpace(name))
//        {
//            var dest = new Destination { Id = Guid.NewGuid(), Name = name };
//            await _db.InsertAsync(dest);
//            Destinations.Add(dest);
//            SelectedTo = dest; // можно сразу выбрать новое
//        }
//    }

//    [RelayCommand]
//    public async Task AddSourceAsync()
//    {
//        var name = await Application.Current.MainPage.DisplayPromptAsync("Добавить источник", "Введите название");
//        if (!string.IsNullOrWhiteSpace(name))
//        {
//            var src = new Source { Id = Guid.NewGuid(), Name = name };
//            await _db.InsertAsync(src);
//            Sources.Add(src);
//            SelectedSource = src;
//        }
//    }
//}
//}
