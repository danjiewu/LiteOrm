using System;
using System.Collections.Generic;
using System.Text;
using MyOrm.Common;
using System.ComponentModel;
using System.Reflection;
using MyOrm;

namespace DAL.Data
{
    [Serializable]
    public abstract class EntityBase : ObjectBase
    {
        [Browsable(false)]
        [DisplayName("序号")]
        [Column(IsPrimaryKey = true, IsIdentity = true)]
        public int ID { get; set; }
    }

    [Serializable]
    [Table]
    public abstract partial class BusinessEntity : EntityBase
    {
        [Column("CreateTime", ColumnMode = ColumnMode.Final)]
        [DisplayName("创建时间")]
        [Log(false)]
        public DateTime? CreateTime { get; set; }

        [Browsable(false)]
        [Column("Creator", ColumnMode = ColumnMode.Final)]
        [DisplayName("创建用户")]
        [Log(false)]
        public string Creator { get; set; }

        [Browsable(false)]
        [Column("UpdateTime")]
        [DisplayName("最后修改时间")]
        [Log(false)]
        public DateTime? UpdateTime { get; set; }

        [Browsable(false)]
        [Column("Updater")]
        [DisplayName("最后修改用户")]
        [Log(false)]
        public string Updater { get; set; }
    }
}