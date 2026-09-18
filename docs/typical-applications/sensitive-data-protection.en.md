# Sensitive Data Protection

Encrypting a column in an ORM means solving three things: replacing plaintext with ciphertext on write, restoring it on read, and still being able to query the value when the business needs it. The third one breaks first, because query parameters and entity write parameters do not travel the same conversion path. This article is organised around two approaches: **column-level ciphertext storage** (declare `ConverterType` on the column) and **custom-type global registration** (give ciphertext a strong type and register its read/write conversion globally).

## Choosing an approach

| Approach | Entity declaration | Equality search by value | Use for |
| --- | --- | --- | --- |
| Column-level ciphertext storage | `ConverterType` on the column | Pass ciphertext explicitly, or add a blind index column | A single column, write-and-read-back only, few search cases |
| Custom-type global registration | Custom property type | Pass the custom type directly in a condition; it converts to ciphertext automatically | Reused across many columns, frequent equality search by value |

For fields that are only written and read back and never searched, either approach works and the first one is more direct. If you need equality search by a value such as an identity number and do not want to annotate converters on every column, prefer the second approach.

## Approach 1: column-level ciphertext storage

**Requirement**: identity numbers in `Customers` must be ciphertext in the database, plaintext in the application, and the key must not live in code.

**Approach**: implement a column-level converter and declare `ConverterType` on the column:

```csharp
using System.Security.Cryptography;
using System.Text;
using LiteOrm;
using LiteOrm.Common;

/// <summary>Identity number converter. Payload is Base64(version(1) + nonce(12) + tag(16) + ciphertext).</summary>
public sealed class IdCardEncryptConverter : IDbValueConverter<string, string>
{
    private const byte KeyVersion = 1;

    private static readonly byte[] Key = Convert.FromBase64String(
        Environment.GetEnvironmentVariable("LITEORM_IDCARD_KEY")
        ?? throw new InvalidOperationException("LITEORM_IDCARD_KEY is missing."));

    Type IDbValueConverter.ValueType => typeof(string);

    // Read direction: database value -> entity property
    DbConvertHandler<string, string>? IDbValueConverter<string, string>.DbReadConverter
        => cipher => Decrypt(cipher);

    // Write direction: entity property -> database value
    DbConvertHandler<string, object>? IDbValueConverter<string, string>.DbWriteConverter
        => plain => Encrypt(plain);

    // The source-generated (AOT) path uses the non-generic delegates with object in and out
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

Both directions are driven by column metadata, so no conversion code appears at the entity level:

| Direction | Path |
| --- | --- |
| Write (insert / update) | Entity property value → column converter `DbWriteConverter` → database parameter |
| Read (query) | Raw database value → read method chosen by the column `DbValueType` → column converter `DbReadConverter` → entity property |
| Primary-key query conditions | Raw values converted with column context (primary key and timestamp conditions use the column-level entry point) |

Notes:

- A converter must have a public parameterless constructor. The framework instantiates it with `Activator.CreateInstance` while parsing column metadata, and the source generator emits `new XxxConverter()` directly. Components needing dependency injection (configuration centre, key service) cannot be constructor-injected; use static initialisation or environment variables.
- The column's `DbType` must match the real storage type. On read, the framework picks the read method from the column's `DbValueType` before handing the value to `DbReadConverter`. A mismatch means the converter receives a value of the wrong type.
- `FuncDbValueConverter<TDbType, TValueType>` can wrap two delegates directly, but it is `sealed` and cannot be derived from. A column-level `ConverterType` needs a type with a parameterless constructor, so it ends up as a class anyway.

### Need to search the ciphertext column?

Accept one fact first. Ordinary `WHERE` parameters generated from `Expr` carry no column context. Parameters are produced as `new Param(name, value)`, and command binding looks up the global converter registry by the value's runtime type plus the target `DbValueType`; it never consults the `ConverterType` declared on a column. So this query returns nothing:

```csharp
using static LiteOrm.Common.Expr;

// Plaintext in, plaintext parameter, no match against the ciphertext in the database
var customer = await customerService.SearchOneAsync(Prop(nameof(Customer.IdCard)) == plainIdCard);
```

The random-IV (non-deterministic) encryption used by the column here returns a different ciphertext every call, so it only supports "write it in, read it back". To search by value, there are two alternatives:

**Pass ciphertext explicitly.** The encryption logic is yours, so encrypt once more before comparing, provided the encryption is deterministic (the same plaintext always yields the same ciphertext):

```csharp
using static LiteOrm.Common.Expr;

