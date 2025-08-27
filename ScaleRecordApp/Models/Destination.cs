using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScaleRecordApp.Models
{
    public enum DestinationCategory
    {
        Other = 0, // Другое
        Field = 1, // Поле
        Storage = 2, // Склад/Хранилище/Локация
        Export = 3  // Экспорт/Отгрузка вне хозяйства
    }

    public class Destination
    {
        [PrimaryKey]
        public Guid Id { get; set; } = Guid.NewGuid();

        // Отображаемое имя (для любого типа)
        public string Name { get; set; } = string.Empty;

        // Что это за «направление»
        public DestinationCategory Category { get; set; } = DestinationCategory.Other;

        // Для Category = Field укажем ссылку на поле
        public Guid? FieldId { get; set; }

        // Для складов/локаций и просто «другое»
        public string? Location { get; set; }
        public string? Description { get; set; }

        [Ignore]
        public string DisplayName
        {
            get
            {
                return Category switch
                {
                    DestinationCategory.Field => string.IsNullOrWhiteSpace(Name) ? "Поле" : $"Поле: {Name}",
                    DestinationCategory.Storage => string.IsNullOrWhiteSpace(Name) ? "Склад" : $"Склад: {Name}",
                    DestinationCategory.Export => string.IsNullOrWhiteSpace(Name) ? "Экспорт" : $"Экспорт: {Name}",
                    _ => Name
                };
            }
        }

        public static Destination FromField(Field f) => new()
        {
            Id = Guid.NewGuid(),
            Category = DestinationCategory.Field,
            FieldId = f.Id,
            Name = f.DisplayName // удобно для UI
        };
    }

    //public class Destination
    //{
    //    [PrimaryKey]
    //    public Guid Id { get; set; } = Guid.NewGuid();

    //    public string Name { get; set; } = string.Empty;
    //}

}
