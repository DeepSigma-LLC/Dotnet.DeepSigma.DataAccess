using Dapper;
using DeepSigma.DataAccess.OperatingSystem;
using Microsoft.Data.SqlClient;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace DeepSigma.DataAccess.Database
{
    public class DatabaseAPI
    {
        private string connection_string {  get; set; }
        private DatabaseType database_type { get; set; }
        public DatabaseAPI(string connection_string, DatabaseType database_type, int connection_timeout = 10)
        { 
            this.connection_string = connection_string;
            this.database_type = database_type;
        }
        
        public async Task<IEnumerable<T>> GetAllAsync<T, F>(string sql,F parameters, int? command_timout = null)
        {
            using var connection = CreateConnection();
            return await connection.QueryAsync<T>(sql, parameters, commandTimeout: command_timout);
        }

        public async Task<T?> GetByIdAsync<T>(string sql, int id, int? command_timout = null)
        {
            using var connection = CreateConnection();
            return await connection.QueryFirstOrDefaultAsync<T>(sql, new { Id = id }, commandTimeout: command_timout);
        }

        public async Task<int> InsertAsync<F>(string sql, F parameters, int? command_timout = null)
        {
            using var connection = CreateConnection();
            return await connection.ExecuteScalarAsync<int>(sql, parameters, commandTimeout: command_timout);
        }

        public async Task<IEnumerable<int>?> InsertAllAsync<F>(string sql, IEnumerable<F> parameters, int? command_timout = null)
        {
            using var connection = CreateConnection();
            return await connection.ExecuteScalarAsync<IEnumerable<int>>(sql, parameters, commandTimeout: command_timout);
        }

        public async Task<int> UpdateAsync<F>(string sql, F parameters, int? command_timout = null)
        {
            using var connection = CreateConnection();
            return await connection.ExecuteScalarAsync<int>(sql, parameters, commandTimeout: command_timout);
        }

        public async Task<IEnumerable<int>?> UpdateAllAsync<F>(string sql, IEnumerable<F> parameters, int? command_timout = null)
        {
            using var connection = CreateConnection();
            return await connection.ExecuteScalarAsync<IEnumerable<int>>(sql, parameters, commandTimeout: command_timout);
        }

        public async Task<T?> ExecuteAsync<T, F>(string sql, F? parameters, int? command_timout = null)
        {
            using var connection = CreateConnection();
            return await connection.ExecuteScalarAsync<T>(sql, parameters, commandTimeout: command_timout);
        }

        private IDbConnection CreateConnection()
        {
            return database_type switch
            {
                DatabaseType.SQLServer => new SqlConnection(connection_string),
                DatabaseType.Postgres => new NpgsqlConnection(connection_string),
                _ => throw new NotSupportedException()
            };
        }
    }
}
