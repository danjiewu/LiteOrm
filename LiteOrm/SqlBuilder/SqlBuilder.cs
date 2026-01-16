using System;
using System.Collections.Generic;
using System.Text;
using LiteOrm.Common;
using System.Text.RegularExpressions;
using System.Collections;
using System.Data;
using System.Collections.Concurrent;

namespace LiteOrm
{
    /// <summary>
    /// SQL 语句生成辅助类 - 提供数据库无关的 SQL 生成功能。
    /// SqlBuilder 是一个抽象 SQL 生成器类，提供了生成各种 SQL 语句的基础功能。
    /// 它实现了 ISqlBuilder 接口，为不同的数据库系统提供了可扩展的基础。
    /// </summary>
    /// <remarks>
    /// 主要功能包括：
    /// 1. SQL 语句生成 - 生成 SELECT、INSERT、UPDATE、DELETE 等 SQL 语句
    /// 2. 名称转换 - 将 .NET 命名转换为 SQL 数据库命名约定
    /// 3. 参数处理 - 生成数据库特定的参数名称和格式
    /// 4. 类型映射 - 将 .NET 类型映射到数据库 DbType
    /// 5. 函数映射 - 映射 .NET 函数到 SQL 函数
    /// 6. 条件生成 - 生成 WHERE 子句的条件语句
    /// 7. 表达式处理 - 将 Lambda 表达式转换为 SQL 条件
    /// 8. 子类可扩展性 - 通过虚方法允许子类自定义SQL生成逻辑
    /// 
    /// 该类有多个具体实现用于不同的数据库：
    /// - SqlServerBuilder - SQL Server 特定的实现
    /// - MySqlBuilder - MySQL 特定的实现
    /// - OracleBuilder - Oracle 特定的实现
    /// - SQLiteBuilder - SQLite 特定的实现
    /// 
    /// 使用示例：
    /// <code>
    /// // 通常由框架自动选择合适的实现
    /// ISqlBuilder builder = SqlBuilderFactory.Instance.GetSqlBuilder(typeof(SqlConnection));
    /// 
    /// // 名称转换
    /// string sqlName = builder.ToSqlName(\"UserName\"); // 可能返回 \"[UserName]\" 或 \"`UserName`\"`
    /// 
    /// // 参数名称生成
    /// string paramName = builder.ToSqlParam(\"id\"); // 可能返回 \"@id\" 或 \":id\"`
    /// 
    /// // 类型映射
    /// DbType dbType = builder.ToDbType(typeof(string)); // 返回 DbType.String
    /// </code>
    /// </remarks>
    public class SqlBuilder : ISqlBuilder
    {
        /// <summary>
        /// 获取默认的 <see cref="SqlBuilder"/> 实例。
        /// </summary>
        public static readonly SqlBuilder Instance = new SqlBuilder();

        private readonly Dictionary<string, string> _functionMappings = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
        {
            ["Substring"] = "SUBSTR",
            ["ToUpper"] = "UPPER",
            ["ToLower"] = "LOWER",
            ["Now"] = "CURRENT_TIMESTAMP",
            ["Today"] = "CURRENT_DATE",
            ["Max"] = "GREATEST",
            ["Min"] = "LEAST",
            ["IfNull"] = "COALESCE"
        };

        /// <summary>
        /// 初始化函数映射关系（子类重写此方法以提供特定数据库的函数映射）。
        /// </summary>
        /// <param name="functionMappings">函数映射字典。</param>
        protected virtual void InitializeFunctionMappings(Dictionary<string, string> functionMappings) { }

        /// <summary>
        /// 初始化 <see cref="SqlBuilder"/> 类的新实例。
        /// </summary>
        public SqlBuilder()
        {
            InitializeFunctionMappings(_functionMappings);
        }

        /// <summary>
        /// 替换函数名为数据库特定的函数名。
        /// </summary>
        /// <param name="functionName">原始函数名。</param>
        /// <returns>替换后的函数名。</returns>
        public virtual string ReplaceFunctionName(string functionName)
        {
            if (string.IsNullOrWhiteSpace(functionName))
                return functionName;

            string key = functionName.Trim();

            // 如果找到映射则返回数据库函数名，否则返回原名称
            return _functionMappings.TryGetValue(key, out string dbFunctionName)
                ? dbFunctionName
                : functionName;
        }

