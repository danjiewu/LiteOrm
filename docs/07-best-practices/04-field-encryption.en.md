# Field Encryption and Masking

Encrypting a column inside an ORM usually means three things: replace plaintext with ciphertext on write, restore it on read, and still be able to search by the value where the business requires it. The third part causes the most trouble, because query parameters and entity writes do not travel the same conversion path.

## 1. Choose the scheme first

| Scheme | Reversible | Equality search | Range / sort / LIKE | Fits |
| --- | --- | --- | --- | --- |
| Non-deterministic encryption (random IV) | Yes | No | No | Fields that are only read back |
| Deterministic encryption + blind index | Yes | Yes (via the index) | No | Fields searched by exact value |
| One-way hash | No | Yes | No | Matching only, original never needed |
| Masking | No | No | No | Display and statistics columns |

The dividing line is "do we need to search by this value". If yes, a comparable form has to exist: a deterministic ciphertext, or a separate hashed blind-index column. Non-deterministic encryption plus equality search is a combination that cannot work.

## 2. The column-level converter

LiteOrm carries encryption in a column-level converter. The converter implements `IDbValueConverter` (non-generic, used by the AOT path) and optionally the generic `IDbValueConverter<TDbType, TValueType>` (used by the JIT path, no boxing):

```csharp
using System.Security.Cryptography;
using System.Text;
using LiteOrm;
using LiteOrm.Common;

/// <summary>Id card column converter. Ciphertext format: Base64(nonce(12) + tag(16) + ciphertext).</summary>
public sealed class IdCardEncryptConverter : IDbValueConverter<string, string>
{
    private static readonly byte[] Key = Convert.FromBase64String(
        Environment.GetEnvironmentVariable("LITEORM_IDCARD_KEY")
        ?? throw new InvalidOperationException("LITEORM_IDCARD_KEY is missing."));

    public Type ValueType => typeof(string);

    // Read direction: database value -> entity property value
    DbConvertHandler<string, string>? IDbValueConverter<string, string>.DbReadConverter
        => cipher => Decrypt(cipher);

    // Write direction: entity property value -> database value
    DbConvertHandler<string, object>? IDbValueConverter<string, string>.DbWriteConverter
        => plain => Encrypt(plain);

    // The AOT path uses the non-generic delegates, taking and returning object
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
        // Split nonce / tag / cipher in reverse, then AesGcm.Decrypt
        return Encoding.UTF8.GetString( /* decrypted bytes */ );
    }

    private static byte[] Combine(params byte[][] parts) { /* concatenate in order */ }
}
```

Requirements and constraints:

- The converter must have a **public parameterless constructor**. The framework instantiates it with `Activator.CreateInstance` while resolving column metadata, and the source generator emits `new XxxConverter()` directly. Components that need dependency injection (a configuration centre, a key service) cannot come from the constructor; use static initialisation or environment variables instead.
- Declare it on the column with `ConverterType`:

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

- The column's `DbType` must match what is actually stored. On read, the framework picks a data reader accessor from the column's `DbValueType` and then hands the result to `DbReadConverter`. If the declared type and the stored type disagree, the converter receives a value of the wrong type.
- For simple cases, `FuncDbValueConverter<TDbType, TValueType>` wraps two delegates without a new type. A column-level `ConverterType` still needs a type, so a small class ends up being the landing place.

## 3. Read and write paths

| Direction | Path |
| --- | --- |
| Write (insert / update) | Entity property value → column converter `DbWriteConverter` → database parameter |
| Read (query) | Raw database value → `Get` accessor chosen by column `DbValueType` → column converter `DbReadConverter` → entity property |
| Key-based condition | Bare values converted through the column-aware entry point (primary key and timestamp conditions) |

Both directions are driven by column metadata, so once `ConverterType` sits on the column, entity-level reads and writes carry the encryption automatically.

## 4. Query parameters do not go through the column converter

This is the trap: an ordinary `WHERE` parameter produced by an `Expr` **has no column context**. The parameter is created as `new Param(name, value)`, and command binding looks up the `SqlBuilder` global converter registry by (runtime value type, target `DbValueType`). It never consults the `ConverterType` declared on a specific column.

The following query therefore returns nothing:

```csharp
using static LiteOrm.Common.Expr;

// Plaintext in, plaintext parameter, compared against ciphertext in the database
var customer = await customerService.SearchOneAsync(Prop(nameof(Customer.IdCard)) == plainIdCard);
```

Three ways out, roughly in order of preference:

