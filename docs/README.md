## 导航

本文档是 LiteOrm 主要内容介绍。如需深入学习，请参考以下导航：

### 入门篇 / Getting Started

| 中文 | English | 说明 |
|-----|---------|------|
| [概览](./01-getting-started/01-overview.md) | [Overview](./01-getting-started/01-overview.en.md) | 框架介绍与适用场景 |
| [安装](./01-getting-started/02-installation.md) | [Installation](./01-getting-started/02-installation.en.md) | 安装步骤与环境配置 |
| [配置](./01-getting-started/03-configuration-and-registration.md) | [Configuration](./01-getting-started/03-configuration-and-registration.en.md) | 服务注册与配置 |
| [示例](./01-getting-started/04-first-example.md) | [First Example](./01-getting-started/04-first-example.en.md) | 完整使用示例 |

### 核心使用篇 / Core Usage

| 中文 | English | 说明 |
|-----|---------|------|
| [实体映射](./02-core-usage/01-entity-mapping.md) | [Entity Mapping](./02-core-usage/01-entity-mapping.en.md) | 实体定义与映射 |
| [视图模型](./02-core-usage/02-view-models-and-services.md) | [View Models](./02-core-usage/02-view-models-and-services.en.md) | 视图模型与服务层 |
| [查询指南](./02-core-usage/03-query-guide.md) | [Query Guide](./02-core-usage/03-query-guide.en.md) | 各种查询方式 |
| [CRUD指南](./02-core-usage/04-crud-guide.md) | [CRUD Guide](./02-core-usage/04-crud-guide.en.md) | 增删改查操作 |
| [关联查询](./02-core-usage/05-associations.md) | [Associations](./02-core-usage/05-associations.en.md) | 表关联与 JOIN |

### 高级特性篇 / Advanced Topics

| 中文 | English | 说明 |
|-----|---------|------|
| [事务](./03-advanced-topics/01-transactions.md) | [Transactions](./03-advanced-topics/01-transactions.en.md) | 事务与并发控制 |
| [分表分库](./03-advanced-topics/02-sharding-and-tableargs.md) | [Sharding](./03-advanced-topics/02-sharding-and-tableargs.en.md) | 分表策略与路由 |
| [性能](./03-advanced-topics/03-performance.md) | [Performance](./03-advanced-topics/03-performance.en.md) | 性能调优建议 |
| [窗口函数](./03-advanced-topics/04-window-functions.md) | [Window Functions](./03-advanced-topics/04-window-functions.en.md) | 窗口函数支持 |
| [自定义分页](./03-advanced-topics/05-custom-paging.md) | [Custom Paging](./03-advanced-topics/05-custom-paging.en.md) | 分页方案扩展 |

### 扩展开发篇 / Extensibility

| 中文 | English | 说明 |
|-----|---------|------|
| [表达式扩展](./04-extensibility/01-expression-extension.md) | [Expression Extension](./04-extensibility/01-expression-extension.en.md) | 自定义表达式 |
| [验证器](./04-extensibility/02-function-validator.md) | [Function Validator](./04-extensibility/02-function-validator.en.md) | 函数验证机制 |
| [SqlBuilder](./04-extensibility/03-custom-sqlbuilder.md) | [SqlBuilder](./04-extensibility/03-custom-sqlbuilder.en.md) | SQL 方言扩展 |

### 参考文档 / Reference

| 中文 | English | 说明 |
|-----|---------|------|
| [配置参考](./05-reference/01-configuration-reference.md) | [Config Reference](./05-reference/01-configuration-reference.en.md) | 配置项说明 |
| [API索引](./05-reference/02-api-index.md) | [API Index](./05-reference/02-api-index.en.md) | API 快速索引 |
| [术语表](./05-reference/03-glossary.md) | [Glossary](./05-reference/03-glossary.en.md) | 术语解释 |
| [AI指南](./05-reference/05-ai-guide.md) | [AI Guide](./05-reference/05-ai-guide.md) | AI 辅助开发 |
| [示例索引](./05-reference/06-example-index.md) | [Example Index](./05-reference/06-example-index.en.md) | 示例代码索引 |
| [SQL示例](./05-reference/07-sql-examples.md) | [SQL Examples](./05-reference/07-sql-examples.en.md) | SQL 生成示例 |
| [兼容性](./05-reference/08-database-compatibility.md) | [Compatibility](./05-reference/08-database-compatibility.en.md) | 各数据库差异 |

