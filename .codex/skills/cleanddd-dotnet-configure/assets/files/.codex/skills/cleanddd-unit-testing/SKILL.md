---
name: cleanddd-unit-testing
description: 为 CleanDDD .NET 项目编写和维护 Command、Command Validator、FastEndpoints Endpoint 与 Query Handler 测试。Command、Endpoint、Query 测试默认集成项目已有的 MyWebApplicationFactory，通过专用测试工厂、IClassFixture、真实依赖和测试容器验证行为；技能自包含且不依赖参考测试类。
---

# CleanDDD Command、Endpoint、Query 测试

## 目标

生成符合目标项目现有结构和命名风格的可运行测试。关键产出是完整的“专用测试应用工厂 + Fixture 测试类”结构。不要依赖指定参考测试类、固定目录、固定命名空间或业务测试辅助类。

## 执行流程

1. 使用 `rg --files` 定位解决方案、测试项目、生产代码和现有测试基础设施。
2. 读取被测类型及直接依赖，提炼输入、输出、副作用、异常、鉴权、过滤、排序、分页和校验规则。
3. 使用 `rg -n "class MyWebApplicationFactory"` 定位工厂定义，完整读取其继承关系、抽象成员、服务替换点、数据库初始化和生命周期。
4. 检查目标项目使用的 .NET、xUnit、FluentValidation、FastEndpoints、EF Core 和 WebApplicationFactory 版本，不擅自升级或新增替代框架。
5. 为每组 Command、Endpoint 或 Query 测试声明专用的 `*TestFactory : MyWebApplicationFactory`，并通过 `IClassFixture<TFactory>` 注入。
6. 按目标项目的目录、命名空间、断言和 Fixture 风格编写最少但完整的场景。
7. 先运行目标测试，再运行测试项目；失败时修复根因，不放宽正确断言。

## 通用约束

- 测试可观察行为，不复制生产实现。
- 每个测试只表达一个清晰场景，按 Arrange、Act、Assert 组织。
- 必须复用目标项目已有的 `MyWebApplicationFactory`。不得重新实现容器启动、连接字符串注入或宿主启动逻辑。
- 每组 Command、Endpoint 或 Query 测试创建一个语义明确的专用工厂；只在该工厂中放置本组测试共享的服务替换和数据初始化。
- 优先复用目标项目已有的认证方法和数据构造器；若不存在，只创建当前测试必需的最小辅助代码。
- 所有异步调用传递测试框架提供或测试创建的 `CancellationToken`。
- 测试相互独立，不依赖执行顺序、共享可变数据、系统当前时间或数据库默认顺序。
- 按目标项目约定添加注释；若项目没有约定，类、类属性和类方法添加中文 XML 注释。
- C# 类属性保持 non-nullable，必要时赋明确默认值。
- 不为测试方便修改生产代码，除非公开行为无法验证且用户已授权。

## 强制测试类结构

Command、Endpoint、Query 使用以下结构：

```csharp
/// <summary>
/// 被测功能的测试应用工厂。
/// </summary>
public class SampleTestFactory : MyWebApplicationFactory
{
    // 仅实现目标项目 MyWebApplicationFactory 要求的抽象成员。
    // 仅在本组测试确有需要时替换服务或初始化共享数据。
}

/// <summary>
/// 被测功能的集成测试。
/// </summary>
public class SampleTests(SampleTestFactory factory)
    : IClassFixture<SampleTestFactory>
{
    // 测试方法通过 factory.Services 创建作用域，或通过 factory 创建 HttpClient。
}
```

- 工厂类与测试类放在同一测试文件，除非目标项目明确采用集中式工厂。
- 工厂命名使用 `<Subject>TestFactory`，测试类使用 `<Subject>Tests`。
- 工厂类保持 `public` 且可由 xUnit 构造，不使用静态工厂或在无关测试类之间共享可变状态。
- 若 `MyWebApplicationFactory` 有 `SetupMockService`、`InitializeServiceAsync` 等抽象成员，严格按其真实签名实现；无配置时保留最小空实现。
- 不猜测工厂 API。先读定义，再决定使用 `Services`、`CreateClient()`、自定义认证方法或初始化入口。
- Command 和 Query 直接调用 Handler，但 Handler 依赖必须从工厂宿主或其作用域获得。
- CommandHandler 只能通过 Repository 查询和保存聚合，不得注入或直接查询 `ApplicationDbContext`；测试中的 DbContext 仅用于准备数据和断言持久化或跟踪结果。
- QueryHandler 直接使用 `ApplicationDbContext` 查询读模型，不通过 Repository 绕行。
- Endpoint 通过工厂创建 `HttpClient`，走真实 HTTP 管道。
- Validator 不需要工厂，保持独立纯测试类。

## Command Handler

