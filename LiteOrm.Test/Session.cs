using LiteOrm.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LogRecord
{
    /// <summary>
    /// 表示一个会话记录的实体类，映射到数据库表 "Session"。
    /// </summary>
    /// <remarks>
    /// 该类用于持久化会话相关信息（例如设备信息、终端信息、会话标识和状态等）。
    /// 标注为 <see cref="SerializableAttribute"/>，并由 ORM 框架通过 <see cref="TableAttribute"/> 映射到数据库表。
    /// </remarks>
    [Table("Session", DataSource = "Radius")]
    [Serializable]
    public class Session : ObjectBase
    {
        /// <summary>
        /// 自增主键序号。
        /// </summary>
        [Column(IsIdentity = true, IsPrimaryKey = true)]
        public int ID { get; set; }

        /// <summary>
        /// 设备 MAC 地址（接入设备或 NAS 的 MAC）。
        /// </summary>
        public string DeviceMac { get; set; }

        /// <summary>
        /// 会话唯一标识 (Acct-Session-Id)。
        /// </summary>
        public string AcctSessionId { get; set; }

        /// <summary>
        /// 计费输入字节数
        /// </summary>
        public long? AcctInputOctets { get; set; }

        /// <summary>
        /// 计费输出字节数
        /// </summary>
        public long? AcctOutputOctets { get; set; }

        /// <summary>
        /// 终端设备的 MAC 地址（客户端 MAC）。
        /// </summary>
        public string ClientMac { get; set; }

        /// <summary>
        /// 关联的用户名（如果有）。
        /// </summary>
        public string UserName { get; set; }

        /// <summary>
        /// 会话当前状态，参见 <see cref="SessionStatus"/> 枚举。
        /// </summary>
        /// <remarks>
        /// 该属性用于表示用户会话是否在线、超时或被强制下线等状态。
        /// </remarks>
        public SessionStatus Status { get; set; }

        /// <summary>
        /// 终端 IP 地址（客户端 IP）。
        /// </summary>
        public string ClientIP { get; set; }

        /// <summary>
        /// NAS 设备的 IP 地址（接入设备 IP）。
        /// </summary>
        public string NasIP { get; set; }

        /// <summary>
        /// 登录时间，可能为空（例如未记录或会话尚未登录完成）。
        /// </summary>
        public DateTime? LoginTime { get; set; }

        /// <summary>
        /// 记录创建时间（非空）。
        /// </summary>
        [Column(ColumnMode = ColumnMode.Final)]
        public DateTime CreatedTime { get; set; }

        /// <summary>
        /// 最近更新时间，可能为空（表示尚未更新过）。
        /// </summary>
        public DateTime? UpdateTime { get; set; }

        /// <summary>
        /// 应用的策略名称（例如鉴权或带宽策略名），用于辅助管理或后续审计。
        /// </summary>
        public string PolicyName { get; set; }
    }

    /// <summary>
    /// 表示会话的状态。
    /// </summary>
    public enum SessionStatus
    {
        /// <summary>
        /// 在线。
        /// </summary>
        [Description("在线")]
        Active = 1,

        /// <summary>
        /// 主动离线（用户或客户端主动断开）。
        /// </summary>
        [Description("主动离线")]
        Inactive = 0,

        /// <summary>
        /// 超时离线（由系统策略超时触发的离线）。
        /// </summary>
        [Description("超时离线")]
        Timeout = -1,

        /// <summary>
        /// 强制离线（管理员或系统强制下线）。
        /// </summary>
        [Description("强制离线")]
        Deactivated = -2
    }
}
