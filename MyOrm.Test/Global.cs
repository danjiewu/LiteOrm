using DAL.Data;
using MyOrm.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Reflection;
using System.ComponentModel;

namespace DAL
{
    [Serializable]
    [Table("GLO_ROLES")]
    [Description("系统角色")]
    public class Roles : BusinessEntity
    {
        [Column("ROLECODE")]
        public string RoleCode { get; set; }
        [Column("ROLENAME")]
        public string RoleName { get; set; }
        [Column("DESCRIPTION", Length = Int32.MaxValue)]
        public string Description { get; set; }
    }

    [Serializable]
    [Table("GLO_USERS")]
    [Description("系统用户")]
    public class Users : BusinessEntity
    {
        [Column("USERID")]
        public string UserID { get; set; }
        [Column("USERNAME")]
        public string UserName { get; set; }
        [Column("TELEPHONE")]
        public string Telephone { get; set; }
        [Column("EMAIL")]
        public string Email { get; set; }
        [Column("PASSWORD")]
        public string Password { get; set; }
        [Column("DESCRIPTION", Length = Int32.MaxValue)]
        public string Description { get; set; }
        [Column("LASTLOGIN")]
        public DateTime? LastLogin { get; set; }
        [Column("ISLOCKED")]
        public int? IsLocked { get; set; }
        [Column("ISCHANGEPWD")]
        public int? IsChangePwd { get; set; }
        [Column("ORGCODE")]
        public string OrgCode { get; set; }
    }

    [Serializable]
    [Table("GLO_ROLES_USERS")]
    [Description("系统用户隶属用户")]
    public class UserInRole : EntityBase
    {
        [Column("ROLEID")]
        public int? RoleID { get; set; }
        [Column("USERID")]
        public int? UserID { get; set; }
    }

    [Serializable]
    [Table("GLO_RIGHTS")]
    [Description("系统权限")]
    public class Rights : BusinessEntity
    {
        [Column("MODULEID")]
        public int? ModuleID { get; set; }
        [Column("ROLEID")]
        public int? RoleID { get; set; }
        [Column("HASQUERY")]
        public int? HasQuery { get; set; }
        [Column("HASADD")]
        public int? HasAdd { get; set; }
        [Column("HASUPDATE")]
        public int? HasUpdate { get; set; }
        [Column("HASDELETE")]
        public int? HasDelete { get; set; }
        [Column("HASSUBMIT")]
        public int? HasSubmit { get; set; }
    }

    [Serializable]
    [Table("GLO_MODULES")]
    [Description("功能菜单")]
    public class Modules : BusinessEntity
    {
        [Column("DISPLAYNAME")]
        public string DisplayName { get; set; }
        [Column("SRC")]
        public string Src { get; set; }
        [Column("UPPERMODULEID")]
        [ForeignType(typeof(Modules),Alias ="T1")]
        public string UpperModuleID { get; set; }
        [ForeignColumn("T1", Property = "DisplayName")]
        public string UpperModuleDisplayName { get; set; }
        [Column("IMAGEURL")]
        public string ImageUrl { get; set; }
        [Column("SEQUENCE")]
        public int? Sequence { get; set; }
        [Column("DESCRIPTION", Length = Int32.MaxValue)]
        public string Description { get; set; }
        [Column("NOTES")]
        public string Notes { get; set; }
        [Column("HIDE")]
        public int? Hide { get; set; }

        [Column("ADDFLAG")]
        public int? AddFlag { get; set; }
        [Column("UPDATEFLAG")]
        public int? UpdateFlag { get; set; }
        [Column("DELETEFLAG")]
        public int? DeleteFlag { get; set; }
        [Column("SUBMITFLAG")]
        public int? SubmitFlag { get; set; }
        [Column("REFRESHFLAG")]
        public int? RefreshFlag { get; set; }
        [Column("QUERYFLAG")]
        public int? QueryFlag { get; set; }
        [Column("KEYWORDFLAG")]
        public int? KeywordFlag { get; set; }
        
