using DocumentFormat.OpenXml.InkML;
using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScaleRecordApp.Models
{
    public class Vehicle
    {
        [PrimaryKey]
        public Guid Id { get; set; } = Guid.NewGuid();

        public string Name { get; set; } = string.Empty;

        public string? Number { get; set; }

        public float TareWeight { get; set; }

        public string? Description { get; set; }
    }

}
