---
description: 查询、DTO 与查询处理器开发规则
globs: src/SolGridFriend.Web/Application/Queries/**/*.cs
alwaysApply: false
---

# 查询

## 文件放置

- 放在 `src/SolGridFriend.Web/Application/Queries/{Module}/`
- 文件名使用 `{Action}{Entity}Query.cs`
- 一个文件只包含一个 Query 及其 Handler；不得混放多个 Query
- 查询的验证器，以及仅供该查询使用的 Result、DTO、VO，可放在同一文件

## 开发规则

- 查询实现 `IQuery<TResponse>`
- 为查询提供 `AbstractValidator<TQuery>`
- 查询处理器实现 `IQueryHandler<TQuery, TResponse>`
- 查询与 DTO 优先使用 `record`
- 查询处理器直接使用 `ApplicationDbContext`

## 查询约束

- 查询只读，不修改状态
- 不通过仓储做展示型读取
- 优先使用投影、过滤、排序、分页优化查询
- 动态排序要保留默认排序，保证结果稳定
- EF Core 异步方法要传递 `CancellationToken`

## 常见引用

- `using SolGridFriend.Infrastructure;`
- `using Microsoft.EntityFrameworkCore;`
