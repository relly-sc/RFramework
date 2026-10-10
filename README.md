# RFramework [![Ask DeepWiki](https://deepwiki.com/badge.svg)](https://deepwiki.com/relly-sc/RFramework)

RFramework 是 UnityRFramework 的纯 C# 核心库。它负责模块契约、状态管理、调度和通用数据逻辑，不引用 `UnityEngine`，也不依赖第三方运行时库。

Unity 项目通常不需要单独安装本仓库，而是通过 [UnityRFramework](https://github.com/relly-sc/UnityRFramework) 使用 Runtime Component、默认 Helper、编辑器工具和可选 Samples。

## 设计目标

- **引擎无关**：核心代码不直接访问 GameObject、场景、资源系统或平台 API。
- **按需创建**：模块由 `RFrameworkModuleHost.Get<T>()` 首次访问时创建并缓存。
- **Helper 解耦**：文件、网络、资源、UI、音频等外部能力通过 `IXxxHelper` 注入。
- **显式生命周期**：宿主负责逐帧调用 `Tick`，并在关闭时调用 `StopAll`。
- **失败可见**：参数、状态和生命周期错误统一抛出 `RFrameworkException`，不静默吞掉核心错误。

## 目录结构

```text
RFramework/
├── Base/          # 模块宿主、模块基类、异常与日志契约
├── Event/         # 类型化事件、线程安全入队、订阅组
├── Pool/          # 对象池
├── Timer/         # 计时器调度
├── Fsm/           # 有限状态机
├── Procedure/     # 流程状态机与 Blackboard
├── Resource/      # 资源加载契约、引用计数与缓存协作
├── Scene/         # 场景加载状态管理
├── UI/            # UI 窗口、层级与窗口栈
├── Entity/        # 实体、实体组与父子附加
├── Audio/         # 音频播放状态与资源协作
├── Network/       # 网络通道、心跳与重连编排
├── WebRequest/    # Web 请求队列与结果模型
├── Download/      # 断点续传、校验与解压扩展边界
├── Config/        # 配置表解析、缓存与可选数据保护
├── Localization/  # 多语言数据与语言切换
├── Storage/       # 多槽位存档、备份恢复、迁移与保护
├── Security/      # 数据保护、密钥契约与受保护基础数值
└── Utility/       # JSON、路径、随机数、校验与加密工具
```

## 模块一览

| 模块契约 | 职责 | 需要宿主提供的主要能力 |
| --- | --- | --- |
| `IEventModule` | 类型化事件、同步安全分发、跨线程入队 | 无 |
| `IPoolModule` | 普通对象池 | 无 |
| `ITimerModule` | 延迟、间隔和有限次数计时器 | 宿主逐帧调用 `Tick` |
| `IFsmModule` | 同步有限状态机 | 无 |
| `IProcedureModule` | 游戏流程状态机与共享数据 | 无 |
| `IResourceModule` | 资源加载、引用计数和并发去重 | `IResourceHelper` |
| `ISceneModule` | 场景加载、卸载和状态追踪 | `IResourceModule`、`IEventModule` |
| `IUIModule` | UI 窗口、层级、栈和全屏遮挡 | `IUIHelper`、资源与事件模块 |
| `IEntityModule` | 实体生命周期、分组和附加关系 | `IEntityHelper` |
| `IAudioModule` | 音频组、播放句柄和状态管理 | `IAudioHelper`、资源模块 |
| `INetworkModule` | 多网络通道管理 | `INetworkHelper`、事件与计时器模块 |
| `IWebRequestModule` | 请求队列、优先级和并发控制 | `IWebRequestHelper` |
| `IDownloadModule` | 下载、断点续传、校验和归档解压 | Web 请求模块、可选 `IArchiveHelper` |
| `IConfigModule` | 配置解析、缓存、查询和可选保护 | `IConfigHelper`、可选 `IDataProtector` |
| `ISettingModule` | 少量应用设置的键值读写与保存，不承载业务存档 | `ISettingHelper`，Unity Runtime 默认使用 PlayerPrefs |
| `ILocalizationModule` | 语言包解析、查询和切换 | `ILocalizationHelper` |
| `IStorageModule` | 多槽位存档、原子提交、备份和迁移 | `IStorageHelper`、`IStorageSerializer`、可选 `IDataProtector` |

`RFrameworkModuleHost` 只创建框架内置模块。项目差异通过 Helper 和业务状态实现，不通过运行时注册任意新模块实现。

## 生命周期

```csharp
using RFramework;

// 首次访问时创建，后续返回同一实例。
IEventModule events = RFrameworkModuleHost.Get<IEventModule>();
ITimerModule timers = RFrameworkModuleHost.Get<ITimerModule>();

// 由宿主主循环驱动。deltaTime 可受时间缩放影响，unscaledDeltaTime 不受影响。
RFrameworkModuleHost.Tick(deltaTime, unscaledDeltaTime);

// 应用退出、重启或测试结束时停止并清空全部模块。
RFrameworkModuleHost.StopAll();
```

模块按内部优先级调度：`Tick` 按更新顺序执行，`StopAll` 按关闭顺序清理。单个模块失败不会阻止其他模块完成本轮更新或关闭；宿主会在本轮结束后收到聚合的 `RFrameworkException`。

## Helper 接入

涉及引擎或平台能力的模块必须先安装 Helper，再执行对应操作：

```csharp
IResourceModule resources = RFrameworkModuleHost.Get<IResourceModule>();
resources.SetHelper(projectResourceHelper);
resources.SetPackageName("DefaultPackage");
resources.SetPlayMode(ResourcePlayMode.Offline);
await resources.InitializeAsync();
```

Helper 的具体实现由宿主工程负责。例如 UnityRFramework Runtime 提供 Resources、UnityWebRequest、AudioSource、UGUI 和本地文件系统实现；YooAsset、UniTask 等第三方实现位于独立 Expansion Sample 中。

## 事件示例

```csharp
public readonly struct PlayerLevelChanged
{
    public PlayerLevelChanged(int level) => Level = level;
    public int Level { get; }
}

IEventModule events = RFrameworkModuleHost.Get<IEventModule>();
EventGroup subscriptions = events.CreateGroup();
subscriptions.Subscribe<PlayerLevelChanged>(message =>
{
    Console.WriteLine(message.Level);
});

events.Fire(new PlayerLevelChanged(2));
subscriptions.Dispose();
```

- `Fire<T>`：立即分发，处理器异常会向调用方报告。
- `FireSafely<T>`：隔离单个订阅者异常，适合框架生命周期通知。
- `FireAsync<T>`：允许其他线程入队，在宿主下一次 `Tick` 时分发。

除明确标注的 API（例如 `FireAsync`）外，不应假设模块可被多个线程并发调用。

## 数据保护边界

`Security` 提供通用能力，不承诺不可破解：

- `DefaultDataProtector`：加密并认证 Config、Storage 等本地数据。
- `IKeyProvider` / `IKeyStore`：由宿主决定密钥来源和平台安全存储。
- `ProtectedInt`、`ProtectedLong`、`ProtectedFloat`：提高常规内存扫描和直接篡改成本，并通过 `MemoryTamperEvent` 报告校验失败。

这些能力适合基础离线防护，不能替代服务端权威校验、平台安全模块或针对高价值数据的专门安全设计。

## 在 Unity 中使用

UnityRFramework 已包含本库，并通过 `RFramework.Library.asmdef` 编译。该程序集设置 `noEngineReferences: true`，用于持续约束 Library 不依赖 Unity API。

Unity 项目应优先通过 `GameEntry` 和各个 Runtime Component 使用模块，由 `UnityRFrameworkController` 负责模块驱动和关闭，不需要业务代码直接调用 `RFrameworkModuleHost.Tick`。

## 开发约束

- Library 不引用 `UnityEngine`、Unity Editor 或第三方 SDK。
- 公共异步 API 使用 `Task`、`CancellationToken` 和 `IProgress<T>`。
- 引擎对象统一以 `object` 或纯 C# 接口跨越 Library/Runtime 边界。
- 新增平台能力时先扩展 Helper 契约，再在宿主层提供实现。
- 修改共享契约后必须同步验证 Unity Runtime、相关 Helper 和模块测试。

## 许可证

RFramework 使用 [Apache License 2.0](LICENSE)。UnityRFramework 及其可选第三方组件以各自仓库中的许可证和第三方声明为准。
