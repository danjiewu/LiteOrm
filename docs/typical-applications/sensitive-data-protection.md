# 敏感字段加密与脱敏

给某一列做加密存储，在 ORM 里要解决三件事：写入时把明文换成密文、读取时换回明文、还要能按业务需要检索。第三件事最容易出问题，因为查询条件的参数和实体写入的参数走的不是同一条转换路径。本文按两种方式组织：**列密文存储**（在列上声明 `ConverterType`）和**自定义类型全局注册**（给密文定义强类型并全局注册读写转换）。

## 两种方式选型

| 方式 | 实体声明 | 按值等值检索 | 适用 |
| --- | --- | --- | --- |
| 列密文存储 | 列上标 `ConverterType` | 需显式传密文，或另加盲索引列 | 单列、只写读回、检索场景少 |
| 自定义类型全局注册 | 属性类型用自定义类型 | 条件直接传该类型，自动转密文 | 多列复用、常要按值等值查询 |

只读回、不按值检索的字段两种方式都行，方式一更直接；要按证件号这类值做等值查询、又不想在每个列上标注转换的，优先方式二。

## 方式一：列密文存储

**需求**：Customers 的证件号在库里必须是密文，应用层读出来是明文，密钥不落代码。

**做法**：实现列级转换器，在列上声明 `ConverterType`：

```csharp
using System.Security.Cryptography;
using System.Text;
using LiteOrm;
using LiteOrm.Common;

/// <summary>证件号列级转换器。密文格式 Base64(版本(1) + nonce(12) + tag(16) + 密文)。</summary>
public sealed class IdCardEncryptConverter : IDbValueConverter<string, string>
{
    private const byte KeyVersion = 1;

    private static readonly byte[] Key = Convert.FromBase64String(
        Environment.GetEnvironmentVariable("LITEORM_IDCARD_KEY")
        ?? throw new InvalidOperationException("LITEORM_IDCARD_KEY is missing."));

    Type IDbValueConverter.ValueType => typeof(string);

    // 读取方向：数据库值 -> 实体属性值
    DbConvertHandler<string, string>? IDbValueConverter<string, string>.DbReadConverter
        => cipher => Decrypt(cipher);

    // 写入方向：实体属性值 -> 数据库值
    DbConvertHandler<string, object>? IDbValueConverter<string, string>.DbWriteConverter
        => plain => Encrypt(plain);

    // 源生成（AOT）路径走非泛型委托，输入输出都是 object
    DbConvertHandler? IDbValueConverter.DbReadConverter => value => Decrypt((string)value);

    DbConvertHandler? IDbValueConverter.DbWriteConverter => value => Encrypt((string)value);

    private static string Encrypt(string plain)
    {
        byte[] nonce = RandomNumberGenerator.GetBytes(12);
        byte[] plainBytes = Encoding.UTF8.GetBytes(plain);
        byte[] cipher = new byte[plainBytes.Length];
        byte[] tag = new byte[16];

        using var aes = new AesGcm(Key, tag.Length);
        aes.Encrypt(nonce, plainBytes, cipher, tag);

        byte[] blob = new byte[1 + nonce.Length + tag.Length + cipher.Length];
        blob[0] = KeyVersion;
        nonce.CopyTo(blob, 1);
        tag.CopyTo(blob, 13);
        cipher.CopyTo(blob, 29);
        return Convert.ToBase64String(blob);
    }

    private static string Decrypt(string value)
    {
        byte[] blob = Convert.FromBase64String(value);
        if (blob.Length < 29 || blob[0] != KeyVersion)
            throw new InvalidOperationException($"Unsupported cipher payload (version {blob[0]}).");

        byte[] nonce = blob.AsSpan(1, 12).ToArray();
        byte[] tag = blob.AsSpan(13, 16).ToArray();
        byte[] cipher = blob.AsSpan(29).ToArray();
        byte[] plain = new byte[cipher.Length];

        using var aes = new AesGcm(Key, tag.Length);
        aes.Decrypt(nonce, cipher, tag, plain);
        return Encoding.UTF8.GetString(plain);
    }
}
```

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