### 相关资源 / Related Resources

| 资源 | Resource |
|-----|----------|
| [Demo 项目](../LiteOrm.Demo/) | [Demo project](../LiteOrm.Demo/) |
| [单元测试](../LiteOrm.Tests/) | [Unit tests](../LiteOrm.Tests/) |
| [性能报告](../LiteOrm.Benchmark/LiteOrm.Benchmark.OrmBenchmark-report-github.md) | [Benchmark report](../LiteOrm.Benchmark/LiteOrm.Benchmark.OrmBenchmark-report-github.md) |

### 推荐阅读路径

1. 第一次接触 LiteOrm：先看"入门篇"的四篇文档。
2. 准备接入业务项目：继续阅读"核心使用篇"，建立实体、查询、写入和关联的整体认识。
3. 涉及事务、分表、性能或数据库方言差异：继续阅读"高级特性篇"。
4. 需要扩展框架能力：查阅"扩展开发篇"。
5. 需要快速确认配置项、接口名或术语：直接查阅"参考篇"。

---

## 1. 概览

LiteOrm 是一个轻量级、高性能的 .NET ORM（对象关系映射）框架，结合了微ORM的速度和全ORM的功能性，适用于需要可预测性能同时仍能干净处理丰富SQL场景的项目。

**主要功能/亮点：**
- 超高性能：性能接近原生Dapper，远超EF Core
- 多数据库支持：原生支持SQL Server、MySQL、Oracle、PostgreSQL、SQLite
- 灵活查询：通过Lambda、`Expr`或`ExprString`多种查询方法
- 自动关联：通过属性实现JOIN查询，无需手动编写SQL
- 声明式事务：通过`[Transaction]`属性实现AOP事务管理
- 动态分表：通过`IArged`接口实现表路由
- 异步支持：完整的async/await支持
- 类型安全：强类型泛型接口，具有编译时类型检查

**典型应用场景：**
- 需要高性能数据访问的企业应用
- 多数据库环境的项目
- 复杂查询需求的系统
- 需要分表策略的大数据量应用
- 追求代码简洁性和可维护性的项目

## 2. 目录结构

LiteOrm 项目采用模块化设计，清晰地分离了核心功能、公共组件、示例和测试代码。项目结构组织合理，便于维护和扩展。

```text
├── LiteOrm/                # 核心库
│   ├── Classes/            # 核心类
│   ├── CodeGen/            # 代码生成
│   ├── Converter/          # 转换器
│   ├── DAO/                # 数据访问对象
│   ├── DAOContext/         # 数据访问上下文
│   ├── DbAccess/           # 数据库访问
│   ├── Initilizer/         # 初始化器
│   ├── Service/            # 服务层
│   └── SqlBuilder/         # SQL构建器
├── LiteOrm.Common/         # 公共组件
│   ├── Attributes/         # 特性
│   ├── Classes/            # 公共类
│   ├── Converter/          # 公共转换器
│   ├── Expr/               # 表达式
│   ├── MetaData/           # 元数据
│   ├── Model/              # 模型
│   ├── Service/            # 公共服务
│   ├── SqlBuilder/         # 公共SQL构建器
│   └── SqlSegment/         # SQL片段
├── LiteOrm.Demo/           # 示例项目
│   ├── DAO/                # 示例DAO
│   ├── Data/               # 示例数据
│   ├── Demos/              # 示例代码
│   ├── Models/             # 示例模型
│   └── Services/           # 示例服务
├── LiteOrm.Tests/          # 测试项目
│   ├── Attributes/         # 特性测试
│   ├── Classes/            # 类测试
│   ├── Converter/          # 转换器测试
│   ├── Expr/               # 表达式测试
│   ├── Infrastructure/     # 测试基础设施
│   ├── MetaData/           # 元数据测试
│   ├── Models/             # 测试模型
│   └── Service/            # 服务测试
├── LiteOrm.Benchmark/      # 性能基准测试
└── docs/                   # 文档
    ├── 01-getting-started/ # 入门指南
    ├── 02-core-usage/      # 核心使用
    ├── 03-advanced-topics/ # 高级主题
    ├── 04-extensibility/   # 扩展性
    └── 05-reference/       # 参考
```

