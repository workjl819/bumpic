---
description: 集成事件开发规则
globs: src/SolGridFriend.Web/Application/IntegrationEvents/**/*.cs
alwaysApply: false
---

# 集成事件

## 文件放置

- 放在 `src/SolGridFriend.Web/Application/IntegrationEvents/`
- 文件名使用 `{Entity}{Action}IntegrationEvent.cs`

## 开发规则

- 使用 `record`
- 名称使用过去式
- 不同事务的 Domain 之间通过 IntegrationEvent 驱动后续数据操作
- 仅包含跨事务或跨服务通信所需的关键数据
- 避免带入敏感或过细的内部实现细节
- 如需复杂属性类型，可在同文件中定义附属 `record`
- 事件对象保持不可变