- 声明 `<CommandName>HandlerTestFactory : MyWebApplicationFactory`。
- 测试类实现 `IClassFixture<TFactory>`。若长期持有 `IServiceScope`，同时实现 `IAsyncLifetime` 并在 `DisposeAsync` 中释放。
- 从 `factory.Services.CreateScope()` 获取真实 Repository 并注入 Handler；不要把 `ApplicationDbContext` 注入 CommandHandler。
- 需要准备前置数据或断言数据库状态时，可从同一作用域获取 `ApplicationDbContext`，但它不是 CommandHandler 的依赖。
- 不要绕过工厂自行创建 Repository 或 DbContextOptions。
- 成功场景验证返回值和业务副作用。
- 仓储型测试验证实体数量、字段映射、强类型 ID、领域状态和 EF 跟踪状态。
- 批量处理验证每个输入生成独立结果，并覆盖空集合、重复项或顺序规则中实际存在的约束。
- 按生产契约覆盖目标不存在、无权限和业务规则异常。
- 遵循 CleanDDD 事务管道；不要强制 Handler 显式调用 `SaveChangesAsync`。

最小结构：

```csharp
/// <summary>
/// 示例命令处理器测试工厂。
/// </summary>
public class SampleCommandHandlerTestFactory : MyWebApplicationFactory
{
}

/// <summary>
/// 示例命令处理器集成测试。
/// </summary>
public class SampleCommandHandlerTests(SampleCommandHandlerTestFactory factory)
    : IClassFixture<SampleCommandHandlerTestFactory>, IAsyncLifetime
{
    private IServiceScope _scope = null!;
    private ApplicationDbContext _db = null!;
    private ISampleRepository _repository = null!;

    /// <summary>
    /// 初始化测试作用域和数据库。
    /// </summary>
    public async ValueTask InitializeAsync()
    {
        _scope = factory.Services.CreateScope();
        _db = _scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        _repository = _scope.ServiceProvider.GetRequiredService<ISampleRepository>();
        await _db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// 释放测试作用域。
    /// </summary>
    public ValueTask DisposeAsync()
    {
        _scope.Dispose();
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// 验证命令产生预期业务结果。
    /// </summary>
    [Fact]
    public async Task Handle_Should_Produce_Expected_Result()
    {
        var handler = new SampleCommandHandler(_repository);
        var command = new SampleCommand(/* 合法输入 */);

        await handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.Equal(/* 期望值 */, _db.Set<ExpectedEntity>().Local.Count);
    }
}
```

将示例类型和 token 来源替换为目标项目实际类型。若 CommandHandler 需要多个依赖，Repository 与其他服务都从工厂作用域解析，但禁止用 DbContext 代替 Repository。若每个测试应使用独立 scope，不实现 `IAsyncLifetime`，改为在测试方法内 `using var scope = factory.Services.CreateScope()`。

## Command Validator

- 使用 `FluentValidation.TestHelper` 直接测试 Validator。
- 至少包含一个完整合法输入，以及每条关键规则的非法输入。
- 用 `ShouldHaveValidationErrorFor` 指向具体属性；合法输入使用 `ShouldNotHaveAnyValidationErrors`。
- 集合验证同时覆盖空集合和非法元素；长度、数量和数值范围覆盖边界值。
- Validator 与 Handler 分开测试，不通过 Handler 间接验证校验规则。

```csharp
[Fact]
public void Validate_Should_Reject_Invalid_Value()
{
    var validator = new SampleCommandValidator();
    var command = new SampleCommand(/* 非法输入 */);

    var result = validator.TestValidate(command);

    result.ShouldHaveValidationErrorFor(x => x.Property);
}
```

## Endpoint

- 声明 `<EndpointName>TestFactory : MyWebApplicationFactory`，并让测试类通过 `IClassFixture<TFactory>` 接收工厂。
- 使用该专用工厂创建 `HttpClient`。
- 初始化最少的真实测试数据，并通过目标项目的认证机制创建已认证客户端。
- 调用真实版本化路由和真实 HTTP 方法，验证状态码及关键响应字段。
- 按契约覆盖未认证、无权限、校验失败、资源不存在或冲突，不机械添加不存在的语义。
- 共享容器或数据库时使用目标项目已有的 xUnit Collection；若没有且测试会并发冲突，再创建最小 Collection。
- 端到端契约测试不要 Mock Mediator，否则会绕过需要验证的管道。