**核心模块职责：**

| 模块 | 主要职责 | 文件位置 | <mcfile>引用 |
|-----|---------|---------|------------|
| DAO | 数据访问操作 | LiteOrm/DAO/ | <mcfile name="DAO" path="/workspace/LiteOrm/DAO/" /> |
| Service | 业务服务 | LiteOrm/Service/ | <mcfile name="Service" path="/workspace/LiteOrm/Service/" /> |
| SqlBuilder | SQL语句构建 | LiteOrm/SqlBuilder/ | <mcfile name="SqlBuilder" path="/workspace/LiteOrm/SqlBuilder/" /> |
| Expr | 查询表达式 | LiteOrm.Common/Expr/ | <mcfile name="Expr" path="/workspace/LiteOrm.Common/Expr/" /> |
| Attributes | 实体映射特性 | LiteOrm.Common/Attributes/ | <mcfile name="Attributes" path="/workspace/LiteOrm.Common/Attributes/" /> |
| MetaData | 元数据管理 | LiteOrm.Common/MetaData/ | <mcfile name="MetaData" path="/workspace/LiteOrm.Common/MetaData/" /> |

## 3. 系统架构与主流程

LiteOrm 采用分层架构设计，清晰地分离了数据访问、业务逻辑和表示层。系统架构遵循依赖倒置原则，通过接口实现各层之间的解耦。

### 核心架构组件

1. **实体层**：定义数据模型，通过特性映射到数据库表
2. **DAO层**：提供基础数据访问操作，处理CRUD操作
3. **服务层**：封装业务逻辑，提供高级操作和事务支持
4. **表达式系统**：提供强大的查询构建能力
5. **SQL构建器**：针对不同数据库生成优化的SQL语句
6. **上下文管理**：处理数据库连接和会话

### 数据流向与主流程

```mermaid
graph TD
    A[应用代码]
    B[Service层]
    B -->|使用| C[DAO层]
    E[DAOContext] -->|创建| J[DbCommandProxy]
    F[数据库] -->|读取| K[AutoLockDataReader]
    J -->|构建| K
    K -->|转换| L[实体对象]
    A -->|构建查询| G1[Lambda表达式]
    A -->|构建查询| G2[Expr表达式]
    A -->|构建查询| G3[ExprString]
    G1 -->|转换为| G2
    G2 -->|构建| G3
    G2 -->|传递| B
    G2 -->|传递| C
    G3 -->|传递| C
    C -->|使用| D[SqlBuilder]
    D -->|构建SQL| H[SQL语句]
    H -->|设置| J
    B -->|事务管理| I[SessionManager]
    I -->|控制| E
    P[DAOContextPool] -->|提供| E
    K -->|释放| E
```

**主要流程说明：**

1. **初始化流程**：
   - 应用启动时，通过`RegisterLiteOrm()`注册服务
   - 扫描实体类型，构建元数据
   - 初始化数据库连接池和DAOContextPool

2. **数据访问流程**：
   - Service层使用DAO执行具体操作
   - DAO从DAOContextPool获取DAOContext
   - DAOContext创建DbCommandProxy命令对象
   - DAO使用SqlBuilder构建SQL语句
   - SqlBuilder构建SQL语句
   - DbCommandProxy设置SQL语句并执行命令
   - 数据库返回数据，DbCommandProxy构建AutoLockDataReader
   - AutoLockDataReader读取数据并转换为实体对象
   - AutoLockDataReader释放资源到DAOContext

3. **查询流程**：
   - 通过Lambda表达式、Expr或ExprString构建查询条件
   - Lambda表达式可以转换为Expr，Expr可以构建为ExprString
   - Expr可以传递给Service层或DAO层
   - ExprString只能传递给DAO层使用
   - DAO接收表达式并使用SqlBuilder构建SQL语句
   - SqlBuilder构建SQL语句
   - DAO从DAOContextPool获取DAOContext
   - DAOContext创建DbCommandProxy执行查询
   - 数据库返回数据，DbCommandProxy构建AutoLockDataReader
   - AutoLockDataReader读取结果并转换为实体对象
   - AutoLockDataReader释放资源到DAOContext

