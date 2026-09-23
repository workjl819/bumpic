# Codex Repo Guide

本仓库的详细规范位于根目录 `AGENTS.md`、`docs/` 和 `.codex/` 下。根目录 `AGENTS.md` 是强制入口；本文件只补充 Codex 规则文件的加载方式。

## Always Apply

- 始终遵循 `.codex/rules/project-conventions.md`
- 始终遵循根目录 `AGENTS.md`、`.codex/rules/development-rules.md` 和 `.codex/rules/testing-rules.md`
- 生成或修改 PRD 时，遵循 `.codex/rules/prd.md`
- Query 默认不要使用联表查询，优先使用“主表查询 + 多次补充查询”的方式组装读模型
- 如果正在修改某一类文件，再补充读取对应的规则文件
- 若现有模块已经形成稳定实现风格，优先保持局部一致性，再在不冲突处遵循规则

## Context Rules

- 聚合与实体：`.codex/rules/aggregate.md`
- 领域事件：`.codex/rules/domain-event.md`
- 领域事件处理器：`.codex/rules/domain-event-handler.md`
- 命令：`.codex/rules/command.md`
- 查询：`.codex/rules/query.md`
- Endpoint：`.codex/rules/endpoint.md`
- 仓储：`.codex/rules/repository.md`
- 实体配置：`.codex/rules/entity-configuration.md`
- DbContext：`.codex/rules/dbcontext.md`
- 集成事件：`.codex/rules/integration-event.md`
- 集成事件转换器：`.codex/rules/integration-event-converter.md`
- 集成事件处理器：`.codex/rules/integration-event-handler.md`
- Swagger：`.codex/rules/swagger.md`
- 测试：`.codex/rules/unit-testing.md`
- PRD：`.codex/rules/prd.md`

## Skills

- 项目级技能位于 `.codex/skills/*/SKILL.md`
- 当任务明显匹配某个技能时，优先加载该技能后再继续工作
- `.github/instructions` 与 `.github/skills` 视为历史来源，`.codex/` 为整理后的 Codex 版本