**Option 1 (preferred): pass the ciphertext explicitly, or add a blind-index column.** The encryption is your own code, so convert once and compare, provided the encryption is deterministic (the same plaintext always yields the same ciphertext):

```csharp
using static LiteOrm.Common.Expr;

// DeterministicIdCardCipher is a deterministic implementation sharing the algorithm and key with the column converter
var cipher = DeterministicIdCardCipher.Encrypt(plainIdCard);
var customer = await customerService.SearchOneAsync(Prop(nameof(Customer.IdCard)) == cipher);
```

The random-IV converter shown earlier does not satisfy that precondition: every encryption differs, so it only supports write-then-read-back. For equality search with non-deterministic encryption, add a blind index instead:

```csharp
[Column("IdCardHash", DbType = DbValueType.String, Length = 64, IsIndex = true)]
public string? IdCardHash { get; set; }   // HMAC-SHA256 of the normalized plaintext, written together with the ciphertext column

// Search by the blind index instead
var customer = await customerService.SearchOneAsync(Prop(nameof(Customer.IdCardHash)) == ComputeHash(plainIdCard));
```

A blind index costs an extra column and the logic that keeps it in sync. In exchange you get equality search without exposing plaintext.

**Option 2: register a global converter.** `RegisterDbValueConverter` can map the combination (`string`, `DbValueType.String`) to an encrypting converter. The side effect is that **every** `string` parameter gets encrypted, including entities with no encrypted column at all. For field-level encryption this route is effectively unusable unless every string column in the database is encrypted.

**Option 3: give up equality search.** Move the lookup to a primary key or a blind index and drop "find by id card" as a feature.

Accept one more fact: range search, sorting and `LIKE` are unavailable over ciphertext. When prefix search is required, a common compromise is a plaintext search column (for example the first six digits) and accepting that this part is no longer protected.

## 5. Column types, lengths and indexes

Ciphertext is longer than plaintext. Base64 inflates by roughly a third, and the nonce and tag add a few dozen bytes on top. Size the column for the largest ciphertext:

| Plaintext | Ciphertext (Base64, nonce 12 + tag 16) | Suggested column length |
| --- | --- | --- |
| ≤ 32 bytes | ≤ 80 chars | 128 |
| ≤ 64 bytes | ≤ 128 chars | 192 |
| ≤ 128 bytes | ≤ 224 chars | 256 |

If the database column was created as `varchar(18)` for a plaintext id card number, turn on encryption only after widening it, otherwise writes are truncated or rejected.

An ordinary index over a ciphertext column does not help queries (except equality on deterministic ciphertext). Index the blind-index column or the search column instead.

## 6. Differences under AOT

- Complex columns (arrays, collections, custom classes) must declare `ConverterType` under AOT, otherwise the source generator skips the column's read mapping.
- The generated reader path always goes through the non-generic `DbReadConverter` with `reader.GetValue(i)` as the input, so boxing is unavoidable. The strongly typed generic delegates only apply on the JIT path. This is one reason AOT mapping performs below the JIT path.
- A converter without a parameterless constructor fails at compile time in the generated code.

## 7. Key management

- Never hard-code keys or commit them. Read them from environment variables, a key service or the OS key store, initialise once in the converter and cache the result.
- Put a key version into the ciphertext (for example two bytes in front of the nonce). New writes use the new key, reads select by version, and the table does not need a one-shot rewrite.
- Losing the key equals losing the data. Non-deterministic encryption has no recovery path. Key backup and access auditing are part of the design.
- During a rotation window a single record can mix old-key and new-key fields, so the decrypt path must handle several versions at once.

## 8. Common mistakes

| Mistake | Consequence |
| --- | --- |
| Encrypting via `ConverterType` then querying with plaintext | No rows found, easily misread as "the record does not exist" |
| Registering a global encrypting converter to fix equality search | Every parameter of that type is encrypted and non-encrypted columns break |
| Equality search over random-IV ciphertext | The same plaintext encrypts differently each time and cannot be compared |
| Forgetting to widen the column | Writes are truncated or rejected |
| Injecting the key through the converter constructor | The framework uses the parameterless constructor, so injection never runs |
| Encrypting without masking | Logs, exports and UI still leak plaintext |

## Related

- [Back to index](../README.md)
- [Data mapping and value conversion](../03-advanced-topics/11-data-mapping.en.md)
- [AOT support](../03-advanced-topics/06-aot.en.md)
- [Security](../03-advanced-topics/08-security.en.md)
- [Audit trails and change tracking](./03-audit-trail.en.md)
- [Data permissions and authorization gaps](./02-data-permission.en.md)