        [Column("ADDFUNC")]
        public string AddFunc { get; set; }
        [Column("UPDATEFUNC")]
        public string UpdateFunc { get; set; }
        [Column("DELETEFUNC")]
        public string DeleteFunc { get; set; }
        [Column("SUBMITFUNC")]
        public string SubmitFunc { get; set; }
        [Column("REFRESHFUNC")]
        public string RefreshFunc { get; set; }
        [Column("QUERYFUNC")]
        public string QueryFunc { get; set; }

        [Column("ADDICON")]
        public string AddIcon { get; set; }
        [Column("UPDATEICON")]
        public string UpdateIcon { get; set; }
        [Column("DELETEICON")]
        public string DeleteIcon { get; set; }
        [Column("SUBMITICON")]
        public string SubmitIcon { get; set; }
        [Column("REFRESHICON")]
        public string RefreshIcon { get; set; }
        [Column("QUERYICON")]
        public string QueryIcon { get; set; }

        [Column("ADDCAPTION")]
        public string AddCaption { get; set; }
        [Column("UPDATECAPTION")]
        public string UpdateCaption { get; set; }
        [Column("DELETECAPTION")]
        public string DeleteCaption { get; set; }
        [Column("SUBMITCAPTION")]
        public string SubmitCaption { get; set; }
        [Column("REFRESHCAPTION")]
        public string RefreshCaption { get; set; }
        [Column("QUERYCAPTION")]
        public string QueryCaption { get; set; }
    }

    [Serializable]
    [Table("GLO_PARAMTYPES")]
    [Description("系统参数分类")]
    public class ParamTypes : EntityBase
    {
        [Column("PARAMTYPECODE")]
        public string ParamTypeCode { get; set; }
        [Column("PARAMTYPENAME")]
        public string ParamTypeName { get; set; }
    }

    [Serializable]
    [Table("GLO_PARAMS")]
    [Description("系统参数")]
    public class Params : BusinessEntity
    {
        [Column("PARAMTYPEID")]
        [ForeignType(typeof(ParamTypes))]
        public string ParamTypeID { get; set; }
        [Column("PARAMNAME")]
        public string ParamName { get; set; }
        [Column("PARAMVALUE")]
        public string ParamValue { get; set; }
        [Column("DESCRIPTION", Length = Int32.MaxValue)]
        public string Description { get; set; }
    }

    [Serializable]
    [Description("系统参数视图")]
    public class ParamsView : Params
    {
        [Column("PARAMTYPECODE")]
        [ForeignColumn(typeof(ParamTypes))]
        public string ParamTypeCode { get; set; }
        [Column("PARAMTYPENAME")]
        [ForeignColumn(typeof(ParamTypes))]
        public string ParamTypeName { get; set; }
    }

    [Serializable]
    [Table("APK_VERSION")]
    [Description("APK版本信息")]
    public class ApkVers : BusinessEntity
    {
        [Column("OS_TYPE")]
        public int? OS_Type { get; set; }
        [Column("VERSIONNAME")]
        public string VersionName { get; set; }
        [Column("VERSIONCODE")]
        public string VersionCode { get; set; }
        [Column("DOWNLOADURL")]
        public string DownloadURL { get; set; }
        [Column("IOSPLISTURL")]
        public string iOSPlistURL { get; set; }
        [Column("UPDATECONTENT")]
        public string UpdateContent { get; set; }
        [Column("ENABLED")]
        public int? Enabled { get; set; }
    }


    [Serializable]
    [Table("B_COMPANY",DataSource ="DZT")]
    [Description("公司地址表")]
    public class Company : BusinessEntity
    {
        public string CompanyName { get; set; }
        public string Address { get; set; }
        public string Telephone { get; set; }
        public string PostCode { get; set; }
        public string Longitude { get; set; }
        public string Latitude { get; set; }
        public string PicSrc { get; set; }
        public string Description { get; set; }

        //公司区域设置
        /// <summary>
        /// 0:姑苏区
        /// 1:工业园区
        /// 2:高新区
        /// 3:吴中区
        /// 4:相城区
        /// </summary>
        public int? DistrictID { get; set; }
        public string BusinessHour { get; set; }

    }


    [Serializable]
    [Table("B_COMMUNITY{0}", DataSource = "DZT")]

    public class COMMUNITY : EntityBase
    {
        public string Name { get; set; }
    }
}