# Json 系统精粹

这套 Json 系统的核心目的，是把“配置数据”从代码里拿出去，放进 JSON 文件里维护；运行时再通过一个明确的 key 找到某条数据，把它消费到实体组件上。

最重要的心智模型是：

```text
Template = JSON 数据结构
TemplateSet = 用 key 查找数据实例的集合
key = 要消费的具体数据实例 ID
Component = JSON 数据容器
Entity = 承载组件，不保存模板 key
```

也就是说，`CEntity` 不负责记住“自己是哪条配置”。用哪条配置，只和本次调用传入的 key 有关。你要消费 `unit_scout`，就把 `unit_scout` 传进去；你要改成 `unit_tank`，就再显式消费一次 `unit_tank`。

## 使用入口

最推荐的使用方式是：先定义模板，再定义模板集，再让组件声明自己消费哪种模板，最后在实体上显式消费某个 key。

```csharp
public class EgUnitTemplate
{
    [PrimaryKey] public string Id;

    public string Name;
    public int Hp;
    public float Speed;
}
```

`EgUnitTemplate` 是一条 JSON 数据反序列化后的 C# 形状。它描述“数据长什么样”，不描述运行时行为。

```csharp
public class EgUnitTemplateSet : JsonTemplateSet<EgUnitTemplateSet, EgUnitTemplate>
{
    protected override string ConfigDir => "Config://Eg/Unit";
}
```

`EgUnitTemplateSet` 是一组 `EgUnitTemplate`。它负责从 `ConfigDir` 加载 JSON 文件，并按主键建立索引。

```csharp
[TemplateComponent(typeof(EgUnitTemplate))]
public class EgUnitStatsComponent : CComponent
{
    [TemplateField] public int Hp;
    [TemplateField] public float Speed;
    [TemplateField("Name")] public string DisplayName;

    public int RuntimeOnlyCounter;
}
```

`EgUnitStatsComponent` 是数据容器。`[TemplateComponent]` 说明它消费 `EgUnitTemplate`。普通 `CComponent` 只会绑定带 `[TemplateField]` 的成员，所以 `RuntimeOnlyCounter` 不会被 JSON 覆盖。

```csharp
CEntity entity = world.AddChild("Eg_Unit_Entity");
entity.AddComponentFromTemplate<EgUnitStatsComponent>("unit_scout");
```

这句话就是完整语义：给实体添加 `EgUnitStatsComponent`，并用 key 为 `unit_scout` 的 JSON 数据填充它。

如果组件已经存在，想用另一条数据重新填充：

```csharp
entity.ApplyTemplate<EgUnitStatsComponent>("unit_tank");
```

它不会创建第二个相同组件，而是把 `unit_tank` 对应的数据重新写入已有容器；如果组件不存在，会先创建再绑定。

## 对外特性

### `[PrimaryKey]`

`[PrimaryKey]` 标记模板里的主键字段。

```csharp
public class WeaponTemplate
{
    [PrimaryKey] public string Id;
    public int Damage;
}
```

`TemplateSet` 加载模板时，会读取所有带 `[PrimaryKey]` 的字段，并建立索引。默认主键名是空字符串，所以最常见调用是：

```csharp
WeaponTemplateSet.Instance.GetTemplate("wp_rifle");
```

`[PrimaryKey]` 也支持命名主键：

```csharp
[PrimaryKey(Name = "Alias")] public string Alias;
```

对应查询是：

```csharp
set.GetTemplate("Alias", "rifle_alias");
```

不过常规业务里，推荐保持一个清晰主键，例如 `Id`。

### `[TemplateComponent(typeof(TTemplate))]`

`[TemplateComponent]` 标在组件类上，说明这个组件消费哪一种模板。

```csharp
[TemplateComponent(typeof(WeaponTemplate))]
public class WeaponStatsComponent : CComponent
{
    [TemplateField] public int Damage;
}
```

它是组件和模板的一一契约：

```text
WeaponStatsComponent -> WeaponTemplate
```

系统会根据这个契约找到模板类型，再通过注册表找到唯一的 `TemplateSet`。

这里不再需要 `TemplateBinding`，也不再需要 slot。组件是容器，不应该声明“MainHand”“OffHand”这种具体实例语义。实例由调用时传入的 key 决定。

