using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;

namespace LiteOrm.CodeGen
{
    /// <summary>
    /// 简易代码生成器：根据数据库表元信息生成实体类 C# 源代码。
    /// </summary>
    public static class CSharpGen
    {
        /// <summary>获取 SQL Server 中所有用户表名称的 SQL 语句。</summary>
        public const string SqlServerGetTableNamesSql = "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE' ORDER BY TABLE_NAME";
        /// <summary>获取 MySQL 中当前数据库所有用户表名称的 SQL 语句。</summary>
        public const string MySqlGetTableNamesSql = "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_TYPE = 'BASE TABLE' ORDER BY TABLE_NAME";
        /// <summary>获取 PostgreSQL 中 public 模式下所有用户表名称的 SQL 语句。</summary>
        public const string PostgreSqlGetTableNamesSql = "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public' AND table_type = 'BASE TABLE' ORDER BY table_name";
        /// <summary>获取 SQLite 中所有用户表名称的 SQL 语句。</summary>
        public const string SqliteGetTableNamesSql = "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%' ORDER BY name";
        /// <summary>获取 Oracle 中当前用户所有表名称的 SQL 语句。</summary>
        public const string OracleGetTableNamesSql = "SELECT table_name FROM user_tables ORDER BY table_name";

        /// <summary>
        /// 根据连接和表名读取数据库结构并生成实体代码。
        /// </summary>
        public static string GenerateEntityCode(
            DbConnection connection,
            string tableName,
            string namespaceName = "Models",
            string className = null,
            string dataSource = null,
            Func<string, string> classNameSelector = null,
            Func<string, string> propertyNameSelector = null)
        {
            var table = GetTableInfo(connection, tableName, className, dataSource, classNameSelector, propertyNameSelector);
            return GenerateEntityCode(table, namespaceName, classNameSelector, c => propertyNameSelector?.Invoke(c.ColumnName));
        }

        /// <summary>
        /// 根据连接和表名读取数据库结构。
        /// </summary>
        public static DatabaseTableInfo GetTableInfo(
            DbConnection connection,
            string tableName,
            string className = null,
            string dataSource = null,
            Func<string, string> classNameSelector = null,
            Func<string, string> propertyNameSelector = null)
        {
            if (connection is null) throw new ArgumentNullException(nameof(connection));
            if (string.IsNullOrWhiteSpace(tableName)) throw new ArgumentException("tableName is required.", nameof(tableName));

            bool closeAfter = EnsureConnectionOpen(connection);
            try
            {
                var columnsSchema = GetColumnsSchema(connection, tableName);
                var table = new DatabaseTableInfo
                {
                    TableName = tableName,
                    ClassName = string.IsNullOrWhiteSpace(className) ? classNameSelector?.Invoke(tableName) : className,
                    DataSource = dataSource
                };

                foreach (DataRow row in columnsSchema.Rows)
                {
                    var currentTableName = GetString(row, "TABLE_NAME", "table_name");
                    if (!string.IsNullOrWhiteSpace(currentTableName) &&
                        !string.Equals(currentTableName, tableName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    var columnName = GetString(row, "COLUMN_NAME", "column_name", "name");
                    if (string.IsNullOrWhiteSpace(columnName)) continue;

                    var dbType = TryGetDbType(row);
                    var clrType = TryGetClrType(row) ?? (dbType.HasValue ? MapDbTypeToClrType(dbType.Value) : typeof(string));

                    table.Columns.Add(new DatabaseColumnInfo
                    {
                        ColumnName = columnName,
                        PropertyName = propertyNameSelector?.Invoke(columnName) ?? ToPascalCaseIdentifier(columnName, "Field"),
                        DbType = dbType,
                        ClrType = clrType,
                        Length = GetInt(row, "CHARACTER_MAXIMUM_LENGTH", "COLUMN_SIZE", "DATA_LENGTH", "character_maximum_length", "column_size", "data_length") ?? 0,
                        AllowNull = GetNullableFlag(row, true, "IS_NULLABLE", "is_nullable", "NULLABLE", "nullable"),
                        IsPrimaryKey = GetPrimaryKeyFlag(row),
                        IsIdentity = GetIdentityFlag(row),
                        IsTimestamp = dbType == DbType.DateTime || dbType == DbType.DateTime2 || dbType == DbType.DateTimeOffset,
                        DefaultValue = GetString(row, "COLUMN_DEFAULT", "column_default", "DEFAULT")
                    });
                }

                return table;
            }
            finally
            {
                if (closeAfter) connection.Close();
            }
        }

        /// <summary>
        /// 按指定 SQL 查询表名，默认读取第一列。
        /// </summary>
        public static IList<string> GetTableNames(DbConnection connection, string getTableNamesSql)
        {
            if (connection is null) throw new ArgumentNullException(nameof(connection));
            if (string.IsNullOrWhiteSpace(getTableNamesSql)) throw new ArgumentException("getTableNamesSql is required.", nameof(getTableNamesSql));

            bool closeAfter = EnsureConnectionOpen(connection);
            try
            {
                var result = new List<string>();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = getTableNamesSql;
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            if (reader.FieldCount == 0 || reader.IsDBNull(0)) continue;
                            var tableName = Convert.ToString(reader.GetValue(0));
                            if (!string.IsNullOrWhiteSpace(tableName)) result.Add(tableName);
                        }
                    }
                }
                return result;
            }
            finally
            {
                if (closeAfter) connection.Close();
            }
        }

