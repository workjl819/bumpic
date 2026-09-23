---
name: netcorepal-swagger-configration
description: 维护 NetCorePal + FastEndpoints Swagger 配置的技能，适用于调整 Program.cs 中的 Swagger 文档与 UI 路径
---

# NetCorePal Swagger 配置技能

## 使用时机

- 修改 `SupeRISEMarketServer/src/SupeRISEMarketServer.Web/Program.cs` 中的 Swagger 配置
- 需要统一 OpenAPI 文档路径、UI 路径和 Tag 分组方式

## 关键约定

- 使用 `builder.Services.SwaggerDocument(...)` 注册文档
- `DocumentSettings.Version` 需与 Swagger 文档访问路径匹配
- `AutoTagPathSegmentIndex = 0` 用于按首段路径自动分组
- `UseSwaggerGen(...)` 统一配置 UI 与 JSON/YAML 路径
- 路径前缀优先复用 `appOptions.DashBoardPathPrefix`

## 当前项目路径模式

- UI：`/{DashBoardPathPrefix}/swagger`
- 文档：`/{DashBoardPathPrefix}/swagger/{documentName}/swagger.{json|yaml}`

## 修改原则

- 改版本号时同步改文档路径约定
- 改分组策略时评估现有 Tag 是否受影响
- 优先复用当前项目已有模板，不单独发明一套 Swagger 配置
