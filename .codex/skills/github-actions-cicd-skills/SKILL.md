---
name: github-actions-cicd-skills
description: 维护本仓库 GitHub Actions 工作流时使用的技能，覆盖 PR 测试和 Docker 构建部署流水线
---

# GitHub Actions CI/CD 技能

## 使用时机

- 修改 `.github/workflows/pr-test.yml`
- 修改 `.github/workflows/docker-images.yml`
- 需要补充、重构或排查 CI/CD 流水线

## 工作流约定

### `pr-test.yml`

- 触发：所有分支的 Pull Request
- 目的：只做构建与测试，不做部署
- 运行环境：`ubuntu-latest`
- .NET 版本：`9.0.x`
- 基本步骤：`checkout -> setup-dotnet -> restore -> build -> test`

### `docker-images.yml`

- 触发：
  - `develop` 分支 PR 合并
  - 任意 Tag push
- Job 顺序：`test -> build-and-push -> deploy-staging`
- `build-and-push` 在 PR 场景只在真正 merge 后执行
- Docker 镜像使用 `latest`、`github.sha`、`github.ref_name` 三个 tag
- 部署通过 Kuboard API 更新 Kubernetes Deployment 镜像

## 修改原则

- 先保留现有触发条件和阶段依赖
- 不把测试流水线和部署流水线混在一起
- Secrets、AWS 区域、ECR 仓库、Kuboard 参数保持集中配置
- 如改动镜像 tag 规则，需同步检查部署步骤
