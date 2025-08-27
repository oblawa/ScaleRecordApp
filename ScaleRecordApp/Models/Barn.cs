using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScaleRecordApp.Models
{
    public class Barn
    {
        [PrimaryKey]
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public string? Location { get; set; }
        public string? Description { get; set; }
    }
}