        private Dictionary<string, Func<string, string[], string>> functionSqlHandlers = new Dictionary<string, Func<string, string[], string>>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 注册自定义函数 SQL 处理程序。
        /// </summary>
        /// <param name="functionName">函数名称。</param>
        /// <param name="handler">处理程序委托，接收函数名和参数数组，返回 SQL 字符串。</param>
        /// <returns>返回是否注册成功。</returns>
        public bool RegisterFunctionSqlHandler(string functionName, Func<string, string[], string> handler)
        {
            if (string.IsNullOrWhiteSpace(functionName) || handler is null)
                return false;
            functionSqlHandlers[functionName] = handler;
            return true;
        }

        /// <summary>
        /// 构建函数调用的 SQL 片段。
        /// </summary>
        /// <param name="functionName">函数名。</param>
        /// <param name="args">参数列表。</param>
        /// <returns>构建后的 SQL 片段。</returns>
        public virtual string BuildFunctionSql(string functionName, params string[] args)
        {
            functionName = ReplaceFunctionName(functionName);
            switch (functionName.ToUpper())
            {
                case "CURRENT_TIMESTAMP":
                case "CURRENT_DATE":
                    return functionName;
                case "SUBSTR":
                case "SUBSTRING":
                    if (args.Length >= 2)
                        args[1] += "+1";
                    break;
            }
            if(functionSqlHandlers.TryGetValue(functionName, out var handler))
            {
                return handler(functionName, args);
            }
            return $"{functionName}({string.Join(", ", args)})";
        }

        #region 内部字段与正则表达式
        /// <summary>
        /// 用于在 LIKE 条件中转义特殊字符的正则表达式。
        /// </summary>
        protected Regex _sqlLikeEscapeReg = new Regex(@"([_/%\[\]])");
        /// <summary>
        /// 用于识别 SQL 对象名称（如 [TableName]）的正则表达式。
        /// </summary>
        protected static Regex _sqlNameRegex = new Regex(@"\[([^\]]+)\]");
        #endregion

        /// <summary>
        /// 是否支持批量插入。
        /// </summary>
        public virtual bool SupportBatchInsert => true;

        /// <summary>
        /// 是否支持标识列插入。
        /// </summary>
        public virtual bool SupportIdentityInsert => true;

        /// <summary>
        /// 将字符串内容转义为适合 LIKE 查询的值。
        /// </summary>
        /// <param name="value">要转义的字符串。</param>
        /// <returns>转义后的字符串。</returns>
        public virtual string ToSqlLikeValue(string value)
        {
            return _sqlLikeEscapeReg.Replace(value, $"{Const.LikeEscapeChar}$1");
        }

        /// <summary>
        /// 生成带标识列的插入 SQL。
        /// </summary>
        /// <param name="command">数据库命令对象。</param>
        /// <param name="identityColumn">标识列定义。</param>
        /// <param name="tableName">目标表名。</param>
        /// <param name="strColumns">插入的列名集合。</param>
        /// <param name="strValues">值参数集合。</param>
        /// <returns>生成的 SQL 语句。</returns>
        public virtual string BuildIdentityInsertSql(IDbCommand command, ColumnDefinition identityColumn, string tableName, string strColumns, string strValues)
        {
            return $"BEGIN insert into {ToSqlName(tableName)} ({strColumns}) \nvalues ({strValues}); select @@IDENTITY as [ID]; END;";
        }

        /// <summary>
        /// 连接各字符串的 SQL 语句
        /// </summary>
        /// <param name="strs">需要连接的sql字符串</param>
        /// <returns>SQL语句</returns>
        public virtual string BuildConcatSql(params string[] strs)
        {
            return $"CONCAT({String.Join(",", strs)})";
        }

        /// <summary>
        /// 生成分页查询的 SQL 语句。
        /// </summary>
        /// <param name="select">SELECT 字段内容。</param>
        /// <param name="from">FROM 子句内容。</param>
        /// <param name="where">WHERE 过滤条件。</param>
        /// <param name="orderBy">ORDER BY 排序子句。</param>
        /// <param name="startIndex">起始行的索引（从 0 开始）。</param>
        /// <param name="sectionSize">请求的数据行数。</param>
        /// <returns>分页 SQL 语句。</returns>
        public virtual string GetSelectSectionSql(string select, string from, string where, string orderBy, int startIndex, int sectionSize)
        {
            if (!String.IsNullOrEmpty(where)) where = "\nwhere " + where;
            return $"select * from (\nselect {select}, Row_Number() over (Order by {orderBy}) as Row_Number \nfrom {from} {where}) TempTable \nwhere Row_Number > {startIndex} and Row_Number <= {startIndex + sectionSize}";
        }

