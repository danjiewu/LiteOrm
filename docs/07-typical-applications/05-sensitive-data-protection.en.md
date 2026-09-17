# Sensitive Data Protection in Practice

Encrypting a column in an ORM means solving three things: replacing plaintext with ciphertext on write, restoring it on read, and still being able to query the value when the business needs it. The third one breaks first, because query parameters and entity write parameters do not travel the same conversion path. The scenarios below show the working patterns.

Choose the scheme first; the dividing line is whether you need to search by this value:

| Scheme | Reversible | Equality search | Range / sort / LIKE | Use for |
| --- | --- | --- | --- | --- |
| Non-deterministic encryption (random IV) | Yes | No | No | Fields that are written and read back, never searched |
| Deterministic encryption | Yes | Yes (compare ciphertext) | No | Fields that must be searched by exact value |
| Blind index column (hash) | No | Yes | No | Matching only, no need to read the original |
| Masking | No | No | No | Display fields |

## Scenario 1: store one column as ciphertext

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

## Scenario 2: query by identity number

**Requirement**: the business must support "find a customer by identity number" while the database stores ciphertext.

**Approach**: accept one fact first. Ordinary `WHERE` parameters generated from `Expr` carry no column context. Parameters are produced as `new Param(name, value)`, and command binding looks up the global converter registry by the value's runtime type plus the target `DbValueType`; it never consults the `ConverterType` declared on a column. So this query returns nothing:

```csharp
using static LiteOrm.Common.Expr;

// Plaintext in, plaintext parameter, no match against the ciphertext in the database
var customer = await customerService.SearchOneAsync(Prop(nameof(Customer.IdCard)) == plainIdCard);
```

Three routes exist, ordered by preference:

**Option 1 (recommended): pass ciphertext explicitly, or use a blind index column.** The encryption logic is yours, so encrypt once more before comparing, provided the encryption is deterministic (the same plaintext always yields the same ciphertext):

```csharp
using static LiteOrm.Common.Expr;

var cipher = DeterministicIdCardCipher.Encrypt(plainIdCard);
var customer = await customerService.SearchOneAsync(Prop(nameof(Customer.IdCard)) == cipher);
```

The random-IV encryption from scenario 1 does not satisfy that prerequisite because every call returns a different ciphertext; it only supports "write it in, read it back". Fields like that need a blind index column for equality search:

```csharp
[Column("IdCardHash", DbType = DbValueType.String, Length = 64, IsIndex = true)]
public string? IdCardHash { get; set; }   // HMAC-SHA256 of the normalised plaintext, written alongside the ciphertext
```

```csharp
var customer = await customerService.SearchOneAsync(
    Prop(nameof(Customer.IdCardHash)) == ComputeHash(plainIdCard));
```

**Option 2: define a custom encrypted string type and register it globally.** This route solves both problems at once, the context-free parameter and the wish to avoid `ConverterType`, by giving ciphertext fields a dedicated strong type.

Registering the pair (`string`, `DbValueType.String`) does not work, because every `string` parameter would be encrypted, including entities with no ciphertext column at all. Invert the idea: use a wrapper type that only ever holds ciphertext as the property type, keep encryption and decryption inside that type, then register its read and write conversion once. The registry key is the custom type, so only columns declared with it match; every other `string` column is untouched.

```csharp
using LiteOrm.Common;

/// <summary>Ciphertext string. Both construction and retrieval are explicit so plaintext and ciphertext cannot be mixed up.</summary>
public readonly struct EncryptedString
{
    public string Cipher { get; }

    private EncryptedString(string cipher) => Cipher = cipher;

    /// <summary>Encrypts plaintext on construction.</summary>
    public static EncryptedString FromPlain(string plain) => new(Encrypt(plain));

    /// <summary>Wraps existing ciphertext, used by the read path.</summary>
    public static EncryptedString FromCipher(string cipher) => new(cipher);

    /// <summary>Decrypts back to plaintext.</summary>
    public string ToPlain() => Decrypt(Cipher);

    private static string Encrypt(string plain) => /* AES-GCM encryption from scenario 1 */;
    private static string Decrypt(string cipher) => /* AES-GCM decryption from scenario 1 */;
}
```

