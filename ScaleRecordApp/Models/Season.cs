using SQLite;

namespace ScaleRecordApp.Models
{
    public class Season
    {
        [PrimaryKey]
        public Guid Id { get; set; } = Guid.NewGuid();

        public string Name { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

}