### `[TemplateField]`

`[TemplateField]` 标在普通 `CComponent` 的字段或属性上，表示这个成员允许被 JSON 写入。

```csharp
[TemplateField] public int Hp;
[TemplateField("Name")] public string DisplayName;
[TemplateField(Optional = true)] public int OptionalValue;
```

规则是：

- 不传参数时，用成员名匹配模板字段。
- 传字符串时，用指定 key 匹配模板字段。
- `Optional = true` 时，模板里缺字段不会报警。

普通 `CComponent` 默认是保守模式：只有显式标记 `[TemplateField]` 的成员会被写入。这可以避免运行时字段被配置误覆盖。

### `[TemplateIgnore]`

`[TemplateIgnore]` 标在字段或属性上，表示永远不要从 JSON 写入。

它主要用于 `JsonComponent`，因为 `JsonComponent` 默认绑定公开字段/属性。

```csharp
public class EgUnitJsonComponent : JsonComponent
{
    public string Name;
    public int Hp;

    [TemplateIgnore]
    public float RuntimeSpawnTime;
}
```

### `[TemplatePreloadOrder]`

`[TemplatePreloadOrder]` 标在模板集类上，用来控制 `KTemplateUtil.PreloadAll()` 的预加载顺序。

```csharp
[TemplatePreloadOrder(10)]
public class WeaponTemplateSet : JsonTemplateSet<WeaponTemplateSet, WeaponTemplate>
{
    protected override string ConfigDir => "Config://Weapon";
}
```

数值越小越早预加载。没有标记时默认 order 是 `0`。

## 对外组件基类

### `JsonComponent`

`JsonComponent` 是一种更自动化的组件基类。

普通 `CComponent` 需要 `[TemplateField]` 才绑定；`JsonComponent` 默认绑定所有 public 字段和可写 public 属性，除非成员标了 `[TemplateIgnore]`。

```csharp
[TemplateComponent(typeof(EgUnitTemplate))]
public class EgUnitJsonComponent : JsonComponent
{
    public string Name;
    public int Hp;
    public float Speed;

    [TemplateIgnore]
    public float RuntimeSpawnTime;

    public override void OnTemplateApplied()
    {
        RuntimeSpawnTime = UnityEngine.Time.time;
    }
}
```

`JsonComponent` 还会记录：

```csharp
public string TemplateId { get; private set; }
```

这个值来自本次消费的 key，方便调试“当前组件是由哪条配置填出来的”。

它的生命周期钩子是：

```csharp
protected virtual void OnTemplateApplying()
public virtual void OnTemplateApplied()
```

其中 `OnTemplateApplying()` 在写入字段前触发，`OnTemplateApplied()` 在写入字段后触发。

## 对外调用接口

### `CEntityTemplateExtensions.AddComponentFromTemplate<TComponent>(key)`

这是最推荐的业务入口。

```csharp
entity.AddComponentFromTemplate<EgUnitStatsComponent>("unit_scout");
```

它做四件事：

1. 读取 `TComponent` 上的 `[TemplateComponent]`，知道组件消费哪种模板。
2. 通过 `TemplateType -> TemplateSetType` 注册表找到对应模板集。
3. 用传入的 key 从模板集里取出数据实例。
4. 给实体添加组件，并把模板数据写入组件。

如果实体已经有这个组件，底层 `AddComponent<T>()` 会返回已有组件，然后重新应用模板数据。

### `CEntityTemplateExtensions.ApplyTemplate<TComponent>(key)`

用于“把某条配置应用到组件”。

```csharp
entity.ApplyTemplate<EgUnitStatsComponent>("unit_tank");
```

语义上它更偏重“重填数据”：如果组件存在，就直接写入；如果不存在，就先创建。

### `JsonTemplateProcessor.AddFromTemplateSet<TComponent, TSet, TTemplate>(...)`

这是更底层、更显式的入口：调用方同时提供组件类型、模板集类型、模板类型。

```csharp
JsonTemplateProcessor.AddFromTemplateSet<EgUnitStatsComponent, EgUnitTemplateSet, EgUnitTemplate>(
    entity,
    "unit_scout");
```

