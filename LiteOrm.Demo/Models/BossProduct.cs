using LiteOrm.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace MyAssist.Data.CBOSS
{
    [Table("UP_Product_Catagory", DataSource = "BOSS")]
    [Serializable]
    [DisplayName("产品信息")]
    public class BossProduct 
    {
        [Column("PRODUCT_ITEM_ID", IsPrimaryKey = true)]
        [PropertyOrder(1)]
        public Int64 ID { get; set; }

        [DisplayName("产品名称")]
        [PropertyOrder(2)]
        public string Name { get; set; }

        [DisplayName("参数1")]
        [PropertyOrder(3)]
        public string Param1 { get; set; }

        [DisplayName("参数2")]
        [PropertyOrder(4)]
        public string Param2 { get; set; }

        [DisplayName("参数3")]
        [PropertyOrder(5)]
        public string Param3 { get; set; }

        [DisplayName("是否真实产品")]
        [Browsable(false)]
        [PropertyOrder(6)]
        public bool? Is_Prod { get; set; }

        [DisplayName("是否主产品")]
        [Browsable(false)]
        [PropertyOrder(7)]
        public bool? Is_Main { get; set; }

        [DisplayName("是否宽带产品")]
        [Browsable(false)]
        [PropertyOrder(8)]
        public KD_ENUM? Is_Kd { get; set; }

        [DisplayName("互动类别")]
        [PropertyOrder(9)]
        public HD_TYPE_ENUM? Is_Hd { get; set; }

        [DisplayName("价格")]
        [PropertyOrder(11)]
        public float? Price { get; set; }

        [DisplayName("月均单价")]
        [PropertyOrder(After = nameof(Price))]
        public float? Unit_Price { get; set; }

        [DisplayName("加权年资费")]
        [PropertyOrder(After = nameof(Unit_Price))]
        public float? Weighted_Price { get; set; }

        [DisplayName("提成金额")]
        [Browsable(false)]
        [PropertyOrder(12)]
        public float? Bonus { get; set; }

        [DisplayName("宽带类型")]
        [PropertyOrder(13)]
        public KD_TYPE_ENUM? KD_Type { get; set; }

        [DisplayName("月份数")]
        [Browsable(false)]
        [PropertyOrder(14)]
        public int? EXP_Month { get; set; }

        [DisplayName("时长类型")]
        [Browsable(false)]
        [PropertyOrder(15)]
        public EXP_ENUM? IS_Exp { get; set; }
        [DisplayName("业务ID")]
        [ForeignType(typeof(ServiceInfo))]
        [PropertyOrder(16)]
        public long? PROD_SERVICE_ID { get; set; }
        [DisplayName("带宽")]
        [PropertyOrder(17)]
        [Browsable(false)]
        public int? Bandwidth { get; set; }
        [DisplayName("分组编号")]
        [PropertyOrder(18)]
        [Browsable(false)]
        public string GroupCode { get; set; }

        [DisplayName("创建时间")]
        [Browsable(false)]
        [PropertyOrder(19)]
        [Column(ColumnMode = ColumnMode.Final)]
        public DateTime Create_Date { get; set; }

        [DisplayName("更新时间")]
        [PropertyOrder(20)]
        [Browsable(false)]
        public DateTime? Update_Date { get; set; }  

        //	IS_PROD	INTEGER	是否实际产品
        //	IS_KD	INTEGER	是否宽带产品,0:非宽带；1:宽带基本；2:宽带增值；3:宽带资费优惠
        //	IS_HD	INTEGER	是否互动产品,0:非互动；1:互动基本；2:互动增值
        //	IS_MAIN	INTEGER	是否主产品
        //	IS_EXP	INTEGER	是否体验产品，0:非体验产品;1:体验到期转订购；2:包年或其他一次性出账产品；3:体验到期退订
        //	EXP_MONTH	INTEGER	体验或一次性出账的月份数
        //	KD_TYPE	INTEGER	宽带类型，0:非宽带；1:有线宽带；2:互动宽带；3:光纤宽带
        //	IS_FREE	INTEGER	是否免费产品
        //	PRICE	NUMBER	产品价格（单位：元，分阶段不同价格的以最高的单价为准）
        //	IS_FF	INTEGER	是否付费产品
        //	PROD_SERVICE_ID	INTEGER	业务ID
        //	EXP_TYPE	NUMBER	体验或包年截止日期的方式，1:按实际天计算；2:截止到月底
        //	CREATE_DATE	DATE	创建日期
        //	IS_CF	INTEGER	是否需要催费
        //	GROUPCODE	VARCHAR2(40)	智慧社区分组
        //	BANDWIDTH	INTEGER	带宽（M）
        //	UPDATE_DATE	DATE	更新日期
        //	UNIT_PRICE	NUMBER	月均单价（单位：元）
    }

    public enum KD_ENUM
    {
        非宽带 = 0,
        宽带基本 = 1,
        宽带增值 = 2,
        宽带资费优惠 = 3
    }

    public enum KD_TYPE_ENUM
    {
        非宽带 = 0,
        有线宽带 = 1,
        互动宽带 = 2,
        移动宽带 = 3
    }
    public enum EXP_ENUM
    {
        非体验产品 = 0,
        体验到期转订购 = 1,
        包年或一次性出账产品 = 2,
        体验到期退订 = 3
    }

    public enum HD_TYPE_ENUM
    {
        非互动 = 0,
        互动基本 = 1,
        互动增值 = 2
    }


    /// <summary>
    /// 服务信息实体类
    /// 对应数据库表：PZG1.AM_SERVICE_INFO
    /// </summary>
    [Table("PZG1.AM_SERVICE_INFO", DataSource = "BOSS")]
    [DefaultProperty(nameof(Service_Name))]
    [Serializable]
    [DisplayName("服务信息")]
    public class ServiceInfo
    {
        /// <summary>
        /// 服务标识
        /// </summary>
        [DisplayName("服务标识")]
        [Column("service_id", IsPrimaryKey = true)]
        [PropertyOrder(0)]
        public Int64 ID { get; set; }

        /// <summary>
        /// 服务名称
        /// </summary>
        [DisplayName("服务名称")]
        [Column("service_name")]
        [PropertyOrder(1)]
        public string Service_Name { get; set; }

        /// <summary>
        /// 服务类型（默认0）
        /// </summary>
        [DisplayName("服务类型")]
        [Column("service_type")]
        [PropertyOrder(2)]
        public int? Service_Type { get; set; } = 0;
    }
}

