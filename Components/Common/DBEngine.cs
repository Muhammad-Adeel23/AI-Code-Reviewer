using Components.Configuration;
using Microsoft.Data.SqlClient;
using Serilog;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Components.Common
{
    public static class DBEngine
    {
        #region Properties, Variables and Initialization

        private static int result = 0;
        private static string connectionString = AppConfiguration._dBConnections;
        private static readonly Serilog.ILogger _logger = Log.ForContext(typeof(DBEngine));

        #endregion

        #region DB Execution Methods

        /// <summary>
        /// Execute query in database
        /// </summary>
        /// <param name="cmd"></param>
        /// <returns></returns>
        public static bool ExecuteNonQuery(SqlCommand cmd, DatabaseOperation? databaseOperation = null, string? query = null)
        {
            SqlConnection conn = new SqlConnection(connectionString);
            try
            {
                if (conn.State == ConnectionState.Closed)
                    conn.Open();

                cmd.Connection = conn;
                cmd.CommandTimeout = int.MaxValue;

                var operation = GetOperationName(databaseOperation);

                if (!databaseOperation.HasValue)
                {
                    operation = "SQL Query";
                }

                if (!string.IsNullOrWhiteSpace(query) || databaseOperation.HasValue)
                {
                    var timer = new Stopwatch();
                    timer.Start();

                    result = cmd.ExecuteNonQuery();

                    timer.Stop();
                    TimeSpan timeTaken = timer.Elapsed;

                    var timeDiff = timeTaken.ToString(@"m\:ss\.fff");

                    LogDBActionWithTime(operation, timeDiff, query);
                }

                else
                {
                    result = cmd.ExecuteNonQuery();
                }

                if (result > 0)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex.Message);
                throw;
            }
            finally
            {
                if (conn.State == ConnectionState.Open)
                    conn.Close();

                conn.Dispose();
            }

        }
        /// <summary>
        /// Execute query row count in database
        /// </summary>
        /// <param name="cmd"></param>
        /// <returns></returns>
        public static int ExecuteNonQueryRowCount(SqlCommand cmd, DatabaseOperation? databaseOperation = null, string? query = null)
        {
            SqlConnection conn = new SqlConnection(connectionString);
            try
            {
                if (conn.State == ConnectionState.Closed)
                    conn.Open();

                cmd.Connection = conn;
                cmd.CommandTimeout = int.MaxValue;

                var operation = GetOperationName(databaseOperation);

                if (!databaseOperation.HasValue)
                {
                    operation = "SQL Query";
                }

                if (!string.IsNullOrWhiteSpace(query) || databaseOperation.HasValue)
                {
                    var timer = new Stopwatch();
                    timer.Start();

                    result = cmd.ExecuteNonQuery();

                    timer.Stop();
                    TimeSpan timeTaken = timer.Elapsed;

                    var timeDiff = timeTaken.ToString(@"m\:ss\.fff");

                    LogDBActionWithTime(operation, timeDiff, query);
                }

                else
                {
                    result = cmd.ExecuteNonQuery();
                }
                return result;
            }
            catch (Exception ex)
            {
                _logger.Error(ex.Message);
                throw;
            }
            finally
            {
                if (conn.State == ConnectionState.Open)
                    conn.Close();

                conn.Dispose();
            }

        }
        /// <summary>
        /// Execute Scaler function calling in database
        /// </summary>
        /// <param name="cmd"></param>
        /// <returns></returns>
        public static long ExecuteScaler(SqlCommand cmd, DatabaseOperation? databaseOperation = null, string? query = null)
        {
            SqlConnection conn = new SqlConnection(connectionString);
            try
            {
                long value = 0;
                if (conn.State == ConnectionState.Closed)
                    conn.Open();

                cmd.Connection = conn;
                cmd.CommandTimeout = int.MaxValue;

                var operation = GetOperationName(databaseOperation);

                if (!databaseOperation.HasValue)
                {
                    operation = "SQL Query";
                }

                if (!string.IsNullOrWhiteSpace(query) || databaseOperation.HasValue)
                {
                    var timer = new Stopwatch();
                    timer.Start();

                    var firstColumn = cmd.ExecuteScalar();

                    timer.Stop();
                    TimeSpan timeTaken = timer.Elapsed;
                    var timeDiff = timeTaken.ToString(@"m\:ss\.fff");

                    LogDBActionWithTime(operation, timeDiff, query);

                    if (firstColumn != null)
                    {
                        value = Convert.ToInt64(firstColumn);
                    }
                }

                else
                {
                    var firstColumn = cmd.ExecuteScalar();
                    if (firstColumn != null)
                    {
                        value = Convert.ToInt64(firstColumn);
                    }
                }


                return value;
            }
            catch (Exception ex)
            {
                _logger.Error(ex.Message);
                throw;
            }
            finally
            {
                if (conn.State == ConnectionState.Open)
                    conn.Close();

                conn.Dispose();
            }

        }
        /// <summary>
        /// Get dataset from database
        /// </summary>
        /// <param name="cmd"></param>
        /// <returns></returns>
        public static DataSet GetDataSet(SqlCommand cmd, DatabaseOperation? databaseOperation = null, string? query = null)
        {
            SqlConnection conn = new SqlConnection(connectionString);
            DataSet ds = new DataSet();
            try
            {
                if (conn.State == ConnectionState.Closed)
                    conn.Open();

                cmd.Connection = conn;
                cmd.CommandTimeout = int.MaxValue;

                var operation = GetOperationName(databaseOperation);

                if (!databaseOperation.HasValue)
                {
                    operation = "SQL Query";
                }

                if (!string.IsNullOrWhiteSpace(query) || databaseOperation.HasValue)
                {
                    var timer = new Stopwatch();
                    timer.Start();

                    SqlDataAdapter adpt = new SqlDataAdapter(cmd);
                    adpt.Fill(ds);
                    adpt.Dispose();

                    timer.Stop();
                    TimeSpan timeTaken = timer.Elapsed;
                    var timeDiff = timeTaken.ToString(@"m\:ss\.fff");

                    LogDBActionWithTime(operation, timeDiff, query);
                }

                else
                {
                    SqlDataAdapter adpt = new SqlDataAdapter(cmd);
                    adpt.Fill(ds);
                    adpt.Dispose();
                }

                return ds;
            }
            catch (Exception ex)
            {
                _logger.Error(ex.Message);
                throw;
            }
            finally
            {
                if (conn.State == ConnectionState.Open)
                    conn.Close();

                conn.Dispose();
            }

        }
        /// <summary>
        /// Get dataset from database using connString
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="connString"></param>
        /// <returns></returns>
        public static DataSet GetDataSet(SqlCommand cmd, string connString, DatabaseOperation? databaseOperation = null, string? query = null)
        {
            SqlConnection conn = new SqlConnection(connString);
            DataSet ds = new DataSet();
            try
            {
                if (conn.State == ConnectionState.Closed)
                    conn.Open();

                cmd.Connection = conn;
                cmd.CommandTimeout = int.MaxValue;

                var operation = GetOperationName(databaseOperation);

                if (!databaseOperation.HasValue)
                {
                    operation = "SQL Query";
                }


                if (!string.IsNullOrWhiteSpace(query) || databaseOperation.HasValue)
                {

                    var timer = new Stopwatch();
                    timer.Start();

                    SqlDataAdapter adpt = new SqlDataAdapter(cmd);
                    adpt.Fill(ds);
                    adpt.Dispose();


                    timer.Stop();
                    TimeSpan timeTaken = timer.Elapsed;
                    var timeDiff = timeTaken.ToString(@"m\:ss\.fff");

                    LogDBActionWithTime(operation, timeDiff, query);

                }

                else
                {
                    SqlDataAdapter adpt = new SqlDataAdapter(cmd);
                    adpt.Fill(ds);
                    adpt.Dispose();
                }

                return ds;
            }
            catch (Exception ex)
            {
                _logger.Error(ex.Message);
                throw;
            }
            finally
            {
                if (conn.State == ConnectionState.Open)
                    conn.Close();

                conn.Dispose();
            }

        }
        /// <summary>
        /// Get datatable from database
        /// </summary>
        /// <param name="cmd"></param>
        /// <returns></returns>
        public static DataTable GetDataTable(SqlCommand cmd, DatabaseOperation? databaseOperation = null, string? query = null, bool? logFlagOnAndOff = null)
        {
            cmd.CommandTimeout = int.MaxValue;
            DataTable dt = new DataTable();

            var operation = GetOperationName(databaseOperation);

            if (!databaseOperation.HasValue)
            {
                operation = "SQL Query";
            }

            if (!string.IsNullOrWhiteSpace(query) || databaseOperation.HasValue)
            {
                var timer = new Stopwatch();
                timer.Start();

                dt = GetDataSet(cmd).Tables[0];

                timer.Stop();
                TimeSpan timeTaken = timer.Elapsed;
                var timeDiff = timeTaken.ToString(@"m\:ss\.fff");

                if (logFlagOnAndOff == null || logFlagOnAndOff == true)
                {
                    LogDBActionWithTime(operation, timeDiff, query);
                }

            }

            else
            {
                dt = GetDataSet(cmd).Tables[0];
            }

            return dt;
        }

        public static void BulkInsert(DataTable dt, string tableName, List<SqlBulkCopyColumnMapping> columnMapping, DatabaseOperation? databaseOperation = null, string? query = null)
        {
            var operation = GetOperationName(DatabaseOperation.BulkInsert);

            using SqlConnection conn = new SqlConnection(connectionString);
            try
            {
                //create object of SqlBulkCopy which help to insert  
                SqlBulkCopy objbulk = new SqlBulkCopy(connectionString, SqlBulkCopyOptions.FireTriggers);

                // set maxcimum command timeout
                objbulk.BulkCopyTimeout = int.MaxValue;

                //assign Destination table name
                objbulk.DestinationTableName = $"{tableName}";


                if (columnMapping.Count > 0)
                {
                    foreach (var mapping in columnMapping)
                    {
                        objbulk.ColumnMappings.Add(mapping);
                    }
                }
                conn.Open();
                //insert bulk Records into DataBase.  

                if (!databaseOperation.HasValue)
                {
                    operation = "SQL Query";
                }

                if (!string.IsNullOrWhiteSpace(query) || databaseOperation.HasValue)
                {
                    var timer = new Stopwatch();
                    timer.Start();

                    objbulk.WriteToServer(dt);
                    conn.Close();

                    timer.Stop();
                    TimeSpan timeTaken = timer.Elapsed;
                    var timeDiff = timeTaken.ToString(@"m\:ss\.fff");

                    LogDBActionWithTime(operation, timeDiff, query);
                }

                else
                {
                    objbulk.WriteToServer(dt);
                    conn.Close();
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex.Message);
                throw;
            }
            finally
            {
                if (conn.State == ConnectionState.Open)
                    conn.Close();

                conn.Dispose();
            }
        }

        public static void BulkInsert(DataTable dt, string tableName, DataTable columnMapping, List<string> extraColumnName = null, DatabaseOperation? databaseOperation = null, string? query = null)
        {
            var operation = GetOperationName(DatabaseOperation.BulkInsert);

            using SqlConnection conn = new SqlConnection(connectionString);
            try
            {
                //create object of SqlBulkCopy which help to insert  
                SqlBulkCopy objbulk = new SqlBulkCopy(connectionString, SqlBulkCopyOptions.FireTriggers);

                // set maxcimum command timeout
                objbulk.BulkCopyTimeout = int.MaxValue;

                //assign Destination table name
                objbulk.DestinationTableName = $"{tableName}";

                foreach (DataRow row in columnMapping.Rows)
                {
                    for (int i = 0; i < dt.Columns.Count; i++)
                    {
                        if (dt.Columns[i].ColumnName.ToLower() == row["ColumnName"].ToString().ToLower())
                        {
                            objbulk.ColumnMappings.Add(row["ColumnName"].ToString(), dt.Columns[i].ColumnName);
                            break;
                        }
                    }
                }
                if (extraColumnName != null)
                {
                    for (int j = 0; j < extraColumnName.Count; j++)
                    {
                        objbulk.ColumnMappings.Add(extraColumnName[j], extraColumnName[j]);
                    }
                }

                conn.Open();
                //insert bulk Records into DataBase.

                if (!databaseOperation.HasValue)
                {
                    operation = "SQL Query";
                }


                if (!string.IsNullOrWhiteSpace(query) || databaseOperation.HasValue)
                {
                    var timer = new Stopwatch();
                    timer.Start();

                    objbulk.WriteToServer(dt);
                    conn.Close();

                    timer.Stop();
                    TimeSpan timeTaken = timer.Elapsed;
                    var timeDiff = timeTaken.ToString(@"m\:ss\.fff");

                    LogDBActionWithTime(operation, timeDiff, query);

                }

                else
                {
                    objbulk.WriteToServer(dt);
                    conn.Close();
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex.Message);
                throw;
            }
            finally
            {
                if (conn.State == ConnectionState.Open)
                    conn.Close();

                conn.Dispose();
            }
        }

        /// <summary>
        /// Bulk Update 
        /// </summary>
        /// <param name="dt"></param>
        /// <param name="tempTableCreation"></param>
        /// <param name="updateQuery"></param>
        /// <param name="tempTableName"></param>
        public static void BulkUpdate(DataTable dt, string tempTableCreation, string updateQuery, string tempTableName, DatabaseOperation? databaseOperation = null, string? query = null)
        {
            var operation = GetOperationName(DatabaseOperation.BulkUpdate);


            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                using (SqlCommand command = new SqlCommand($"{tempTableCreation}", conn))
                {
                    try
                    {
                        conn.Open();
                        command.ExecuteNonQuery();
                        using (SqlBulkCopy bulkcopy = new SqlBulkCopy(conn))
                        {
                            bulkcopy.BulkCopyTimeout = int.MaxValue;
                            bulkcopy.DestinationTableName = tempTableName;
                            for (int i = 0; i < dt.Columns.Count; i++)
                            {
                                bulkcopy.ColumnMappings.Add(i, i);
                            }
                            bulkcopy.WriteToServer(dt);
                            bulkcopy.Close();
                        }
                        command.CommandTimeout = int.MaxValue;
                        command.CommandText = updateQuery;

                        if (!databaseOperation.HasValue)
                        {
                            operation = "SQL Query";
                        }

                        if (!string.IsNullOrWhiteSpace(query) || databaseOperation.HasValue)
                        {
                            var timer = new Stopwatch();
                            timer.Start();

                            command.ExecuteNonQuery();

                            timer.Stop();
                            TimeSpan timeTaken = timer.Elapsed;
                            var timeDiff = timeTaken.ToString(@"m\:ss\.fff");

                            LogDBActionWithTime(operation, timeDiff, query);
                        }

                        else
                        {
                            command.ExecuteNonQuery();
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex.Message);
                        throw;
                    }
                    finally
                    {
                        conn.Close();
                        conn.Dispose();
                    }
                }
            }
        }

        /// <summary>
        /// Execute multiple using Transactional approach
        /// </summary>
        /// <param name="commands"></param>
        /// <param name="databaseOperation"></param>
        /// <returns></returns>
        public static bool ExecuteNonQueryWithTransaction(List<SqlCommand> commands, DatabaseOperation? databaseOperation = null)
        {
            SqlConnection conn = new SqlConnection(connectionString);
            SqlTransaction transaction = null;

            try
            {
                if (conn.State == ConnectionState.Closed)
                    conn.Open();

                transaction = conn.BeginTransaction();

                foreach (var cmd in commands)
                {
                    cmd.Connection = conn;
                    cmd.Transaction = transaction;
                    cmd.CommandTimeout = int.MaxValue;
                    result = cmd.ExecuteNonQuery();

                    if (result <= 0)
                        throw new Exception("Failed to execute one of the queries in transaction.");
                }

                transaction.Commit();
                return true;
            }
            catch (Exception ex)
            {
                transaction?.Rollback();
                _logger.Error(ex, "Transaction rolled back due to error.");
                return false;
            }
            finally
            {
                if (conn.State == ConnectionState.Open)
                    conn.Close();

                conn.Dispose();
            }
        }
        #endregion

        #region DB Logging
        public static void LogDBActionWithTime(string? databaseOperation = null, string? timeDiff = null, string? sqlBlock = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(sqlBlock))
                {
                    _logger.Debug($"Operation : {databaseOperation} | Execution Time : {timeDiff}");
                }

                else
                {
                    _logger.Debug($"Operation : {databaseOperation} | Execution Time : {timeDiff} | SQL Block : {sqlBlock}");
                }

            }
            catch (Exception ex)
            {
                _logger.Error(ex.Message);
            }
        }
        public static string GetOperationName(DatabaseOperation? databaseOperation)
        {
            var operation = string.Empty;

            switch (databaseOperation)
            {
                case DatabaseOperation.INSERT:
                    operation = "INSERT";
                    break;
                case DatabaseOperation.SELECT:
                    operation = "SELECT";
                    break;
                case DatabaseOperation.UPDATE:
                    operation = "UPDATE";
                    break;
                case DatabaseOperation.DELETE:
                    operation = "DELETE";
                    break;
                case DatabaseOperation.GetDataSet:
                    operation = "GETDATASET";
                    break;
                case DatabaseOperation.GetDataTable:
                    operation = "GetDataTable";
                    break;
                case DatabaseOperation.BulkUpdate:
                    operation = "BulkUpdate";
                    break;
                case DatabaseOperation.BulkInsert:
                    operation = "BulkInsert";
                    break;
                case DatabaseOperation.SQLBlock:
                    operation = "SQLBlock";
                    break;
            }

            return operation;
        }
        public static async Task<SqlDataReader> ExecuteReaderAsync(SqlCommand cmd, DatabaseOperation? databaseOperation = null, string? query = null)
        {
            var conn = new SqlConnection(connectionString);
            try
            {
                await conn.OpenAsync();
                cmd.Connection = conn;
                cmd.CommandTimeout = int.MaxValue;

                var operation = GetOperationName(databaseOperation);

                if (!databaseOperation.HasValue)
                {
                    operation = "SQL Query";
                }

                var timer = new Stopwatch();
                timer.Start();

                var reader = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection); // ensures conn closes with reader.Close()

                timer.Stop();
                TimeSpan timeTaken = timer.Elapsed;
                var timeDiff = timeTaken.ToString(@"m\:ss\.fff");

                // Log execution time
                if (!string.IsNullOrWhiteSpace(query) || databaseOperation.HasValue)
                {
                    LogDBActionWithTime(operation, timeDiff, query);
                }

                return reader;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error executing async reader for query: {Query}", query);
                if (conn.State == ConnectionState.Open)
                    await conn.CloseAsync();
                conn.Dispose();
                throw;
            }
        }
        #endregion

    }
}
