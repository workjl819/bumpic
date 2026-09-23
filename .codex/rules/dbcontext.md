---
description: ApplicationDbContext 聚合暴露规则
globs: src/SolGridFriend.Infrastructure/ApplicationDbContext.cs
alwaysApply: false
---

# DbContext

## 开发规则

- 新增聚合后，在 `src/SolGridFriend.Infrastructure/ApplicationDbContext.cs` 中补对应 `DbSet`
- 头部补充聚合命名空间引用
- 使用 `public DbSet<TEntity> Entities => Set<TEntity>();`
- 继续依赖 `ApplyConfigurationsFromAssembly` 自动注册实体配置
