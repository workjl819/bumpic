---
description: FastEndpoints Swagger 配置规则
globs: src/SolGridFriend.Web/Program.cs
alwaysApply: false
---

# Swagger

## 适用范围

- 适用于 `src/SolGridFriend.Web/Program.cs` 中的 FastEndpoints Swagger 配置

## 开发规则

- 使用 `builder.Services.SwaggerDocument(...)` 统一注册 OpenAPI 文档
- `DocumentSettings.Version` 与 Swagger 文档访问路径保持一致
- 通过 `AutoTagPathSegmentIndex` 统一接口分组策略
- 在 `UseSwaggerGen(...)` 中统一配置 UI 路径与 JSON/YAML 文档路径
- Swagger 路径前缀优先复用 `AppOptions.DashBoardPathPrefix`

## 当前项目约定

- 文档标题包含应用名与环境标识
- Swagger UI 路径应落在 `/{DashBoardPathPrefix}/swagger`
- 文档路径应落在 `/{DashBoardPathPrefix}/swagger/{documentName}/swagger.{json|yaml}`