```csharp
/// <summary>
/// 示例 Endpoint 测试工厂。
/// </summary>
public class SampleEndpointTestFactory : MyWebApplicationFactory
{
    /// <summary>
    /// 初始化 Endpoint 所需的真实测试数据。
    /// </summary>
    public async Task<TestData> InitDataAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        // 创建并保存当前 Endpoint 所需的最少数据。
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return new TestData(/* 返回认证和请求所需标识 */);
    }
}

/// <summary>
/// 示例 Endpoint 集成测试。
/// </summary>
public class SampleEndpointTests(SampleEndpointTestFactory factory)
    : IClassFixture<SampleEndpointTestFactory>
{
    /// <summary>
    /// 验证真实 HTTP 管道返回预期响应。
    /// </summary>
    [Fact]
    public async Task Should_Return_Expected_Response()
    {
        var data = await factory.InitDataAsync();
        var client = factory.CreateClient(); // 按项目认证方式替换
        var request = new SampleRequest { /* 使用 data 构造输入 */ };

        var response = await client.PostAsJsonAsync(
            "/api/v1/resource/action",
            request,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<SampleResponse>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        Assert.Equal(/* 期望值 */, body.Property);
    }
}
```

## Query Handler

- 声明 `<QueryFeature>TestFactory : MyWebApplicationFactory`，并通过 `IClassFixture<TFactory>` 注入。
- 使用工厂的服务提供器创建作用域，获取真实 `ApplicationDbContext`，保存测试数据并实例化 Handler。
- 查询依赖 LINQ 翻译、Include、数据库排序、日期范围或分页时，使用目标项目的真实测试数据库和 `DbContext`，直接实例化 Handler。
- 测试数据同时包含应命中和应排除的数据，分别断言包含与排除。
- 主排序字段可能相同时，构造并列数据，验证按确定性次排序字段稳定排序。
- 分页覆盖第一页、中间页、末页和空页，并验证跨页无重复、无遗漏。
- 返回分页总数时，同时验证 `Total` 和 `Items`。
- 日期查询使用固定时区和固定边界，覆盖开始包含、结束排除及默认日期范围。
- 请求 DTO 有分页默认值时，单独验证默认页码和每页数量。
- 仅在构造测试状态确有必要时，用集中封装的辅助方法设置非公开属性或清除无关领域事件。

最小结构与稳定分页断言模式：

```csharp
/// <summary>
/// 示例查询测试工厂。
/// </summary>
public class SampleQueryTestFactory : MyWebApplicationFactory
{
}

/// <summary>
/// 示例查询集成测试。
/// </summary>
public class SampleQueryTests(SampleQueryTestFactory factory)
    : IClassFixture<SampleQueryTestFactory>
{
    /// <summary>
    /// 验证查询稳定分页且无重复遗漏。
    /// </summary>
    [Fact]
    public async Task Handle_Should_Page_Without_Duplicates_Or_Omissions()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        // 创建包含命中、排除和并列排序值的测试数据。
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var handler = new SampleQueryHandler(db);
        var pages = new List<PagedData<ItemDto>>();
        for (var page = 1; page <= 4; page++)
        {
            pages.Add(await handler.Handle(
                new SampleQuery(page, 2),
                TestContext.Current.CancellationToken));
        }

        Assert.Equal(2, pages[0].Items.Count());
        Assert.Equal(2, pages[1].Items.Count());
        Assert.Single(pages[2].Items);
        Assert.Empty(pages[3].Items);
        Assert.Equal(expectedIds, pages.Take(3).SelectMany(x => x.Items).Select(x => x.Id));
    }
}
```

根据目标项目的分页 DTO 类型、集合类型和 token 来源替换示例占位类型。

## 场景矩阵

| 类型 | 必选 | 按真实契约选择 |
| --- | --- | --- |
| Command Handler | 成功结果、关键副作用 | 不存在、权限、业务异常、批量边界 |
| Command Validator | 合法输入、每条关键非法规则 | 集合元素、长度、范围边界 |
| Endpoint | 成功状态、关键响应 | 401、403、校验失败、404、409 |
| Query | 命中、排除、排序 | 稳定次序、分页边界、日期边界、默认值 |

## 命名与组织

- 测试文件跟随目标项目已有布局；没有先例时，按生产功能目录镜像组织。
- 每个 Command、Endpoint、Query 测试文件按“工厂在前、测试类在后”组织。
- 类名使用 `<Subject>Tests`，工厂使用 `<Subject>TestFactory`；二者使用相同 Subject 前缀。
- 测试方法优先使用 `Method_Should_Result_When_Condition`；若项目已有风格，保持一致。
- 只在至少两个测试复用时提取 `Create...`、`Build...`、`SaveAsync` 等辅助方法。
- 不引入新的断言库或测试框架。

## 验证

先发现测试项目，不假设名称或路径：

```bash
rg --files -g '*Tests.csproj' -g '*Test.csproj'
```

运行目标测试类：

```bash
dotnet test <测试项目路径> --filter FullyQualifiedName~<目标测试类名>
```

目标测试通过后运行整个测试项目：

```bash
dotnet test <测试项目路径>
```

若依赖 Testcontainers，先确认 Docker 可用。区分断言失败、生产缺陷、容器故障和数据污染，不用重试或降低断言掩盖不稳定测试。