        /// <summary>
        /// 根据连接类型返回预定义的“查询表名”SQL。
        /// </summary>
        public static string GetPredefinedGetTableNamesSql(DbConnection connection)
        {
            if (connection is null) throw new ArgumentNullException(nameof(connection));
            var typeName = connection.GetType().Name;

            if (typeName.IndexOf("Sqlite", StringComparison.OrdinalIgnoreCase) >= 0) return SqliteGetTableNamesSql;
            if (typeName.IndexOf("MySql", StringComparison.OrdinalIgnoreCase) >= 0) return MySqlGetTableNamesSql;
            if (typeName.IndexOf("Oracle", StringComparison.OrdinalIgnoreCase) >= 0) return OracleGetTableNamesSql;
            if (typeName.IndexOf("Npgsql", StringComparison.OrdinalIgnoreCase) >= 0 || typeName.IndexOf("Postgre", StringComparison.OrdinalIgnoreCase) >= 0) return PostgreSqlGetTableNamesSql;
            if (typeName.IndexOf("SqlConnection", StringComparison.OrdinalIgnoreCase) >= 0 || typeName.IndexOf("SqlClient", StringComparison.OrdinalIgnoreCase) >= 0) return SqlServerGetTableNamesSql;

            return SqlServerGetTableNamesSql;
        }

        /// <summary>
        /// 使用预定义 SQL 查询当前连接可见的表名。
        /// </summary>
        public static IList<string> GetTableNames(DbConnection connection)
        {
            return GetTableNames(connection, GetPredefinedGetTableNamesSql(connection));
        }

