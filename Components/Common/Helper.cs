using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Components.Common
{
    public static class Helper
    {
        public static string DecryptString(string encrypted_data)
        {
            var decryptor = new Encrypt_BT();
            return decryptor.DecryptString(encrypted_data);

        }
        public static string EncryptString(string encrypted_data)
        {
            var decryptor = new Encrypt_BT();
            return decryptor.EncryptString(encrypted_data);
        }
        public static List<T> ConvertToList<T>(DataTable dt)
        {
            var columnNames = dt.Columns.Cast<DataColumn>().Select(c => c.ColumnName.ToLower()).ToList();
            var properties = typeof(T).GetProperties();
            return dt.AsEnumerable().Select(row =>
            {
                var objT = Activator.CreateInstance<T>();
                foreach (var pro in properties)
                {
                    if (columnNames.Contains(pro.Name.ToLower()))
                    {
                        try
                        {
                            pro.SetValue(objT, row[pro.Name]);
                        }
                        catch (Exception ex) { }
                    }
                }
                return objT;
            }).ToList();
        }

        public static List<Dictionary<string, object>> DataTableToList(DataTable table)
        {
            var result = new List<Dictionary<string, object>>();

            foreach (DataRow row in table.Rows)
            {
                var dict = new Dictionary<string, object>();

                foreach (DataColumn col in table.Columns)
                {
                    dict[col.ColumnName] = row[col] == DBNull.Value ? null : row[col];
                }

                result.Add(dict);
            }

            return result;
        }


        public static List<T> ToModelList<T>(this DataTable table) where T : new()
        {
            var properties = typeof(T).GetProperties().Where(p => p.CanWrite).ToList();
            var result = new List<T>();

            foreach (DataRow row in table.Rows)
            {
                T item = new T();

                foreach (var prop in properties)
                {
                    if (!table.Columns.Contains(prop.Name) || row[prop.Name] == DBNull.Value)
                        continue;

                    try
                    {
                        var value = row[prop.Name];

                        // Handle Dictionary<string, string>
                        if (prop.PropertyType == typeof(Dictionary<string, string>))
                        {
                            var json = value.ToString();
                            var deserialized = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                            prop.SetValue(item, deserialized);
                        }
                        // Handle List<string>
                        else if (prop.PropertyType == typeof(List<string>))
                        {
                            var json = value.ToString();
                            var deserialized = JsonSerializer.Deserialize<List<string>>(json);
                            prop.SetValue(item, deserialized);
                        }
                        else
                        {
                            var safeValue = Convert.ChangeType(value, Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType);
                            prop.SetValue(item, safeValue);
                        }
                    }
                    catch
                    {
                        // Optionally log the failed property
                    }
                }

                result.Add(item);
            }

            return result;
        }


        public static TViewModel Map<TViewModel, TEntity>(TEntity entity)
           where TViewModel : new()
           where TEntity : class
        {
            if (entity == null) return default;

            TViewModel viewModel = new TViewModel();
            var entityProps = typeof(TEntity).GetProperties();
            var viewModelProps = typeof(TViewModel).GetProperties();

            foreach (var sourceProp in entityProps)
            {
                var targetProp = viewModelProps.FirstOrDefault(p => p.Name == sourceProp.Name && p.PropertyType == sourceProp.PropertyType);
                if (targetProp != null)
                {
                    var value = sourceProp.GetValue(entity, null);
                    targetProp.SetValue(viewModel, value, null);
                }
            }

            return viewModel;
        }

        public static string GetDescriptionFromValue<TEnum>(int value) where TEnum : Enum
        {
            var enumValue = (TEnum)Enum.ToObject(typeof(TEnum), value);
            return GetDescription(enumValue);
        }

        public static string GetDescription<TEnum>(TEnum value) where TEnum : Enum
        {
            var field = value.GetType().GetField(value.ToString());

            if (field != null)
            {
                var attr = field.GetCustomAttribute<DescriptionAttribute>();
                if (attr != null)
                    return attr.Description;
            }

            return value.ToString(); // fallback to name
        }

        public static bool IsUncPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            return path.StartsWith(@"\\") && Path.IsPathRooted(path) && !Path.GetPathRoot(path).Equals(Path.GetPathRoot(Environment.CurrentDirectory), StringComparison.OrdinalIgnoreCase);
        }
        public static string Escape(string field)
        {
            if (!string.IsNullOrWhiteSpace(field) || !string.IsNullOrEmpty(field))
            {
                if (field.Contains(",") || field.Contains("\"") || field.Contains("\n"))
                {
                    field = field.Replace("\"", "\"\"");
                    field = $"\"{field}\"";
                }
            }
            return field;
        }
        public static bool ValidDateTime(DateTime? FromDate, DateTime? ToDate)
        {
            if (FromDate is not null && FromDate.Value != DateTime.MinValue && FromDate.Value != DateTime.MaxValue && ToDate is not null && ToDate.Value != DateTime.MinValue && ToDate.Value != DateTime.MaxValue)
            {
                return true;
            }
            return false;
        }

        public static string GenerateToken()
        {
            var bytes = RandomNumberGenerator.GetBytes(32);

            return Convert.ToBase64String(bytes)
                .Replace("+", "-")
                .Replace("/", "_")
                .Replace("=", "");
        }

        public static string HashToken(string token)
        {
            using var sha = SHA256.Create();

            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(token));

            return Convert.ToHexString(hash);
        }

    }
}
