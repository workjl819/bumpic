# 测试规则

本文档是根目录 `AGENTS.md` 的详细测试规则。

## 测试文件隔离

每个 Command、Query、Endpoint 必须有独立的 Test 类文件：

- `CreateOrderCommandTests.cs`
- `GetOrderQueryTests.cs`
- `CreateOrderEndpointTests.cs`

禁止使用一个通用测试类同时覆盖多个 Command、Query 或 Endpoint。

## MyWebApplicationFactory 独立测试工厂

- Web 层的 Command、Query、Endpoint 测试必须基于项目现有的 `MyWebApplicationFactory` 测试基础设施。
- 每个 Command、Query、Endpoint 的 Test 类必须使用自己的专用 `MyWebApplicationFactory` 派生测试工厂文件；不得直接使用默认 `WebApplicationFactory`。
- 专用工厂只配置当前测试对象所需的依赖、数据库、消息队列、缓存和测试替身，不得把多个测试对象的配置合并到同一个工厂中。
- 测试类通过 `IClassFixture<专用MyWebApplicationFactory>` 使用工厂，并通过真实依赖验证行为；不得仅通过 Mock Controller 或直接调用 Handler 代替 Web 层集成测试。
- 如果项目已有可复用的基础 `MyWebApplicationFactory`，专用工厂应继承它，只覆盖当前测试所需配置。

推荐命名：

- `CreateOrderCommandTestWebApplicationFactory.cs`
- `GetOrderQueryTestWebApplicationFactory.cs`
- `CreateOrderEndpointTestWebApplicationFactory.cs`

## 测试内容

- 使用 Arrange、Act、Assert 结构。
- 每个测试方法只验证一个清晰场景。
- 覆盖成功路径、参数验证失败路径和关键异常路径。
- Command 测试验证业务异常、仓储交互和 `CancellationToken` 传递。
- Query 测试验证查询结果、过滤、分页和只读行为。
- Endpoint 测试验证路由、授权、状态码、响应契约和参数校验。
- 聚合行为测试验证状态变化及领域事件发布。
- 强类型 ID 直接比较对象本身，不解包 `.Value` 进行断言。

## 完成前检查

- [ ] 每个新增或修改的 Command 都有 Validator 和独立 Test 类文件。
- [ ] 每个新增或修改的 Query 都有 Validator 和独立 Test 类文件。
- [ ] 每个新增或修改的 Endpoint 都有独立 Test 类文件。
- [ ] 每个 Web 层 Test 类都使用对应的独立 `MyWebApplicationFactory` 派生工厂。
- [ ] 相关测试已运行并通过。
- [ ] 测试没有通过共享测试类隐藏多个功能对象的覆盖关系。