4. **事务流程**：
   - 通过`[Transaction]`属性标记需要事务的方法
   - SessionManager管理事务上下文
   - SessionManager直接控制DAOContext
   - 多个操作在同一事务中执行

5. **命令执行流程**：
   - DAO使用SqlBuilder构建SQL语句
   - SqlBuilder构建SQL语句
   - DAO从DAOContextPool获取DAOContext
   - DAOContext创建DbCommandProxy对象
   - DbCommandProxy设置命令文本和参数
   - DbCommandProxy执行命令
   - 数据库返回数据，DbCommandProxy构建AutoLockDataReader
   - AutoLockDataReader安全读取结果
   - AutoLockDataReader释放资源到DAOContext
   - 自动处理资源释放

## 4. 核心功能模块

### 4.1 数据访问对象 (DAO)

DAO层是LiteOrm的核心，提供了直接的数据访问操作。它包括多个实现类，针对不同的数据访问场景。

**主要组件：**

- **DAOBase**：所有DAO的抽象基类，提供通用操作
- **ObjectDAO**：对象化数据访问，处理实体对象的CRUD
- **DataDAO**：数据化访问，返回DataTable等数据结构
- **ObjectViewDAO**：处理视图对象的访问
- **DataViewDAO**：处理数据视图的访问
- **DbCommandProxy**：数据库命令代理，封装了IDbCommand，提供参数处理和执行功能
- **AutoLockDataReader**：自动锁定的数据读取器，确保数据读取过程中的线程安全

**核心功能：**
- 实体对象的增删改查
- 批量操作支持
- 表达式查询
- 分表支持
- 异步操作
- 命令执行与参数处理
- 安全的数据读取

### 4.2 服务层 (Service)

Service层封装了业务逻辑，提供了更高级的操作接口。它基于DAO层构建，增加了事务管理和业务规则。

**主要组件：**

- **EntityService**：实体服务，提供完整的CRUD操作
- **EntityViewService**：视图服务，专注于查询操作

**核心功能：**
- 完整的CRUD操作
- 批量操作
- 事务支持
- 异步方法
- 表达式查询

### 4.3 表达式系统 (Expr)

表达式系统是LiteOrm的特色功能，提供了强大的查询构建能力。它支持三种查询方式：Lambda表达式、Expr对象和ExprString。

**主要组件：**

- **Expr**：表达式基类
- **LogicExpr**：逻辑表达式
- **ValueExpr**：值表达式
- **SelectExpr**：选择表达式
- **UpdateExpr**：更新表达式
- **DeleteExpr**：删除表达式

**核心功能：**
- 构建复杂查询条件
- 支持各种运算符
- 支持子查询
- 支持JOIN操作
- 类型安全

### 4.4 SQL构建器 (SqlBuilder)

SQL构建器负责根据表达式生成针对不同数据库的优化SQL语句。它支持多种数据库类型，提供了数据库特定的语法和函数支持。

**主要组件：**

- **SqlBuilder**：SQL构建器基类
- **SqlServerBuilder**：SQL Server专用构建器
- **MySqlBuilder**：MySQL专用构建器
- **OracleBuilder**：Oracle专用构建器
- **PostgreSqlBuilder**：PostgreSQL专用构建器
- **SQLiteBuilder**：SQLite专用构建器

**核心功能：**
- 生成数据库特定的SQL语句
- 处理参数化查询
- 支持分页
- 支持函数调用
- 处理数据库特定的语法

### 4.5 元数据管理 (MetaData)

元数据管理负责处理实体类型与数据库表之间的映射关系。它通过特性系统构建元数据，为DAO和SQL构建器提供必要的信息。

**主要组件：**

- **TableInfoProvider**：表信息提供者
- **SqlTable**：表信息
- **SqlColumn**：列信息
- **TableDefinition**：表定义
- **ColumnDefinition**：列定义