        /// <summary>
        /// 名称转化为数据库合法名称
        /// </summary>
        /// <param name="name">字符串名称</param>
        /// <returns>数据库合法名称</returns>
        public virtual string ToSqlName(string name)
        {
            if (name is null) throw new ArgumentNullException("name");
            return String.Join(".", Array.ConvertAll(name.Split('.'), n => $"[{n}]"));
        }

        /// <summary>
        /// 原始名称转化为数据库参数
        /// </summary>
        /// <param name="nativeName">原始名称</param>
        /// <returns>数据库参数</returns>
        public virtual string ToSqlParam(string nativeName)
        {
            return $"@{nativeName}";
        }

        /// <summary>
        /// 原始名称转化为参数名称
        /// </summary>
        /// <param name="nativeName">原始名称</param>
        /// <returns>参数名称</returns>
        public virtual string ToParamName(string nativeName)
        {
            return nativeName;
        }

        /// <summary>
        /// 参数名称转化为原始名称
        /// </summary>
        /// <param name="paramName">参数名称</param>
        /// <returns>原始名称</returns>
        public virtual string ToNativeName(string paramName)
        {
            return paramName;
        }

        /// <summary>
        /// 将列名、表名等替换为数据库合法名称
        /// </summary>
        /// <param name="sql">sql语句</param>
        /// <returns></returns>
        public virtual string ReplaceSqlName(string sql)
        {
            return sql;
        }

