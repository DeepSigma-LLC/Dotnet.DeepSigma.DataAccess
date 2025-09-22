using Dapper;
using Microsoft.Data.SqlClient;
using Npgsql;
using System.Data;

namespace DeepSigma.DataAccess.Database
{
    public class DatabaseAPI
    {
        private string connection_string {  get; set; }
        private RelationalDatabaseType database_type { get; set; }
        public DatabaseAPI(string connection_string, RelationalDatabaseType database_type, int connection_timeout = 10)
        { 
            this.connection_string = connection_string;
            this.database_type = database_type;
        }

        /// <summary>
        /// Gets all records from the database based on the provided SQL query and parameters.
        /// </summary>
        /// <typeparam name="P"></typeparam>
        /// <typeparam name="T"></typeparam>
        /// <param name="sql"></param>
        /// <param name="parameters"></param>
        /// <param name="command_timout"></param>
        /// <returns></returns>
        public async Task<IEnumerable<T>> GetAllAsync<Parameters, T>(string sql, Parameters parameters, int? command_timout = null)
        {
            using var connection = CreateConnection();
            return await connection.QueryAsync<T>(sql, parameters, commandTimeout: command_timout);
        }

        /// <summary>
        /// Gets a single record by its ID from the database based on the provided SQL query.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="sql"></param>
        /// <param name="id"></param>
        /// <param name="command_timout"></param>
        /// <returns></returns>
        public async Task<T?> GetByIdAsync<T>(string sql, int id, int? command_timout = null)
        {
            using var connection = CreateConnection();
            return await connection.QueryFirstOrDefaultAsync<T>(sql, new { Id = id }, commandTimeout: command_timout);
        }

        /// <summary>
        /// Inserts a new record into the database and returns the generated ID.
        /// </summary>
        /// <typeparam name="Parameters"></typeparam>
        /// <param name="sql"></param>
        /// <param name="parameters"></param>
        /// <param name="command_timout"></param>
        /// <returns></returns>
        public async Task<int> InsertAsync<Parameters>(string sql, Parameters parameters, int? command_timout = null)
        {
            using var connection = CreateConnection();
            return await connection.ExecuteScalarAsync<int>(sql, parameters, commandTimeout: command_timout);
        }

        /// <summary>
        /// Updates an existing record in the database and returns the number of affected rows.
        /// </summary>
        /// <typeparam name="F"></typeparam>
        /// <param name="sql"></param>
        /// <param name="parameters"></param>
        /// <param name="command_timout"></param>
        /// <returns></returns>
        public async Task<IEnumerable<int>?> InsertAllAsync<F>(string sql, IEnumerable<F> parameters, int? command_timout = null)
        {
            using var connection = CreateConnection();
            return await connection.ExecuteScalarAsync<IEnumerable<int>>(sql, parameters, commandTimeout: command_timout);
        }

        /// <summary>
        /// Updates an existing record in the database and returns the number of affected rows.
        /// </summary>
        /// <typeparam name="Parameters"></typeparam>
        /// <param name="sql"></param>
        /// <param name="parameters"></param>
        /// <param name="command_timout"></param>
        /// <returns></returns>
        public async Task<int> UpdateAsync<Parameters>(string sql, Parameters parameters, int? command_timout = null)
        {
            using var connection = CreateConnection();
            return await connection.ExecuteScalarAsync<int>(sql, parameters, commandTimeout: command_timout);
        }

        /// <summary>
        /// Updates multiple records in the database and returns the number of affected rows for each update.
        /// </summary>
        /// <typeparam name="F"></typeparam>
        /// <param name="sql"></param>
        /// <param name="parameters"></param>
        /// <param name="command_timout"></param>
        /// <returns></returns>
        public async Task<IEnumerable<int>?> UpdateAllAsync<F>(string sql, IEnumerable<F> parameters, int? command_timout = null)
        {
            using var connection = CreateConnection();
            return await connection.ExecuteScalarAsync<IEnumerable<int>>(sql, parameters, commandTimeout: command_timout);
        }

        /// <summary>
        /// Executes a SQL command that returns a single scalar value of type T.
        /// </summary>
        /// <typeparam name="Parameters"></typeparam>
        /// <typeparam name="T"></typeparam>
        /// <param name="sql"></param>
        /// <param name="parameters"></param>
        /// <param name="command_timout"></param>
        /// <returns></returns>
        public async Task<T?> ExecuteAsync<Parameters, T>(string sql, Parameters? parameters, int? command_timout = null)
        {
            using var connection = CreateConnection();
            return await connection.ExecuteScalarAsync<T>(sql, parameters, commandTimeout: command_timout);
        }

        private IDbConnection CreateConnection()
        {
            return database_type switch
            {
                RelationalDatabaseType.SQLServer => new SqlConnection(connection_string),
                RelationalDatabaseType.Postgres => new NpgsqlConnection(connection_string),
                _ => throw new NotSupportedException()
            };
        }
    }
}
