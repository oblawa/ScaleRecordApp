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




//public class HistoryRepository : IHistoryRepository
//{
//    private readonly SQLiteAsyncConnection _db;

//    public HistoryRepository(SQLiteAsyncConnection db) => _db = db;

//    public async Task<List<HistoryRecordDto>> GetRangeAsync(DateTime fromExclusiveLocal, DateTime toInclusiveLocal)
//    {
//        const string sql = @"SELECT wr.Id,
//                                     wr.Timestamp,
//                                     v.Name AS VehicleName,
//                                     ct.Name AS CargoName,
//                                     ct.Kind AS Kind,
//                                     ct.Variety AS Variety,
//                                     CAST(wr.NetWeight AS INT) AS NetWeight,
//                                     f.Name AS FromName,
//                                     t.Name AS ToName,
//                                     s.Name AS SourceName,
//                                     wr.Comment AS Comment
//                              FROM WeighingRecord wr
//                              JOIN Vehicle v ON v.Id = wr.VehicleId
//                              JOIN CargoType ct ON ct.Id = wr.CargoTypeId
//                              JOIN Destination f ON f.Id = wr.FromId
//                              JOIN Destination t ON t.Id = wr.ToId
//                              LEFT JOIN Source s ON s.Id = wr.SourceId
//                              WHERE wr.Timestamp > ? AND wr.Timestamp <= ?
//                              ORDER BY wr.Timestamp ASC;";

//        var rows = await _db.QueryAsync<HistoryRow>(sql, fromExclusiveLocal, toInclusiveLocal);
//        var list = rows.Select(r => new HistoryRecordDto
//        {
//            Id = r.Id,
//            Timestamp = r.Timestamp,
//            VehicleName = r.VehicleName,
//            CargoDisplay = string.Join(" | ", new[] { r.CargoName, r.Kind, r.Variety }.Where(s => !string.IsNullOrWhiteSpace(s))),
//            NetWeight = r.NetWeight,
//            FromName = r.FromName,
//            ToName = r.ToName,
//            SourceName = r.SourceName ?? string.Empty,
//            Comment = r.Comment ?? string.Empty
//        }).ToList();
//        return list;
//    }

//    public async Task<DateTime?> GetFirstRecordTimestampAsync()
//    {
//        const string sql = "SELECT Timestamp FROM WeighingRecord ORDER BY Timestamp ASC LIMIT 1";
//        var rows = await _db.QueryScalarsAsync<DateTime>(sql);
//        return rows.FirstOrDefault();
//    }
//}