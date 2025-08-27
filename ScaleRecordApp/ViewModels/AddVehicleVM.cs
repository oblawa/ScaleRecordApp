using CommunityToolkit.Mvvm;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScaleRecordApp.Models;
using ScaleRecordApp.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace ScaleRecordApp.ViewModels
{
    //public partial class AddVehicleVM : BaseViewModel
    //{
    //    private readonly DatabaseService _db;


    //    [ObservableProperty]
    //    public ObservableCollection<VehicleItemVM> vehicleItems = new();

    //    [ObservableProperty]
    //    private VehicleItemVM selectedVehicleItemVM;

    //    [ObservableProperty]
    //    private string formTitle = "Добавить новую тару";

    //    [ObservableProperty]
    //    public string name;

    //    [ObservableProperty]
    //    public string number;

    //    [ObservableProperty]
    //    public string tareWeight;

    //    [ObservableProperty]
    //    public string description;
    //    public AddVehicleVM(DatabaseService db) 
    //    {
    //        _db = db;
    //    }

    //    [RelayCommand(CanExecute = nameof(CanSaveVehicle))]
    //    public async Task SaveVehicleAsync()
    //    {
    //        if (SelectedVehicleItemVM?.Vehicle.Id == Guid.Empty || SelectedVehicleItemVM == null)
    //        {
    //            // Добавление
    //            var vehicle = new Vehicle
    //            {
    //                Id = Guid.NewGuid(),
    //                Name = Name,
    //                Number = Number,
    //                TareWeight = float.Parse(TareWeight),
    //                Description = Description
    //            };
    //            await _db.InsertAsync(vehicle);

    //            await Application.Current.MainPage.DisplayAlert(
    //                "Успешно",
    //                $"Новая машина '{vehicle.Name}' добавлена.",
    //                "ОК");
    //        }
    //        else
    //        {
    //            // Обновление
    //            SelectedVehicleItemVM.Vehicle.Name = Name;
    //            SelectedVehicleItemVM.Vehicle.Number = Number;
    //            SelectedVehicleItemVM.Vehicle.TareWeight = float.Parse(TareWeight);
    //            SelectedVehicleItemVM.Vehicle.Description = Description;
    //            await _db.UpdateAsync(SelectedVehicleItemVM.Vehicle);


    //            await Application.Current.MainPage.DisplayAlert(
    //                "Успешно",
    //                $"Машина '{SelectedVehicleItemVM.Vehicle.Name}' обновлена.",
    //                "ОК");
    //        }

    //        await LoadVehiclesAsync();
    //        SelectedVehicleItemVM = VehicleItems.FirstOrDefault(c => c.Vehicle.Id == Guid.Empty);
    //    }

    //    [RelayCommand]
    //    public async Task LoadVehiclesAsync()
    //    {
    //        var list = await _db.GetAllAsync<Vehicle>();

    //        VehicleItems = new ObservableCollection<VehicleItemVM>();

    //        VehicleItems.Add(new VehicleItemVM
    //        {
    //            Vehicle = new Vehicle { Id = Guid.Empty, Name = "+ Добавить новую тару" },
    //            Index = 0
    //        });

    //        int i = 1;
    //        foreach (var v in list)
    //        {
    //            VehicleItems.Add(new VehicleItemVM { Vehicle = v, Index = i++ });
    //        }
    //    }

    //    partial void OnSelectedVehicleItemVMChanged(VehicleItemVM value)
    //    {
    //        if (value == null) return;

    //        if (value.Vehicle.Id == Guid.Empty) // это "новая тара"
    //        {
    //            FormTitle = "Добавить новую тару";
    //            Name = Number = TareWeight = Description = string.Empty;
    //        }
    //        else // редактирование существующей
    //        {
    //            FormTitle = "Изменение тары";
    //            Name = value.Vehicle.Name;
    //            Number = value.Vehicle.Number;
    //            TareWeight = value.Vehicle.TareWeight.ToString();
    //            Description = value.Vehicle.Description;
    //        }
    //    }

    //    //private bool CanSaveVehicle()
    //    //{
    //    //    return !String.IsNullOrWhiteSpace(Name) && !String.IsNullOrWhiteSpace(TareWeight);
    //    //}
    //    private bool CanSaveVehicle()
    //    {
    //        // Если добавляем новую машину
    //        if (SelectedVehicleItemVM == null || SelectedVehicleItemVM.Vehicle.Id == Guid.Empty)
    //        {
    //            return !string.IsNullOrWhiteSpace(Name) &&
    //                   !string.IsNullOrWhiteSpace(TareWeight);
    //        }

    //        // Если редактируем существующую
    //        var vehicle = SelectedVehicleItemVM.Vehicle;
    //        return
    //            !string.IsNullOrWhiteSpace(Name) &&
    //            !string.IsNullOrWhiteSpace(TareWeight) &&
    //            (
    //                Name != vehicle.Name ||
    //                Number != vehicle.Number ||
    //                Description != vehicle.Description ||
    //                (float.TryParse(TareWeight, out var tare) && tare != vehicle.TareWeight)
    //            );
    //    }

    //    partial void OnNameChanged(string oldValue, string newValue) => SaveVehicleCommand.NotifyCanExecuteChanged();
    //    partial void OnNumberChanged(string oldValue, string newValue) => SaveVehicleCommand.NotifyCanExecuteChanged();
    //    partial void OnTareWeightChanged(string oldValue, string newValue) => SaveVehicleCommand.NotifyCanExecuteChanged();
    //    partial void OnDescriptionChanged(string oldValue, string newValue) => SaveVehicleCommand.NotifyCanExecuteChanged();

    //}
    //public class VehicleItemVM
    //{
    //    public Vehicle Vehicle { get; set; }
    //    public int Index { get; set; }
    //}
    public partial class AddVehicleVM : BaseViewModel
    {
        private readonly DatabaseService _db;

        [ObservableProperty]
        public ObservableCollection<VehicleItemVM> vehicleItems = new();

        [ObservableProperty]
        private VehicleItemVM selectedVehicleItemVM;

        [ObservableProperty]
        private string formTitle = "Добавить новую машину";

        [ObservableProperty]
        public string name;

        [ObservableProperty]
        public string number;

        [ObservableProperty]
        public string tareWeight;

        [ObservableProperty]
        public string description;

        [ObservableProperty]
        private bool isDeleteVisible = false;

        public AddVehicleVM(DatabaseService db)
        {
            _db = db;
        }

        [RelayCommand(CanExecute = nameof(CanSaveVehicle))]
        public async Task SaveVehicleAsync()
        {
            if (SelectedVehicleItemVM?.Vehicle.Id == Guid.Empty || SelectedVehicleItemVM == null)
            {
                // Добавление
                var vehicle = new Vehicle
                {
                    Id = Guid.NewGuid(),
                    Name = Name,
                    Number = Number,
                    TareWeight = float.Parse(TareWeight),
                    Description = Description
                };
                await _db.InsertAsync(vehicle);

                await Application.Current.MainPage.DisplayAlert(
                    "Успешно",
                    $"Новая машина '{vehicle.Name}' добавлена.",
                    "ОК");
            }
            else
            {
                // Обновление
                SelectedVehicleItemVM.Vehicle.Name = Name;
                SelectedVehicleItemVM.Vehicle.Number = Number;
                SelectedVehicleItemVM.Vehicle.TareWeight = float.Parse(TareWeight);
                SelectedVehicleItemVM.Vehicle.Description = Description;

                await _db.UpdateAsync(SelectedVehicleItemVM.Vehicle);

                await Application.Current.MainPage.DisplayAlert(
                    "Успешно",
                    $"Машина '{SelectedVehicleItemVM.Vehicle.Name}' обновлена.",
                    "ОК");
            }

            await LoadVehiclesAsync();
            SelectedVehicleItemVM = VehicleItems.FirstOrDefault(c => c.Vehicle.Id == Guid.Empty);
        }

        [RelayCommand(CanExecute = nameof(CanDeleteVehicle))]
        public async Task DeleteVehicleAsync()
        {
            if (SelectedVehicleItemVM?.Vehicle.Id == Guid.Empty) return;

            bool confirm = await Application.Current.MainPage.DisplayAlert(
                "Подтверждение",
                $"Удалить машину '{SelectedVehicleItemVM.Vehicle.Name}'?",
                "Да",
                "Нет");

            if (!confirm) return;

            await _db.DeleteAsync(SelectedVehicleItemVM.Vehicle);

            await LoadVehiclesAsync();

            // Сброс в "новую машину"
            SelectedVehicleItemVM = VehicleItems.FirstOrDefault(c => c.Vehicle.Id == Guid.Empty);
        }

        [RelayCommand]
        public async Task LoadVehiclesAsync()
        {
            var list = await _db.GetAllAsync<Vehicle>();

            VehicleItems = new ObservableCollection<VehicleItemVM>();

            VehicleItems.Add(new VehicleItemVM
            {
                Vehicle = new Vehicle { Id = Guid.Empty, Name = "+ Добавить новую машину" },
                Index = 0
            });

            int i = 1;
            foreach (var v in list)
            {
                VehicleItems.Add(new VehicleItemVM { Vehicle = v, Index = i++ });
            }
        }

        partial void OnSelectedVehicleItemVMChanged(VehicleItemVM value)
        {
            if (value == null) return;

            if (value.Vehicle.Id == Guid.Empty) // это "новая машина"
            {
                FormTitle = "Добавить новую машину";
                Name = Number = TareWeight = Description = string.Empty;
                IsDeleteVisible = false;
            }
            else // редактирование существующей
            {
                FormTitle = "Изменение машины";
                Name = value.Vehicle.Name;
                Number = value.Vehicle.Number;
                TareWeight = value.Vehicle.TareWeight.ToString();
                Description = value.Vehicle.Description;
                IsDeleteVisible = true;
            }

            SaveVehicleCommand.NotifyCanExecuteChanged();
            DeleteVehicleCommand.NotifyCanExecuteChanged();
        }

        private bool CanSaveVehicle()
        {
            // Если добавляем новую машину
            if (SelectedVehicleItemVM == null || SelectedVehicleItemVM.Vehicle.Id == Guid.Empty)
            {
                return !string.IsNullOrWhiteSpace(Name) &&
                       !string.IsNullOrWhiteSpace(TareWeight) &&
                       float.TryParse(TareWeight, out _);
            }

            // Если редактируем существующую
            var vehicle = SelectedVehicleItemVM.Vehicle;
            return
                !string.IsNullOrWhiteSpace(Name) &&
                !string.IsNullOrWhiteSpace(TareWeight) &&
                float.TryParse(TareWeight, out var tare) &&
                (
                    Name != vehicle.Name ||
                    Number != vehicle.Number ||
                    Description != vehicle.Description ||
                    tare != vehicle.TareWeight
                );
        }

        private bool CanDeleteVehicle()
        {
            return SelectedVehicleItemVM != null && SelectedVehicleItemVM.Vehicle.Id != Guid.Empty;
        }

        partial void OnNameChanged(string oldValue, string newValue) => SaveVehicleCommand.NotifyCanExecuteChanged();
        partial void OnNumberChanged(string oldValue, string newValue) => SaveVehicleCommand.NotifyCanExecuteChanged();
        partial void OnTareWeightChanged(string oldValue, string newValue) => SaveVehicleCommand.NotifyCanExecuteChanged();
        partial void OnDescriptionChanged(string oldValue, string newValue) => SaveVehicleCommand.NotifyCanExecuteChanged();
    }

    public class VehicleItemVM
    {
        public Vehicle Vehicle { get; set; }
        public int Index { get; set; }

        public string DisplayText =>
            string.Join(" | ", new[]
            {
            Vehicle.Name,
            string.IsNullOrWhiteSpace(Vehicle.Number) ? null : Vehicle.Number,
            Vehicle.TareWeight > 0 ? $"Тара: {Vehicle.TareWeight} кг" : null
            }.Where(s => !string.IsNullOrWhiteSpace(s)));
    }


}