它适合框架内部或需要绕过注册表的场景。普通业务更推荐 `entity.AddComponentFromTemplate<TComponent>(key)`。

### `JsonTemplateProcessor.AddFromTemplate<TComponent>(entity, template, templateId)`

这个入口不通过 key 查模板，而是直接给一个已经拿到的模板对象。

```csharp
EgUnitTemplate template = EgUnitTemplateSet.Instance.GetTemplate("unit_scout");
JsonTemplateProcessor.AddFromTemplate<EgUnitStatsComponent>(entity, template, "unit_scout");
```

适合你已经手动查到了模板对象，只想复用绑定逻辑的情况。

### `JsonTemplateProcessor.AddFromTemplate(entity, componentType, template, templateId)`

这是运行时动态类型版本。

```csharp
JsonTemplateProcessor.AddFromTemplate(entity, typeof(EgUnitStatsComponent), template, "unit_scout");
```

当组件类型不是泛型参数，而是运行时 `Type` 时使用。

### `JsonTemplateBinder.Apply(source, target, invokeApplyCallback)`

这是最底层的字段拷贝器。

```csharp
JsonTemplateBinder.Apply(template, component);
```

它不负责查模板、不负责创建组件，只负责把 source 里的 public 字段/属性映射到 target 的可写成员上。

绑定规则是大小写不敏感的。比如模板字段 `Name` 可以写入 `[TemplateField("name")]` 指定的目标成员。

一般业务不需要直接调用它，除非你正在写自定义绑定流程。

## TemplateSet 查询接口

### `BaseTemplateSet<TSelf, TTemplate>.GetTemplate(key)`

最基础的查表接口。

```csharp
EgUnitTemplate unit = EgUnitTemplateSet.Instance.GetTemplate("unit_scout");
```

查不到会抛异常，并附带 `TemplateEnv` 上下文，方便定位是哪个 JSON 或哪个引用链出错。

### `GetTemplate(name, key)`

按命名主键查询。

```csharp
set.GetTemplate("Alias", "scout_alias");
```

### `TryGetTemplate(key, out template)`

不想抛异常时使用。

```csharp
if (EgUnitTemplateSet.Instance.TryGetTemplate("unit_scout", out EgUnitTemplate unit))
{
    // use unit
}
```

### `TryGetTemplateWithError(key, out template)`

查不到时不抛异常，但会打错误日志。

### `AllTemplates / Keys / Values`

用于遍历模板集。

```csharp
foreach (EgUnitTemplate unit in EgUnitTemplateSet.Instance.Values)
{
}
```

`AllTemplates` 返回当前默认主键索引下的字典。

### `Default`

`JsonTemplateSet` 提供 `Default`，用于取默认模板。

默认逻辑是：

- 如果子类重写 `DefaultTemplateKey`，用这个 key 查。
- 否则取第一条模板。

```csharp
EgUnitTemplate defaultUnit = EgUnitTemplateSet.Instance.Default;
```

## 加载和资源路径

`JsonTemplateSet<TSelf, TTemplate>` 通过子类的 `ConfigDir` 知道去哪里加载 JSON。

```csharp
public class EgUnitTemplateSet : JsonTemplateSet<EgUnitTemplateSet, EgUnitTemplate>
{
    protected override string ConfigDir => "Config://Eg/Unit";
}
```

加载链路是：

```text
JsonTemplateSet
-> KResource.LoadAll<TextAsset>(ConfigDir)
-> KResourceRouter 解析 Config://
-> ResourceManager / AssetDatabase / AB
-> JsonConvert.DeserializeObject<TTemplate>
-> RegisterTemplate 建立主键索引
```

所以 `Config://Eg/Unit` 不是普通文件路径，而是资源系统 schema。它会通过 `KResourceRouter` 映射到实际资源目录。

模板集是 singleton：访问 `EgUnitTemplateSet.Instance` 时会加载配置。也可以显式调用预加载接口。

## 预加载和卸载

### `JsonTemplateSet<TSelf, TTemplate>.Preload()`

启动该模板集的预加载。

```csharp
EgUnitTemplateSet.Preload();
```

预加载会被放进 `PreloadingQueue`，允许系统逐步推进加载，减少一次性卡顿。

