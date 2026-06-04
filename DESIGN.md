# ClickFX — 设计文档

## 概述

ClickFX 是一个轻量级 Windows 桌面工具，为鼠标点击提供全局视觉反馈动画。支持多套动效预设、用户自定义配置、持久化设置。

### 对系统无侵入

- **不拦截鼠标**：全局钩子仅监听，所有事件通过 `CallNextHookEx` 原样透传，不影响任何点击操作
- **不抢焦点**：Overlay 窗口设为 `WS_EX_TRANSPARENT`，鼠标事件穿透，不获取焦点、不进任务栏
- **极低资源占用**：动画生命周期 ≤600ms，结束后停止计算；无动画时定时器仍运行但仅做空画面刷新（清空上次残留），开销极低
- **单进程单 exe**：无服务、无后台驻留进程、不写注册表（仅 `%APPDATA%` 下一个 JSON 配置文件）

---

## 项目结构

```
ClickFX/
├── Program.cs          # 入口 + 应用生命周期
├── ClickFX.cs        # 核心：鼠标钩子、Overlay 窗口管理、动画渲染
├── Effects.cs          # 动效接口 IClickEffect + 所有效果实现（LineBurst / Ripple / GlowDot）
├── Config.cs           # 配置模型 + 读写 + 配置 UI 窗口
└── icon.ico            # 应用图标（可选）
```

4 个 `.cs` 文件，职责清晰，无嵌套目录。

---

## 动效系统

### 接口定义

```csharp
public interface IClickEffect
{
    /// <summary>效果名称，用于配置 UI 展示</summary>
    string Name { get; }

    /// <summary>绘制一帧动画</summary>
    /// <param name="g">GDI+ Graphics 画布</param>
    /// <param name="anim">当前动画状态</param>
    /// <param name="color">当前点击对应的 ColorConfig（左键或右键）</param>
    void Draw(Graphics g, AnimationState anim, ColorConfig color);
}
```

### 内置效果

| 效果名 | 类名 | 描述 |
|--------|------|------|
| 线条爆发 | `LineBurstEffect` | 3 根线从点击点弹出，带发光描边，依次延迟出现后消失 |
| 水波纹 | `RippleEffect` | 从点击点扩散的同心圆环，半径增大同时透明度衰减 |
| 光点 | `GlowDotEffect` | 中心亮点 + 向四周发散的小粒子，粒子带拖尾 |

### 动画状态

```csharp
public class AnimationState
{
    public Point Position;        // 屏幕坐标
    public MouseButtons Button;   // 左键 / 右键
    public int Age;               // 已存活毫秒数
    public int Duration;          // 总持续时长（由效果决定）
    public bool IsExpired => Age > Duration;
}
```

### 效果注册与切换

`Effects.cs` 内维护一个静态字典，启动时注册所有内置效果。用户在配置页选择效果名，运行时按名查找对应的 `IClickEffect.Draw`。

---

## 配置系统

### 配置文件

- 路径：`%APPDATA%/ClickFX/config.json`
- 格式：JSON
- 不存在时使用默认值，首次保存时自动创建

### 配置模型

```csharp
public class AppConfig
{
    public int Version { get; set; } = 1;     // 配置版本号，用于未来迁移兼容

    // ---- 通用 ----
    public string ActiveEffect { get; set; } = "线条爆发";

    // ---- 颜色 ----
    public ColorConfig LeftClick  { get; set; } = new("#508CFF");
    public ColorConfig RightClick { get; set; } = new("#FF6060");

    // ---- 效果专属参数（可选，各效果自行读取） ----
    public Dictionary<string, Dictionary<string, string>> EffectParams { get; set; } = new();

    // ---- 关于信息 ----
    public string InfoText { get; set; } = "";       // 自定义显示文本（支持换行）
    public string InfoUrl { get; set; } = "";        // 可点击链接（如 GitHub 地址）
}

public class ColorConfig
{
    public string Primary { get; set; }       // 主色（HEX）
    public float GlowIntensity { get; set; }  // 发光强度 0~1，默认 0.3

    public ColorConfig() { Primary = "#508CFF"; GlowIntensity = 0.3f; }
    public ColorConfig(string primary) : this() { Primary = primary; }
}
```

### 配置 UI

- 入口：托盘右键菜单 → **设置**
- 窗口内容：
  - **动效选择**：下拉框，列出所有已注册效果
  - **左键颜色**：
    - 颜色预览块（实时显示当前颜色）
    - HEX 输入框（`#508CFF` 格式，支持手动输入）
    - 「选择」按钮 → 弹出系统颜色对话框（`ColorDialog`），选中后自动回填 HEX 值
  - **右键颜色**：同上，独立配置
  - **效果参数区**：根据当前选中效果动态展示其专属参数（如有）
  - **恢复默认** 按钮（左键 `#508CFF`，右键 `#FF6060`）
  - **关于 / 信息区**：窗口底部或独立 Tab，显示自定义文本（如 GitHub 地址、打赏文案、版本号等），内容写在配置中可随时修改
  - **应用 / 确定 / 取消** 按钮
- 配置变更后实时预览（可选，后期迭代）

---

## 应用生命周期

```
Main()
  ├── 加载配置
  ├── 注册动效
  ├── 创建托盘图标 + 右键菜单
  ├── 安装全局鼠标钩子
  ├── 为每个屏幕创建 Overlay 窗口
  ├── 启动动画定时器 (8ms)
  └── Application.Run()  ← 阻塞，直到退出
      └── 卸载钩子、销毁窗口、保存配置
```

---

## 编译与分发

- 目标框架：.NET Framework 4.8
- 编译方式：`csc.exe` 一次编译全部 `.cs` 文件
- 产物：单个 `ClickFX.exe`，无外部依赖

---

## 迭代路线

### Phase 1 — 结构重构（当前）
- [ ] 拆分现有单文件为 4 个 `.cs` 文件
- [ ] 提取 `IClickEffect` 接口 + `LineBurstEffect` 实现
- [ ] 提取配置模型 + 默认值
- [ ] `Program.cs` 标准入口

### Phase 2 — 配置 UI
- [ ] 配置窗口：颜色选择 + 效果切换
- [ ] 托盘菜单增加「设置」入口
- [ ] JSON 持久化读写

### Phase 3 — 新效果
- [ ] 水波纹效果
- [ ] 光点效果

### Phase 4 — 打磨
- [ ] 效果参数自定义 UI
- [ ] 开机自启动选项