Register a database value type mapping for the custom type first, then register the read and write conversion globally:

```csharp
using static LiteOrm.Common.DbValueType;

// Declared once; from then on EncryptedString is treated as a String column
DbValueTypeMap.Set(typeof(EncryptedString), DbValueType.String);

SqlBuilder.Instance.RegisterDbValueConverter<SqlBuilder, string, EncryptedString>(
    targetType: DbValueType.String,
    fromDb: cipher => EncryptedString.FromCipher(cipher),   // read: ciphertext from the database -> EncryptedString
    toDb:   value => value.Cipher                           // write: EncryptedString -> ciphertext in the database
);
```

With that mapping in place the entity only declares the property type; neither `DbType` nor `ConverterType` is needed:

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

Writing, reading back, and searching by value:

```csharp
// Write encrypts and read decrypts automatically; no ciphertext appears in business code
await customerService.InsertAsync(new Customer { IdCard = EncryptedString.FromPlain(plainIdCard) });

var loaded = await customerService.Search(c => c.Id == id).FirstOrDefaultAsync();
string plain = loaded.IdCard!.Value.ToPlain();
```

```csharp
using static LiteOrm.Common.Expr;

// The value in a query condition must be converted to ciphertext explicitly, and then the WHERE matches
var cipher = EncryptedString.FromPlain(plainIdCard).Cipher;
var customer = await customerService.SearchOneAsync(Prop(nameof(Customer.IdCard)) == cipher);
```

Three points to watch:

- `DbValueTypeMap.Set` is global. Once registered, every property of that type is treated as `String` and the DDL emits `VARCHAR` (`Length` still applies as usual). Because it affects the whole process, reserve it for custom types that genuinely share one storage form.
- Without the mapping, `GetDbValueType` falls back to `Object` and the column has to carry `DbType = DbValueType.String` itself, otherwise the framework reads through `GetValue` and hands you a boxed value. Pick one of the two; do not omit both.
- Under AOT the column must be explicit, because neither the mapping nor the global registration is enough: the source generator infers the column's value type from the actual CLR type reported by `reader.GetFieldType(i)`, and a custom type only ever infers as `Object`. On top of that, the source generator skips the read mapping for a complex-typed column that has no `ConverterType`. Neither behaviour changes because of `DbValueTypeMap.Set`, so an AOT column reads `[Column("IdCard", DbType = DbValueType.String, ConverterType = typeof(...))]` (see scenario 6).

**Option 3: do not support equality search.** Restrict lookups to primary key or blind index and drop "find a person by identity number" from the product.

Notes:

- Range queries, sorting and `LIKE` are unavailable on ciphertext. When prefix search is required, the usual compromise is a separate plaintext search column (for example the first six digits of the identity number) and accepting that this part is no longer protected.
- A blind index costs an extra column plus the logic that keeps it in sync, and buys equality search without exposing plaintext. Put the synchronisation in the entity service so both columns stay consistent.
- Deterministic ciphertext can be correlated and compared, so it is weaker than random IV. Use it only where search is mandatory.
- The global registration in option 2 is a one-off process-wide action. Run it during startup and register before the first query. The registry is keyed by `(value type, DbValueType)` and registering the same key again overwrites the previous entry, so switching to a new converter implementation is just another registration. If a column-level converter was already backfilled from the old registration, clear the column's `DbValueConverter` first, otherwise the old instance keeps taking effect.
- The global registration covers entity column reads and writes, and it also covers bare values written by hand inside `Expr`, which are looked up in the same registry by the value's runtime type. The value in a condition still has to be converted to ciphertext yourself: passing plaintext matches the plaintext type and returns nothing.

## Scenario 3: only prove a value exists, never read it back

**Requirement**: sign-in and duplicate checks only need to know whether the value is already in the database.

**Approach**: store a one-way hash and hash the input before comparing:

