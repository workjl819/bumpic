---
description: 集成事件处理器开发规则
globs: src/SolGridFriend.Web/Application/IntegrationEventHandlers/**/*.cs
alwaysApply: false
---

# 集成事件处理器

## 文件放置

- 放在 `src/SolGridFriend.Web/Application/IntegrationEventHandlers/`
- 文件名使用 `{IntegrationEvent}HandlerFor{Action}.cs`

## 开发规则

- 实现 `IIntegrationEventHandler<T>`
- 通过 `HandleAsync(TEvent integrationEvent, CancellationToken cancellationToken)` 处理消息
- 主要负责跨服务同步、编排和副作用处理
- 通过发送 Command 操作聚合，不直接跨层修改数据
- 依赖通过构造函数注入
