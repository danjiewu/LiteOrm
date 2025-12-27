using MyOrm.Common;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace LogRecord
{
    [Table("AccountingLog_{0}",DataSource ="Radius")]
    public class AccountingLog : ObjectBase, IArged
    {
        [Column(IsPrimaryKey = true, IsIdentity = true)]
        public long Id { get; set; }

        /// <summary>
        /// 大区分公司编码
        /// </summary>
        public string OrgCode { get; set; }

        /// <summary>
        /// 账号名
        /// </summary>
        public string UserName { get; set; }

        /// <summary>
        /// BOSS分公司编码
        /// </summary>
        public string RegionOrgCode { get; set; }

        /// <summary>
        /// 计费状态类型（Start：上线包，Stop：下线包 , Inter）
        /// </summary>
        public int? AcctStatusType { get; set; }

        /// <summary>
        /// 计费上线时间
        /// </summary>
        public DateTime? AcctStartTime { get; set; }

        /// <summary>
        /// 计费下线时间
        /// </summary>
        public DateTime? AcctStopTime { get; set; }

        /// <summary>
        /// 计费会话时长（单位：秒，根据实际业务调整）
        /// </summary>
        public long? AcctSessionTime { get; set; }

        /// <summary>
        /// 请求记录时间
        /// </summary>
        public DateTime? RequestDate { get; set; }

        /// <summary>
        /// 计费输入字节数
        /// </summary>
        public long? AcctInputOctets { get; set; }

        /// <summary>
        /// 计费输出字节数
        /// </summary>
        public long? AcctOutputOctets { get; set; }

        /// <summary>
        /// 计费会话ID
        /// </summary>
        public string AcctSessionId { get; set; }

        /// <summary>
        /// 服务大类编码
        /// </summary>
        public string ServiceCode { get; set; }

        /// <summary>
        /// 计费终止原因
        /// </summary>
        public string AcctTerminateCause { get; set; }

        /// <summary>
        /// NAS名称
        /// </summary>
        public string NasIdentifier { get; set; }

        /// <summary>
        /// NAS IP地址
        /// </summary>
        public string NasIpAddress { get; set; }

        /// <summary>
        /// 分配的用户IP地址
        /// </summary>
        public string FramedIpAddress { get; set; }

        /// <summary>
        /// 线路信息
        /// </summary>
        public string NasPortIdString { get; set; }

        /// <summary>
        /// MAC地址
        /// </summary>
        public string MacAddress { get; set; }
        /// <summary>
        /// 采集时间
        /// </summary>
        public DateTime EtlTime { get; set; }

        string[] IArged.TableArgs => new string[] { (RequestDate ?? DateTime.Now).ToString("yyyyMM") };    

    }
}
