---
description: 领域事件到集成事件的转换规则
globs: src/SolGridFriend.Web/Application/IntegrationEventConverters/**/*.cs
alwaysApply: false
---

# 集成事件转换器

## 文件放置

- 放在 `src/SolGridFriend.Web/Application/IntegrationEventConverters/`
- 文件名使用 `{Entity}{Action}IntegrationEventConverter.cs`

## 开发规则

- 实现 `IIntegrationEventConverter<TDomainEvent, TIntegrationEvent>`
- 只负责把领域事件映射成集成事件
- 不把业务逻辑塞进转换器
- 框架负责自动注册时，保持类型清晰、依赖最小
