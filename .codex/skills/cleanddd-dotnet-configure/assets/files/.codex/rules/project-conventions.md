---
description: SolGridFriend 项目核心开发规范与目录约定
alwaysApply: true
---

# 项目总则

## 适用范围

- 适用于当前仓库 `src/` 与 `test/` 下所有领域层、基础设施层、Web 层与测试代码
- 适用于当前仓库 `docs/` 下的产品需求文档编写约定

## 基本原则

- 优先遵循本目录下与当前文件类型匹配的专项规则
- 生成 PRD 文档时，遵循 `.codex/rules/prd.md`
- 优先保持 `SolGridFriend` 现有模块的局部一致性
- 优先使用异步 API，并将 `CancellationToken` 传递到底层调用
- 使用 `KnownException` 表达已知业务错误
- 命令处理器不手动调用 `SaveChanges`
- 聚合根与实体负责发布领域事件，跨聚合影响通过事件处理器完成
- 一个 Command 只能对一个 Domain 进行数据操作
- 一个业务流程涉及多个 Domain 时，必须通过事件驱动：同一事务内的 Domain 使用 DomainEvent，不同事务的 Domain 使用 IntegrationEvent

## 推荐开发顺序

1. 定义聚合、实体和值对象
2. 定义领域事件
3. 创建仓储接口与实现
4. 编写实体配置与 DbContext 暴露
5. 编写命令、验证器与命令处理器
6. 编写查询、验证器与查询处理器
7. 编写 FastEndpoints Endpoint
8. 编写领域事件处理器
9. 编写集成事件、转换器与处理器
10. 补充单元测试或集成测试

## 项目结构

- `src/SolGridFriend.Domain/`
- `src/SolGridFriend.Infrastructure/`
- `src/SolGridFriend.Web/`
- `test/`

## 分层约束

- 依赖方向保持 `Web -> Infrastructure -> Domain`
- 查询直接读 `ApplicationDbContext`
- 命令通过仓储获取和持久化聚合
- 不在查询里调用仓储做展示型读取

## 根目录强制规则

强制开发与测试规则统一维护在根目录 `AGENTS.md`、`docs/development-rules.md` 和 `docs/testing-rules.md`，本文件不重复维护，避免多份规则发生漂移。