        /// <summary>
        /// 为指定数据库表元信息生成实体类代码。
        /// </summary>
        /// <param name="table">数据库表元信息。</param>
        /// <param name="namespaceName">生成代码的命名空间，若为 null 或空则不生成 namespace 声明。</param>
        /// <param name="classNameSelector">类名选择器，将表名转换为类名，为 null 时使用默认转换。</param>
        /// <param name="propertyNameSelector">属性名选择器，将列信息转换为属性名，为 null 时使用默认转换。</param>
        /// <returns>C# 源代码字符串。</returns>
        public static string GenerateEntityCode(
            DatabaseTableInfo table,
            string namespaceName = "Models",
            Func<string, string> classNameSelector = null,
            Func<DatabaseColumnInfo, string> propertyNameSelector = null)
        {
            if (table is null) throw new ArgumentNullException(nameof(table));
            if (string.IsNullOrWhiteSpace(table.TableName)) throw new ArgumentException("TableName is required.", nameof(table));
            if (table.Columns is null) throw new ArgumentException("Columns is required.", nameof(table));

            var sb = new StringBuilder();
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using System.Data;");
            sb.AppendLine("using LiteOrm.Common;");
            sb.AppendLine();

            bool hasNamespace = !string.IsNullOrEmpty(namespaceName);
            if (hasNamespace)
            {
                sb.AppendLine($"namespace {namespaceName}");
                sb.AppendLine("{");
            }

            string tableName = table.TableName;
            string className = string.IsNullOrWhiteSpace(table.ClassName)
                ? (classNameSelector?.Invoke(tableName) ?? ToPascalCaseIdentifier(tableName, "Entity"))
                : table.ClassName;

            if (!string.IsNullOrEmpty(table.DataSource))
                sb.AppendLine($"    [Table(\"{tableName}\", DataSource = \"{table.DataSource}\")] ");
            else
                sb.AppendLine($"    [Table(\"{tableName}\")] ");

            sb.AppendLine($"    public class {className}");
            sb.AppendLine("    {");

            foreach (var col in table.Columns)
            {
                if (col is null || string.IsNullOrWhiteSpace(col.ColumnName)) continue;

                string propertyName = string.IsNullOrWhiteSpace(col.PropertyName)
                    ? (propertyNameSelector?.Invoke(col) ?? ToPascalCaseIdentifier(col.ColumnName, "Field"))
                    : col.PropertyName;

                var attrParts = new List<string>();
                if (!string.Equals(col.ColumnName, propertyName, StringComparison.Ordinal))
                    attrParts.Add($"\"{col.ColumnName}\"");

                var attrProps = new List<string>();
                if (col.IsPrimaryKey) attrProps.Add("IsPrimaryKey = true");
                if (col.IsIdentity) attrProps.Add("IsIdentity = true");
                if (col.IsTimestamp) attrProps.Add("IsTimestamp = true");
                if (!col.AllowNull) attrProps.Add("AllowNull = false");
                if (col.Length > 0) attrProps.Add($"Length = {col.Length}");
                if (col.DbType.HasValue) attrProps.Add($"DbType = DbType.{col.DbType.Value}");
                if (col.IsIndex) attrProps.Add("IsIndex = true");
                if (col.IsUnique) attrProps.Add("IsUnique = true");
                if (!string.IsNullOrWhiteSpace(col.IdentityExpression)) attrProps.Add($"IdentityExpression = \"{col.IdentityExpression}\"");
                if (!string.IsNullOrWhiteSpace(col.DefaultValue)) attrProps.Add($"DefaultValue = \"{col.DefaultValue}\"");

                if (attrParts.Count > 0 || attrProps.Count > 0)
                {
                    var attrBuilder = new StringBuilder();
                    attrBuilder.Append("        [Column(");
                    if (attrParts.Count > 0) attrBuilder.Append(string.Join(", ", attrParts));
                    if (attrProps.Count > 0)
                    {
                        if (attrParts.Count > 0) attrBuilder.Append(", ");
                        attrBuilder.Append(string.Join(", ", attrProps));
                    }
                    attrBuilder.Append(")]");
                    sb.AppendLine(attrBuilder.ToString());
                }

                var clrType = col.ClrType ?? (col.DbType.HasValue ? MapDbTypeToClrType(col.DbType.Value) : typeof(string));
                string typeName = GetCSharpTypeName(clrType, col.AllowNull);
                sb.AppendLine($"        public {typeName} {propertyName} {{ get; set; }}");
                sb.AppendLine();
            }

            sb.AppendLine("    }");
            if (hasNamespace) sb.AppendLine("}");
            return sb.ToString();
        }

