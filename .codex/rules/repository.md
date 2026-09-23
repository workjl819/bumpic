---
description: 仓储接口与实现开发规则
globs: src/SolGridFriend.Infrastructure/Repositories/*.cs
alwaysApply: false
---

# 仓储

## 文件放置

- 放在 `src/SolGridFriend.Infrastructure/Repositories/`
- 文件名使用 `{Aggregate}Repository.cs`
- 接口与实现放在同一文件

## 开发规则

- 一个聚合根对应一个仓储
- 仓储接口优先继承 `IRepository<TEntity, TKey>`
- 实现优先继承 `RepositoryBase<TEntity, TKey, ApplicationDbContext>`
- 基类已有常用方法时，不重复定义通用 CRUD

## 使用约束

- 通过构造函数参数访问 `ApplicationDbContext`
- 优先暴露体现业务意图的方法，而不是泛化数据访问方法
- 仓储方法用于命令处理器的业务操作，不承担展示型查询职责
- 所有数据库 IO 使用异步版本
