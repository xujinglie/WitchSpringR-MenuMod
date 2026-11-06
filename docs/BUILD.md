# 构建与排错

## 0. 环境

| 需要 | 说明 |
| --- | --- |
| .NET 6 SDK | `dotnet --list-sdks` 能看到 6.x；只有运行时不够 |
| 游戏本体 | WitchSpring R（Unity 2021.3.39f1 / IL2CPP） |
| BepInEx 6.0.0-be.791（IL2CPP） | 已解压到游戏根目录，且**启动过一次游戏** |

启动过一次游戏很关键：`BepInEx\interop\` 里的 `Assembly-CSharp.dll`、`UnityEngine.*.dll`
是 Il2CppInterop 从 `GameAssembly.dll` 生成的，没有它们工程引用不到游戏类型。

## 1. 放置仓库

推荐直接放在游戏根目录下（默认 `GameDir` 就指向那里）：

```
<游戏根目录>\
  WitchSpringR.exe
  BepInEx\
  dotnet\
  WitchSpringR-MenuMod\
    src\
    README.md
```

## 2. 编译

```powershell
dotnet build src\MenuMod.csproj -c Release
```

产物路径：

```
<游戏根目录>\BepInEx\plugins\WSMenuMod\WSMenuMod.dll
```

仓库不在游戏目录下时，用 `GameDir` 覆盖（结尾记得带反斜杠）：

```powershell
dotnet build src\MenuMod.csproj -c Release -p:GameDir="D:\steam\steamapps\common\WitchSpring R\"
```

也可以直接跑 `build.ps1`：

```powershell
.\build.ps1
.\build.ps1 -GameDir "D:\steam\steamapps\common\WitchSpring R\"
```

## 3. 安装与验证

1. 启动游戏，看 `<游戏根目录>\BepInEx\LogOutput.log` 里有没有：

   ```
   [Info   :WitchSpring R Menu Mod] WitchSpring R Menu Mod v1.2.0 正在加载...
   [Info   :WitchSpring R Menu Mod] 菜单已注册，按 F8 打开/关闭。
   ```

2. 进游戏后按 `F8`，能看到菜单即成功。

## 4. 常见问题

**编译报 `The process cannot access the file ... WSMenuMod.dll`**

游戏正开着，DLL 被占用。退出游戏再编译。

**编译报找不到 `BepInEx.Core` / `Assembly-CSharp`（CS0246）**

- `GameDir` 没指对，检查 `<GameDir>BepInEx\core\` 与 `<GameDir>BepInEx\interop\` 是否存在；
- 或者 interop 还没生成：先启动一次游戏，等 `BepInEx\interop\` 里出现 `Assembly-CSharp.dll`。

**游戏能开，但菜单不出来**

- 看 `BepInEx\LogOutput.log` 是否存在；不存在说明 Doorstop 没注入（`winhttp.dll` 被杀软隔离，或启动的不是该目录下的 `WitchSpringR.exe`）；
- 日志里有 `unsupported parameter ...` 的 Warning 属正常（Il2CppInterop 扫描 MonoBehaviour 方法时的提示）。

**菜单打开了但某个标签页空白 / 按钮没反应**

多半是 BepInEx 版本或游戏版本不一致导致 interop 重新生成后类型对不上。
用 `F9` 输出诊断，确认 `DataSet` / `enemyList` 是否为空。

**改了敌人数值，场上怪没变化**

先在 `F9` 日志里看 `场上 EnemyBasic: ...` 是否为空：
场景里没有存活的怪时自然同步不到，等刷出来会在 1 秒内自动套用。

## 5. 发布打包

```powershell
# 插件本体（给已装 BepInEx 的人）
Compress-Archive -Path BepInEx\plugins\WSMenuMod\* -DestinationPath WSMenuMod-plugin.zip

# 完整包（含加载器）：BepInEx\（不含 cache/log）、dotnet\、winhttp.dll、
# doorstop_config.ini、.doorstop_version 一起压缩
```

注意：`BepInEx\interop\` 是**对方机器游戏版本**的产物，版本不同会被自动重建，
把 `assembly-hash.txt` 一起带上能让同版本的机器跳过生成步骤。
