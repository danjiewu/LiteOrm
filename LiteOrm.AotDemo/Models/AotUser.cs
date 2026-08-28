using LiteOrm.Common;
using System.Text.Json.Nodes;

namespace LiteOrm.AotDemo.Models
{
    /// <summary>
    /// AOT 演示用实体：基础类型列 + 一个 JsonNode 复杂列（经列级 <see cref="JsonNodeConverter"/> 以 JSON 文本存储）。
    /// </summary>
    [Table("AotUsers")]
    public class AotUser : ObjectBase
    {
        [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }

        [Column("UserName")]
        public string? UserName { get; set; }

        [Column("Age")]
        public int Age { get; set; }
        [Column("Role")]
        public UserRole? Role { get; set; }

        [Column("CreateTime")]
        public DateTime CreateTime { get; set; }
        [Column("Guid")]
        public Guid Guid { get; set; }

        // JsonNode 复杂列：须显式 DbType=Json 并声明 ConverterType，源生成 mapper 才能对其进行读写。
        [Column("Info", DbType = DbValueType.Json, ConverterType = typeof(JsonNodeConverter))]
        public JsonNode? Info { get; set; }
    }

    /// <summary>
    /// JsonNode 列的列级转换器：数据库侧为 JSON 文本（string），实体侧为 <see cref="JsonNode"/>。
    /// </summary>
    public class JsonNodeConverter : IDbValueConverter<string, JsonNode>
    {
        public DbConvertHandler<string, JsonNode>? DbReadConverter => s => JsonNode.Parse(s ?? "null")!;

        public DbConvertHandler<JsonNode, object>? DbWriteConverter => n => n?.ToJsonString() ?? "null";

        Type IDbValueConverter.ValueType => typeof(JsonNode);

        DbConvertHandler? IDbValueConverter.DbReadConverter => o => JsonNode.Parse((string)o!)!;

        DbConvertHandler? IDbValueConverter.DbWriteConverter => n => ((JsonNode?)n)?.ToJsonString() ?? "null";
    }

    public enum UserRole
    {
        Staff,
        Manager ,        
        Admin 
    }

    /// <summary>
    /// 用户视图实体，继承自 <see cref="AotUser"/>，用于演示 EntityService&lt;T, TView&gt; 模式。
    /// </summary>
    public class AotUserView : AotUser
    {
    }
}
