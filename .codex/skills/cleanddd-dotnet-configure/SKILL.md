---
name: cleanddd-dotnet-configure
description: 将本项目约定的 .NET 基础配置安装到新建的 netcorepal-web 解决方案，包括 Web 配置、包引用、测试基础设施和 Codex 规则。适用于 MySql + RabbitMQ、未启用 Aspire 的 net10.0 新项目。
---

# CleanDDD 项目基础配置

此技能面向刚由 `dotnet new netcorepal-web` 创建的空项目。生成逻辑固定在 `scripts/configure_project.py` 与 `assets/` 中，不逐文件手工生成。

当前资产以 `net10.0`、`MySql`、`RabbitMQ`、`UseAspire=false`、`IncludeCopilotInstructions=false` 的 NetCorePal 模板为基线。其他参数组合需要单独验证后才能支持。脚本会在写入前检查补丁和已有文件；模板版本不匹配时停止并报告。

运行：

```bash
python3 <skill-directory>/scripts/configure_project.py <solution-root>
dotnet build <solution-root>/<project-name>.slnx
```

脚本安装当前项目的通用配置，替换项目名前缀，并创建 Domain 的 `AggregateModel`、`DomainEvents` 目录以及 Web/Application 的 `Commands`、`DomainEventHandlers`、`IntegrationEventConverters`、`IntegrationEvents`、`IntegrationEventHandlers`、`Jobs`、`Queries`、`Vos` 目录。不会复制本项目的业务代码。已有配置或文件冲突时停止；不要强行覆盖。生成项目应能构建成功。若构建失败，先检查是否使用了上述模板参数及版本，再修复资产与模板之间的具体差异。