**核心功能：**
- 构建实体类型的元数据
- 处理表和列的映射
- 管理主键和外键关系
- 支持分表配置

### 4.6 事务管理

LiteOrm提供了声明式事务管理，通过`[Transaction]`属性标记需要事务的方法。事务管理由SessionManager负责，确保多个操作在同一事务中执行。

**核心功能：**
- 声明式事务
- 事务嵌套
- 事务回滚
- 异步事务支持

## 5. 核心 API/类/函数

### 5.1 数据访问核心 API

#### DAOBase

**功能**：所有DAO的抽象基类，提供通用操作方法

**主要方法**：
- `NewCommand()`：创建数据库命令
- `MakeNamedParamCommand()`：创建带参数的命令
- `MakeExprCommand()`：根据表达式创建命令
- `GetValue<T>()`：执行查询并返回单个值
- `Execute()`：执行非查询SQL
- `Query<TResult>()`：执行查询并返回结果集

**使用场景**：作为DAO的基类，提供通用功能

<mcfile name="DAOBase.cs" path="/workspace/LiteOrm/DAO/DAOBase.cs" />

#### ObjectDAO<T>

**功能**：处理实体对象的CRUD操作

**主要方法**：
- `Insert()`：插入实体
- `Update()`：更新实体
- `Delete()`：删除实体
- `DeleteByKeys()`：根据主键删除
- `Search()`：查询实体
- `BatchInsert()`：批量插入
- `BatchUpdate()`：批量更新
- `BatchDelete()`：批量删除

**使用场景**：直接操作实体对象，执行CRUD操作

<mcfile name="ObjectDAO.cs" path="/workspace/LiteOrm/DAO/ObjectDAO.cs" />

#### DbCommandProxy

**功能**：数据库命令代理，封装了DbCommand，提供参数处理和执行功能，结合AutoLockDataReader实现事务、异步锁管理。

**主要方法**：
- `CreateParameter()`：创建数据库参数
- `ExecuteNonQuery()`：执行非查询命令
- `ExecuteReader()`：执行查询并返回数据读取器
- `ExecuteScalar()`：执行查询并返回单个值

**使用场景**：封装数据库命令，处理参数和执行操作

<mcfile name="DbCommandProxy.cs" path="/workspace/LiteOrm/DbAccess/DbCommandProxy.cs" />

#### AutoLockDataReader

**功能**：自动锁定的数据读取器，确保数据读取过程中的线程安全

**主要方法**：
- `Read()`：读取下一条记录
- `GetValue()`：获取指定列的值
- `GetInt32()/GetString()/等`：获取指定类型的值
- `Dispose()`：释放资源

**使用场景**：安全地读取数据库查询结果

<mcfile name="AutoLockDataReader.cs" path="/workspace/LiteOrm/DbAccess/AutoLockDataReader.cs" />

### 5.2 服务层核心 API

#### EntityService<T, TView>

**功能**：提供实体的完整业务操作

**主要方法**：
- `Insert()`：插入实体
- `Update()`：更新实体
- `Delete()`：删除实体
- `BatchInsert()`：批量插入
- `BatchUpdate()`：批量更新
- `BatchDelete()`：批量删除
- `Search()`：查询实体
- `SearchOne()`：查询单个实体
- `SearchAsync()`：异步查询

**使用场景**：在业务逻辑层使用，提供完整的实体操作

<mcfile name="EntityService.cs" path="/workspace/LiteOrm/Service/EntityService.cs" />

#### EntityViewService<TView>

**功能**：专注于查询操作的服务

**主要方法**：
- `Search()`：查询实体
- `SearchOne()`：查询单个实体
- `SearchAsync()`：异步查询
- `Count()`：统计记录数

**使用场景**：仅需要查询功能的场景

<mcfile name="EntityViewService.cs" path="/workspace/LiteOrm/Service/EntityViewService.cs" />

### 5.3 表达式系统核心 API

#### Expr

**功能**：表达式基类，提供查询构建能力

**主要方法**：
- `Prop()`：创建属性表达式
- `Exists<T>()`：创建存在性子查询
- `From<T>()`：创建从表开始的查询
- `ToPreparedSql()`：转换为带参数的SQL语句

