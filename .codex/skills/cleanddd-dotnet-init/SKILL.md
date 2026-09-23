---
name: cleanddd-dotnet-init
description: 初始化 CleanDDD dotnet 项目，适合从零创建 netcorepal-web 模板工程或演示环境
---

# CleanDDD dotnet 初始化技能

## 使用时机

- 从零新建 CleanDDD 解决方案
- 需要快速拉起一个可演示的模板工程

## 关键参数

- `Framework`: `net8.0` / `net9.0` / `net10.0`
- `Database`: `MySql` / `SqlServer` / `PostgreSQL` / `Sqlite` / `GaussDB` / `DMDB` / `MongoDB`
- `MessageQueue`: `RabbitMQ` / `Kafka` / `AzureServiceBus` / `AmazonSQS` / `NATS` / `RedisStreams` / `Pulsar`
- `UseAspire`: `true` / `false`
- `IncludeCopilotInstructions`: `true` / `false`
- `ProjectName`
- `OutputDir`

## 执行要求

- 运行前先向用户汇总参数
- 获得确认后再执行 `dotnet new`
- 如有需要，先安装 `NetCorePal.Template`

## 示例命令

```bash
dotnet new install NetCorePal.Template

dotnet new netcorepal-web \
  --Framework net10.0 \
  --Database MySql \
  --MessageQueue RabbitMQ \
  --UseAspire true \
  --IncludeCopilotInstructions false \
  --name My.Project \
  --output /path/to/target
```

## 可选脚本

在技能目录下运行：

```bash
python3 scripts/interactive_init.py
```

脚本会校验参数、展示命令预览，并在确认后执行。