读写都由列元数据驱动，实体层面不需要写转换代码：

| 方向 | 路径 |
| --- | --- |
| 写入（插入 / 更新） | 实体属性值 → 列转换器 `DbWriteConverter` → 数据库参数 |
| 读取（查询） | 数据库原始值 → 按列 `DbValueType` 选择读取方法 → 列转换器 `DbReadConverter` → 实体属性 |
| 按主键查询条件 | 裸值按列上下文转换（主键、时间戳条件走列级入口） |

要点：

- 转换器必须有公共无参构造函数。框架解析列元数据时用 `Activator.CreateInstance` 实例化，源生成器路径直接生成 `new XxxConverter()`。需要依赖注入的组件（配置中心、密钥服务）不能从构造函数注入，改用静态初始化或环境变量。
- 列的 `DbType` 要和真实存储类型一致。读取时框架按列的 `DbValueType` 选择数据读取方法，再送进 `DbReadConverter`，列类型与实际存储不符时转换器拿到的是错误类型的值。
- `FuncDbValueConverter<TDbType, TValueType>` 可以直接用两个委托构造，但它本身是 `sealed`，不能继承；列级 `ConverterType` 需要一个带无参构造函数的类型，所以最终还是落成一个类。

### 需要按密文列检索？

先接受一个事实：由 `Expr` 生成的普通 `WHERE` 参数不带列上下文。参数以 `new Param(name, value)` 的形式产生，绑定命令时只按“值的运行时类型 + 目标 `DbValueType`”查全局转换器注册表，不会去查某一列上声明的 `ConverterType`。所以下面这条查询查不到任何数据：

```csharp
using static LiteOrm.Common.Expr;

// 明文传入，参数也是明文，与库里的密文比不出来
var customer = await customerService.SearchOneAsync(Prop(nameof(Customer.IdCard)) == plainIdCard);
```

方式一里列上的随机 IV（非确定性）加密每次结果都不同，只适合“写进去、读回来”。要按值等值检索，有两条替代路：

**显式传密文。** 加密逻辑是自己写的，转一次即可，前提是加密必须是确定性的（同一明文恒定得到同一密文）：

```csharp
using static LiteOrm.Common.Expr;

var cipher = DeterministicIdCardCipher.Encrypt(plainIdCard);
var customer = await customerService.SearchOneAsync(Prop(nameof(Customer.IdCard)) == cipher);
```

**或者加盲索引列。** 随机 IV 加密无法比较，就在旁边再存一个哈希列用于等值匹配，与密文列同步写入：

```csharp
[Column("IdCardHash", DbType = DbValueType.String, Length = 64, IsIndex = true)]
public string? IdCardHash { get; set; }   // 值为 HMAC-SHA256(规范化后的明文)，与密文列同步写入
```

```csharp
var customer = await customerService.SearchOneAsync(
    Prop(nameof(Customer.IdCardHash)) == ComputeHash(plainIdCard));
```

检索边界：

- 密文上的范围查询、排序、`LIKE` 都不可用。需要按前缀检索时，常见的折中是拆出明文的检索列（例如证件号前 6 位），并接受这部分信息不再受加密保护。
- 盲索引要额外维护一列和它的写入逻辑，换来等值查询能力，同时不暴露明文。写入同步建议放在实体服务里，保证两个列永远一致。
- 确定性加密的密文可被关联比较，安全性弱于随机 IV，只用在必须检索的字段上。
- 若业务能放弃“按证件号查人”，把检索入口收敛到主键或盲索引即可，不必引入密文比较。

## 方式二：自定义类型全局注册

**需求**：既要按密文等值查询，又不想在每个列上写 `ConverterType`；密文列不止一处，希望一处配置、处处生效。

