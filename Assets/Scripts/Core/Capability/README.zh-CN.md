# Core.Capability 迁移框架说明（中文）

本文档说明 `Assets/Scripts/Core/Capability` 下的 Capability 框架迁移结果、设计意图与使用方式。

## 1. 迁移目标与结果

本次迁移的目标是将 `UnityGXGameFrame-master` 中 ECC(Capability) 相关运行时代码迁入当前项目，并完成以下改造：

- 完成 **彻底去耦**：不再依赖 `GameFrame.Runtime`（如 `EffEntity`、`World`、`ReferencePool` 等）。
- 将 `ECCWorld` 重命名为 `CapabilityWorld`。
- 统一命名风格：
  - `public`：PascalCase。
  - `private`：`m_` + PascalCase。
- 命名空间统一为最多两层：`Core.Capability` 与 `Tool`。
- 抽取通用数据结构至 `Assets/Scripts/Tool/Collection`。

## 2. 目录结构与分层职责

### 2.1 `Assets/Scripts/Core/Capability/Common`

- `CapabilityUpdateMode.cs`：能力更新模式（`Update` / `FixedUpdate`）。
- `CapabilityId.cs`：能力类型 ID 生成与类型注册。
- `CapabilityWorldRegistry.cs`：运行时 world 注册表（供 Editor 调试窗口发现活跃 world）。
- `ComponentId.cs`：组件类型 ID 生成与类型注册。
- `ChangeEventState.cs`：Group 事件掩码（Add/Remove/Update）。
- `Interfaces.cs`：`IEntity`、`IUpdateSystem`、`IFixedUpdateSystem` 等。
- `GroupChanged.cs`：Group 变化委托定义。

### 2.2 `Assets/Scripts/Core/Capability/Core`

- `CComponent.cs`：基础组件类型。
- `CEntity.cs`：实体（组件容器、组件增删查、生命周期）。
- `EntityMatcher.cs`：按组件组合匹配实体（All/Any/None）。
- `EntityGroup.cs`：匹配结果集合与变化事件。
- `CapabilityWorldBase.cs`：基础世界（Group 管理、组件变化响应、时间推进）。
- `CapabilityWorldBase.Children.cs`：世界中的实体创建/移除/回收逻辑。

### 2.3 `Assets/Scripts/Core/Capability/Base`

- `CapabilityBase.cs`：能力抽象基类（激活/停用判定、Tick、Filter）。
- `CapabilityCollector.cs`：监听 Group 变化，驱动能力重评估。
- `CapabilityRegistry.cs`：能力容器与 Update/FixedUpdate 主循环。
- `CapabilityRegistry.Transform.cs`：能力绑定、解绑、按实体查询。

### 2.4 `Assets/Scripts/Core/Capability/Components`

- `CapabilityBlockComponent.cs`：能力 Tag 阻塞组件（Block/Unblock/IsBlocked）。
- `CapabilityBlockComponent.Debug.cs`：Editor 下的阻塞来源校验。
- `DestroyComponent.cs`：销毁标记组件。

### 2.5 `Assets/Scripts/Core/Capability/BuiltIn`

- `DestroyCapability.cs`：内置销毁能力（检测 `DestroyComponent` 后移除实体）。

### 2.6 `Assets/Scripts/Core/Capability/World`

- `CapabilityWorld.cs`：能力世界入口（能力系统初始化、默认内置能力注入、更新桥接）。

### 2.7 `Assets/Scripts/Tool/Collection`

- `IndexedObjectArray.cs`：稀疏索引数组（按 ID 存储对象，支持枚举有效项）。
- `IndexedSet.cs`：去重集合封装。
- `CollectionExtensions.cs`：`List<T>.RemoveSwapBack` 等通用扩展。

### 2.8 `Assets/Scripts/Core/Capability/Editor/Debug`

- `CapabilityDebugWindow.cs`：Capability 调试可视化窗口（按 World/Entity 观察 Update/FixedUpdate 能力状态时间轴）。
- `CapabilityTimelineTrack.cs`：时间轴环形缓存与状态段压缩逻辑。

### 2.9 `Assets/Scripts/Core/Capability/Editor/CodeGen`

- `CapabilityCodeGenerator.cs`：Capabilys 代码生成器入口与菜单。
- `Templates/AllCapability.Template.txt`：`AllCapability.g.cs` 生成模板。

### 2.10 `Assets/Scripts/Core/Capability/Generated`

- `AllCapability.g.cs`：自动生成的能力顺序注册与总量常量。

## 3. 核心运行机制

## 3.1 实体与组件

`CEntity` 使用 `IndexedObjectArray<CComponent>` 按组件 ID 存储组件：

- `AddComponent<TComponent>()`：创建组件并触发世界的组件变化通知。
- `RemoveComponent(int componentId)`：释放组件并通知世界。
- `HasComponents/HasAnyComponent`：供匹配器判断。

## 3.2 匹配与分组

`EntityMatcher` 定义匹配条件（All/Any/None 组件 ID）。

`CapabilityWorldBase.GetGroup(matcher)` 会缓存 `EntityGroup`：

- 实体组件变化后，通过 `NotifyComponentChanged` 增量更新受影响 Group。
- `EntityGroup` 触发 `GroupAdded/GroupRemoved/GroupUpdated` 事件。