如果预加载尚未完成就访问 `Instance`，系统会强制完成加载，并打印等待耗时。

### `Unload()` / `UnLoad()`

释放模板集缓存。

```csharp
EgUnitTemplateSet.Unload();
```

`UnLoad()` 是同义入口。

### `KTemplateUtil.PreloadAll()`

扫描所有 `IJsonTemplateSet`，按 `[TemplatePreloadOrder]` 排序后预加载。

```csharp
KTemplateUtil.PreloadAll();
```

### `KTemplateUtil.LoadAll()`

立即访问所有模板集 `Instance`，触发同步加载。

### `KTemplateUtil.UnloadAll()`

卸载所有模板集。

### `KTemplateUtil.IsPreloading`

用于判断预加载队列是否仍在运行。

## 模板引用

JSON 数据之间经常会互相引用。比如单位模板引用技能模板：

```csharp
public class EgUnitTemplate
{
    [PrimaryKey] public string Id;

    [JsonConverter(typeof(JsonTemplateSet<EgSkillTemplateSet, EgSkillTemplate>.ReferenceCvt))]
    public EgSkillTemplate Skill;
}
```

JSON 里只需要写技能 key：

```json
{
  "Id": "unit_scout",
  "Skill": "skill_dash"
}
```

反序列化时，`ReferenceCvt` 会用 `skill_dash` 去 `EgSkillTemplateSet` 查出实际 `EgSkillTemplate` 对象。

如果不想立刻解析，也可以使用 `Ref`：

```csharp
[JsonConverter(typeof(JsonTemplateSet<EgSkillTemplateSet, EgSkillTemplate>.ReferenceCvt))]
public JsonTemplateSet<EgSkillTemplateSet, EgSkillTemplate>.Ref SkillRef;
```

`Ref` 保存 key，并在需要时通过 `Get()` 懒查询：

```csharp
EgSkillTemplate skill = SkillRef.Get();
```

`Ref` 也支持隐式转换成模板对象。

## 分类索引

`JsonTemplateSet` 内置了 `Classifier`，用于按某种业务字段给模板建立二级索引。

第一种形式返回模板本身：

```csharp
public class UnitByTagClassifier :
    EgUnitTemplateSet.Classifier<UnitByTagClassifier, string>
{
    protected override void GetKeys(EgUnitTemplate template, ref List<string> keys)
    {
        keys.Add(template.Tag);
    }
}
```

查询：

```csharp
IReadOnlyList<EgUnitTemplate> units = UnitByTagClassifier.Instance.GetTemplates("Elite");
```

第二种形式返回派生值，要求值实现 `ITemplateRelated<TTemplate>`：

```csharp
public interface ITemplateRelated<out TTemplate>
{
    TTemplate BaseTemplate { get; }
}
```

这种适合一个模板拆出多个可索引子项的场景。

分类器会订阅模板集热重载事件。编辑器中 JSON 变化后，索引会跟随更新。

## 热重载和 `IReloadable`

编辑器下，`JsonAssetPostProcessor` 会监听 JSON 资源重新导入。模板集收到变化后，会重新反序列化对应 JSON。

默认情况下，系统用 `Utility.Reflection.SoftCopy` 把新数据拷到旧模板对象上，这样已经持有模板引用的运行时代码不容易失效。

如果模板类实现：

```csharp
public interface IReloadable<in TTemplate>
{
    void OnReload(TTemplate t);
}
```

则重载时会调用 `OnReload`，由模板自己决定如何吸收新数据。

## 错误上下文

`TemplateEnv` 是模板加载和查错用的上下文栈。

系统在加载某个 JSON 文件、解析某个引用、进入某段上下文时，会把路径压入 `TemplateEnv`。当查不到模板、主键重复、反序列化失败时，错误信息可以带上类似“当前正在处理哪个模板”的上下文。

常用接口包括：

```csharp
TemplateEnv.GetFullPath();
TemplateEnv.GetFullPathStack();
using (TemplateEnv.Begin("SomeContext")) { }
```

业务代码通常不需要直接使用它，除非你在扩展加载器或自定义解析流程。

## 系统核心流程

显式消费的完整链路如下：