直接注册（`string`, `DbValueType.String`）是不行的，那会让所有 `string` 参数都被加密，包括没有任何密文列的实体。换个思路：用一个只表示密文的包装类型当属性类型，加密解密实现在这个类型里，再把该类型的读写转换全局注册一次。注册的键是自定义类型，只有用它声明的列会命中，其它 `string` 列完全不受影响。

**定义密文类型**：

```csharp
using LiteOrm.Common;

/// <summary>密文字符串。构造与取值都显式，避免明文与密文互相误传。</summary>
public class EncryptedString
{
    public string Cipher { get; }

    private EncryptedString(string cipher) => Cipher = cipher;

    /// <summary>明文加密后构造。</summary>
    public static EncryptedString FromPlain(string plain) => new(Encrypt(plain));

    /// <summary>直接由库中密文构造，读取路径专用。</summary>
    public static EncryptedString FromCipher(string cipher) => new(cipher);

    /// <summary>解密取回明文。</summary>
    public string ToPlain() => Decrypt(Cipher);

    // 等值比较，供 Lambda 条件（c.IdCard == EncryptedString.FromPlain(...)）使用，比较的是密文
    public static bool operator ==(EncryptedString left, EncryptedString right) => left.Cipher == right.Cipher;
    public static bool operator !=(EncryptedString left, EncryptedString right) => !(left == right);
    public override bool Equals(object? obj) => obj is EncryptedString other && Cipher == other.Cipher;
    public override int GetHashCode() => Cipher.GetHashCode();

    private static string Encrypt(string plain) => /* 同 AES-GCM 加密：随机 IV + 版本头，见方式一 */;
    private static string Decrypt(string cipher) => /* 同 AES-GCM 解密，见方式一 */;
}
```

**全局注册读写转换**（一次声明，放在程序启动处）：

```csharp
using static LiteOrm.Common.DbValueType;

// 声明一次，此后 EncryptedString 类型的值在数据库中按 String 处理
DbValueTypeMap.Set(typeof(EncryptedString), DbValueType.String);

SqlBuilder.Instance.RegisterDbValueConverter<SqlBuilder, string, EncryptedString>(
    targetType: DbValueType.String,
    fromDb: cipher => EncryptedString.FromCipher(cipher),   // 读取：库中密文 -> EncryptedString
    toDb:   value => value.Cipher                           // 写入：EncryptedString -> 库中密文
);
```

**实体只声明属性类型**，`DbType` 和 `ConverterType` 都不用写：

```csharp
[Table("Customers")]
public class Customer : ObjectBase
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
    public long Id { get; set; }

    [Column("IdCard", Length = 256)]
    public EncryptedString? IdCard { get; set; }
}
```

**写入、读回与按值查询**：

```csharp
// 写进去自动加密，读回来自动解密，业务代码里不出现密文
await customerService.InsertAsync(new Customer { IdCard = EncryptedString.FromPlain(plainIdCard) });

var loaded = await customerService.Search(c => c.Id == id).FirstOrDefaultAsync();
string plain = loaded.IdCard!.ToPlain();
```

```csharp
// 参数直接传 EncryptedString，写入转换自动转成密文，即可命中
var customer = await customerService.SearchOneAsync(
    c => c.IdCard == EncryptedString.FromPlain(plainIdCard));
```

要点：

