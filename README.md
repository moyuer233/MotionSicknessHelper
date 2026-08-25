# 晕3D辅助 / MotionSicknessHelper

一个极小、完全置顶、鼠标点击穿透的 Windows 屏幕覆盖层小工具，用来在游戏画面边缘显示指向屏幕中心的三角形或长条，帮助缓解“晕 3D”时的空间定向不适。

- 三角形/长条自动朝向屏幕中心
- 支持四个角（corner）或四条边中点（edge）位置
- 每个图形可独立设置：位置、形状、长度、粗细、颜色、不透明度
- 支持闪烁：勾选后按设定的 2 种颜色交替变换，闪烁间隔可调
- 支持“一键启用闪烁”开关，可一次开启/关闭所有图形闪烁
- 置顶显示，且不拦截鼠标/键盘输入（`WS_EX_TRANSPARENT + WS_EX_NOACTIVATE`）
- 有托盘图标：设置、重新加载、退出
- 体积小：框架依赖版单文件 EXE 约 **170 KB**
- 当前版本在主显示器上显示；多显示器支持可在后续版本加入

## 运行要求

- Windows 10 / 11
- 小体积版：需要安装 [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)（x64）
- 自包含版：不需要安装任何运行时，但 EXE 约 70 MB

> 提示：如果游戏使用“全屏独占”模式，Windows 不会把覆盖层显示在游戏上面；请把游戏设为“无边框窗口化/窗口化全屏”。

## 下载 / 使用

1. 从 GitHub Releases 下载 `MotionSicknessHelper.zip`（推荐框架依赖版，体积最小；如果电脑没装 .NET 就下载 `MotionSicknessHelper-SelfContained.zip`）。
2. 解压后运行 `MotionSicknessHelper.exe`。
3. 第一次运行会自动生成 `config.json`，默认在四个角显示绿色三角形。
4. 在系统托盘找到“晕3D辅助”图标：
   - 双击/右键 → **设置...** 可图形化调整每个图形
   - 设置窗口中的“一键启用闪烁”可一次开启/关闭所有图形闪烁
   - **重新加载配置** 可热加载手动编辑后的 `config.json`
   - **退出** 关闭软件

## 配置说明

`config.json` 和 EXE 放在同一目录。示例：

```json
{
  "edgeInset": 8,
  "flashIntervalMs": 500,
  "shapes": [
    {
      "position": "TopLeft",
      "shape": "Triangle",
      "size": 240,
      "thickness": 50,
      "color": "#00FF00",
      "flashEnabled": false,
      "color2": "#FF0000",
      "opacity": 140
    }
  ]
}
```

字段：

| 字段 | 说明 |
|---|---|
| `edgeInset` | 图形离屏幕边缘的距离（像素） |
| `flashIntervalMs` | 闪烁间隔（毫秒），至少有一个图形开启闪烁时生效 |
| `position` | `TopLeft` / `TopRight` / `BottomLeft` / `BottomRight` / `Left` / `Top` / `Right` / `Bottom` |
| `shape` | `Triangle`（三角）或 `Bar`（长条） |
| `size` | 图形向屏幕中心延伸的长度（像素） |
| `thickness` | 三角底边宽度 / 长条粗细（像素） |
| `color` | 第一种颜色，`#RRGGBB` 格式 |
| `flashEnabled` | `true` 开启该图形闪烁，`false` 关闭 |
| `color2` | 第二种颜色，闪烁时和 `color` 交替，`#RRGGBB` 格式 |
| `opacity` | 不透明度，`0` 完全透明，`255` 完全不透明 |

也可以直接在托盘菜单里用图形界面设置，保存后立即生效。

## 从源码构建

需要 [.NET SDK 8.0](https://dotnet.microsoft.com/download/dotnet/8.0)。

```powershell
cd MotionSicknessHelper

# 构建小体积框架依赖版（需要目标机器装有 .NET 8 Desktop Runtime）
dotnet publish .\MotionSicknessHelper.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o ..\publish\MotionSicknessHelper

# 或构建自包含版（不需要装运行时，体积较大）
dotnet publish .\MotionSicknessHelper.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o ..\publish\MotionSicknessHelper-SelfContained
```

或者直接运行仓库里的脚本：

```powershell
.\build.ps1              # 只构建小体积版
.\build.ps1 -SelfContained  # 同时构建自包含版
```

## 项目结构

```
MotionSicknessHelper/
├─ Program.cs            # 入口、托盘、热加载
├─ OverlayConfig.cs      # 配置模型与读写
├─ OverlayForm.cs        # 透明点击穿透置顶覆盖层 + 绘制
├─ SettingsForm.cs       # 设置窗口
├─ config.json           # 默认配置
├─ build.ps1             # 构建脚本
└─ .github/workflows/    # GitHub Actions 自动构建 Release
```
