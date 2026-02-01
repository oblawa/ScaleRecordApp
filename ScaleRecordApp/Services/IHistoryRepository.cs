using SQLite;
using ScaleRecordApp.Models;
using ScaleRecordApp.ViewModels; // for HistoryRecordDto

namespace ScaleRecordApp.Services;

public interface IHistoryRepository
{
    Task<List<HistoryRecordDto>> GetRangeAsync(DateTime fromExclusiveLocal, DateTime toInclusiveLocal);
    Task<DateTime?> GetFirstRecordTimestampAsync();
}

internal class HistoryRow
{
    public Guid Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string VehicleName { get; set; } = "";
    public string CargoName { get; set; } = "";
    public string? Kind { get; set; }
    public string? Variety { get; set; }
    public float NetWeight { get; set; }
    public string FromName { get; set; } = "";
    public string ToName { get; set; } = "";
    public string? SourceName { get; set; }
    public string? Comment { get; set; }
}

public class HistoryRepository : IHistoryRepository
{
    private readonly DatabaseService _db;

    public HistoryRepository(DatabaseService db) => _db = db;

    public async Task<List<HistoryRecordDto>> GetRangeAsync(DateTime fromExclusiveLocal, DateTime toInclusiveLocal)
    {
        // 1) Берём взвешивания
        var weighings = await _db.GetWhereAsync<WeighingRecord>(
            r => r.Timestamp > fromExclusiveLocal && r.Timestamp <= toInclusiveLocal);

        if (weighings.Count == 0) return new();

        // 2) Подтягиваем справочники разом
        var vehicles = (await _db.GetAllAsync<Vehicle>()).ToDictionary(v => v.Id, v => v);
        var cargos = (await _db.GetAllAsync<CargoType>()).ToDictionary(c => c.Id, c => c);
        var destinations = (await _db.GetAllAsync<Destination>()).ToDictionary(d => d.Id, d => d);
        var sources = (await _db.GetAllAsync<Source>()).ToDictionary(s => s.Id, s => s);

        // 3) Проекция в DTO
        var list = new List<HistoryRecordDto>(weighings.Count);
        foreach (var wr in weighings.OrderBy(w => w.Timestamp))
        {
            vehicles.TryGetValue(wr.VehicleId, out var v);
            cargos.TryGetValue(wr.CargoTypeId, out var c);
            destinations.TryGetValue(wr.FromId, out var from);
            destinations.TryGetValue(wr.ToId, out var to);
            sources.TryGetValue(wr.SourceId ?? Guid.Empty, out var src);

            list.Add(new HistoryRecordDto
            {
                Id = wr.Id,
                Timestamp = wr.Timestamp,
                VehicleName = v?.Name ?? "",
                CargoDisplay = c?.DisplayName ?? "",
                NetWeight = wr.NetWeight,
                FromName = from?.Name ?? "",
                ToName = to?.Name ?? "",
                SourceName = src?.Name ?? "",
                Comment = wr.Comment ?? ""
            });
        }
        return list;
    }

    public async Task<DateTime?> GetFirstRecordTimestampAsync()
    {
        var all = await _db.GetAllAsync<WeighingRecord>();
        return all.Count == 0 ? null : all.Min(w => w.Timestamp);
    }
}




