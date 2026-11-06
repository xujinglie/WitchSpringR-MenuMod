# WitchSpring R 菜单 Mod（WSMenuMod）

[![release](https://img.shields.io/badge/release-v1.2.0-blue)]()
[![platform](https://img.shields.io/badge/platform-Windows%20x64-lightgrey)]()
[![BepInEx](https://img.shields.io/badge/BepInEx-6.0.0--be.791-orange)]()

《魔女之泉 R》（WitchSpring R）的 BepInEx 6（IL2CPP）修改菜单，按 `F8` 打开。

> 只在本机内存里改数值，**不修改游戏任何文件**，也不写存档；关掉游戏即恢复原样。

## 功能

| 标签页 | 内容 |
| --- | --- |
| 角色属性 | 生命 / 魔力 / 力量 / 防御 / 敏捷 / 技能点 / 名气 / 战斗经验 / 体质 / 精神 / 金币，一键满血满蓝、批量加点 |
| 背包物品 | 全物品库搜索、添加、增减、删除，直接读写背包列表并校验结果 |
| 怪物刷新 | 刷新时间倍率、剩余时间设置、立即刷新全部 |
| 敌人属性 | 生命上限 / 力量 / 魔力 / 防御 / 防御属性 / 物理抗性 / 魔法抗性 / 敏捷 / 技能属性 / 大小 |

### 敌人属性

- 改的是 `EnemyListData` 数据表，对**同一种敌人**全部生效；
- 已经在场上的实例会在约 1 秒内自动套用新数值，残血按比例换算；
- 顶部「全局倍率」对全部敌人生效（只缩放生命 / 力量 / 魔力 / 防御 / 敏捷）；
- 「恢复该敌人 / 恢复全部敌人」还原为进入游戏时的原始值；
- 物理抗性 / 魔法抗性是 **0~1 的小数**（`0.5` = 50%）；
- 防御属性对应 `BodyType`：`NONE / TREE / STONE / STRONGATPHYSICAL / STRONGATMAGIC / FIRE / THUNDER / ICE / IRON / WATER`；
- 技能属性对应 `SkillType`：`NONE / FIRE / THUNDER / ICE / EXPLOSION / … / RECOVERDOWN`。

## 环境要求

- Windows x64
- 游戏：《魔女之泉 R》，Unity **2021.3.39f1**，**IL2CPP** 构建（Steam 版或同版本构建）
- 加载器：**BepInEx 6.0.0-be.791（IL2CPP）** + Unity Doorstop + .NET 6 运行时
- 编译还需要 .NET 6 SDK

BepInEx 6 的 be 构建之间 API 有变动，请保持 `be.791`，否则插件可能加载失败。

## 安装

### 前置：装 BepInEx 6（IL2CPP）

游戏本体不自带加载器，先去 BepInEx 的 bleeding-edge 构建页下载 **6.0.0-be.791** 的
`BepInEx-Unity.IL2CPP-win-x64` 包（一定要 IL2CPP 版、x64），解压到游戏根目录，
和 `WitchSpringR.exe` 同级。这个包自带 Unity Doorstop 与 `dotnet\` 运行时。

首次启动游戏时 BepInEx 会生成 `BepInEx\interop`，可能需要几分钟，期间黑屏属正常，别强退。

### 安装本插件

把 Releases 里的 `WSMenuMod-v1.2.0-plugin.zip` 解压到游戏根目录，或手动放这两个文件：

```
<游戏根目录>\BepInEx\plugins\WSMenuMod\WSMenuMod.dll
<游戏根目录>\BepInEx\plugins\WSMenuMod\WSMenuMod.deps.json
```

`WSMenuMod.deps.json` 必须和 dll 在同一目录，否则插件不加载。

## 使用

| 快捷键 | 作用 |
| --- | --- |
| `F8` | 打开 / 关闭菜单 |
| `F9` | 输出诊断信息到 `BepInEx\LogOutput.log` |
| `F10` | 截图到游戏根目录 `menu_capture.png` |

## 构建

需要先在本机装好 BepInEx 6（IL2CPP）并**启动过一次游戏**，让 `BepInEx\interop` 生成出来。

```powershell
# 把仓库放在游戏根目录下（推荐）
<游戏根目录>\WitchSpringR-MenuMod\
    dotnet build src\MenuMod.csproj -c Release

# 放在别处时用 GameDir 指过去（注意结尾带反斜杠）
dotnet build src\MenuMod.csproj -c Release -p:GameDir="D:\steam\steamapps\common\WitchSpring R\"
```

编译产物直接输出到 `<游戏根目录>\BepInEx\plugins\WSMenuMod\`。
更细的说明与排错见 [docs/BUILD.md](docs/BUILD.md)。

## 目录结构

```
src/
  Plugin.cs         BepInEx 入口
  MenuBehaviour.cs  IMGUI 菜单（四个标签页、样式、快捷键）
  GameApi.cs        游戏数据访问层（玩家 / 背包 / 刷新表 / 敌人数值）
docs/BUILD.md       构建与排错
build.ps1           一键构建脚本
```

## 已知限制

- 只支持 Windows x64；安卓 / Switch 版不适用。
- 数值改动只在内存里，不写存档，重开游戏恢复。
- 与游戏版本强相关：版本不同时 BepInEx 会重建 interop，类或字段若有变动，对应功能可能失效
  （代码里所有调用都有异常保护，不会把游戏搞崩，只是按钮没反应）。
- 敌人属性依赖 `EnemyBasic.enemyID` 与数据表 id 一致；若某天对不上，
  数据表仍然生效，只是场上已有实例不会立刻同步。

## 免责声明

- 本仓库**不包含**任何游戏资源、游戏程序集或 BepInEx 二进制文件，只包含本 mod 的源码。
- 仅供个人学习与单机修改使用，请支持正版。
- 使用本 mod 可能影响成就 / 存档，风险自负。

## 许可

代码以 [MIT](LICENSE) 许可发布。

---

### English

A BepInEx 6 (IL2CPP) mod menu for **WitchSpring R**: character stats, inventory,
monster respawn timers, and enemy stat editing (HP / STR / MP / DEF / body type /
physical & magic resistance / AGI / skill type / size). Press `F8` in game.

Requirements: Windows x64, WitchSpring R (Unity 2021.3.39f1, IL2CPP),
BepInEx 6.0.0-be.791 (IL2CPP) + Unity Doorstop + .NET 6 runtime.

The repo contains source only — no game assets, no BepInEx binaries.
Licensed under MIT.
