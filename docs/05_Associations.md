# 关联查询（TableJoin / ForeignType / AutoExpand）

本文档介绍 LiteOrm 的关联查询能力。

- 涵盖 TableJoin（类级）与 ForeignType（属性级）的使用。
- 说明 ForeignColumn 的取值方式、AutoExpand 的作用与常见实践。

## 概念总览

- ForeignType（属性级）：在某个列上声明它引用的外表实体类型（例如外键列）。支持 Alias（别名）、JoinType（连接类型）、AutoExpand（自动扩展）。
  适用于单列外键场景。

- TableJoin（类级）：在实体或表上预定义与其它表的连接关系，适合多列联合外键或复用同一连接逻辑。
  支持指定 Source、TargetType、ForeignKeys、AliasName、JoinType、AutoExpand 等。

- ForeignColumn（视图字段）：在视图模型中声明要从外表选择的列。Foreign 参数可以是外部类型或 TableJoin 中定义的 AliasName。

- AutoExpand（自动扩展）：当被标记为 true 时，如果该表作为外表被引用，LiteOrm 会自动把该表自身定义的关联表一并引入查询（级联 JOIN）。

---

## 使用示例

### 1) ForeignType（属性级）

```csharp
[Table("Orders")]
public class Order
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [Column("UserId")]
    [ForeignType(typeof(User), Alias = "U", JoinType = TableJoinType.Left, AutoExpand = false)]
    public int UserId { get; set; }

    [Column("Amount")]
    public decimal Amount { get; set; }
}
```

- 说明：ForeignType 用于标注外键列对应的外部实体。查询视图时，通过视图类中的 ForeignColumn 可以自动生成 JOIN 并读取外表列。

### 2) TableJoin（类级）

```csharp
[TableJoin(typeof(Department), "ParentId", AliasName = "Parent", JoinType = TableJoinType.Left)]
[TableJoin(typeof(Department), "DeptId", AliasName = "Dept", JoinType = TableJoinType.Left)]
public class User { /* ... */ }

public class OrderView : Order
{
    [ForeignColumn(typeof(User), Property = "UserName")]
    public string? UserName { get; set; }

    [ForeignColumn("Dept", Property = "Name")]
    public string? DeptName { get; set; } // 使用 TableJoin 的 Alias 引用
}
```

### 3) 多级关联与 AutoExpand

```csharp
// SalesRecord 示例：SalesUserId 关联 User，并自动展开 User 的关联（如 Department）
[Column("SalesUserId")]
[ForeignType(typeof(User), AutoExpand = true)]
public int SalesUserId { get; set; }

public class SalesRecordView : SalesRecord
{
    [ForeignColumn(typeof(User))]
    public string? UserName { get; set; }

    [ForeignColumn(typeof(Department), Property = nameof(Department.Name))]
    public string? DepartmentName { get; set; }
}

// 查询 SalesRecordView 时，LiteOrm 会把 User 的关联（例如 Department）自动加入 JOIN，无需在 SalesRecord 上额外声明。
```

- 注意：AutoExpand 会级联引入被引用表定义的关联，避免在视图上显式写出多级关联字段，但会增加 JOIN 数量。

---

#### 错误示例（说明性对比）

##### 示例 A：未开启 AutoExpand 时，不能直接引用二级关联表的属性

```csharp
// 假设：User 类型通过 TableJoin 关联到 Department（User -> Department）
public class SalesRecord
{
    [Column("SalesUserId")]
    [ForeignType(typeof(User))]  // AutoExpand 未设置（默认 false）
    public int SalesUserId { get; set; }
}

public class SalesRecordView : SalesRecord
{
    [ForeignColumn(typeof(Department), Property = "Name")]
    public string? DepartmentName { get; set; } // 期望通过 SalesRecord -> User -> Department 引入 Department.Name
}
```

说明：由于 SalesRecord 上的 ForeignType 未启用 AutoExpand，LiteOrm 不会将 User 的 TableJoin 级联到 Department。
因此，视图中直接引用 Department 的字段不会自动生成对应的 JOIN。
要么在 SalesRecord 或查询层显式声明 TableJoin，
要么在中间的 ForeignType 上设置 AutoExpand = true。

##### 示例 B：AutoExpand 会忽略已显式引用的别名以避免重复 JOIN

