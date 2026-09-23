---
name: cleanddd-dotnet-coding
description: 在 CleanDDD .NET 项目中落地聚合、命令、查询、Endpoint、事件、仓储、实体配置与测试的编码技能
---

# CleanDDD .NET 编码技能

## 使用时机

- 已经有需求分析或建模结果，需要开始写代码
- 需要新增或修改聚合、命令、查询、Endpoint、事件、仓储、配置、测试

## 前置输入

- 优先先有 `cleanddd-modeling` 的产出
- 至少明确聚合边界、不变式、命令/查询职责、事件流向

## 核心原则

- 命令处理器不显式 `SaveChanges`
- 查询直接访问 `ApplicationDbContext`
- 跨聚合影响使用领域事件或集成事件
- 强类型 ID 依赖 EF 值生成器，不手动赋值
- FastEndpoints 使用特性配置，优先通过 `IMediator` 发送命令或查询
- 所有 IO 使用异步方法并传递 `CancellationToken`

## 推荐实施顺序

1. 聚合与实体
2. 领域事件
3. 仓储与实体配置
4. 命令、验证器、处理器
5. 查询、验证器、处理器
6. Endpoint
7. 领域事件处理器
8. 集成事件、转换器、处理器
9. 测试

## 交付要求

- 命名使用 PascalCase
- 事件名使用过去式
- C# XML 文档注释中的 `<summary>` 必须使用三行格式：起始标签、中文说明、结束标签各占一行
- 优先保持单文件聚合相关类型的组织方式
- 遵循仓库中的 `.codex/rules/*.md`