```csharp
[Column("IdHash", DbType = DbValueType.String, Length = 64, IsIndex = true)]
public string? IdHash { get; set; }   // value = HMAC-SHA256(normalised plaintext)
```

```csharp
var exists = await customerService.ExistsAsync(
    Prop(nameof(Customer.IdHash)) == ComputeHash(plainIdCard));
```

Notes:

- A hash is irreversible, so this column never yields the original. When display needs the original, keep a ciphertext column or a masking column alongside.
- Hashing needs a fixed key (HMAC), otherwise the plaintext can be reversed from a rainbow table.
- Normalise first and pin the rules: case, whitespace, full-width versus half-width. Otherwise the same value hashes differently on write and on lookup.

## Scenario 4: mask values for display

**Requirement**: the service desk screen shows phone numbers but must hide the middle digits, and the mask must never be written back.

**Approach**: put the mask on a read-only column. Declared as `ColumnMode.Read`, the column takes no part in `INSERT` / `UPDATE` and applies the masking converter on read:

```csharp
public sealed class PhoneMaskConverter : IDbValueConverter<string, string>
{
    Type IDbValueConverter.ValueType => typeof(string);

    DbConvertHandler<string, string>? IDbValueConverter<string, string>.DbReadConverter
        => value => Mask(value);

    DbConvertHandler<string, object>? IDbValueConverter<string, string>.DbWriteConverter => null;

    DbConvertHandler? IDbValueConverter.DbReadConverter => value => Mask((string)value);

    DbConvertHandler? IDbValueConverter.DbWriteConverter => null;

    private static string Mask(string phone)
        => phone.Length < 7 ? "***" : $"{phone[..3]}****{phone[^4..]}";
}
```

```csharp
[Table("Customers")]
public class CustomerMaskView : ObjectBase
{
    [Column("Id", IsPrimaryKey = true)]
    public long Id { get; set; }

    [Column("Name")]
    public string? Name { get; set; }

    [Column("Phone", DbType = DbValueType.String, Length = 32,
            ColumnMode = ColumnMode.Read, ConverterType = typeof(PhoneMaskConverter))]
    public string? Phone { get; set; }
}
```

Notes:

- The mask column only has `Read` mode, so `TableDefinition.InsertableColumns` and `UpdatableColumns` both exclude it and the mask never reaches the database. Writes go through an entity without the masking converter.
- Put display masking on a view model and keep the entity's original value, so one type does not drift in meaning between screens.
- Logs, exports and audits need the same masking rules. Masking only in the front end misses the export path; see [Audit and Change Tracking in Practice](./04-audit-and-change-tracking.en.md).

## Scenario 5: column length, indexes and keys

**Requirement**: before release, confirm the columns are long enough, decide where indexes go and how keys are managed.

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

## Scenario 6: differences under NativeAOT

**Requirement**: the project publishes with NativeAOT and encrypted columns must keep working.

Notes:

- Complex-typed columns (arrays, collections, custom classes) must declare `ConverterType` explicitly under AOT, otherwise the source generator skips the read mapping for that column.
- The source generator always reads through the non-generic `DbReadConverter` with `reader.GetValue(i)` as input, so boxing is unavoidable. The strongly typed generic delegates only apply on the JIT path. This is one reason AOT mapping performs differently from JIT mapping.
- A converter without a public parameterless constructor fails at compile time in generated code, which is earlier than discovering it at runtime.
- The metadata source switches to the generated `ColumnInfo` under AOT, as described in scenario 4 of [Tenant Isolation in Practice](./01-tenant-isolation.en.md). Encrypted columns are unaffected; only `Constant` slices are, and the replacement is to express the fixed condition as a runtime fragment.

## Related links

- [Back to index](../README.md)
- [Data Mapping and Value Conversion](../03-advanced-topics/11-data-mapping.en.md)
- [NativeAOT Support](../03-advanced-topics/06-aot.en.md)
- [Security](../03-advanced-topics/08-security.en.md)
- [Audit and Change Tracking in Practice](./04-audit-and-change-tracking.en.md)
- [Data Permissions in Practice](./02-data-permission.en.md)
