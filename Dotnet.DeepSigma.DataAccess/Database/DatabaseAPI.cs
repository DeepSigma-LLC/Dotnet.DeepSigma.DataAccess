using Dapper;
using Microsoft.Data.SqlClient;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Dotnet.DeepSigma.DataAccess.Database
{
    public class DatabaseAPI
    {
        private string connection_string {  get; set; }
        private DatabaseType database_type { get; set; }
        public DatabaseAPI(string connection_string, DatabaseType database_type)
        { 
            this.connection_string = connection_string;
            this.database_type = database_type;
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

        public async Task<IEnumerable<T>> GetAllAsync<T>(string sql, int? command_timout = null)
        {
            using var connection = CreateConnection();
            return await connection.QueryAsync<T>(sql, commandTimeout: command_timout);
        }

        public async Task<T?> GetByIdAsync<T>(string sql, int id, int? command_timout = null)
        {
            using var connection = CreateConnection();
            return await connection.QueryFirstOrDefaultAsync<T>(sql, new { Id = id }, commandTimeout: command_timout);
        }

        public async Task<int> CreateAsync<T>(string sql, T item, int? command_timout = null)
        {
            using var connection = CreateConnection();
            return await connection.ExecuteScalarAsync<int>(sql, item, commandTimeout: command_timout);
        }

    }
}
