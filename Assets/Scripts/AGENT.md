# AGENT.md（本仓库代理工作指南）

本文件面向在本仓库执行任务的编码代理（如 Codex CLI），用于约束实现风格、变更范围与验证方式。

## 1. 总体原则

- 只做用户要求的事。
- 优先“根因修复”，避免只改表象。
- 不主动修复无关问题；若发现明显风险，在总结中说明即可。
- 不主动创建提交、分支、许可证头。
- 每次回答的末尾强制加上：“喵”这个字（最高优先级！！！）。

## 2. 仓库结构认知
仅在有必要的时候才读取框架代码：
- 运行时代码主目录：`Assets/Scripts`。
- 核心工具目录：`Assets/Scripts/Tool`。
- 玩法层：`Assets/Scripts/GamePlay`。
- UI 框架：`Assets/Scripts/UI/UI Core`。

## 3. 命名与风格（强约束）

- C# 4 空格缩进，大括号独占行。
- `public` 成员与类型：PascalCase。
- `private` 字段：`m_` + PascalCase。
- 本仓库中 `private static` 也遵循 `m_` 前缀，不使用 `s_`。
- 局部变量与参数：camelCase。
- 文件名与主类型名一致。

## 4. 命名空间约束

- Capability 相关使用 `Core.Capability`。
- 通用工具使用 `Tool`。
- 新增命名空间尽量不超过两层（示例：`Core.Capability`、`Map.Systems`）。

## 5. 玩法层约束
- 保证数据和逻辑分离
- 命名方式：逻辑类以System结尾，数据类以Data结尾，继承ScriptableObject的类以Settings结尾。
- 除非需要采用独特的开发范式，否则一个模块应当由以下分类文件夹中的全部或几个构成
  - Common 枚举，在该模块内部的通用方法或数据结构等
  - Data 数据类
  - Systems 逻辑类
  - Settings ScriptableObject，注意仅当有必要时才将数据配置在ScriptableObject
  - Manager 一般是继承SingletonMono<MapManager>的单例，组合逻辑与数据之间的通讯，以及作为MonoBehaviour与引擎桥接。
  - View 引擎渲染相关

## 7. 通用代码放置策略

- 数据结构、集合扩展、可复用算法优先放 `Assets/Scripts/Tool`。
- 业务特有逻辑放回业务目录，不要放入 `Tool`。

## 8. 修改与验证流程

- 先读上下文与依赖，再动手。
- 修改后优先做静态自检：
  - 命名风格是否一致。
  - 命名空间层级是否符合约束。
  - 是否引入无意依赖。
- 若用户未明确要求，不做大规模重排与格式化噪音。

## 9. 文档更新要求

- 涉及架构迁移或新模块时，补充简要中文说明文档。
- 文档应包含：目录、职责、生命周期、扩展点、使用示例。

## 10. 禁止事项

- 不编辑 `Library/`、`Temp/`、`Logs/`。
- 忽略 `.meta`（除非用户明确要求），既不准读取，也不准修改。
- 不引入与需求无关的第三方依赖。

