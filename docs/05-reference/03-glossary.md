# 术语表

## `Expr`

LiteOrm 的表达式对象模型，用来描述 SQL 结构，可用于动态拼接查询、更新和函数调用。

## `LogicExpr`

用于表达逻辑条件的表达式，如比较、与或非、`IN`、`EXISTS` 等。

## `UpdateExpr`

用于描述条件更新语句的表达式结构，常与 `Expr.Update<T>()` 配合使用。

## `ExprString`

基于插值字符串构建 SQL 片段的方式，适合需要局部自定义 SQL 的场景。

## `RawSql`

`ExprString` 的辅助标记类型（独立 `readonly struct`，不继承 `Expr`），专用于在插值字符串中原样插入**不适合参数化的动态值**，典型场景：`LIMIT`/`OFFSET` 的整数值、`ASC`/`DESC` 排序方向、动态列名等。其内容**绕过参数化机制**，内联动态值时调用方必须先校验——数值类用范围校验（如非负整数），字符串/token 类用白名单校验；纯静态的 SQL 文本直接写在 `ExprString` 字面量中即可，无需使用 `RawSql`；不被 `ExprValidator` 扫描，也不支持 Expr JSON 序列化往返。需要运行时参数化的可复用片段请改用 `GenericSqlExpr`。

## `ObjectDAO<T>`

面向实体的基础数据访问对象，适合直接封装底层写入逻辑。

## `ObjectViewDAO<T>`

面向视图模型的查询 DAO，适合关联查询和结果投影。

## `EntityService<T>` / `EntityService<T, TView>`

业务层访问入口，适合承载事务、组合多 DAO、封装业务规则。

## `ForeignType`

属性级外键声明，用于说明当前字段引用哪个外部实体。

## `TableJoin`

类级关联声明，适合复合连接或可复用连接关系。

## `ForeignColumn`

视图字段声明，用于从外表读取具体列。

## `AutoExpand`

自动展开关联路径的机制，用于让更深层的关联继续可被解析。它本身不会强制增加 JOIN 数量，是否生成 JOIN 取决于查询是否真正引用了对应路径。

## `IArged` / `TableArgs`

动态分表参数机制，用于在执行期替换表名中的占位符。

## `SqlBuilder`

数据库方言构建器，负责将表达式转换成具体数据库可执行的 SQL。

## `ConstFilter` / `Column.Constant`

`ColumnAttribute` 的 `Constant` 属性，用于声明固定筛选条件。在元数据阶段被解析并收敛为 `TableDefinition.ConstFilter`，生成 SQL 时自动注入主表 `WHERE` 和关联表 `JOIN ... ON`。适合启用态、固定分区、固定租户类型等模型级恒定规则，不适合当前用户或当前租户等运行时上下文。详见[权限过滤](../06-di/02-permission-filtering.md)。

## `GenericSqlExpr`

基于委托的动态 SQL 表达式（`sealed class GenericSqlExpr : LogicExpr`），允许在不构建完整 Expr 树的情况下注入自定义 SQL 生成逻辑。通过 `GenericSqlExpr.Register` 注册回调委托，使用时以 `Expr.Sql(key, arg)` 引用。位于 `LiteOrm.Common` 命名空间。

## `ExprVisitor`

表达式访问器（`static class`），提供对 `Expr` 树的多模式遍历能力（委托、`IExprNodeVisitor`、`ExprValidator`）。其静态扩展方法 `Validate(this ExprValidator, Expr)` 用于驱动整树校验。位于 `LiteOrm.Common` 命名空间。

## `ExprValidator`

表达式验证器基类（`abstract class`），`Validate(Expr node)` 实例方法仅校验单个节点；整树校验通过 `ExprVisitor.Validate(validator, expr)` 驱动遍历，验证失败时自动记录到 `FailedExpr`。位于 `LiteOrm.Common` 命名空间。

## `FunctionExprValidator`

函数表达式验证器（`class FunctionExprValidator : ExprValidator`），基于 `FunctionPolicy` 枚举（`AllowAll` / `AllowRegisted` / `Disallow`）控制函数表达式的使用策略。位于 `LiteOrm` 命名空间。

## `IBulkProvider`

批量写入提供者接口，用于数据库原生批量导入（如 `MySqlBulkCopy`、`SqlBulkCopy`）。实现后直接设置到对应的 `SqlBuilder.BulkProvider` 属性即可生效，未设置时批量插入回退到普通 SQL。位于 `LiteOrm` 命名空间。

## `CycleDetector`

Expr 循环引用检测器（`static class`），提供 `HasCycle` / `FindCycle` / `Detect` 方法，基于引用相等性（`ReferenceEquals`）检测 Expr 树中的循环引用，防止遍历/转换时出现栈溢出。位于 `LiteOrm.Common` 命名空间。

## `SqlBuildContext`

SQL 构建上下文，携带构建过程中的表别名、作用域、表名参数等状态信息，供 `ISqlBuilder` 和 Expr 转 SQL 流程使用。DAO 可通过重写 `CreateSqlBuildContext` 自定义上下文（如注入分表参数）。位于 `LiteOrm.Common` 命名空间。

