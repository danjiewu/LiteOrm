# 字段加密与脱敏存储

给某一列做加密存储，在 ORM 里通常意味着三件事：写入时把明文换成密文、读取时换回明文、查询时还能按业务需要检索。第三件事最容易出问题，因为查询条件的参数和实体写入的参数走的不是同一条转换路径。

## 1. 先选方案

| 方案 | 可逆 | 等值查询 | 范围 / 排序 / 模糊 | 适用 |
| --- | --- | --- | --- | --- |
| 非确定性加密（随机 IV） | 是 | 否 | 否 | 只需读回、不需要按值检索的字段 |
| 确定性加密 + 盲索引列 | 是 | 是（查盲索引） | 否 | 需要按值精确检索的字段 |
| 单向哈希 | 否 | 是 | 否 | 只看是否匹配，不需读回原文 |
| 脱敏（掩码） | 否 | 否 | 否 | 展示用字段、统计用字段 |

分界线是“要不要按这个值检索”。要检索，就必须有一个可比较的形态：确定性加密的密文，或者独立的哈希盲索引列。只有非确定性加密又要求等值查询，是走不通的组合。

## 2. 列级转换器

LiteOrm 用列级转换器承载加密。转换器实现 `IDbValueConverter`（非泛型，供 AOT 路径使用）与可选的泛型 `IDbValueConverter<TDbType, TValueType>`（供 JIT 路径使用，无装箱）：

```csharp
using System.Security.Cryptography;
using System.Text;
using LiteOrm;
using LiteOrm.Common;

/// <summary>证件号列级转换器。密文格式为 Base64(nonce(12) + tag(16) + ciphertext)。</summary>
public sealed class IdCardEncryptConverter : IDbValueConverter<string, string>
{
    private static readonly byte[] Key = Convert.FromBase64String(
        Environment.GetEnvironmentVariable("LITEORM_IDCARD_KEY")
        ?? throw new InvalidOperationException("LITEORM_IDCARD_KEY is missing."));

    public Type ValueType => typeof(string);

    // 读取方向：数据库值 → 实体属性值
    DbConvertHandler<string, string>? IDbValueConverter<string, string>.DbReadConverter
        => cipher => Decrypt(cipher);

    // 写入方向：实体属性值 → 数据库值
    DbConvertHandler<string, object>? IDbValueConverter<string, string>.DbWriteConverter
        => plain => Encrypt(plain);

    // AOT 路径使用非泛型委托，输入输出都是 object
    DbConvertHandler? IDbValueConverter.DbReadConverter => cipher => Decrypt((string)cipher!);

    DbConvertHandler? IDbValueConverter.DbWriteConverter => plain => Encrypt((string)plain!);

    private static string Encrypt(string plain)
    {
        byte[] nonce = RandomNumberGenerator.GetBytes(12);
        byte[] plainBytes = Encoding.UTF8.GetBytes(plain);
        byte[] cipher = new byte[plainBytes.Length];
        byte[] tag = new byte[16];

        using var aes = new AesGcm(Key, tag.Length);
        aes.Encrypt(nonce, plainBytes, cipher, tag);
        return Convert.ToBase64String(Combine(nonce, tag, cipher));
    }

    private static string Decrypt(string value)
    {
        byte[] blob = Convert.FromBase64String(value);
        // 反向拆分 nonce / tag / cipher，再用 AesGcm.Decrypt
        return Encoding.UTF8.GetString( /* 解密结果 */ );
    }

    private static byte[] Combine(params byte[][] parts) { /* 顺序拼接 */ }
}
```

要求与约束：

- 转换器必须有**公共无参构造函数**。框架在解析列元数据时用 `Activator.CreateInstance` 实例化，源生成器路径则直接生成 `new XxxConverter()`。需要依赖注入的组件（配置中心、密钥服务）不能从构造函数注入，要改用静态初始化或环境变量。
- 列上用 `ConverterType` 声明：

  ```csharp
  [Table("Customers")]
  public class Customer : ObjectBase
  {
      [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
      public long Id { get; set; }

      [Column("IdCard", DbType = DbValueType.String, Length = 256, ConverterType = typeof(IdCardEncryptConverter))]
      public string? IdCard { get; set; }
  }
  ```

- 列的 `DbType` 要与真实存储类型一致。读取时框架按列的 `DbValueType` 选择数据读取方法，再把它送进 `DbReadConverter`。列类型和实际存储不符时，转换器拿到的是错误类型的值。
- 简单场景可以用 `FuncDbValueConverter<TDbType, TValueType>` 直接包装两个委托，不必新建类型；但列级 `ConverterType` 需要一个类型，所以最终还是要落成类。

## 3. 读写路径

| 方向 | 路径 |
| --- | --- |
| 写入（插入 / 更新） | 实体属性值 → 列转换器 `DbWriteConverter` → 数据库参数 |
| 读取（查询） | 数据库原始值 → 按列 `DbValueType` 选择 `Get` 方法 → 列转换器 `DbReadConverter` → 实体属性 |
| 按主键查询条件 | 裸值按列上下文转换（主键、时间戳条件走列级入口） |

写入和读取都由列元数据驱动，所以只要 `ConverterType` 挂在列上，实体层面的读写就自动带上加密。

## 4. 查询条件的参数不走列转换器

