using SQLite;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using ScaleRecordApp.Models;
using System.Linq.Expressions;

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
            await _db.CreateTableAsync<Vehicle>();
            await _db.CreateTableAsync<CargoType>();
            await _db.CreateTableAsync<Destination>();
            await _db.CreateTableAsync<Source>();
            await _db.CreateTableAsync<Season>();
            await _db.CreateTableAsync<WeighingRecord>();
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