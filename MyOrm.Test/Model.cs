using MyOrm.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace MyOrm.Test
{
    [Table("city", DataSource = "sspanel")]
    public class City : ObjectBase
    {
        [Column(IsIdentity = true)]
        public int City_ID { get; set; }
        [Column("Name")]
        public string Name { get; set; }

    }

    [Table("Appointment")]
    public class Appointment : BusinessEntity
    {
        public string ChildName { get; set; }
        public string Mobile { get; set; }
        public string Grade { get; set; }
        public string OpenID { get; set; }
        [Column(ColumnMode = ColumnMode.Read)]
        public DateTime TimeStamp { get; set; }
    }


    [Serializable]
    public class EntityBase : ObjectBase
    {
        [Column(IsIdentity = true, IsPrimaryKey = true)]
        [Browsable(false)]
        public long ID { get; set; }

        [Column(ColumnMode = ColumnMode.Final)]
        [DisplayName("创建时间")]
        [Log(false)]
        public DateTime CreateTime { get; set; }
    }

    [Serializable]
    public abstract partial class BusinessEntity : EntityBase
    {
        [Browsable(false)]
        [Column(AllowNull = false, ColumnMode = ColumnMode.Final)]
        [DisplayName("创建人员")]
        public string Creator { get; set; }

        [DisplayName("更新人员")]
        [Log(false)]
        public string Updater { get; set; }

        [Browsable(false)]
        [DisplayName("最后更新时间")]
        [Log(false)]
        public DateTime? UpdateTime { get; set; }
    }

    [Serializable]
    [Table("Sys_User")]
    [DefaultProperty(nameof(Name))]
    [DisplayName("系统用户")]
    public partial class SysUser : EntityBase
    {
        [Column(ColumnMode = ColumnMode.Final), DisplayName("用户名")]
        public string UserName { get; set; }
        [DisplayName("姓名")]
        public string Name { get; set; }

        [Column(ColumnMode = ColumnMode.Insert), DisplayName("格式化姓名")]
        public string NormalizedUserID { get { return UserName?.ToUpper(); } }

        [Column(ColumnMode = ColumnMode.Final), Log(false), DisplayName("密码"), Browsable(false)]
        public string PasswordHash { get; set; }

        [DisplayName("是否审核")]
        public bool IsApproved { get; set; }

        [Column(ColumnMode = ColumnMode.Final), DisplayName("是否锁定")]
        public bool IsLockedOut { get; set; }

        [DisplayName("IP地址过滤")]
        public string AddressFilter { get; set; }

        [DisplayName("备注")]
        public string Remark { get; set; }

        [Column(ColumnMode = ColumnMode.Final), DisplayName("密码更改日期")]
        [DisplayFormat(DataFormatString = "{0:D}")]
        public DateTime? LastPasswordChangedDate { get; set; }

        [Column, DisplayName("活动日期")]
        [DisplayFormat(DataFormatString = "{0:D}")]
        public DateTime? LastActivityDate { get; set; }

        [Column(ColumnMode = ColumnMode.Final), DisplayName("登录日期")]
        public DateTime? LastLoginDate { get; set; }
        [Column(ColumnMode = ColumnMode.Final), DisplayName("锁定日期")]
        public DateTime? LastLockedOutDate { get; set; }
    }


    public enum FunctionStatus
    {
        [Description("有效")]
        Valid = 1,
        [Description("无效")]
        Invalid = 0
    }


    [Table("Sys_Function")]
    [DisplayName("功能")]
    public class Function : BusinessEntity
    {

        [Column(IsUnique = true)]
        [DisplayName("是否菜单")]
        public bool IsMenu { get; set; }
        [DisplayName("编码")]
        public string Code { get; set; }
        [DisplayName("名称")]
        public string Name { get; set; }
        [DisplayName("状态")]
        public FunctionStatus Status { get; set; }
        [DisplayName("模块")]
        public string Controller { get; set; }
        [DisplayName("功能")]
        public string Action { get; set; }
        [DisplayName("路径")]
        public string Url { get; set; }
        [DisplayName("图标")]
        [Category("icon")]
        public string Icon { get; set; }
        [DisplayName("描述")]
        public string Description { get; set; }
        [ForeignType(typeof(Function), Alias = "Parent",FilterExpression = "[Parent].[IsMenu] = 0")]
        [DisplayName("父节点")]
        public long ParentID { get; set; }
        [Browsable(false)]
        [DisplayName("位置")]
        public int Position { get; set; }
    }
}
