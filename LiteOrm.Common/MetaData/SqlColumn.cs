using System;
using System.Collections.Generic;
using System.Text;
using System.Data;
using System.Reflection;
using System.Linq.Expressions;

namespace LiteOrm.Common
{
    /// <summary>
    /// 列的引用
    /// </summary>
    public class ColumnRef : SqlObject
    {
        /// <summary>
        /// 创建列的引用
        /// </summary>
        /// <param name="column">列信息</param>
        public ColumnRef(SqlColumn column)
        {
            Name = column.Name;
            _column = column;
        }

        /// <summary>
        /// 创建指定表的列引用
        /// </summary>
        /// <param name="table">表</param>
        /// <param name="column">列引用</param>
        public ColumnRef(TableRef table, SqlColumn column)
        {
            Name = column.Name;
            _column = column;
            _table = table;
        }

        private TableRef _table;
        /// <summary>
        /// 列所在的表
        /// </summary>
        public TableRef Table
        {
            get { return _table; }
            internal set { _table = value; }
        }

        private SqlColumn _column;
        /// <summary>
        /// 列信息
        /// </summary>
        public SqlColumn Column
        {
            get { return _column; }
        }

        /// <summary>
        /// 格式化的表达式
        /// </summary>
        public override string FormattedExpression(ISqlBuilder sqlBuilder)
        {
            return Table == null ? Column.FormattedExpression(sqlBuilder) :
                String.Format("{0}.{1}", Table.FormattedName(sqlBuilder), Column.FormattedName(sqlBuilder));
        }
    }

    /// <summary>
    /// 关联外表的列信息
    /// </summary>
    public class ForeignColumn : SqlColumn
    {
        internal ForeignColumn(PropertyInfo property) : base(property) { }

        /// <summary>
        /// 指向的列
        /// </summary>
        public ColumnRef TargetColumn { get; internal set; }

        /// <summary>
        /// 格式化的表达式
        /// </summary>
        public override string FormattedExpression(ISqlBuilder sqlBuilder)
        {
            return TargetColumn.FormattedExpression(sqlBuilder);
        }

        /// <summary>
        /// 名称
        /// </summary>
        public override string Name
        {
            get
            {
                return TargetColumn == null ? null : TargetColumn.Name;
            }
            internal set
            {
            }
        }
    }

    /// <summary>
    /// 基本列信息
    /// </summary>
    public abstract class SqlColumn : SqlObject
    {
        private PropertyInfo _property;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="property">列对应的实体属性</param>
        internal SqlColumn(PropertyInfo property)
        {
            _property = property;
            PropertyName = Name = property.Name;
        }

        /// <summary>
        /// 所属的表信息
        /// </summary>
        public SqlTable Table { get; internal set; }

        /// <summary>
        /// 属性名
        /// </summary>
        public string PropertyName { get; private set; }

        /// <summary>
        /// 列所对应的属性类型
        /// </summary>
        /// 
        public Type PropertyType
        {
            get { return _property.PropertyType; }
        }

        /// <summary>
        /// 列对应的属性
        /// </summary>
        public PropertyInfo Property { get { return _property; } }

        /// <summary>
        /// 关联的外部对象类型
        /// </summary>
        public Type ForeignType { get { return ForeignTable?.ForeignType; } }

        /// <summary>
        /// 关联的外部表信息
        /// </summary>
        public ForeignTable ForeignTable { get; internal set; }

        /// <summary>
        /// 关联的外部对象别名
        /// </summary>
        public string ForeignAlias { get; internal set; }

        /// <summary>
        /// 赋值
        /// </summary>
        /// <param name="target">要赋值的对象</param>
        /// <param name="value">值</param>
        public virtual void SetValue(object target, object value)
        {
            if (target == null) throw new ArgumentNullException("target");
            try
            {
                Property.SetValueFast(target, value);
            }
            catch (Exception e)
            {
                throw new InvalidOperationException(String.Format("Value {0} can not be assigned to {1}.{2}", value, Property.DeclaringType.Name, Property.Name), e);
            }
        }
        

