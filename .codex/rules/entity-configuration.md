---
description: 实体配置开发规则
globs: src/SolGridFriend.Infrastructure/EntityConfigurations/*.cs
alwaysApply: false
---

# 实体配置

## 文件放置

- 放在 `src/SolGridFriend.Infrastructure/EntityConfigurations/`
- 文件名使用 `{EntityName}EntityTypeConfiguration.cs`
- 每个实体一个配置文件

## 开发规则

- 实现 `IEntityTypeConfiguration<T>`
- 主键显式使用 `HasKey(x => x.Id)`
- 字符串字段设置最大长度
- 必填字段使用 `IsRequired()`
- 根据查询需求添加索引
- 字段尽量使用 `HasComment()` 补充数据库注释
- `RowVersion` 通常无需单独配置

## 强类型 ID

- `IInt64StronglyTypedId` 使用 `UseSnowFlakeValueGenerator()`
- `IGuidStronglyTypedId` 使用 `UseGuidVersion7ValueGenerator()`
- 不使用 `HasConversion<{Id}.EfCoreValueConverter>()`

## 常见引用

- `using Microsoft.EntityFrameworkCore;`
- `using Microsoft.EntityFrameworkCore.Metadata.Builders;`
