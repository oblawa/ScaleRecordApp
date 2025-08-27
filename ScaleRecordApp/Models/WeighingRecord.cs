using SQLite;

namespace ScaleRecordApp.Models
{
    public class WeighingRecord
    {
        [PrimaryKey]
        public Guid Id { get; set; } = Guid.NewGuid();

        public DateTime Timestamp { get; set; } = DateTime.Now;

        public Guid VehicleId { get; set; }

        public float GrossWeight { get; set; }
        public float TareWeight { get; set; }
        public float NetWeight { get; set; }

        public Guid CargoTypeId { get; set; }

        public Guid SeasonId { get; set; }

        public Guid? SourceId { get; set; }

        public Guid FromId { get; set; }

        public Guid ToId { get; set; }

        public string? Comment { get; set; }
    }

}