        /// <summary>
        /// 取值
        /// </summary>
        /// <param name="target">对象</param>
        /// <returns>值</returns>
        public virtual object GetValue(object target)
        {
            if (target == null) throw new ArgumentNullException("target");
            return Property.GetValueFast(target);
        }

        /// <summary>
        /// 格式化的表达式
        /// </summary>
        public override string FormattedExpression(ISqlBuilder sqlBuilder)
        {
            return String.Format("{0}.{1}", Table.FormattedName(sqlBuilder), FormattedName(sqlBuilder));
        }
    }

    /// <summary>
    /// 数据库列信息
    /// </summary>
    public class ColumnDefinition : SqlColumn
    {
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="property">列对应的实体属性</param>
        internal ColumnDefinition(PropertyInfo property)
            : base(property)
        {

        }

        /// <summary>
        /// 是否是主键
        /// </summary>
        public bool IsPrimaryKey { get; internal set; }

        /// <summary>
        /// 是否是自增长标识
        /// </summary>
        public bool IsIdentity { get; internal set; }

        /// <summary>
        /// 是否是时间戳
        /// </summary>
        public bool IsTimestamp { get; set; }

        /// <summary>
        /// 标识表达式
        /// </summary>
        public string IdentityExpression { get; internal set; }

        /// <summary>
        /// 是否是索引
        /// </summary>
        public bool IsIndex { get; internal set; }

        /// <summary>
        /// 是否唯一
        /// </summary>
        public bool IsUnique { get; internal set; }

        /// <summary>
        /// 长度
        /// </summary>
        public int Length { get; internal set; }

        /// <summary>
        /// 数据库类型
        /// </summary>
        public DbType DbType { get; internal set; }

        /// <summary>
        /// 是否允许为空
        /// </summary>
        public bool AllowNull { get; internal set; }

        /// <summary>
        /// 列操作模式
        /// </summary>
        public ColumnMode Mode { get; internal set; }
    }

    /// <summary>
    /// 列操作模式
    /// </summary>
    [Flags]
    public enum ColumnMode
    {
        /// <summary>
        /// 所有操作
        /// </summary>
        Full = Read | Update | Insert,
        /// <summary>
        /// 无
        /// </summary>
        None = 0,
        /// <summary>
        /// 从数据库中读
        /// </summary>
        Read = 1,
        /// <summary>
        /// 向数据库更新
        /// </summary>
        Update = 2,
        /// <summary>
        /// 向数据库添加
        /// </summary>
        Insert = 4,
        /// <summary>
        /// 只写
        /// </summary>
        Write = Insert | Update,
        /// <summary>
        /// 不可更改
        /// </summary>
        Final = Insert | Read
    }

    /// <summary>
    /// 列操作模式的扩展方法
    /// </summary>
    public static class ColumnModeExt
    {
        /// <summary>
        /// 检查列模式是否允许插入操作
        /// </summary>
        /// <param name="mode">列操作模式</param>
        /// <returns>如果允许插入则返回true，否则返回false</returns>
        public static bool CanInsert(this ColumnMode mode)
        {
            return (mode & ColumnMode.Insert) != ColumnMode.None;
        }

        /// <summary>
        /// 检查列模式是否允许更新操作
        /// </summary>
        /// <param name="mode">列操作模式</param>
        /// <returns>如果允许更新则返回true，否则返回false</returns>
        public static bool CanUpdate(this ColumnMode mode)
        {
            return (mode & ColumnMode.Update) != ColumnMode.None;
        }

        /// <summary>
        /// 检查列模式是否允许读取操作
        /// </summary>
        /// <param name="mode">列操作模式</param>
        /// <returns>如果允许读取则返回true，否则返回false</returns>
        public static bool CanRead(this ColumnMode mode)
        {
            return (mode & ColumnMode.Read) != ColumnMode.None;
        }

    }
}
