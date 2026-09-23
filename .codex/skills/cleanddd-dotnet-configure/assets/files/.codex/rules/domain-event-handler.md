---
description: 领域事件与集成事件处理器开发规则
globs: src/SolGridFriend.Web/Application/{DomainEventHandlers,IntegrationEventHandlers}/**/*.cs
alwaysApply: false
---

# 事件处理器

## 文件放置

- 领域事件处理器放在 `src/SolGridFriend.Web/Application/DomainEventHandlers/`
- 集成事件处理器放在 `src/SolGridFriend.Web/Application/IntegrationEventHandlers/`
- 文件名应明确表达事件类型和处理目的

## 开发规则

- 领域事件处理器实现 `IDomainEventHandler<T>` 和 `Handle(TEvent domainEvent, CancellationToken cancellationToken)`
- 集成事件处理器实现 `IIntegrationEventHandler<T>` 和 `HandleAsync(TEvent eventData, CancellationToken cancellationToken)`
- 一个领域事件可以有多个处理器，但每个处理器只负责单一业务目的
- 通过发送 Command 协调其他聚合，不直接跨聚合改数据
- 事件处理器不得直接调用聚合或实体的增删改方法，也不得通过 Repository、`DbContext` 直接执行 `Add`、`Update`、`Delete`、`SaveChanges` 等数据库写操作
- 所有数据库状态变更必须由事件处理器通过 `IMediator.Send(...)` 调用对应 Command，并在 Command Handler 内加载模型、调用模型方法和持久化
- 事件处理器可以为流程判断执行只读查询；只读查询不得顺带修改被查询模型
- 依赖通过构造函数注入
- 优先保留日志，便于定位事件链路
