---
description: 领域事件开发规则
globs: src/SolGridFriend.Domain/DomainEvents/*.cs
alwaysApply: false
---

# 领域事件

## 文件放置

- 放在 `src/SolGridFriend.Domain/DomainEvents/`
- 文件名使用 `{Aggregate}DomainEvents.cs`
- 同一聚合的多个领域事件可放在同一文件

## 开发规则

- 使用 `record`
- 实现 `IDomainEvent`
- 事件名使用过去式：`{Entity}{Action}DomainEvent`
- 如果没有额外载荷需求，直接把聚合作为构造参数
- 领域事件只表达“已经发生了什么”，不承载业务逻辑
- 同一业务流程需要在同一事务内驱动其他 Domain 进行数据操作时，使用 DomainEvent
