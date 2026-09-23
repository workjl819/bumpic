# 开发规则

本文档是根目录 `AGENTS.md` 的详细开发规则。适用于当前仓库 `src/` 下的领域层、基础设施层、Web 层及相关应用代码。

## 强类型 ID 与聚合根

- 聚合根必须继承 `Entity<TId>` 并实现 `IAggregateRoot`。
- `TId` 必须是 `IInt64StronglyTypedId` 或 `IGuidStronglyTypedId` 对应的强类型 ID。
- 聚合根 ID 依赖 EF Core ValueGenerator 生成，不在业务代码中手动赋值。
- 实体配置必须使用项目现有的强类型 ID 值生成器配置。
- 强类型 ID 默认直接传递，不调用 `.Value`。
- 只有数据库、EF 配置、序列化或外部 API 边界允许解包；解包必须限制在边界适配代码中。

## Command 与 Query

- 每个 Command 必须有独立的 `AbstractValidator<TCommand>`。
- 每个 Query 必须有独立的 `AbstractValidator<TQuery>`。
- 一个文件只定义一个 Command 或 Query。
- Command 通过仓储操作聚合，Query 直接使用 `ApplicationDbContext` 进行只读查询。
- 所有异步调用必须传递 `CancellationToken`。

## FastEndpoints

- 路由、授权、匿名访问和标签优先使用 `[HttpGet]`、`[HttpPost]`、`[Authorize]`、`[AllowAnonymous]`、`[Tags]` 等特性。
- 只有特性无法表达配置时，才使用 `Configure()`，并保留必要原因。
- 一个请求对应一个 Endpoint 类。
- Endpoint 请求和响应优先直接使用强类型 ID。

## C# 代码风格

- 禁止 expression-bodied method，例如 `void Run() => ...`；统一使用普通方法体。
- 所有 `if` 必须写成带 `{}` 的形式。
- 新建对象必须使用 Named Arguments，例如 `new User(name: name, id: userId)`。
- XML `<summary>` 如果涉及参数，必须为每个参数提供 `<param name="...">`。
- 类、类属性和类方法按仓库要求添加中文注释；不为显而易见的局部代码添加无意义注释。

## 路径

规则和样例中的旧项目路径不得继续扩散。涉及当前项目时统一使用当前仓库实际路径，例如 `src/SolGridFriend.Web/...`。
