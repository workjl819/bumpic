---
description: 单元测试与集成测试开发规则
globs: test/**/*.cs
alwaysApply: false
---

# 测试

## 文件放置

- 领域层测试：`test/SolGridFriend.Domain.Tests/`
- Web 层测试：`test/SolGridFriend.Web.Tests/`
- 基础设施层测试：`test/SolGridFriend.Infrastructure.Tests/`

## 开发规则

- 使用 AAA：Arrange、Act、Assert
- Web 层 Command、Query、Endpoint 测试必须使用独立的 `MyWebApplicationFactory` 派生测试工厂
- 每个 Command、Query、Endpoint 的 Test 类使用对应的专用测试工厂文件，通过 `IClassFixture<专用MyWebApplicationFactory>` 注入
- 专用测试工厂只配置当前测试对象所需的依赖和测试基础设施，不与其他测试对象共用专用工厂配置
- 优先使用真实依赖和项目现有测试容器验证行为，不以直接调用 Handler 或 Mock Endpoint 代替 Web 层测试
- 一个测试方法只覆盖一个清晰场景
- 命名建议：`{Method}_{Scenario}_{ExpectedBehavior}`
- 既覆盖成功路径，也覆盖异常路径
- 涉及领域行为时验证领域事件发布

## 常见关注点

- 聚合与实体：业务规则、状态变化、领域事件
- 命令处理器：业务异常、仓储交互、取消令牌传递
- 领域事件/集成事件处理器：副作用与下游调用
- Endpoint：状态码、响应契约、鉴权与参数校验

## 时间与强类型 ID

- 时间断言优先使用相对比较，避免脆弱的绝对时间
- 强类型 ID 直接比较对象本身，不做拆箱比较
