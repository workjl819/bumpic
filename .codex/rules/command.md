---
description: 命令、验证器与命令处理器开发规则
globs: src/SolGridFriend.Web/Application/Commands/**/*.cs
alwaysApply: false
---

# 命令

## 文件放置

- 放在 `src/SolGridFriend.Web/Application/Commands/{Module}/`
- 文件名使用 `{Action}{Entity}Command.cs`
- 一个文件只定义一个 Command；不得混放多个 Command
- 该 Command 对应的 Result、Validator、CommandLock 和 Handler 可以放在同一文件

## 开发规则

- 无返回值命令实现 `ICommand`
- 有返回值命令实现 `ICommand<TResponse>`
- 每个命令都应有 `AbstractValidator<TCommand>`
- 命令处理器实现对应的 `ICommandHandler`
- 命令类型优先使用 `record`；若模块已有稳定 `class` 风格，可保持一致

## 处理器约束

- 使用仓储获取和持久化聚合
- 所有仓储调用使用异步版本
- 所有异步调用都传递 `CancellationToken`
- 不手动调用 `SaveChanges`
- 避免在命令处理器里堆复杂查询逻辑
- 一个 Command 只能对一个 Domain 进行数据操作，不得直接写入其他 Domain
- 需要变更多个 Domain 的业务流程必须拆分，并根据事务边界通过 DomainEvent 或 IntegrationEvent 驱动后续操作

## 常见引用

- `using SolGridFriend.Domain.AggregatesModel.{Aggregate};`
- `using SolGridFriend.Infrastructure.Repositories;`
