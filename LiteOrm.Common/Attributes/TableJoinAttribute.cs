using System;
using System.Collections.Generic;
using System.Text;

namespace LiteOrm.Common
{
    /// <summary>
    /// 数据库表在查询时的关联关系
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = true, Inherited = true)]
    public class TableJoinAttribute : System.Attribute
    {
        private Type targetType;
        private string foreignKeys;
        private string aliasName;
        private TableJoinType joinType = TableJoinType.Left;
        private object sourceTable;

        /// <summary>
        /// 指定源表，关联的对象类型和外键生成关联信息
        /// </summary>
        /// <param name="sourceTable">关联的源表</param>
        /// <param name="targetType">关联的对象类型</param>
        /// <param name="foreignKeys">外键</param>
        public TableJoinAttribute(string sourceTable, Type targetType, string foreignKeys)
        {
            this.sourceTable = sourceTable;
            this.targetType = targetType;
            this.foreignKeys = foreignKeys;
        }


        /// <summary>
        /// 指定源表，关联的对象类型和外键生成关联信息
        /// </summary>
        /// <param name="sourceTable">关联的源表</param>
        /// <param name="targetType">关联的对象类型</param>
        /// <param name="foreignKeys">外键</param>
        public TableJoinAttribute(Type sourceTable, Type targetType, string foreignKeys)
        {
            this.sourceTable = sourceTable;
            this.targetType = targetType;
            this.foreignKeys = foreignKeys;
        }

        /// <summary>
        /// 指定关联的对象类型和外键生成关联信息
        /// </summary>
        /// <param name="targetType">关联的对象类型</param>
        /// <param name="foreignKey">外键</param>
        public TableJoinAttribute(Type targetType, string foreignKey)
        {
            this.targetType = targetType;
            this.foreignKeys = foreignKey;
        }

        /// <summary>
        /// 源表，可以是字符串，也可以是对应的对象类型
        /// </summary>
        public object Source
        {
            get { return sourceTable; }
        }

        /// <summary>
        /// 关联的对象类型
        /// </summary>
        public Type TargetType
        {
            get { return targetType; }
        }

        /// <summary>
        /// 别名
        /// </summary>
        public string AliasName
        {
            get { return aliasName; }
            set { aliasName = value; }
        }

        /// <summary>
        /// 关联类型，默认为TableJoinType.Left
        /// </summary>
        public TableJoinType JoinType
        {
            get { return joinType; }
            set { joinType = value; }
        }

        /// <summary>
        /// 外键，多个外键以","分隔，按照主键顺序排列
        /// </summary>
        public string ForeignKeys
        {
            get { return foreignKeys; }
        }

        /// <summary>
        /// 筛选条件
        /// </summary>
        public string FilterExpression { get; set; }
    }

    
}
