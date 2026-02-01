using SQLite;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using ScaleRecordApp.Models;
using System.Linq.Expressions;
using System.Runtime.InteropServices;

namespace ScaleRecordApp.Services
{
    public class DatabaseService
    {
        private readonly SQLiteAsyncConnection _db;
        public Task<List<T>> GetWhereAsync<T>(Expression<Func<T, bool>> predicate) where T : new()
        {
            return _db.Table<T>().Where(predicate).ToListAsync();
        }


        public DatabaseService(string dbPath)
        {
            _db = new SQLiteAsyncConnection(dbPath);
            Initialize().ConfigureAwait(false);
        }

        private async Task Initialize()
        {
            //await _db.CreateTableAsync<Vehicle>();
            //await _db.CreateTableAsync<CargoType>();
            //await _db.CreateTableAsync<Destination>();
            //await _db.CreateTableAsync<Source>();
            //await _db.CreateTableAsync<Season>();
            //await _db.CreateTableAsync<WeighingRecord>();
            await EnsureSchemaAsync();
        }

        // Services/DatabaseService.cs (фрагмент инициализации)
        private async Task EnsureSchemaAsync()
        {
            await _db.CreateTableAsync<Vehicle>();
            await _db.CreateTableAsync<CargoType>();
            await _db.CreateTableAsync<Season>();
            await _db.CreateTableAsync<Source>();
            await _db.CreateTableAsync<Field>();
            await _db.CreateTableAsync<Destination>();
            await _db.CreateTableAsync<WeighingRecord>();
            // ... остальные если есть

            // ensure columns for Destination
            var cols = await _db.QueryAsync<PragmaTableInfo>(
                "PRAGMA table_info(Destination)");

            bool Has(string name) => cols.Any(c => string.Equals(c.name, name, StringComparison.OrdinalIgnoreCase));

            if (!Has("Category"))
                await _db.ExecuteAsync("ALTER TABLE Destination ADD COLUMN Category INTEGER NOT NULL DEFAULT 0");

            if (!Has("FieldId"))
                await _db.ExecuteAsync("ALTER TABLE Destination ADD COLUMN FieldId TEXT NULL");

            if (!Has("Location"))
                await _db.ExecuteAsync("ALTER TABLE Destination ADD COLUMN Location TEXT NULL");

            if (!Has("Description"))
                await _db.ExecuteAsync("ALTER TABLE Destination ADD COLUMN Description TEXT NULL");
        }

        private class PragmaTableInfo
        {
            public int cid { get; set; }
            public string name { get; set; } = "";
            public string type { get; set; } = "";
            public int notnull { get; set; }
            public string? dflt_value { get; set; }
            public int pk { get; set; }
        }


        // Generic helpers (simple)
        public Task<List<T>> GetAllAsync<T>() where T : new()
        {
            return _db.Table<T>().ToListAsync();
        }

        public Task<T> GetAsync<T>(Guid id) where T : new()
        {
            return _db.FindAsync<T>(id);
        }

        public Task<int> InsertAsync<T>(T item)
        {
            return _db.InsertAsync(item);
        }

        public Task<int> UpdateAsync<T>(T item)
        {
            return _db.UpdateAsync(item);
        }

        public Task<int> DeleteAsync<T>(T item)
        {
            return _db.DeleteAsync(item);
        }
    }
}