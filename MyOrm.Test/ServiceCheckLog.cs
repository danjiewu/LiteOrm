using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MyOrm.Common;

namespace MyOrm
{
    [Table("ServiceCheckLog")]
    [Serializable]
    [DisplayName("检查记录")]
    public class ServiceCheckLog : BusinessEntity
    {
        [DisplayName("服务ID")]
        [Browsable(false)]
        public long ServiceEntryId { get; set; }
        [DisplayName("结果")]
        public string Result { get; set; }
        [DisplayName("信息")]
        public string Message { get; set; }
    }

    [Serializable]
    [Table]
    public abstract class EntityBase : ObjectBase
    {
        [Browsable(false)]
        [Column(IsPrimaryKey = true, IsIdentity = true)]
        [Category("ID")]
        [DisplayName("序号")]
        public virtual Int64 ID { get; set; }
    }


    [Serializable]
    public abstract partial class BusinessEntity : EntityBase
    {
        [Column(ColumnMode = ColumnMode.Final)]
        [DisplayName("创建时间")]
        [Log(false)]
        [ReadOnly(true)]
        public DateTime CreateTime { get; set; }

        [Browsable(false)]
        [Column(AllowNull = false, ColumnMode = ColumnMode.Final)]
        [DisplayName("创建人员")]
        [Log(false)]
        [ReadOnly(true)]
        public string Creator { get; set; }

        [Browsable(false)]
        [DisplayName("最后更新时间")]
        [Log(false)]
        [ReadOnly(true)]
        public DateTime? UpdateTime { get; set; }

        [Browsable(false)]
        [DisplayName("更新人员")]
        [Log(false)]
        [ReadOnly(true)]
        public string Updater { get; set; }
    }
}
