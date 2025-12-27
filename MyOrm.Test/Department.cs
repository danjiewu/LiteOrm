using System;
using System.Collections.Generic;
using System.Text;
using MyOrm.Common;
using System.ComponentModel;

namespace DAL.Data
{
    [Serializable]
    [Table("bi.base_department")]
    [DisplayName("单位信息")]
    public partial class Department : BusinessEntity
    {
        [DisplayName("部门名称")]
        public string Name { get; set; }
        [DisplayName("上级部门ID")]
        [ForeignType(typeof(Department), Alias = "Parent")]
        public int? ParentDepartmentId { get; set; }
    }

    public partial class DepartmentView : Department
    {
        [DisplayName("上级部门")]
        [ForeignColumn("Parent", Property = nameof(Department.Name))]
        public string ParentDepartmentName { get; set; }
    }

    [Serializable]
    [DisplayName("外部单位")]
    public partial class ForeignORG : BusinessEntity
    {
        [DisplayName("名称")]
        public string Name { get; set; }
        [DisplayName("目标ID")]
        public string TID { get; set; }

        [DisplayName("部门ID")]
        [Browsable(false)]
        [ForeignType(typeof(Department))]
        public int DepartmentID { get; set; }

    }

    [Serializable]
    [Table("bi.base_boss_dw")]
    [DisplayName("BOSS单位信息")]
    public partial class BOSS_DW : ForeignORG { }

    public partial class BOSS_DWView : BOSS_DW
    {
        [DisplayName("部门")]
        [ForeignColumn(typeof(Department), Property = "Name")]
        public string Department { get; set; }
    }

    [Serializable]
    [Table("bi.base_boss_org")]
    [DisplayName("BOSS营业厅信息")]
    public partial class BOSS_ORG : ForeignORG { }

    public partial class BOSS_ORGView : BOSS_ORG
    {
        [DisplayName("部门")]
        [ForeignColumn(typeof(Department), Property = "Name")]
        public string Department { get; set; }
    }

    [Serializable]
    [Table("bi.base_grid")]
    [DisplayName("网格信息")]
    public partial class GRID : ForeignORG { }

    public partial class GRIDView : GRID
    {
        [DisplayName("部门")]
        [ForeignColumn(typeof(Department), Property = "Name")]
        public string Department { get; set; }
    }

    [Serializable]
    [Table("bi.base_task_org")]
    [DisplayName("工单单位信息")]
    public partial class TASK_ORG : ForeignORG { }

    public partial class TASK_ORGView : TASK_ORG
    {
        [DisplayName("部门")]
        [ForeignColumn(typeof(Department), Property = "Name")]
        public string Department { get; set; }
    }

    [Serializable]
    [Table("bi.base_erp_org")]
    [DisplayName("工单单位信息")]
    public partial class ERP_ORG : ForeignORG { }

    public partial class ERP_ORGView : ERP_ORG
    {
        [DisplayName("部门")]
        [ForeignColumn(typeof(Department), Property = "Name")]
        public string Department { get; set; }
    }
}

