using LiteOrm.Common;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LiteOrm
{
    /// <summary>
    /// 实体类的查询数据访问对象实现
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <remarks>
    /// ObjectViewDAO&lt;T&gt; 是 IObjectViewDAO&lt;T&gt; 接口的实现，提供针对特定实体类型的查询操作。
    /// 
    /// 主要功能包括：
    /// 1. 单对象查询 - 根据主键获取单个实体对象
    /// 2. 列表查询 - 根据条件获取实体对象列表
    /// 3. 分页查询 - 支持带分页参数的查询操作
    /// 4. 存在性检查 - 检查实体是否存在于数据库中
    /// 5. 关联查询 - 支持多表关联查询以获取关联的实体数据
    /// 6. 异步查询 - 提供基于 Task 的异步查询方法
    /// 7. 动态条件查询 - 支持使用 Lambda 表达式或 Expr 对象构建动态查询条件
    /// 
    /// 该类继承自 DAOBase 并实现了相应的查询接口，
    /// 处理复杂的SQL生成、参数处理和数据映射工作。
    /// 它支持与 TableJoinAttribute 定义的多表关联进行查询。
    /// </remarks>
    [AutoRegister(ServiceLifetime.Scoped)]
    public class ObjectViewDAO<T> : DAOBase, IObjectViewDAO<T> where T : new()
    {

        #region 属性
        /// <summary>
        /// 实体对象类型
        /// </summary>
        public override Type ObjectType
        {
            get { return typeof(T); }
        }

        /// <summary>
        /// 查询关联表
        /// </summary>
        public override SqlTable Table
        {
            get { return TableInfoProvider.GetTableView(ObjectType); }
        }

        /// <summary>
        /// <see cref="ObjectViewDAO{T}"/> 为视图DAO，视图DAO不支持增删改操作
        /// </summary>
        protected override bool IsView => true;

        /// <summary>
        /// 使用指定的参数创建新的DAO实例
        /// </summary>
        /// <param name="args">表名参数</param>
        /// <returns>新的DAO实例</returns>
        public ObjectViewDAO<T> WithArgs(params string[] args)
        {
            ObjectViewDAO<T> newDAO = MemberwiseClone() as ObjectViewDAO<T>;
            newDAO.TableArgs = args;
            return newDAO;
        }
        #endregion

        #region 预定义Command
        /// <summary>
        /// 实现获取对象操作的IDbCommand
        /// </summary>
        protected virtual DbCommandProxy MakeGetObjectCommand()
        {
            DbCommandProxy command = NewCommand();
            string where = MakeKeyCondition(command);
            command.CommandText = $"SELECT {AllFields} \nFROM {From} {ToWhereSql(where)}";
            return command;
        }


        /// <summary>
        /// 实现检查对象是否存在操作的IDbCommand
        /// </summary>
        protected virtual DbCommandProxy MakeObjectExistsCommand()
        {
            ThrowExceptionIfNoKeys();
            DbCommandProxy command = NewCommand();
            StringBuilder strConditions = new StringBuilder();
            foreach (ColumnDefinition key in TableDefinition.Keys)
            {
                if (strConditions.Length != 0) strConditions.Append(" AND ");
                strConditions.AppendFormat("{0} = {1}", ToColumnSql(key), ToSqlParam(key.PropertyName));
                DbParameter param = command.CreateParameter();
                param.Size = key.Length;
                param.DbType = key.DbType;
                param.ParameterName = ToParamName(key.PropertyName);
                command.Parameters.Add(param);
            }
            command.CommandText = $"SELECT 1 \nFROM {FactTableName} {ToWhereSql(strConditions.ToString())}";
            return command;
        }
        #endregion

        #region 常用方法

        /// <summary>
        /// 将一行记录转化为对象
        /// </summary>
        /// <param name="record">一行记录</param>
        /// <returns>对象</returns>
        protected Func<DbDataReader, T> ConvertToObjectHandler = DataReaderConverter.GetConverter<T>();

        #endregion

        #region 方法

        /// <summary>
        /// 根据主键获取对象
        /// </summary>
        /// <param name="keys">主键，多个主键按照主键名顺序排列</param>
        /// <returns>可枚举结果对象，可通过FirstOrDefault()和FirstOrDefaultAsync()获取结果</returns>
        public virtual EnumerableResult<T> GetObject(params object[] keys)
        {
            ThrowExceptionIfWrongKeys(keys);
            var getObjectCommand = GetPreparedCommand("GetObject", MakeGetObjectCommand);
            int i = 0;
            foreach (DbParameter param in getObjectCommand.Parameters)
            {
                param.Value = ConvertToDbValue(keys[i], TableDefinition.Keys[i].DbType);
                i++;
            }
            return new EnumerableResult<T>(getObjectCommand, ConvertToObjectHandler, false);
        }


        /// <summary>
        /// 获取符合条件的对象个数
        /// </summary>
        /// <param name="expr">属性名与值的列表，若为null则表示没有条件</param>
        /// <returns>值结果对象，可通过GetValue()和GetValueAsync()获取结果</returns>
        public virtual ValueResult<int> Count(Expr expr)
        {
            var selectExpr = new SelectExpr(expr.ToSource<T>(), Expr.Aggregate("Count", Expr.Const(1)));
            var command = MakeExprCommand(selectExpr);
            return new ValueResult<int>(command);
        }

        /// <summary>
        /// 判断对象是否存在
        /// </summary>
        /// <param name="o">对象</param>
        /// <returns>值结果对象，可通过GetValue()和GetValueAsync()获取结果</returns>
        public virtual ValueResult<bool> Exists(object o)
        {
            if (o is null) throw new ArgumentNullException("o");
            return ExistsKey(GetKeyValues(o));
        }

        /// <summary>
        /// 判断对象是否存在
        /// </summary>
        /// <param name="o">对象</param>
        /// <returns>值结果对象，可通过GetValue()和GetValueAsync()获取结果</returns>
        public virtual ValueResult<bool> Exists(T o)
        {
            if (o is null) throw new ArgumentNullException("o");
            return ExistsKey(GetKeyValues(o));
        }

        /// <summary>
        /// 判断主键对应的对象是否存在
        /// </summary>
        /// <param name="keys">主键，多个主键按照名称顺序排列</param>
        /// <returns>值结果对象，可通过GetValue()和GetValueAsync()获取结果</returns>
        public virtual ValueResult<bool> ExistsKey(params object[] keys)
        {
            ThrowExceptionIfWrongKeys(keys);
            var objectExistsCommand = GetPreparedCommand("ExistsKey", MakeObjectExistsCommand);
            int i = 0;
            foreach (DbParameter param in objectExistsCommand.Parameters)
            {
                param.Value = ConvertToDbValue(keys[i], TableDefinition.Keys[i].DbType);
                i++;
            }
            return new ValueResult<bool>(objectExistsCommand, (obj) => obj != null && Convert.ToInt32(obj) > 0, false);
        }

        /// <summary>
        /// 判断符合条件的对象是否存在
        /// </summary>
        /// <param name="expr">属性名与值的列表，若为null则表示没有条件</param>
        /// <returns>值结果对象，可通过GetValue()和GetValueAsync()获取结果</returns>
        public virtual ValueResult<bool> Exists(Expr expr)
        {
            var selectExpr = new SelectExpr(expr.ToSource<T>(), Expr.Const(1));
            var command = MakeExprCommand(selectExpr);
            return new ValueResult<bool>(command, (obj) => obj != null);
        }

        /// <summary>
        /// 根据条件查询，多个条件以逻辑与连接
        /// </summary>
        /// <param name="expr">属性名与值的列表，若为null则表示没有条件</param>
        /// <returns>符合条件的对象枚举，同时支持同步和异步操作</returns>
        public virtual EnumerableResult<T> Search(Expr expr = null)
        {
            var command = MakeSelectExprCommand(expr);
            return new EnumerableResult<T>(command, ConvertToObjectHandler);
        }


#if NET8_0_OR_GREATER
        /// <summary>
        /// 使用带参数的SQL查询
        /// </summary>
        /// <param name="sqlBody">查询SQL，使用插值字符串格式</param>
        /// <param name="isFull">传入的是否是完整SQL，默认为 false</param>
        /// <returns>符合条件的对象枚举，同时支持同步和异步操作</returns>
        /// <example>
        /// var users = objectViewDAO.Search($"WHERE {Expr.Prop("Age") > 20 }");
        /// </example>
        public virtual EnumerableResult<T> Search([InterpolatedStringHandlerArgument("")] ref ExprString sqlBody, bool isFull = false)
        {
            string sql = isFull ? sqlBody.GetSqlResult() : $"SELECT {AllFields} FROM {From} {sqlBody.GetSqlResult()}";
            var command = MakeNamedParamCommand(sql, sqlBody.GetParams());
            return new EnumerableResult<T>(command, ConvertToObjectHandler);
        }
#endif

        #endregion

        #region IObjectViewDAO Members

        /// <summary>
        /// 根据主键获取对象（接口实现）
        /// </summary>
        /// <param name="keys">主键，多个主键按照主键名顺序排列</param>
        /// <returns>对象，若存在则返回null</returns>
        IEnumerableResult IObjectViewDAO.GetObject(params object[] keys)
        {
            return GetObject(keys);
        }

        /// <summary>
        /// 根据条件查询，多个条件以逻辑与连接（接口实现）
        /// </summary>
        /// <param name="expr">属性名与值的列表，若为null则表示没有条件</param>
        /// <returns>符合条件的对象列表</returns>
        IEnumerableResult IObjectViewDAO.Search(Expr expr)
        {
            return Search(expr);
        }

        #endregion

    }
}