var cipher = DeterministicIdCardCipher.Encrypt(plainIdCard);
var customer = await customerService.SearchOneAsync(Prop(nameof(Customer.IdCard)) == cipher);
```

**Or add a blind index column.** Random-IV ciphertext cannot be compared, so keep a hash column alongside it for equality matching, written in sync with the ciphertext column:

```csharp
[Column("IdCardHash", DbType = DbValueType.String, Length = 64, IsIndex = true)]
public string? IdCardHash { get; set; }   // HMAC-SHA256 of the normalised plaintext, written alongside the ciphertext
```

```csharp
var customer = await customerService.SearchOneAsync(
    Prop(nameof(Customer.IdCardHash)) == ComputeHash(plainIdCard));
```

Search boundaries:

- Range queries, sorting and `LIKE` are unavailable on ciphertext. When prefix search is required, the usual compromise is a separate plaintext search column (for example the first six digits of the identity number) and accepting that this part is no longer protected.
- A blind index costs an extra column plus the logic that keeps it in sync, and buys equality search without exposing plaintext. Put the synchronisation in the entity service so both columns stay consistent.
- Deterministic ciphertext can be correlated and compared, so it is weaker than random IV. Use it only where search is mandatory.
- If the business can drop "find a person by identity number", restrict lookups to the primary key or blind index and skip ciphertext comparison altogether.

## Approach 2: custom-type global registration

**Requirement**: search by ciphertext equality, but without writing `ConverterType` on every column. Ciphertext columns appear in more than one place and you want one config that applies everywhere.

Registering the pair (`string`, `DbValueType.String`) does not work, because every `string` parameter would be encrypted, including entities with no ciphertext column at all. Invert the idea: use a wrapper type that only ever holds ciphertext as the property type, keep encryption and decryption inside that type, then register its read and write conversion once. The registry key is the custom type, so only columns declared with it match; every other `string` column is untouched.

**Define the ciphertext type**:

```csharp
using LiteOrm.Common;

/// <summary>Ciphertext string. Both construction and retrieval are explicit so plaintext and ciphertext cannot be mixed up.</summary>
public class EncryptedString
{
    public string Cipher { get; }

    private EncryptedString(string cipher) => Cipher = cipher;

    /// <summary>Encrypts plaintext on construction.</summary>
    public static EncryptedString FromPlain(string plain) => new(Encrypt(plain));

    /// <summary>Wraps existing ciphertext, used by the read path.</summary>
    public static EncryptedString FromCipher(string cipher) => new(cipher);

    /// <summary>Decrypts back to plaintext.</summary>
    public string ToPlain() => Decrypt(Cipher);

    // Equality comparison stores ciphertext equality; used by Lambda conditions (c.IdCard == EncryptedString.FromPlain(...))
    public static bool operator ==(EncryptedString left, EncryptedString right) => left.Cipher == right.Cipher;
    public static bool operator !=(EncryptedString left, EncryptedString right) => !(left == right);
    public override bool Equals(object? obj) => obj is EncryptedString other && Cipher == other.Cipher;
    public override int GetHashCode() => Cipher.GetHashCode();

    private static string Encrypt(string plain) => /* AES-GCM with random IV and a version header, as in approach 1 */;
    private static string Decrypt(string cipher) => /* AES-GCM decryption, as in approach 1 */;
}
```

**Register the read and write conversion globally** (declared once, run during startup):

```csharp
using static LiteOrm.Common.DbValueType;

// Declared once; from then on EncryptedString values are handled as String in the database
DbValueTypeMap.Set(typeof(EncryptedString), DbValueType.String);

SqlBuilder.Instance.RegisterDbValueConverter<SqlBuilder, string, EncryptedString>(
    targetType: DbValueType.String,
    fromDb: cipher => EncryptedString.FromCipher(cipher),   // read: ciphertext from the database -> EncryptedString
    toDb:   value => value.Cipher                           // write: EncryptedString -> ciphertext in the database
);
```

**The entity only declares the property type**; neither `DbType` nor `ConverterType` is needed:

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

**Writing, reading back, and searching by value**:

```csharp
// Write encrypts and read decrypts automatically; no ciphertext appears in business code
await customerService.InsertAsync(new Customer { IdCard = EncryptedString.FromPlain(plainIdCard) });

var loaded = await customerService.Search(c => c.Id == id).FirstOrDefaultAsync();
string plain = loaded.IdCard!.ToPlain();
```

```csharp
// Pass an EncryptedString; the registered write converter turns it into ciphertext automatically
var customer = await customerService.SearchOneAsync(
    c => c.IdCard == EncryptedString.FromPlain(plainIdCard));