        /// <summary>
        /// 为多个数据库表生成实体代码，返回表名到代码的字典。
        /// </summary>
        public static IDictionary<string, string> GenerateEntityCodes(
            IEnumerable<DatabaseTableInfo> tables,
            string namespaceName = "Models",
            Func<string, string> classNameSelector = null,
            Func<DatabaseColumnInfo, string> propertyNameSelector = null)
        {
            if (tables is null) throw new ArgumentNullException(nameof(tables));
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var t in tables)
            {
                if (t is null || string.IsNullOrWhiteSpace(t.TableName)) continue;
                dict[t.TableName] = GenerateEntityCode(t, namespaceName, classNameSelector, propertyNameSelector);
            }
            return dict;
        }

        private static DataTable GetColumnsSchema(DbConnection connection, string tableName)
        {
            try
            {
                var dt = connection.GetSchema("Columns", new[] { null, null, tableName, null });
                if (dt is not null && dt.Rows.Count > 0) return dt;
            }
            catch { }

            try
            {
                var dt = connection.GetSchema("Columns", new[] { null, tableName, null });
                if (dt is not null && dt.Rows.Count > 0) return dt;
            }
            catch { }

            try
            {
                var dt = connection.GetSchema("Columns", new[] { tableName });
                if (dt is not null && dt.Rows.Count > 0) return dt;
            }
            catch { }

            var allColumns = connection.GetSchema("Columns");
            if (allColumns is null) return new DataTable();

            var filtered = allColumns.Clone();
            foreach (DataRow row in allColumns.Rows)
            {
                var currentTable = GetString(row, "TABLE_NAME", "table_name");
                if (string.Equals(currentTable, tableName, StringComparison.OrdinalIgnoreCase))
                    filtered.ImportRow(row);
            }
            return filtered;
        }

        private static bool EnsureConnectionOpen(DbConnection connection)
        {
            if (connection.State == ConnectionState.Open) return false;
            connection.Open();
            return true;
        }

        private static Type TryGetClrType(DataRow row)
        {
            var typeObj = GetValue(row, "DATA_TYPE", "data_type", "ProviderType", "provider_type", "DataType", "dataType");

            if (typeObj is Type t) return t;
            if (typeObj is int || typeObj is short || typeObj is long)
            {
                var dbType = (DbType)Convert.ToInt32(typeObj);
                return MapDbTypeToClrType(dbType);
            }

            if (typeObj is string str)
            {
                if (Type.GetType(str, false) is Type clrType) return clrType;
                if (TryParseDbType(str, out var dbType)) return MapDbTypeToClrType(dbType);
            }

            return null;
        }

        private static DbType? TryGetDbType(DataRow row)
        {
            var typeObj = GetValue(row, "DATA_TYPE", "data_type", "ProviderType", "provider_type");
            if (typeObj is null || typeObj == DBNull.Value) return null;

            if (typeObj is DbType dbType) return dbType;
            if (typeObj is int || typeObj is short || typeObj is long) return (DbType)Convert.ToInt32(typeObj);
            if (typeObj is string typeName && TryParseDbType(typeName, out var parsed)) return parsed;

            return null;
        }

        private static bool TryParseDbType(string typeName, out DbType dbType)
        {
            dbType = DbType.String;
            if (string.IsNullOrWhiteSpace(typeName)) return false;

            var n = typeName.Trim().ToLowerInvariant();
            if (n.Contains("bigint")) { dbType = DbType.Int64; return true; }
            if (n.Contains("smallint")) { dbType = DbType.Int16; return true; }
            if (n == "int" || n.Contains("integer") || n.Contains("number(10")) { dbType = DbType.Int32; return true; }
            if (n.Contains("tinyint")) { dbType = DbType.Byte; return true; }
            if (n.Contains("bit") || n.Contains("bool")) { dbType = DbType.Boolean; return true; }
            if (n.Contains("decimal") || n.Contains("numeric") || n.Contains("money") || n.Contains("number(")) { dbType = DbType.Decimal; return true; }
            if (n.Contains("double")) { dbType = DbType.Double; return true; }
            if (n.Contains("float") || n.Contains("real")) { dbType = DbType.Single; return true; }
            if (n.Contains("datetimeoffset")) { dbType = DbType.DateTimeOffset; return true; }
            if (n.Contains("datetime") || n == "timestamp") { dbType = DbType.DateTime; return true; }
            if (n == "date") { dbType = DbType.Date; return true; }
            if (n == "time") { dbType = DbType.Time; return true; }
            if (n.Contains("guid") || n.Contains("uniqueidentifier") || n.Contains("uuid")) { dbType = DbType.Guid; return true; }
            if (n.Contains("binary") || n.Contains("blob") || n.Contains("image") || n == "bytea") { dbType = DbType.Binary; return true; }
            if (n.Contains("char") || n.Contains("text") || n.Contains("clob") || n.Contains("xml") || n.Contains("json")) { dbType = DbType.String; return true; }

            return Enum.TryParse(typeName, true, out dbType);
        }

        private static bool GetPrimaryKeyFlag(DataRow row)
        {
            if (GetBool(row, "IS_PRIMARY_KEY", "is_primary_key", "PRIMARY_KEY", "primary_key") is bool direct) return direct;

            var key = GetString(row, "COLUMN_KEY", "column_key", "CONSTRAINT_TYPE", "constraint_type");
            if (string.IsNullOrWhiteSpace(key)) return false;
            return string.Equals(key, "PRI", StringComparison.OrdinalIgnoreCase)
                || key.IndexOf("PRIMARY", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool GetIdentityFlag(DataRow row)
        {
            if (GetBool(row, "IS_IDENTITY", "is_identity", "AUTOINCREMENT", "autoincrement", "AUTO_INCREMENT", "auto_increment") is bool direct)
                return direct;

            var extra = GetString(row, "EXTRA", "extra");
            return !string.IsNullOrWhiteSpace(extra) && extra.IndexOf("auto_increment", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool GetNullableFlag(DataRow row, bool defaultValue, params string[] candidateNames)
        {
            var boolVal = GetBool(row, candidateNames);
            if (boolVal.HasValue) return boolVal.Value;

            var strVal = GetString(row, candidateNames);
            if (string.IsNullOrWhiteSpace(strVal)) return defaultValue;
            if (strVal.Equals("YES", StringComparison.OrdinalIgnoreCase) || strVal.Equals("Y", StringComparison.OrdinalIgnoreCase)) return true;
            if (strVal.Equals("NO", StringComparison.OrdinalIgnoreCase) || strVal.Equals("N", StringComparison.OrdinalIgnoreCase)) return false;
            if (int.TryParse(strVal, out var n)) return n != 0;

            return defaultValue;
        }

        private static int? GetInt(DataRow row, params string[] candidateNames)
        {
            var value = GetValue(row, candidateNames);
            if (value is null || value == DBNull.Value) return null;
            if (value is int i) return i;
            if (int.TryParse(Convert.ToString(value), out var n)) return n;
            return null;
        }

        private static bool? GetBool(DataRow row, params string[] candidateNames)
        {
            var value = GetValue(row, candidateNames);
            if (value is null || value == DBNull.Value) return null;
            if (value is bool b) return b;

            var s = Convert.ToString(value);
            if (string.IsNullOrWhiteSpace(s)) return null;
            if (s.Equals("1") || s.Equals("Y", StringComparison.OrdinalIgnoreCase) || s.Equals("YES", StringComparison.OrdinalIgnoreCase) || s.Equals("TRUE", StringComparison.OrdinalIgnoreCase)) return true;
            if (s.Equals("0") || s.Equals("N", StringComparison.OrdinalIgnoreCase) || s.Equals("NO", StringComparison.OrdinalIgnoreCase) || s.Equals("FALSE", StringComparison.OrdinalIgnoreCase)) return false;

            return null;
        }

        private static string GetString(DataRow row, params string[] candidateNames)
        {
            var value = GetValue(row, candidateNames);
            if (value is null || value == DBNull.Value) return null;
            return Convert.ToString(value);
        }

        private static object GetValue(DataRow row, params string[] candidateNames)
        {
            if (row is null || row.Table is null || candidateNames is null) return null;

            foreach (var name in candidateNames)
            {
                if (string.IsNullOrWhiteSpace(name)) continue;
                if (!row.Table.Columns.Contains(name)) continue;
                return row[name];
            }

            return null;
        }

        private static Type MapDbTypeToClrType(DbType dbType)
        {
            switch (dbType)
            {
                case DbType.Boolean: return typeof(bool);
                case DbType.Byte: return typeof(byte);
                case DbType.SByte: return typeof(sbyte);
                case DbType.Int16: return typeof(short);
                case DbType.UInt16: return typeof(ushort);
                case DbType.Int32: return typeof(int);
                case DbType.UInt32: return typeof(uint);
                case DbType.Int64: return typeof(long);
                case DbType.UInt64: return typeof(ulong);
                case DbType.Single: return typeof(float);
                case DbType.Double: return typeof(double);
                case DbType.Decimal:
                case DbType.Currency:
                case DbType.VarNumeric: return typeof(decimal);
                case DbType.Date:
                case DbType.DateTime:
                case DbType.DateTime2: return typeof(DateTime);
                case DbType.DateTimeOffset: return typeof(DateTimeOffset);
                case DbType.Time: return typeof(TimeSpan);
                case DbType.Guid: return typeof(Guid);
                case DbType.Binary: return typeof(byte[]);
                case DbType.AnsiString:
                case DbType.String:
                case DbType.AnsiStringFixedLength:
                case DbType.StringFixedLength:
                case DbType.Xml: return typeof(string);
                default: return typeof(string);
            }
        }

        private static string GetCSharpTypeName(Type type, bool allowNull)
        {
            if (type is null) return "object";
            Type underlying = Nullable.GetUnderlyingType(type);
            bool isNullableValue = underlying != null;
            Type baseType = underlying ?? type;

            string name;
            if (baseType == typeof(int)) name = "int";
            else if (baseType == typeof(long)) name = "long";
            else if (baseType == typeof(short)) name = "short";
            else if (baseType == typeof(byte)) name = "byte";
            else if (baseType == typeof(bool)) name = "bool";
            else if (baseType == typeof(decimal)) name = "decimal";
            else if (baseType == typeof(double)) name = "double";
            else if (baseType == typeof(float)) name = "float";
            else if (baseType == typeof(string)) name = "string";
            else if (baseType == typeof(DateTime)) name = "DateTime";
            else if (baseType == typeof(DateTimeOffset)) name = "DateTimeOffset";
            else if (baseType == typeof(Guid)) name = "Guid";
            else if (baseType == typeof(TimeSpan)) name = "TimeSpan";
            else if (baseType == typeof(byte[])) name = "byte[]";
            else if (baseType.IsGenericType)
            {
                var genericDef = baseType.GetGenericTypeDefinition();
                var genericArgs = baseType.GetGenericArguments().Select(t => GetCSharpTypeName(t, true));
                var defName = genericDef.Name;
                var idx = defName.IndexOf('`');
                if (idx > 0) defName = defName.Substring(0, idx);
                name = defName + "<" + string.Join(", ", genericArgs) + ">";
            }
            else
            {
                name = baseType.Name;
            }

            if ((baseType.IsValueType && (isNullableValue || allowNull)) && !name.EndsWith("?"))
                name += "?";

            return name;
        }

        /// <summary>
        /// 将原始字符串转换为 PascalCase 标识符。
        /// </summary>
        /// <param name="raw">原始字符串。</param>
        /// <param name="fallback">原始字符串为空时的回退值。</param>
        /// <returns>PascalCase 标识符。</returns>
        public static string ToPascalCaseIdentifier(string raw, string fallback)
        {
            if (string.IsNullOrWhiteSpace(raw)) return fallback;
            var parts = raw.Split(new[] { '_', '-', ' ', '.' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return fallback;

            var sb = new StringBuilder(raw.Length);
            foreach (var p in parts)
            {
                var token = p.Trim();
                if (token.Length == 0) continue;
                if (char.IsLetter(token[0]))
                    sb.Append(char.ToUpperInvariant(token[0])).Append(token.Substring(1));
                else
                    sb.Append('_').Append(token);
            }

            if (sb.Length == 0) return fallback;
            if (!char.IsLetter(sb[0]) && sb[0] != '_') sb.Insert(0, '_');
            return sb.ToString();
        }
    }

    /// <summary>
    /// 数据库表元信息。
    /// </summary>
    public class DatabaseTableInfo
    {
        /// <summary>表名。</summary>
        public string TableName { get; set; }
        /// <summary>类名。</summary>
        public string ClassName { get; set; }
        /// <summary>数据源名称。</summary>
        public string DataSource { get; set; }
        /// <summary>列信息集合。</summary>
        public IList<DatabaseColumnInfo> Columns { get; set; } = new List<DatabaseColumnInfo>();
    }

    /// <summary>
    /// 数据库列元信息。
    /// </summary>
    public class DatabaseColumnInfo
    {
        /// <summary>列名。</summary>
        public string ColumnName { get; set; }
        /// <summary>属性名。</summary>
        public string PropertyName { get; set; }
        /// <summary>CLR 类型。</summary>
        public Type ClrType { get; set; }
        /// <summary>数据库类型。</summary>
        public DbType? DbType { get; set; }
        /// <summary>是否为主键。</summary>
        public bool IsPrimaryKey { get; set; }
        /// <summary>是否为自增列。</summary>
        public bool IsIdentity { get; set; }
        /// <summary>是否为时间戳列。</summary>
        public bool IsTimestamp { get; set; }
        /// <summary>是否建有索引。</summary>
        public bool IsIndex { get; set; }
        /// <summary>是否为唯一索引。</summary>
        public bool IsUnique { get; set; }
        /// <summary>列长度。</summary>
        public int Length { get; set; }
        /// <summary>是否允许为空。</summary>
        public bool AllowNull { get; set; } = true;
        /// <summary>自增表达式。</summary>
        public string IdentityExpression { get; set; }
        /// <summary>列的默认值。</summary>
        public string DefaultValue { get; set; }
    }
}
