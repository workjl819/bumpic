---
name: testcontainer-integration-testing
description: 为 CleanDDD .NET 项目配置基于 Testcontainers 的 MySQL、Redis、RabbitMQ 集成测试基础设施
---

# Testcontainers 集成测试技能

## 使用时机

- 需要真实 MySQL、Redis、RabbitMQ 的集成测试
- 需要扩展或排查 `TestContainerFixture`、`MyWebApplicationFactory`、`EntityExtensions`

## 目标结构

- `Extensions/TestContainerFixture.cs`
- `Extensions/MyWebApplicationFactory.cs`
- `Extensions/EntityExtensions.cs`
- 配套 `Fixtures/` 与 `GlobalUsings.cs`

## 核心职责

- `TestContainerFixture`
  负责统一启动和停止 MySQL、Redis、RabbitMQ 容器
- `MyWebApplicationFactory`
  将容器连接参数注入 ASP.NET Core 配置，支持多实例并发测试
- `EntityExtensions`
  通过反射给测试实体设置强类型 ID

## 关键约束

- 多实例测试时使用独立数据库名避免相互污染
- RabbitMQ VirtualHost 需要显式准备
- 测试完成后统一释放容器
- 仅在确实需要真实中间件时使用该模式，避免把所有测试都变成慢测试