```

Notes:

- `DbValueTypeMap.Set` is global. Once registered, every property of that type is treated as `String` and the DDL emits `VARCHAR` (`Length` still applies as usual). Because it affects the whole process, reserve it for custom types that genuinely share one storage form.
- Without the mapping, `GetDbValueType` falls back to `Object` and the column has to carry `DbType = DbValueType.String` itself, otherwise the framework reads through `GetValue` and hands you a boxed value. Pick one of the two; do not omit both.
- The global registration is a one-off process-wide action. Run it during startup and register before the first query. The registry is keyed by `(value type, DbValueType)` and registering the same key again overwrites the previous entry, so switching to a new converter implementation is just another registration. If a column-level converter was already backfilled from the old registration, clear the column's `DbValueConverter` first, otherwise the old instance keeps taking effect.
- The global registration covers entity column reads and writes, and it also covers bare values written by hand in both `Expr` and Lambda conditions, which are looked up in the same registry by the value's runtime type. Pass an `EncryptedString` in the condition (e.g. `EncryptedString.FromPlain(...)`); passing plaintext matches the plaintext type and returns nothing.
- Range queries, sorting and `LIKE` are still unavailable on the ciphertext, the same search boundaries as approach 1.
- Under AOT the column must be explicit (see [AOT differences](#aot-differences) below); the mapping and the global registration alone are not enough.

## General considerations

Before release, confirm the columns are long enough, decide where indexes go and how keys are managed.

Ciphertext is longer than plaintext. Base64 inflates by roughly 33 percent, plus a few dozen bytes for the nonce and tag:

| Plaintext length | Ciphertext length (Base64, with version 1 + nonce 12 + tag 16) | Suggested column length |
| --- | --- | --- |
| Up to 32 bytes | Up to 80 characters | 128 |
| Up to 64 bytes | Up to 128 characters | 192 |
| Up to 128 bytes | Up to 224 characters | 256 |

If a column was created at plaintext width, `varchar(18)` being common for identity numbers, widen it before enabling encryption. Otherwise the database truncates the value or errors during the write.

Notes:

- A plain index on a ciphertext column does not help queries, with the exception of equality search on deterministic ciphertext. Build indexes on the blind index or search columns instead.
- Never put keys in code or in the repository. Read them from environment variables, a key service or the OS keystore, and initialise them once into a static field inside the converter.
- Put a key version in the payload (the first byte in the example) so rotation can write with the new key and read by version, instead of re-encrypting the whole table in one pass.
- Losing the key means losing the data; non-deterministic encryption has no recovery path. Key backup and access auditing are part of this design.
- During rotation a record can hold fields encrypted with the old key and fields encrypted with the new one, so the decrypt path must handle several versions at once.

## AOT differences

When the project publishes with NativeAOT, keep the encrypted columns working by watching the following:

- Complex-typed columns (arrays, collections, custom classes) must declare `ConverterType` explicitly under AOT, otherwise the source generator skips the read mapping for that column. In approach 2, registering the mapping and the global conversion alone is not enough: the source generator infers the column's value type from the actual CLR type reported by `reader.GetFieldType(i)`, and a custom type only ever infers as `Object`; `DbValueTypeMap.Set` cannot change that. So an AOT column reads explicitly `[Column("IdCard", DbType = DbValueType.String, ConverterType = typeof(...))]`.
- The source generator always reads through the non-generic `DbReadConverter` with `reader.GetValue(i)` as input, so boxing is unavoidable. The strongly typed generic delegates only apply on the JIT path. This is one reason AOT mapping performs differently from JIT mapping.
- A converter without a public parameterless constructor fails at compile time in generated code, which is earlier than discovering it at runtime.
- The metadata source switches to the generated `ColumnInfo` under AOT, as described in example 3 of [Tenant Isolation](./tenant-isolation.en.md). Encrypted columns are unaffected; only `Constant` slices are, and the replacement is to carry the fixed condition with a runtime `GenericSqlExpr`.

## Related links

- [Back to index](../README.md)
- [Data Mapping and Value Conversion](../advanced-topics/data-mapping.en.md)
- [NativeAOT Support](../advanced-topics/aot.en.md)
- [Security](../advanced-topics/security.en.md)
- [Audit and Change Tracking](./audit-and-change-tracking.en.md)
- [Data Permissions](./data-permission.en.md)