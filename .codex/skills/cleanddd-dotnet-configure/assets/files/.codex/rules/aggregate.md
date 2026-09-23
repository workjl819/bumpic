---
description: 聚合根、实体与强类型 ID 开发规则
globs: src/SolGridFriend.Domain/AggregatesModel/**/*.cs
alwaysApply: false
---

# 聚合与强类型 ID

## 文件放置

- 放在 `src/SolGridFriend.Domain/AggregatesModel/{AggregateName}Aggregate/`
- 聚合根类名与文件名保持一致
- 强类型 ID 与聚合根定义在同一文件中
- 每个聚合根或从属 Entity 使用独立文件，不在一个文件中混放多个 Entity
- 从属 Entity 的强类型 ID 可与该 Entity 定义在同一文件中

## 强类型 ID

- 使用 `IInt64StronglyTypedId` 或 `IGuidStronglyTypedId`
- 使用 `public partial record {EntityName}Id`
- 尽量优先 `IGuidStronglyTypedId`

## 聚合根

- 继承 `Entity<TId>` 并实现 `IAggregateRoot`
- 提供 `protected` 无参构造器供 EF Core 使用
- 属性使用 `private set`，并显式给默认值
- 不手动为 ID 赋值
- 状态改变时使用 `this.AddDomainEvent(...)`
- 需要并发控制时保留 `RowVersion`

## 子实体

- 使用 `public` 类
- 继承 `Entity<TId>` 并实现 `IEntity`
- 提供无参构造器
- 使用独立强类型 ID

## 常见提醒

- 聚合内只应有一个聚合根
- 聚合间不要直接引用彼此实体
- 缺少领域事件类型时，先检查是否引用了 `SolGridFriend.Domain.DomainEvents`

## 属性
- 除主键 ID 外，每个 Entity 都包含 `RowVersion`、`CreatedAt`、`UpdatedAt`、`CreatedBy`、`UpdatedBy`、`Deleted`
- `CreatedBy`、`UpdatedBy` 使用非空字符串，未指定时默认 `string.Empty`，数据库字段不保存 `NULL`
