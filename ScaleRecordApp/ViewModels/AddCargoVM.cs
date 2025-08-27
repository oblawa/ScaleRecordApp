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
using System.Xml.Linq;

namespace ScaleRecordApp.ViewModels
{
    public partial class AddCargoVM : BaseViewModel
    {
        private readonly DatabaseService _db;

        [ObservableProperty]
        public ObservableCollection<CargoItemVM> cargoItems = new();

        [ObservableProperty]
        private CargoItemVM selectedCargoItemVM;

        [ObservableProperty]
        private string formTitle = "Добавить новый груз";

        [ObservableProperty]
        public string name;

        [ObservableProperty]
        public string kind; // тип (необязательное поле)

        [ObservableProperty]
        public string variety; // сорт (необязательное поле)

        [ObservableProperty]
        private bool isDeleteVisible = false;

        public AddCargoVM(DatabaseService db)
        {
            _db = db;
        }

        [RelayCommand(CanExecute = nameof(CanSaveCargo))]
        public async Task SaveCargoAsync()
        {
            if (SelectedCargoItemVM?.Cargo.Id == Guid.Empty || SelectedCargoItemVM == null)
            {
                // Добавление
                var cargo = new CargoType
                {
                    Id = Guid.NewGuid(),
                    Name = Name,
                    Kind = Kind,
                    Variety = Variety
                };

                await _db.InsertAsync(cargo);

                await Application.Current.MainPage.DisplayAlert(
                    "Успешно",
                    $"Новый груз '{cargo.Name}' добавлен.",
                    "ОК");
            }
            else
            {
                // Обновление
                SelectedCargoItemVM.Cargo.Name = Name;
                SelectedCargoItemVM.Cargo.Kind = Kind;
                SelectedCargoItemVM.Cargo.Variety = Variety;

                await _db.UpdateAsync(SelectedCargoItemVM.Cargo);

                await Application.Current.MainPage.DisplayAlert(
                    "Успешно",
                    $"Груз '{SelectedCargoItemVM.Cargo.Name}' обновлён.",
                    "ОК");
            }


            await LoadCargoAsync();

            SelectedCargoItemVM = CargoItems.FirstOrDefault(c => c.Cargo.Id == Guid.Empty);
        }

        [RelayCommand(CanExecute = nameof(CanDeleteCargo))]
        public async Task DeleteCargoAsync()
        {
            if (SelectedCargoItemVM?.Cargo.Id == Guid.Empty) return;

            bool confirm = await Application.Current.MainPage.DisplayAlert(
                "Подтверждение",
                $"Удалить груз '{SelectedCargoItemVM.Cargo.Name}'?",
                "Да",
                "Нет");

            if (!confirm) return;

            await _db.DeleteAsync(SelectedCargoItemVM.Cargo);

            //await Application.Current.MainPage.DisplayAlert(
            //    "Успешно",
            //    $"Груз '{SelectedCargoItemVM.Cargo.Name}' удалён.",
            //    "ОК");

            await LoadCargoAsync();

            // Сброс в "новый груз"
            SelectedCargoItemVM = CargoItems.FirstOrDefault(c => c.Cargo.Id == Guid.Empty);
        }



        [RelayCommand]
        public async Task LoadCargoAsync()
        {
            var list = await _db.GetAllAsync<CargoType>();

            CargoItems = new ObservableCollection<CargoItemVM>();

            CargoItems.Add(new CargoItemVM
            {
                Cargo = new CargoType { Id = Guid.Empty, Name = "+ Добавить новый груз" },
                Index = 0
            });

            int i = 1;
            foreach (var c in list)
            {
                CargoItems.Add(new CargoItemVM { Cargo = c, Index = i++ });
            }
        }

        partial void OnSelectedCargoItemVMChanged(CargoItemVM value)
        {
            if (value == null) return;

            if (value.Cargo.Id == Guid.Empty) // это "новый груз"
            {
                FormTitle = "Добавить новый груз";
                Name = Kind = Variety = string.Empty;
                IsDeleteVisible = false;
            }
            else // редактирование существующего
            {
                FormTitle = "Изменение груза";
                Name = value.Cargo.Name;
                Kind = value.Cargo.Kind;
                Variety = value.Cargo.Variety;
                IsDeleteVisible = true;
            }

            SaveCargoCommand.NotifyCanExecuteChanged();
            DeleteCargoCommand.NotifyCanExecuteChanged();
        }

        private bool CanSaveCargo()
        {
            // Если добавляем новый груз
            if (SelectedCargoItemVM == null || SelectedCargoItemVM.Cargo.Id == Guid.Empty)
            {
                return !string.IsNullOrWhiteSpace(Name);
            }

            // Если редактируем существующий
            var cargo = SelectedCargoItemVM.Cargo;
            return
                !string.IsNullOrWhiteSpace(Name) &&
                (
                    Name != cargo.Name ||
                    Kind != cargo.Kind ||
                    Variety != cargo.Variety
                );
        }
        private bool CanDeleteCargo()
        {
            return SelectedCargoItemVM != null && SelectedCargoItemVM.Cargo.Id != Guid.Empty;
        }

        partial void OnNameChanged(string oldValue, string newValue) => SaveCargoCommand.NotifyCanExecuteChanged();
        partial void OnKindChanged(string oldValue, string newValue) => SaveCargoCommand.NotifyCanExecuteChanged();
        partial void OnVarietyChanged(string oldValue, string newValue) => SaveCargoCommand.NotifyCanExecuteChanged();
    }

    public class CargoItemVM
    {
        public CargoType Cargo { get; set; }
        public int Index { get; set; }
        public string DisplayText =>
                    string.Join(" | ", new[] { Cargo.Name, Cargo.Kind, Cargo.Variety }
                        .Where(s => !string.IsNullOrWhiteSpace(s)));
    }
}
