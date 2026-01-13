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
    /// SQL语句生成辅助类 - 提供数据库无关的SQL生成功能
    /// </summary>
    /// <remarks>
    /// SqlBuilder 是一个抽象SQL生成器类，提供了生成各种SQL语句的基础功能。
    /// 它实现了 ISqlBuilder 接口，为不同的数据库系统提供了可扩展的基础。
    /// 
    /// 主要功能包括：
    /// 1. SQL语句生成 - 生成 SELECT、INSERT、UPDATE、DELETE 等 SQL 语句
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
    /// string sqlName = builder.ToSqlName(\"UserName\"); // 可能返回 \"[UserName]\" 或 \"`UserName`\"
    /// 
    /// // 参数名称生成
    /// string paramName = builder.ToSqlParam(\"id\"); // 可能返回 \"@id\" 或 \":id\"
    /// 
    /// // 类型映射
    /// DbType dbType = builder.ToDbType(typeof(string)); // 返回 DbType.String
    /// </code>
    /// </remarks>
    /// <summary>
    /// SQL 语句构建器基类。
    /// </summary>
    public class SqlBuilder : ISqlBuilder
    {
        /// <summary>
        /// 获取默认的 <see cref="SqlBuilder"/> 实例。
        /// </summary>
        public static readonly SqlBuilder Instance = new SqlBuilder();

        private ConcurrentDictionary<Type, DbType> typeToDbTypeCache = new ConcurrentDictionary<Type, DbType>();

        private Dictionary<string, string> _functionMappings = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
        {
            ["ToUpper"] = "UPPER",
            ["ToLower"] = "LOWER",
            ["Now"] = "CURRENT_TIMESTAMP",
            ["Today"] = "CURRENT_DATE",
            ["Max"] = "GREATEST",
            ["Min"] = "LEAST"
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
            InitTypeToDbType();
            InitializeFunctionMappings(_functionMappings);
        }

        /// <summary>
        /// 初始化类型到 <see cref="DbType"/> 的映射关系。
        /// </summary>
        protected virtual void InitTypeToDbType()
        {
            typeToDbTypeCache[typeof(Enum)] = DbType.Int32;
            typeToDbTypeCache[typeof(Byte)] = DbType.Byte;
            typeToDbTypeCache[typeof(Byte[])] = DbType.Binary;
            typeToDbTypeCache[typeof(Char)] = DbType.String;
            typeToDbTypeCache[typeof(Boolean)] = DbType.Boolean;
            typeToDbTypeCache[typeof(DateTime)] = DbType.DateTime;
            typeToDbTypeCache[typeof(Decimal)] = DbType.Decimal;
            typeToDbTypeCache[typeof(Double)] = DbType.Double;
            typeToDbTypeCache[typeof(Guid)] = DbType.Guid;
            typeToDbTypeCache[typeof(Int16)] = DbType.Int16;
            typeToDbTypeCache[typeof(Int32)] = DbType.Int32;
            typeToDbTypeCache[typeof(Int64)] = DbType.Int64;
            typeToDbTypeCache[typeof(SByte)] = DbType.SByte;
            typeToDbTypeCache[typeof(Single)] = DbType.Single;
            typeToDbTypeCache[typeof(String)] = DbType.String;
            typeToDbTypeCache[typeof(TimeSpan)] = DbType.Time;
            typeToDbTypeCache[typeof(UInt16)] = DbType.UInt16;
            typeToDbTypeCache[typeof(UInt32)] = DbType.UInt32;
            typeToDbTypeCache[typeof(UInt64)] = DbType.UInt64;
            typeToDbTypeCache[typeof(DateTimeOffset)] = DbType.DateTimeOffset;
        }

        /// <summary>
        /// 注册类型到 <see cref="DbType"/> 的映射关系。
        /// </summary>
        /// <param name="type">.NET 类型。</param>
        /// <param name="dbType">数据库类型。</param>
        public void RegisterDbType(Type type, DbType dbType)
        {
            typeToDbTypeCache[type] = dbType;
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

        /// <summary>
        /// 构建函数调用的 SQL 片段。
        /// </summary>
        /// <param name="functionName">函数名。</param>
        /// <param name="args">参数列表。</param>
        /// <returns>构建后的 SQL 片段。</returns>
        public virtual string BuildFunctionSql(string functionName, params string[] args)
        {
            switch (functionName.ToUpper())
            {
                case "NOW":
                    return "CURRENT_TIMESTAMP";
                case "TODAY":
                    return "CURRENT_DATE";
            }
            string dbFunctionName = ReplaceFunctionName(functionName);
            return $"{dbFunctionName}({string.Join(", ", args)})";
        }

        /// <summary>
        /// 数据类型转换为DbType
        /// </summary>
        /// <param name="type">数据类型</param>
        /// <returns></returns>
        public DbType GetDbType(Type type)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;
            if (!typeToDbTypeCache.ContainsKey(type) && type.IsEnum) type = typeof(Enum);
            if (typeToDbTypeCache.ContainsKey(type)) return typeToDbTypeCache[type];
            return DbType.Object;
        }

        /// <summary>
        /// 获取指定数据类型的默认长度
        /// </summary>
        /// <param name="columnType">数据库列的数据类型</param>
        /// <returns></returns>
        public int GetDefaultLength(DbType columnType)
        {
            switch (columnType)
            {
                case DbType.String:
                case DbType.AnsiString:
                case DbType.AnsiStringFixedLength:
                case DbType.StringFixedLength: return 255;
                case DbType.Byte:
                case DbType.Boolean: return 1;
                case DbType.Single:
                case DbType.Int32: return 4;
                case DbType.Double: return 8;
                case DbType.Xml: return 1 << 20;
                case DbType.Binary: return Int32.MaxValue;
                default: return 0;
            }
        }

        #region 预定义变量
        /// <summary>
        /// SQL语句中like条件中的转义符
        /// </summary>
        public const char LikeEscapeChar = '\\';
        /// <summary>
        /// 对like条件的字符串内容中的转义符进行替换的正则表达
        /// </summary>
        protected Regex sqlLikeEscapeReg = new Regex(@"([_/%\[\]])");
        /// <summary>
        /// 查找列名、表名等的正则表达式
        /// </summary>
        protected static Regex sqlNameRegex = new Regex(@"\[([^\]]+)\]");
        #endregion
        /// <summary>
        /// 将字符串内容转义为适合 Like 查询的值。
        /// </summary>
        /// <param name="value">要转义的字符串。</param>
        /// <returns>转义后的字符串。</returns>
        public virtual string ToSqlLikeValue(string value)
        {
            return sqlLikeEscapeReg.Replace(value, LikeEscapeChar + "$1");
        }

        /// <summary>
        /// 生成带标识列的插入SQL
        /// </summary>
        /// <param name="command">SQLCommand</param>
        /// <param name="identityColumn">标识列</param>
        /// <param name="tableName">表名</param>
        /// <param name="strColumns">插入数据列名称</param>
        /// <param name="strValues">插入数据名称</param>
        /// <returns></returns>
        public virtual string BuildIdentityInsertSQL(IDbCommand command, ColumnDefinition identityColumn, string tableName, string strColumns, string strValues)
        {
            return $"BEGIN insert into {ToSqlName(tableName)} ({strColumns}) \nvalues ({strValues}); select @@IDENTITY as [ID]; END;";
        }

        /// <summary>
        /// 连接各字符串的SQL语句
        /// </summary>
        /// <param name="strs">需要连接的sql字符串</param>
        /// <returns>SQL语句</returns>
        public virtual string BuildConcatSql(params string[] strs)
        {
            return String.Join(" + ", strs);
        }

        /// <summary>
        /// 生成分页查询的SQL语句
        /// </summary>
        /// <param name="select">select内容</param>
        /// <param name="from">from块</param>
        /// <param name="where">where条件</param>
        /// <param name="orderBy">排序</param>
        /// <param name="startIndex">起始位置，从0开始</param>
        /// <param name="sectionSize">查询条数</param>
        /// <returns></returns>
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
    }

}