这是最容易踩的一点：由 `Expr` 生成的普通 `WHERE` 参数**不带列上下文**。参数以 `new Param(name, value)` 的形式产生，绑定命令时只按「值的运行时类型 + 目标 `DbValueType`」查 `SqlBuilder` 的全局转换器注册表，不会去查某一列上声明的 `ConverterType`。

结果是下面这条查询查不到任何数据：

```csharp
using static LiteOrm.Common.Expr;

// 明文传入，参数也是明文，与库里的密文比不出来
var customer = await customerService.SearchOneAsync(Prop(nameof(Customer.IdCard)) == plainIdCard);
```

补救方式有三种，按推荐程度排序：

**方案一（推荐）：显式传密文，或用盲索引列。** 加密逻辑是你自己写的，转一次再比较即可，前提是加密必须是确定性的（同一明文恒定得到同一密文）：

```csharp
using static LiteOrm.Common.Expr;

// DeterministicIdCardCipher 是确定性实现，与列转换器共用同一套密钥与算法
var cipher = DeterministicIdCardCipher.Encrypt(plainIdCard);
var customer = await customerService.SearchOneAsync(Prop(nameof(Customer.IdCard)) == cipher);
```

前面示例里的随机 IV 加密不满足这个前提，它每次加密结果都不同，只适合“写进去、读回来”。非确定性加密要做等值查询，只能改用盲索引列：

```csharp
[Column("IdCardHash", DbType = DbValueType.String, Length = 64, IsIndex = true)]
public string? IdCardHash { get; set; }   // 值为 HMAC-SHA256(规范化后的明文)，与密文列同步写入

// 查询改为按盲索引命中
var customer = await customerService.SearchOneAsync(Prop(nameof(Customer.IdCardHash)) == ComputeHash(plainIdCard));
```

盲索引要额外维护一列和它的写入逻辑，换来的是等值查询能力，同时不暴露明文。

**方案二：全局注册转换器。** 用 `RegisterDbValueConverter` 把（`string`, `DbValueType.String`）这一组合注册成加密转换器。副作用是**所有** `string` 参数都会被加密，包括没有任何密文列的实体。字段级加密基本不能用这条路，除非你的库里所有字符串列都加密。

**方案三：不接受等值查询。** 把检索入口改成只按主键或盲索引，业务上放弃“按证件号查人”。

另外要接受一个事实：密文上的范围查询、排序、`LIKE` 都不可用。需要按前缀检索时，常见的折中是拆出明文的检索列（例如证件号前 6 位），并接受这部分信息不再受加密保护。

## 5. 列类型、长度与索引

密文比明文长：Base64 会膨胀约 33%，加上 nonce 与 tag 还要再加几十字节。列长度要按最大密文长度放余量：

| 明文长度 | 密文长度（Base64，含 nonce 12 + tag 16） | 建议列长 |
| --- | --- | --- |
| ≤ 32 字节 | ≤ 80 字符 | 128 |
| ≤ 64 字节 | ≤ 128 字符 | 192 |
| ≤ 128 字节 | ≤ 224 字符 | 256 |

数据库列如果按明文长度建成了 `varchar(18)`（常见于身份证号列），启用加密前必须改列宽，否则写入阶段就会被数据库截断或报错。

密文列上的普通索引对查询没有帮助（除了确定性加密的等值查询），要建索引应建在盲索引列或检索列上。

## 6. AOT 下的差异

- 复杂类型（数组、集合、自定义类）列在 AOT 下必须显式声明 `ConverterType`，否则源生成器会跳过该列的读取映射。
- 源生成器读取时统一走非泛型 `DbReadConverter`，输入是 `reader.GetValue(i)`，装箱无法避免；泛型强类型委托只在 JIT 路径生效。这是 AOT 路径映射性能与 JIT 路径有差距的原因之一。
- 转换器不能缺少无参构造函数，生成代码在编译期就会报错。

## 7. 密钥管理

- 密钥不要写进代码或提交到仓库。从环境变量、密钥服务或 OS 密钥库读取，转换器里做一次静态初始化并缓存。
- 密文里带上密钥版本（例如 nonce 前两个字节存 key id），轮换时新写入用新密钥，读取时按版本选密钥，避免一次性重刷全表。
- 密钥丢失等同于数据丢失，非确定性加密没有恢复路径。密钥备份与访问审计是这套方案的一部分。
- 轮换期间会出现“同一条记录里旧字段用旧密钥、新字段用新密钥”的中间状态，解密路径要能同时处理多个版本。

## 8. 常见误区

| 误区 | 后果 |
| --- | --- |
| 用列级 `ConverterType` 加密后直接按明文等值查询 | 查不到数据，且容易误判为“数据不存在” |
| 为了等值查询全局注册加密转换器 | 所有同类型参数被加密，非密文列全部失效 |
| 用随机 IV 的加密做等值查询 | 同一明文每次密文不同，无法比较 |
| 忘记放宽列长度 | 写入被截断或抛错 |
| 把密钥放在转换器的构造函数注入 | 框架用无参构造函数实例化，注入不会生效 |
| 只加密、不做脱敏展示 | 日志、导出、前端展示仍会泄露明文 |

## 相关链接

- [返回目录](../README.md)
- [数据映射与值转换](../03-advanced-topics/11-data-mapping.md)
- [AOT 支持](../03-advanced-topics/06-aot.md)
- [安全性](../03-advanced-topics/08-security.md)
- [审计与变更追踪](./03-audit-trail.md)
- [数据权限与越权防护](./02-data-permission.md)