## 3.3 能力激活模型

每个能力继承 `CapabilityBase`，核心流程：

1. `Init`：注入 `Id / World / Owner` 并调用 `OnInit`。
2. `OnInit` 内可调用 `Filter(componentIds)`，让 `CapabilityCollector` 监听条件变化。
3. 每帧由 `CapabilityRegistry` 调度：
   - 先检查是否被 `CapabilityBlockComponent` 的 Tag 阻塞。
   - 若组件变化标记为真，执行激活/停用判定：
     - 未激活 -> `ShouldActivate`。
     - 已激活 -> `ShouldDeactivate`。
   - 激活状态下执行 `TickActive`。

## 3.4 Update 与 FixedUpdate 分流

- 能力通过 `CapabilityBase.UpdateMode` 选择更新通道。
- `CapabilityRegistry` 内部维护两套数组：
  - `m_UpdateCapabilities`
  - `m_FixedUpdateCapabilities`

## 3.5 世界层桥接

`CapabilityWorld` 负责：

- `InitCapabilities(maxCapabilityCount, maxTag, estimatedEntityCount)` 初始化能力系统。
- `AddChild()` / `AddChild<TEntity>()` 时自动注入：
  - `DestroyCapability`
  - `CapabilityBlockComponent`
- 在 `OnUpdate/OnFixedUpdate` 将帧时间传递给能力注册器。

## 4. 与原框架的关键差异

相较原始 `GameFrame.Runtime` 实现，本迁移版本有以下明确变化：

- 去掉 `ReferencePool`、`Profiler`、`Debugger`、`Assert` 等外部依赖。
- 去掉对原 ECS 基建（`EffEntity`、`World`、`Matcher`、`Group`）依赖，改为本地实现。
- 名称统一修正：
  - `Capabilty` -> `Capability`
  - `Capabilitys` -> `Capability`
  - `ECCWorld` -> `CapabilityWorld`
- 数据容器改为 `Tool` 下的通用结构，便于后续复用。

## 5. 使用示例

## 5.1 定义世界

```csharp
using Core.Capability;

public class BattleWorld : CapabilityWorld
{
    public override void OnInitialize(int maxComponentCount)
    {
        base.OnInitialize(maxComponentCount);
        int capabilityCount = AllCapability.TotalCapabilities > 0 ? AllCapability.TotalCapabilities : 1;
        InitCapabilities(maxCapabilityCount: capabilityCount, maxTag: 64, estimatedEntityCount: 512);
    }
}
```

## 5.2 定义组件

```csharp
using Core.Capability;

public class StunComponent : CComponent
{
}
```

## 5.3 定义能力

```csharp
using Core.Capability;

public class StunCapability : CapabilityBase
{
    protected override void OnInit()
    {
        Filter(ComponentId<StunComponent>.TId);
    }

    public override bool ShouldActivate()
    {
        return Owner.GetComponent(ComponentId<StunComponent>.TId) != null;
    }

    public override bool ShouldDeactivate()
    {
        return Owner.GetComponent(ComponentId<StunComponent>.TId) == null;
    }
}
```

## 5.4 绑定与解绑

```csharp
CEntity entity = world.AddChild();
world.BindCapability<StunCapability>(entity);
world.UnbindCapability<StunCapability>(entity);
```

## 6. 编辑器工具使用

### 6.1 Capability 可视化调试

- 菜单：`GX框架工具/Capability/调试/Capability 可视化`
- 使用方式：
  - 进入 Play Mode。
  - 在窗口中选择 `World` 与 `Entity`。
  - 查看 `Update` / `FixedUpdate` 两组能力状态时间轴：
    - 青色：激活
    - 白色：未激活
    - 黄色：被阻塞
    - 灰色：不存在/未采样

### 6.2 Capabilys 代码生成

- 菜单：`GX框架工具/Capability/代码生成/生成 AllCapability`
- 输出：`Assets/Scripts/Core/Capability/Generated/AllCapability.g.cs`
- 生成逻辑：
  - 扫描所有 `CapabilityBase` 非抽象子类
  - 按 `TickGroupOrder` 排序
  - 依据 `UpdateMode` 生成 `CapabilityId<,>.TId` 顺序触发代码与总量常量

## 7. 二次开发建议

- 新能力优先在 `Base` 规则内完成，不要把业务逻辑塞进 `CapabilityWorld`。
- Tag 阻塞适合做“互斥状态”或“技能锁定”控制。
- 若能力数量可动态增长，当前注册器已支持自动扩容能力数组。
- 组件槽位（`m_GroupsByComponent`）已支持按组件 ID 自动扩容，不再依赖初始化时一次性预估固定上限。
- 若后续引入单元测试，建议优先覆盖：
  - `EntityMatcher` 匹配正确性。
  - `Capability` 激活/停用切换。
  - `Bind/Unbind` 对 `Update` 与 `FixedUpdate` 两通道的正确性。

## 8. 当前边界与注意事项

- 当前调试窗口仅针对 `Core.Capability` 的 `CapabilityWorld` / `CEntity` 体系，不覆盖旧版 `GameFrame.Runtime` 的实体结构窗口。
- 本框架是否接入 `MapManager/FactionManager` 由上层业务决定。
- 文档示例中的 `BattleWorld` 仅示意，需按你项目启动流程接入。