        /// <summary>
        /// 将列名、表名等替换为数据库合法名称
        /// </summary>
        /// <param name="sql">sql语句</param>
        /// <param name="left">左定界符</param>
        /// <param name="right">右定界符</param>
        /// <param name="handler"></param>
        /// <returns></returns>
        protected string ReplaceSqlName(string sql, char left, char right, Func<char, char> handler = null)
        {
            if (sql is null) return null;
            StringBuilder sb = new StringBuilder();
            bool passNext = false;
            Stack<char> stack = new Stack<char>();
            foreach (char ch in sql)
            {
                if (passNext)
                {
                    sb.Append(ch);
                    passNext = false;
                }
                else
                {
                    switch (ch)
                    {
                        case '[': sb.Append(stack.Count == 0 ? left : ch); break;
                        case ']': sb.Append(stack.Count == 0 ? right : ch); break;
                        case '"':
                            if (stack.Count > 0 && stack.Peek() == '"') stack.Pop();
                            else stack.Push('"');
                            sb.Append(ch); break;
                        case '\'':
                            if (stack.Count > 0 && stack.Peek() == '\'') stack.Pop();
                            else stack.Push('\'');
                            sb.Append(ch); break;
                        case '\\': sb.Append(ch); passNext = true; break;
                        default:
                            if (handler is not null)
                            {
                                sb.Append(handler(ch));
                            }
                            else
                                sb.Append(ch);
                            break;
                    }
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// 将数据库取得的值转化为对象属性类型所对应的值。
        /// </summary>
        /// <param name="dbValue">数据库取得的值。</param>
        /// <param name="objectType">目标属性类型。</param>
        /// <returns>转换后的对象值。</returns>
        public object ConvertFromDbValue(object dbValue, Type objectType = null)
        {
            if (objectType == null)
            {
                return (dbValue == null || dbValue == DBNull.Value) ? null : dbValue;
            }

            bool nullable = !objectType.IsValueType || Nullable.GetUnderlyingType(objectType) is not null;

            if (dbValue is null || dbValue == DBNull.Value)
                return nullable ? null : Activator.CreateInstance(objectType);

            Type underlyingType = objectType.GetUnderlyingType();

            if (underlyingType.IsInstanceOfType(dbValue))
                return dbValue;

            if (dbValue is string s && s == string.Empty)
                return nullable ? null : Activator.CreateInstance(objectType);

            if (underlyingType.IsEnum)
            {
                if (dbValue is string strEnum) return Enum.Parse(underlyingType, strEnum, true);
                return Enum.ToObject(underlyingType, Convert.ChangeType(dbValue, Enum.GetUnderlyingType(underlyingType)));
            }

            if (underlyingType == typeof(bool))
            {
                if (dbValue is string strBool)
                {
                    if (bool.TryParse(strBool, out bool result)) return result;
                    if (strBool == "1" || strBool.Equals("Y", StringComparison.OrdinalIgnoreCase) || strBool.Equals("T", StringComparison.OrdinalIgnoreCase)) return true;
                    if (strBool == "0" || strBool.Equals("N", StringComparison.OrdinalIgnoreCase) || strBool.Equals("F", StringComparison.OrdinalIgnoreCase)) return false;
                }
                return Convert.ToInt64(dbValue) != 0;
            }

            if (underlyingType == typeof(Guid))
            {
                if (dbValue is string strGuid && Guid.TryParse(strGuid, out Guid guid)) return guid;
                if (dbValue is byte[] bytesGuid && bytesGuid.Length == 16) return new Guid(bytesGuid);
            }

            if (underlyingType == typeof(TimeSpan))
            {
                if (dbValue is long ticks) return TimeSpan.FromTicks(ticks);
                if (dbValue is string strTs && TimeSpan.TryParse(strTs, out TimeSpan ts)) return ts;
            }

            if (underlyingType == typeof(DateTimeOffset))
            {
                if (dbValue is DateTime dt) return new DateTimeOffset(dt);
                if (dbValue is string strDto && DateTimeOffset.TryParse(strDto, out DateTimeOffset dto)) return dto;
            }

            return Convert.ChangeType(dbValue, underlyingType);

        }

        /// <summary>
        /// 将对象的属性值转化为数据库中的值，根据列的 DbType 进行转换
        /// </summary>
        /// <param name="value">值</param>
        /// <param name="dbType">数据字段类型</param>
        /// <returns>数据库中的值</returns>
        public virtual object ConvertToDbValue(object value, DbType dbType = DbType.Object)
        {
            if (value is null) return DBNull.Value;
            if (dbType == DbType.Object)
                dbType = GetDbType(value.GetType());

            Type type = value.GetType();

            // 处理枚举：优先根据基础类型转换，除非 DbType 要求字符串
            if (type.IsEnum)
            {
                if (dbType == DbType.String || dbType == DbType.AnsiString ||
                    dbType == DbType.StringFixedLength || dbType == DbType.AnsiStringFixedLength)
                {
                    return value.ToString();
                }
                return Convert.ChangeType(value, Enum.GetUnderlyingType(type));
            }

            // 根据 dbType 进行特定转换
            switch (dbType)
            {
                case DbType.Boolean:
                    return Convert.ToBoolean(value);

                case DbType.Int16:
                case DbType.Int32:
                case DbType.Int64:
                case DbType.Byte:
                case DbType.SByte:
                case DbType.UInt16:
                case DbType.UInt32:
                case DbType.UInt64:
                    if (value is bool b) return b ? 1 : 0;
                    return Convert.ChangeType(value, dbType.ToType());

                case DbType.Guid:
                    if (value is Guid guid) return guid;
                    if (value is string s && Guid.TryParse(s, out Guid g)) return g;
                    if (value is byte[] bytes && bytes.Length == 16) return new Guid(bytes);
                    break;

                case DbType.Binary:
                    if (value is Guid g2) return g2.ToByteArray();
                    break;

                case DbType.Date:
                case DbType.DateTime:
                case DbType.DateTime2:
                case DbType.Time:
                    if (value is DateTimeOffset dto) return dto.DateTime;
                    if (value is DateTime date) return date;
                    break;

                case DbType.DateTimeOffset:
                    if (value is DateTime dt) return new DateTimeOffset(dt);
                    break;

                case DbType.String:
                case DbType.AnsiString:
                case DbType.StringFixedLength:
                case DbType.AnsiStringFixedLength:
                    if (value is Guid g3) return g3.ToString();
                    if (value is TimeSpan ts) return ts.ToString();
                    return value.ToString();
            }

            // 兜底通用逻辑
            if (value is bool bv) return bv ? 1 : 0;
            if (value is DateTimeOffset dtov) return dtov.DateTime;
            if (value is TimeSpan tsv) return tsv.Ticks;

            return value;
        }

        /// <summary>
        /// 获取对应的数据库类型
        /// </summary>
        /// <param name="type">要转换的 .NET 类型</param>
        /// <returns>对应的数据库类型</returns>
        public virtual DbType GetDbType(Type type)
        {
            return DbTypeMap.GetDbType(type);
        }

        /// <summary>
        /// 生成批量插入的 SQL 语句。
        /// </summary>
        /// <param name="tableName">目标表名。</param>
        /// <param name="columns">插入的列名集合（逗号分隔的 SQL 名称）。</param>
        /// <param name="valuesList">每个实体的占位符集合（例如 "(@0,@1,@2)"）。</param>
        /// <returns>返回目标数据库可执行的批量插入 SQL 字符串。</returns>
        public virtual string BuildBatchInsertSql(string tableName, string columns, List<string> valuesList)
        {
            return $"INSERT INTO {ToSqlName(tableName)} ({columns}) \nVALUES {string.Join(",", valuesList)}";
        }
    }

}


