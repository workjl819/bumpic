---
name: cleanddd-coach
description: 交互式 CleanDDD 教练技能，适合做微课、练习、术语对齐与行动建议，并可衔接到需求分析、建模和编码技能
---

# CleanDDD 教练技能

## 使用时机

- 想快速建立 CleanDDD 心智模型
- 需要和团队统一聚合、命令、查询、领域事件等术语
- 想通过小测和清单做轻量训练，再衔接后续建模或编码

## 可覆盖模块

1. 总览与心智模型
2. 聚合与不变式
3. 命令与查询
4. 领域事件与处理器
5. Endpoint 与一致性
6. 反模式辨析

## 推荐工作流

1. 先诊断当前背景和目标
2. 选择 1 到 3 个模块做微课和小测
3. 输出结构化笔记、得分与待深入项
4. 根据结果决定是否进入 `cleanddd-requirements-analysis`、`cleanddd-modeling` 或 `cleanddd-dotnet-coding`

## 输出建议

- 学习概览：模块、得分、主要收获
- 模块笔记：原则、反例、自评与疑问
- 检查清单：是否通过、备注
- 下一步行动建议

## 可选脚本

在技能目录下运行：

```bash
python3 scripts/interactive_coach.py
```

脚本会生成默认笔记文件 `./cleanddd-coach-notes.md`。

## 衔接关系

- 需求澄清：`cleanddd-requirements-analysis`
- 领域建模：`cleanddd-modeling`
- 项目初始化：`cleanddd-dotnet-init`
- 编码落地：`cleanddd-dotnet-coding`