- `DbValueTypeMap.Set` 是全局的，登记后凡是这个类型的属性都按 `String` 处理，DDL 会生成 `VARCHAR`（`Length` 也照常用）。因为它影响面是全进程，只给确实要统一存储形式的自定义类型用。
- 不登记映射时，`GetDbValueType` 会退回 `Object`，列上就得自己补 `DbType = DbValueType.String`，否则框架读取会走 `GetValue` 拿装箱值。两种写法二选一，不要都省略。
- 全局注册是进程级的一次性动作，放在程序启动处执行，完成后再发起第一次查询。注册表以 `(值类型, DbValueType)` 为键，同一键重复注册会覆盖旧值，转换器改成新实现直接重新注册即可；但如果列级转换器已经按旧注册回填过，要先清掉列的 `DbValueConverter`，否则旧实例会继续生效。
- 全局注册覆盖实体列的读写，也覆盖 `Expr` 与 Lambda 条件里的裸值参数（都按值的运行时类型查同一张注册表）。条件里的值要传 `EncryptedString`（如 `EncryptedString.FromPlain(...)`），传明文进去命中的是明文的类型，查不出结果。
- 密文上的范围查询、排序、`LIKE` 依然不可用，与方式一的检索边界相同。
- AOT 下需显式指定（见下文 [AOT 差异](#aot-差异)），只靠映射和全局注册不够。

## 注意事项

项目上线前确认列够不够长、索引建在哪、密钥怎么管。

密文比明文长。Base64 膨胀约 33%，再加上 nonce 与 tag 的几十字节：

| 明文长度 | 密文长度（Base64，含版本 1 + nonce 12 + tag 16） | 建议列长 |
| --- | --- | --- |
| 32 字节以内 | 80 字符以内 | 128 |
| 64 字节以内 | 128 字符以内 | 192 |
| 128 字节以内 | 224 字符以内 | 256 |

数据库列如果按明文长度建成了 `varchar(18)`（常见于身份证号列），启用加密前必须改列宽，否则写入阶段就会被数据库截断或报错。

要点：

- 密文列上的普通索引对查询没有帮助（确定性加密的等值查询除外）。要建索引就建在盲索引列或检索列上。
- 密钥不要写进代码或提交到仓库，从环境变量、密钥服务或 OS 密钥库读取，转换器里做一次静态初始化并缓存。
- 密文里带上密钥版本（示例里的第一个字节），轮换时新写入用新密钥、读取时按版本选密钥，避免一次性重刷全表。
- 密钥丢失等同于数据丢失，非确定性加密没有恢复路径。密钥备份与访问审计是这套方案的一部分。
- 轮换期间会出现“同一条记录里旧字段用旧密钥、新字段用新密钥”的中间状态，解密路径要能同时处理多个版本。

## AOT 差异

项目用 NativeAOT 发布时，要保证加密列还能正常工作，注意以下几点：

- 复杂类型（数组、集合、自定义类）列在 AOT 下必须显式声明 `ConverterType`，否则源生成器会跳过该列的读取映射。方式二里只注册映射和全局转换不够：源生成器读取时按 `reader.GetFieldType(i)` 拿到的实际 CLR 类型推断列的取值类型，自定义类型只会推成 `Object`，`DbValueTypeMap.Set` 改变不了这一点。所以 AOT 下的列要显式写成 `[Column("IdCard", DbType = DbValueType.String, ConverterType = typeof(...))]`。
- 源生成器读取时统一走非泛型 `DbReadConverter`，输入是 `reader.GetValue(i)`，装箱无法避免；泛型强类型委托只在 JIT 路径生效。这是 AOT 路径映射性能与 JIT 路径有差距的原因之一。
- 转换器缺少公共无参构造函数时，生成代码在编译期就会报错，比运行时才发现要早。
- 元数据来源在 AOT 下会切换成源生成的 `ColumnInfo`，见[多租户隔离](./tenant-isolation.md)的示例三，加密列本身不受影响，受影响的只有 `Constant` 切片，替代做法是改用运行时 `GenericSqlExpr` 承载固定条件。

## 相关链接

- [返回目录](../README.md)
- [数据映射与值转换](../advanced-topics/data-mapping.md)
- [AOT 支持](../advanced-topics/aot.md)
- [安全性](../advanced-topics/security.md)
- [审计与变更追踪](./audit-and-change-tracking.md)
- [数据权限](./data-permission.md)