// ViewModels/FieldsVM.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DocumentFormat.OpenXml.Spreadsheet;
using ScaleRecordApp.Models;
using ScaleRecordApp.Services;
using System.Collections.ObjectModel;
using Field = ScaleRecordApp.Models.Field;

namespace ScaleRecordApp.ViewModels;

public partial class FieldsVM : BaseViewModel
{
    private readonly DatabaseService _db;
    public FieldsVM() { }

    public FieldsVM(DatabaseService db)
    {
        _db = db;
    }
    [ObservableProperty]
    private string formTitle = "Добавить новое поле";


    [ObservableProperty] private ObservableCollection<FieldItem> items = new();

    [ObservableProperty] private string name;
    [ObservableProperty] private string number;
    [ObservableProperty] private string areaHa;
    [ObservableProperty] private string location;
    [ObservableProperty] private string description;

    [ObservableProperty] private FieldItem selectedItem;

    [ObservableProperty]
    private bool isDeleteVisible = false;

    [RelayCommand(CanExecute = nameof(CanSaveField))]
    public async Task SaveFieldAsync()
    {
        if(SelectedItem?.Field.Id == Guid.Empty || SelectedItem == null)
        {
            // добавление
            var field = new Field()
            {
                Id = Guid.NewGuid(),
                Name = Name,
                Number = Number,
                AreaHa = double.Parse(AreaHa),
                Location = Location,
                Description = Description
            };
            await _db.InsertAsync(field);
            await Application.Current.MainPage.DisplayAlert(
                    "Успешно",
                    $"Новое поле '{field.Name}' добавлено.",
                    "ОК");
        }
        else
        {
            // обновление
            SelectedItem.Field.Name = Name;
            SelectedItem.Field.Number = Number;
            SelectedItem.Field.AreaHa = double.Parse(AreaHa);
            SelectedItem.Field.Location = Location;
            SelectedItem.Field.Description = Description;

            await _db.UpdateAsync(SelectedItem.Field);

            await Application.Current.MainPage.DisplayAlert("Успешно", $"Поле {SelectedItem.Field.Name} обновлено.", "ОК");
            await LoadFieldsAsync();
            SelectedItem = Items.FirstOrDefault(c => c.Field.Id == Guid.Empty);
        }
    }

    [RelayCommand(CanExecute = nameof(CanDeleteField))]
    public async Task DeleteFieldAsync()
    {
        if (SelectedItem.Field.Id == Guid.Empty || SelectedItem == null) return;

        bool confirm = await Application.Current.MainPage.DisplayAlert(
            "Подтверждение",
            $"Удалить поле №{SelectedItem.Field.Number} \"{SelectedItem.Field.Name}\"?",
            "Да","Нет");

        if (!confirm) return;

        await _db.DeleteAsync(SelectedItem.Field);

        await LoadFieldsAsync();

        SelectedItem = Items.FirstOrDefault(c => c.Field.Id == Guid.Empty);
    }

    [RelayCommand]
    public async Task LoadFieldsAsync()
    {
        var list = await _db.GetAllAsync<Field>();

        Items = new ObservableCollection<FieldItem>();

        Items.Add(new FieldItem
        {
            Field = new Field { Id = Guid.Empty, Name = "+ Добавить поле" },
            Index = 0
        });

        int i = 1;
        foreach(var f in list)
        {
            Items.Add(new FieldItem { Field = f, Index = i++ });
        }
    }

    partial void OnSelectedItemChanged(FieldItem value)
    {
        if (value ==  null) return;

        if(value.Field.Id == Guid.Empty)
        {
            FormTitle = "Добавить новое поле";
            Name = Number = Location = Description = AreaHa = string.Empty;

            IsDeleteVisible = false;
        }
        else
        {
            FormTitle = "Изменение поля";
            Name = value.Field.Name;
            Number = value.Field.Number;
            AreaHa = value.Field.AreaHa.ToString();
            Location = value.Field.Location;
            Description = value.Field.Description;
            IsDeleteVisible = true;
        }

        SaveFieldCommand.NotifyCanExecuteChanged();
        DeleteFieldCommand.NotifyCanExecuteChanged();        
    }

    private bool CanDeleteField()
    {
        return SelectedItem != null && SelectedItem.Field.Id != Guid.Empty;
    }

    private bool CanSaveField()
    {
        if(SelectedItem == null || SelectedItem.Field.Id == Guid.Empty)
        {
            return !string.IsNullOrEmpty(Number) &&
                double.TryParse(AreaHa, out _);
        }

        var field = SelectedItem.Field;
        return
            !string.IsNullOrWhiteSpace(Number) &&
            float.TryParse(AreaHa, out var tare) &&
            (
                Name != field.Name ||
                Number != field.Number ||
                Description != field.Description
            );
    }

    partial void OnNumberChanged(string? oldValue, string newValue) => SaveFieldCommand.NotifyCanExecuteChanged();
    partial void OnNameChanged(string? oldValue, string newValue) => SaveFieldCommand.NotifyCanExecuteChanged();
    partial void OnAreaHaChanged(string? oldValue, string newValue) => SaveFieldCommand.NotifyCanExecuteChanged();
    partial void OnLocationChanged(string? oldValue, string newValue) => SaveFieldCommand.NotifyCanExecuteChanged();
    partial void OnDescriptionChanged(string? oldValue, string newValue) => SaveFieldCommand.NotifyCanExecuteChanged();


    //[RelayCommand]
    //public async Task LoadAsync()
    //{
    //    var list = await _db.GetAllAsync<Field>();
    //    Items = new ObservableCollection<Field>(list.OrderBy(f => f.Number));
    //}

    //[RelayCommand]
    //public async Task AddAsync()
    //{
    //    var field = new Field
    //    {
    //        Name = Name,
    //        Number = Number,
    //        AreaHa = AreaHa,
    //        Location = Location,
    //        Description = Description
    //    };

    //    await _db.InsertAsync(field);
    //    Items.Add(field);

    //    // очистим форму
    //    Name = null;
    //    Number = null;
    //    AreaHa = null;
    //    Location = null;
    //    Description = null;
    //}

    //[RelayCommand]
    //public async Task UpdateAsync()
    //{
    //    if (SelectedItem == null) return;
    //    await _db.UpdateAsync(SelectedItem);
    //}

    //[RelayCommand]
    //public async Task DeleteAsync()
    //{
    //    if (SelectedItem == null) return;
    //    await _db.DeleteAsync(SelectedItem);
    //    Items.Remove(SelectedItem);
    //    SelectedItem = null;
    //}
}

public class FieldItem
{
    public Field Field { get; set; }
    public int Index { get; set; }
    public string DisplayText
    => string.Join(" | ",
        new[] {
                    string.IsNullOrEmpty(Field.Number) ? null : $"Поле №{Field.Number}",
                    string.IsNullOrWhiteSpace(Field.Name) ? null : Field.Name,
                    Field.AreaHa > 0 && !double.IsNaN(Field.AreaHa) && !double.IsInfinity(Field.AreaHa)
                    ? $"{Field.AreaHa:0.#} га"
                    : null
        }.Where(s => !string.IsNullOrWhiteSpace(s)));
}