```text
entity.AddComponentFromTemplate<EgUnitStatsComponent>("unit_scout")

1. 读取 EgUnitStatsComponent 的 [TemplateComponent]
   -> 得到 EgUnitTemplate

2. JsonTemplateRegistry 查找 TemplateType -> TemplateSetType
   -> EgUnitTemplate -> EgUnitTemplateSet

3. 访问 EgUnitTemplateSet.Instance
   -> 如果尚未加载，则从 ConfigDir 加载 JSON
   -> 每个 JSON 反序列化成 EgUnitTemplate
   -> 根据 [PrimaryKey] 建立 key -> template 索引

4. 用 key 查询模板
   -> EgUnitTemplateSet.Instance.GetTemplate("unit_scout")

5. 添加或取得组件
   -> entity.AddComponent<EgUnitStatsComponent>()

6. 绑定字段
   -> 普通 CComponent 只写 [TemplateField]
   -> JsonComponent 写 public 成员，跳过 [TemplateIgnore]

7. 完成回调
   -> JsonComponent 记录 TemplateId
   -> 调用 OnTemplateApplied()
```

这条链路里没有 entity template key，也没有 slot。Entity 不保存“我是谁的配置”。配置实例只在调用时由 key 决定。

## 为什么不要 Slot

旧模型里，slot 的作用是区分同一个 `TemplateSet` 在同一个实体上的多个 key，例如：

```text
WeaponTemplateSet + MainHand -> wp_rifle
WeaponTemplateSet + OffHand  -> wp_pistol
```

但这个设计把“实例语义”塞进了组件声明或实体上下文，容易让人误以为组件可以因为 slot 拥有多个实例。实际上，组件是容器，数据实例是 key 指向的模板。一个实体是否有主手、副手，是业务模型的问题，不应该由 JSON 绑定系统的 attribute 来表达。

新模型把问题拆干净：

```text
组件声明自己能消费哪种数据。
调用方传入 key，决定本次消费哪条数据。
如果要消费另一条数据，就再次显式调用。
```

所以：

```csharp
entity.AddComponentFromTemplate<WeaponLoadoutComponent>("wp_rifle");
entity.ApplyTemplate<WeaponLoadoutComponent>("wp_pistol");
```

含义非常直接：同一个容器先消费 rifle，之后又消费 pistol。系统不需要知道 MainHand/OffHand，也不会偷偷创建多个相同组件。

## 设计边界

这套系统只负责“配置数据到运行时组件”的绑定，不负责表达所有 gameplay 语义。

它负责：

- 从 JSON 加载模板。
- 用主键 key 查找模板实例。
- 把模板字段写入组件。
- 维护模板引用、预加载、热重载、分类索引。

它不负责：

- 判断一个实体应该装备几把武器。
- 判断一个组件内部如何表达多个业务位置。
- 保存实体的模板身份。
- 根据 slot 自动推导业务实例。

如果业务真的需要“主手武器”和“副手武器”，应该在业务组件里明确建模，例如：

```csharp
public class WeaponLoadoutComponent : CComponent
{
    public WeaponRuntimeData MainHand;
    public WeaponRuntimeData OffHand;
}
```

然后由业务代码分别读取两个 key，填入两个明确字段。JSON 系统提供查找和绑定能力，但不替业务语义命名。

## 最小推荐范式

定义模板：

```csharp
public class UnitTemplate
{
    [PrimaryKey] public string Id;
    public string Name;
    public int Hp;
}
```

定义模板集：

```csharp
public class UnitTemplateSet : JsonTemplateSet<UnitTemplateSet, UnitTemplate>
{
    protected override string ConfigDir => "Config://Unit";
}
```

定义组件：

```csharp
[TemplateComponent(typeof(UnitTemplate))]
public class UnitStatsComponent : CComponent
{
    [TemplateField] public string Name;
    [TemplateField] public int Hp;
}
```

消费数据：

```csharp
CEntity entity = world.AddChild("Unit");
entity.AddComponentFromTemplate<UnitStatsComponent>("unit_scout");
```

这就是这套 Json 系统最精粹的用法：

```text
key 指向数据实例。
TemplateSet 负责查数据。
Component 声明自己能消费哪种数据。
Entity 只承载组件，不保存 key。
```
