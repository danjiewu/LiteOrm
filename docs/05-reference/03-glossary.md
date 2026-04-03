# 术语表

## `Expr`

LiteOrm 的表达式对象模型，用来描述 SQL 结构，可用于动态拼接查询、更新和函数调用。

## `LogicExpr`

用于表达逻辑条件的表达式，如比较、与或非、`IN`、`EXISTS` 等。

## `UpdateExpr`

用于描述条件更新语句的表达式结构，常与 `Expr.Update<T>()` 配合使用。

## `ExprString`

基于插值字符串构建 SQL 片段的方式，适合需要局部自定义 SQL 的场景。

## `ObjectDAO<T>`

面向实体的基础数据访问对象，适合直接封装底层读写逻辑。

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

