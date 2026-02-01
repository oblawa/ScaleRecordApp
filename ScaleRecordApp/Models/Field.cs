using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScaleRecordApp.Models
{
    public class Field
    {
        [PrimaryKey]
        public Guid Id { get; set; } = Guid.NewGuid();

        // Например, номер поля и (опц.) его имя
        public string Number { get; set; } = string.Empty;
        public string? Name { get; set; }

        // Площадь поля в гектарах для расчёта урожайности
        public double AreaHa { get; set; }

        public string? Location { get; set; }
        public string? Description { get; set; } 

        [Ignore]
        public string DisplayName
            => string.Join(" | ",
                new[] {
                    string.IsNullOrEmpty(Number) ? null : $"Поле №{Number}",
                    string.IsNullOrWhiteSpace(Name) ? null : Name,
                    AreaHa > 0 && !double.IsNaN(AreaHa) && !double.IsInfinity(AreaHa)
                    ? $"{AreaHa:0.#} га"
                    : null
                }.Where(s => !string.IsNullOrWhiteSpace(s)));
    }

    //public class Field
    //{
    //    [PrimaryKey]
    //    public Guid Id { get; set; } = Guid.NewGuid();

    //    public string Name { get; set; } = string.Empty;

    //    public int? Number { get; set; }
    //    public float? Area { get; set; }

    //    [Ignore]
    //    public string DisplayName
    //    {
    //        get
    //        {
    //            var parts = new List<string>();

    //            if (Number.HasValue)
    //                parts.Add($"Поле № {Number}");

    //            if (!string.IsNullOrWhiteSpace(Name))
    //                parts.Add(Name);

    //            if (Area.HasValue)
    //                parts.Add($"{Area.Value:0.#} га");

    //            return string.Join(" | ", parts);
    //        }
    //    }

    //}
}