**使用场景**：构建复杂的查询条件

<mcfile name="Expr.cs" path="/workspace/LiteOrm.Common/Expr/Expr.cs" />

#### LogicExpr

**功能**：逻辑表达式，用于构建WHERE条件

**主要操作符**：
- `&`：AND操作
- `|`：OR操作
- `!`：NOT操作

**使用场景**：构建复杂的逻辑条件

<mcfile name="LogicExpr.cs" path="/workspace/LiteOrm.Common/Expr/LogicExpr.cs" />

### 5.4 SQL构建器核心 API

#### SqlBuilder

**功能**：SQL构建器基类，提供SQL生成功能

**主要方法**：
- `ToSqlName()`：转换为SQL名称
- `ToSqlParam()`：转换为SQL参数
- `ConvertToDbValue()`：转换为数据库值
- `ConvertFromDbValue()`：从数据库值转换
- `BuildSelectSql()`：构建SELECT语句
- `BuildFunctionSql()`：构建函数SQL语句

**使用场景**：生成数据库特定的SQL语句

<mcfile name="SqlBuilder.cs" path="/workspace/LiteOrm/SqlBuilder/SqlBuilder.cs" />

### 5.5 元数据核心 API

#### SqlTable

**功能**：表示数据库表的元数据

**主要属性**：
- `Name`：表名
- `Columns`：列集合
- `Keys`：主键列
- `Definition`：表定义

**使用场景**：提供表的元数据信息

<mcfile name="SqlTable.cs" path="/workspace/LiteOrm.Common/MetaData/SqlTable.cs" />

#### TableInfoProvider

**功能**：提供表信息的提供者

**主要方法**：
- `GetTable()`：获取表信息
- `GetColumn()`：获取列信息

**使用场景**：构建和管理表的元数据

<mcfile name="TableInfoProvider.cs" path="/workspace/LiteOrm.Common/MetaData/TableInfoProvider.cs" />

## 6. 技术栈与依赖

| 技术/依赖 | 版本 | 用途 | 来源 |
|----------|------|-----|------|
| .NET | 8.0+ | 运行环境 | <mcfile name="LiteOrm.csproj" path="/workspace/LiteOrm/LiteOrm.csproj" /> |
| .NET Standard | 2.0+ | 跨平台支持 | <mcfile name="LiteOrm.csproj" path="/workspace/LiteOrm/LiteOrm.csproj" /> |
| Autofac | 10.0.0 | 依赖注入 | <mcfile name="LiteOrm.csproj" path="/workspace/LiteOrm/LiteOrm.csproj" /> |
| Autofac.Extensions.DependencyInjection | 10.0.0 | 依赖注入扩展 | <mcfile name="LiteOrm.csproj" path="/workspace/LiteOrm/LiteOrm.csproj" /> |
| Autofac.Extras.DynamicProxy | 7.1.0 | 动态代理 | <mcfile name="LiteOrm.csproj" path="/workspace/LiteOrm/LiteOrm.csproj" /> |
| Castle.Core | 5.2.1 | 动态代理核心 | <mcfile name="LiteOrm.csproj" path="/workspace/LiteOrm/LiteOrm.csproj" /> |
| Castle.Core.AsyncInterceptor | 2.1.0 | 异步拦截器 | <mcfile name="LiteOrm.csproj" path="/workspace/LiteOrm/LiteOrm.csproj" /> |
| Microsoft.Extensions.Hosting.Abstractions | 10.0.5 | 主机抽象 | <mcfile name="LiteOrm.csproj" path="/workspace/LiteOrm/LiteOrm.csproj" /> |
| Microsoft.Extensions.Logging.Abstractions | 10.0.5 | 日志抽象 | <mcfile name="LiteOrm.csproj" path="/workspace/LiteOrm/LiteOrm.csproj" /> |
| System.Text.Json | 10.0.5 | JSON处理 | <mcfile name="LiteOrm.Common.csproj" path="/workspace/LiteOrm.Common/LiteOrm.Common.csproj" /> |

**数据库支持：**
- SQL Server 2012+
- Oracle 12c+
- PostgreSQL
- MySQL
- SQLite