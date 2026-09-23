---
description: FastEndpoints Endpoint 开发规则
globs: src/SolGridFriend.Web/Endpoints/**/*.cs
alwaysApply: false
---

# Endpoint

## 文件放置

- 放在 `src/SolGridFriend.Web/Endpoints/{Module}/`
- 文件名使用 `{Action}{Entity}Endpoint.cs`
- 请求类型、响应类型与 Endpoint 可放在同一文件
- 一个请求对应一个 Endpoint 类；同一文件不得包含多个 Endpoint 类

## 开发规则

- 继承合适的 `Endpoint` 基类
- 使用 `[HttpPost]`、`[HttpGet]`、`[AllowAnonymous]`、`[Tags]` 等特性配置
- 优先使用特性，而不是 `Configure()`
- 通过构造函数注入 `IMediator`
- 在 `HandleAsync()` 中组织业务流程
- 使用 `ResponseData<T>`、`.AsResponseData()` 或项目现有成功响应扩展统一返回

## 响应约定

- `Send.OkAsync()` 用于读取或常规成功
- `Send.CreatedAsync()` 用于创建资源
- `Send.NoContentAsync()` 用于无响应体的更新或删除

## 强类型 ID

- 请求和响应类型中直接使用强类型 ID
- 避免为了传输而手动拆 `.Value`