```csharp
[TableJoin(typeof(Department), "DeptId", AliasName = "Dept", JoinType = TableJoinType.Left)]
public class User { /* ... */ }

public class SalesRecord
{
    [Column("SalesUserId")]
    [ForeignType(typeof(User), Alias = "U", AutoExpand = true)]
    public int SalesUserId { get; set; }
}

public class SalesRecordView : SalesRecord
{
    [ForeignColumn(typeof(User), Property = "UserName")]
    public string? UserName { get; set; }

    [ForeignColumn("Dept", Property = "Name")]
    public string? DepartmentName { get; set; }
}
```

说明：SalesRecord 引用了 User 且 User 的 TableJoin 已包含 Alias "Dept"。
当 AutoExpand = true 时，LiteOrm 会自动把 User 的 Dept 引入查询。
如果视图已经通过别名 "Dept" 显式引用 Department 字段，框架会避免重复生成第二次 Dept 的 JOIN，以免产生冗余连接。

##### 示例 C：未被任何属性直接或间接引用的关联表不会被构造为 JOIN（避免额外开销）

```csharp
// 假设 A -> B（TableJoin），B -> C（TableJoin，AutoExpand 可选）
// 如果查询的视图只引用了 A 与 A 直接关联的字段，而没有任何字段引用 C，则 C 不会被加入 JOIN。
```

说明：LiteOrm 在元数据阶段会基于视图中声明的 ForeignColumn 所需列，计算最短的关联路径并只加入必要的 JOIN。未被任何属性对应列直接或间接引用的关联表不会构造成 JOIN，从而避免不必要的查询开销。

总结：

- 当需要从二级或更深层级的关联中读取列时，若希望自动级联，请在中间 ForeignType 或 TableJoin 上设置 AutoExpand = true；
- AutoExpand 会智能避免重复别名的重复 JOIN；
- 框架只会为视图中声明的列构造必要的 JOIN，未引用的关联不会被加入。

- 说明：TableJoin 允许在类型层面配置复杂或复合键的连接关系，便于在多个视图或查询重用。

---

- 注意：AutoExpand 会级联引入被引用表定义的关联，避免在视图上显式写出多级关联字段，但会增加 JOIN 数量。

---

## API 要点

- ForeignTypeAttribute: ObjectType、Alias、JoinType、AutoExpand
- TableJoinAttribute: Source、TargetType、ForeignKeys、AliasName、JoinType、AutoExpand
- ForeignColumnAttribute: Foreign（Type 或 AliasName）、Property（要获取的列）

实现上，LiteOrm 会在元数据阶段合并 ForeignType 与 TableJoin 的信息，生成 JoinedTable / ForeignTable 结构。
最终在构建 SQL 时，会把 TableJoinExpr 插入 FromExpr.Joins 中。

---

## 最佳实践

- 常规单列外键：优先使用 ForeignType + ForeignColumn，语义清晰、维护成本低。
- 复合键或需要复用的连接：使用 TableJoin 在类型上预定义，避免在多个视图重复声明。
- AutoExpand：仅对经常需要的、稳定的级联路径开启。避免在大型表或可变关联路径上滥用，以免导致过多无用 JOIN。
- Alias：使用 Alias/AliasName 避免列名冲突，视图中只声明必要的外表列以减少网络传输。
- 性能验证：对复杂视图在生产前审查生成的 SQL，并用数据库执行计划（EXPLAIN）检查索引与连接顺序。

---

## 常见问题

- Q：ForeignColumn 的 Foreign 可以是 TableJoin 的 Alias 吗？
  A：可以。ForeignColumn 的 Foreign 参数既可为外部类型（Type），也可为 TableJoin 中的 AliasName。

- Q：AutoExpand 是否会展开无限层级？
  A：AutoExpand 按定义的关联逐级扩展，但实际扩展深度取决于已注册的 TableJoin/ForeignType 配置，需谨慎控制以避免循环或爆炸式扩展。

---

## 下一步

- 基础概念：[基础概念](./02_CoreConcepts.md)
- 查询指南：[查询指南](./03_QueryGuide.md)
- 增删改查：[增删改查](./04_CrudGuide.md)
- 性能优化：[性能优化](./EXP/EXP_Performance.md)
- API 参考：[API 参考](../LITEORM_API_REFERENCE.zh.md)

更多示例请参考代码中的 Demo（LiteOrm.Demo.Models）以及单元测试中的 TableJoin/AutoExpand 相关测试用例。
