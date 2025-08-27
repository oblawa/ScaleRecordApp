using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScaleRecordApp.Models
{
    public class CargoType
    {
        [PrimaryKey]
        public Guid Id { get; set; } = Guid.NewGuid();

        public string Name { get; set; } = string.Empty;

        public string? Variety { get; set; }

        public string? Kind { get; set; }

        [Ignore]
        public string DisplayName =>
           string.Join(" | ", new[] { Name, Kind, Variety }
                               .Where(s => !string.IsNullOrWhiteSpace(s)));
    }

}
