using LiteOrm.Common;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LiteOrm
{
    public partial class ObjectViewDAO<T>
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
        /// 使用指定的参数创建新的DAO实例
        /// </summary>
        /// <param name="args">表名参数</param>
        /// <returns>新的DAO实例</returns>
        public ObjectViewDAO<T> WithArgs(params string[] args)
        {
            ObjectViewDAO<T> newDAO = MemberwiseClone() as ObjectViewDAO<T>;
            newDAO.TableNameArgs = args;
            newDAO.SqlBuildContext = null;
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
            command.CommandText = $"select {AllFieldsSql} \nfrom {From} {ToWhereSql(where)}";
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
                if (strConditions.Length != 0) strConditions.Append(" and ");
                strConditions.AppendFormat("{0} = {1}", ToColumnSql(key), ToSqlParam(key.PropertyName));
                IDbDataParameter param = command.CreateParameter();
                param.Size = key.Length;
                param.DbType = key.DbType;
                param.ParameterName = ToParamName(key.PropertyName);
                command.Parameters.Add(param);
            }
            command.CommandText = $"select 1 \nfrom {FactTableName} {ToWhereSql(strConditions.ToString())}";
            return command;
        }

        #endregion

        #region 常用方法

        /// <summary>
        /// 替换 SQL 中的标记为实际 SQL。
        /// </summary>
        /// <param name="sqlWithParam">包含标记的 SQL 语句，标记可以为 ParamAllFields，ParamFromTable。</param>
        /// <returns>替换后的 SQL 语句。</returns>
        protected override string ReplaceParam(string sqlWithParam)
        {
            return base.ReplaceParam(sqlWithParam).Replace(ParamAllFields, AllFieldsSql);
        }

        /// <summary>
        /// 读取所有记录并转化为对象集合，查询 AllFieldsSql 时可用
        /// </summary>
        /// <param name="reader">只读结果集</param>
        /// <returns>对象列表</returns>
        private List<T> ReadAll(IDataReader reader)
        {
            List<T> results = new List<T>();
            while (reader.Read())
            {
                results.Add(ConvertToObject(reader));
            }
            return results;
        }

        /// <summary>
        /// 读取所有记录并转化为对象集合，查询 AllFieldsSql 时可用
        /// </summary>
        /// <param name="reader">只读结果集</param>
        /// <param name="count">查询结果条数</param>
        /// <returns>对象列表</returns>
        private List<T> Read(IDataReader reader, int count)
        {
            List<T> results = new List<T>();
            int i = 0;
            while (reader.Read() && i < count)
            {
                results.Add(ConvertToObject(reader));
                i++;
            }
            return results;
        }

        /// <summary>
        /// 从IDataReader中读取一条记录转化为对象，若无记录则返回null
        /// </summary>
        /// <param name="dataReader">IDataReader</param>
        /// <returns>对象，若无记录则返回null</returns>
        private T ReadOne(IDataReader dataReader)
        {
            return dataReader.Read() ? ConvertToObject(dataReader) : default(T);
        }

        /// <summary>
        /// 将一行记录转化为对象
        /// </summary>
        /// <param name="record">一行记录</param>
        /// <returns>对象</returns>
        protected virtual T ConvertToObject(IDataRecord record)
        {
            T t = new T();
            int count = SelectColumns.Length;
            for (int i = 0; i < count; i++)
            {
                SqlColumn column = SelectColumns[i];
                column.SetValue(t, record.IsDBNull(i) ? null : ConvertFromDbValue(record[i], column.PropertyType));
            }
            return t;
        }


        /// <summary>
        /// 执行 IDbCommand，读取所有记录并转化为对象的集合，查询 AllFieldsSql 时可用
        /// </summary>
        /// <param name="command">待执行的 IDbCommand</param>
        /// <returns></returns>
        protected List<T> GetAll(DbCommandProxy command)
        {
            using (IDataReader reader = command.ExecuteReader())
            {
                return ReadAll(reader);
            }
        }

        /// <summary>
        /// 执行 IDbCommand，读取所有记录并转化为对象的集合，查询 AllFieldsSql 时可用
        /// </summary>
        /// <param name="command">待执行的 IDbCommand</param>
        /// <param name="count">查询结果条数</param>
        /// <returns></returns>
        protected List<T> GetAll(DbCommandProxy command, int count)
        {
            using (IDataReader reader = command.ExecuteReader())
            {
                return Read(reader, count);
            }
        }

        /// <summary>
        /// 执行 IDbCommand，读取一条记录并转化为单个对象，查询 AllFieldsSql 时可用
        /// </summary>
        /// <param name="command">待执行的 IDbCommand</param>
        /// <returns></returns>
        protected T GetOne(DbCommandProxy command)
        {
            using (IDataReader reader = command.ExecuteReader())
            {
                return ReadOne(reader);
            }
        }

        /// <summary>
        /// 异步读取所有记录并转化为对象集合
        /// </summary>
        private async Task<List<T>> ReadAllAsync(IDataReader reader, CancellationToken cancellationToken)
        {
            List<T> results = new List<T>();
            if (reader is AutoLockDataReader autoLockReader)
            {
                while (await autoLockReader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    results.Add(ConvertToObject(reader));
                }
            }
            else if (reader is DbDataReader dbReader)
            {
                while (await dbReader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    results.Add(ConvertToObject(reader));
                }
            }
            else
            {
                while (reader.Read())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    results.Add(ConvertToObject(reader));
                }
            }
            return results;
        }

        /// <summary>
        /// 从IDataReader中异步读取一条记录转化为对象
        /// </summary>
        private async Task<T> ReadOneAsync(IDataReader reader, CancellationToken cancellationToken)
        {
            if (reader is AutoLockDataReader autoLockReader)
            {
                return await autoLockReader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ConvertToObject(reader) : default(T);
            }
            if (reader is DbDataReader dbReader)
            {
                return await dbReader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ConvertToObject(reader) : default(T);
            }
            return reader.Read() ? ConvertToObject(reader) : default(T);
        }


        /// <summary>
        /// 异步执行 IDbCommand，读取所有记录并转化为对象的集合
        /// </summary>
        protected async Task<List<T>> GetAllAsync(DbCommandProxy command, CancellationToken cancellationToken)
        {
            using (IDataReader reader = await command.ExecuteReaderAsync(CommandBehavior.Default, cancellationToken))
            {
                return await ReadAllAsync(reader, cancellationToken);
            }
        }

        /// <summary>
        /// 异步执行 IDbCommand，读取一条记录并转化为单个对象
        /// </summary>
        protected async Task<T> GetOneAsync(DbCommandProxy command, CancellationToken cancellationToken)
        {
            using (IDataReader reader = await command.ExecuteReaderAsync(CommandBehavior.Default, cancellationToken))
            {
                return await ReadOneAsync(reader, cancellationToken);
            }
        }
        #endregion
    }
}
