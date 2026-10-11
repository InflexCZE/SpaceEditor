# Space Editor 简体中文说明

Space Editor 是用于编辑《太空工程师 2》（Space Engineers 2）游戏数据的独立工具。

## 功能

- 蓝图生成器：将三维模型转换为游戏蓝图，支持模型缩放和方块尺寸设置。
- PCU 解锁：提高游戏中的 PCU 上限。
- 角色编辑：连接正在运行的游戏，修改角色宇航服颜色。

项目默认显示英文，可使用窗口右上角的语言选项切换到“简体中文”。切换语言只改变显示，不重新加载游戏，也不清空当前编辑的数据；语言选择在当前会话内生效。

## 安装与使用

1. 解压发行包，保留全部依赖文件，运行 `SpaceEditor.exe`。
2. 选择“简体中文”，在主页选择游戏安装目录。
3. 加载完成后，在标签页中选择功能。
4. 对游戏文件应用修改后，重启游戏查看效果；角色颜色编辑需要连接正在运行的游戏。

运行环境为 Windows 和 .NET 9 Windows Desktop 运行时。

## 中文语言包

中文翻译位于 `Localization/Strings.zh-CN.xaml`，随程序一起编译。英文资源 `Localization/Strings.en.xaml` 始终作为后备：尚未翻译的文案显示英文。

以 `Name.` 开头的键用于翻译属性、按键和控制器等游戏名称；未收录的名称保留原文。

## 构建

安装 .NET 9 SDK 后，在项目根目录执行：

```powershell
dotnet build SpaceEditor.csproj -c Release -r win-x64 --self-contained false
```

## 恢复游戏文件

如需撤销工具对游戏文件的修改，可以使用 Steam 的“验证游戏文件的完整性”功能。Steam 更新也可能覆盖工具所做的修改。

英文说明见 [README.md](README.md)，许可证见 [LICENSE](LICENSE)。原作者的权利和免责声明保持不变。
