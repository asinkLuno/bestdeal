# 安装 BestDeal

给玩家的说明。**不需要编译、不需要 .NET SDK**，装两个东西就行：加载器 MelonLoader + 本 mod 的 `BestDeal.dll`。

- 实测环境：Probably Stolen Demo **0.46D**、MelonLoader **0.7.3**
- Windows 与 Linux/Steam Deck（Proton）步骤不同，见下

---

## 一、装 MelonLoader

MelonLoader 是这个游戏所有 mod 的前置（N 网专区的 mod 全部要求它）。

### Windows

1. 去 [MelonLoader Releases](https://github.com/LavaGang/MelonLoader/releases/latest) 下载 **`MelonLoader.Installer.exe`**
2. 运行安装器，选择游戏目录（Steam 库 → `steamapps\common\Probably Stolen Demo`）
3. 装完游戏根目录会出现 `version.dll` 和 `MelonLoader\` 文件夹

不想用安装器也可以手动：下载 **`MelonLoader.x64.zip`**，把里面的 `version.dll` 和 `MelonLoader\` 解压到游戏根目录。

### Linux / Steam Deck（Proton）

游戏是 Windows 版跑在 Proton 里，所以要装 **Windows 版** MelonLoader（不是 `.Linux.x64.zip` 那个）：

1. 同样从 [MelonLoader Releases](https://github.com/LavaGang/MelonLoader/releases/latest) 下载 **`MelonLoader.x64.zip`**
2. 解压，把 `version.dll` 和 `MelonLoader/` 放进游戏根目录
   （Steam 里右键游戏 → 管理 → 浏览本地文件）
3. **关键一步**：Steam → 该游戏 → 属性 → 启动选项，填上：

   ```
   WINEDLLOVERRIDES="version=n,b" %command%
   ```

   这行是让 Wine 加载游戏目录里的 `version.dll`（而不是它自带的那个）。**漏了这行 MelonLoader 就不会启动**，表现为游戏能玩、但什么都没有。

---

## 二、首次启动（生成必要文件，需要联网）

启动一次游戏，然后退出。这一步 MelonLoader 会：

- 下载 Cpp2IL 等依赖，生成 `MelonLoader/Il2CppAssemblies/` —— **要几分钟，期间可能黑屏或卡住，别急着强退**
- 在游戏根目录创建 `Mods/` 文件夹（mod 放这里）

验证：游戏根目录出现 `Mods/`，且 `MelonLoader/Latest.log` 里有 `MelonLoader v0.7.3`、`Game Name: Probably Stolen`。

---

## 三、装 BestDeal

把 `BestDeal.dll` 放进游戏根目录的 **`Mods/`**，重启游戏。

---

## 四、验证装好了

`MelonLoader/Latest.log` 里应出现：

```
Melon Assembly loaded: '.\Mods\BestDeal.dll'
[BestDeal] BestDeal v1.5.0 已加载（MelonLoader，已打补丁 3 处）
```

游戏里：顾客走到柜台开始对话后，屏幕**左上角出现推荐清单**（顾客/预算/类别 + 建议拿上柜台的物品和游戏给的价格）。
没有顾客时左上角是空的，这是正常的。

---

## 排错

| 现象 | 原因 / 处理 |
|---|---|
| 没有 `MelonLoader/Latest.log`，也没有 `Mods/` 文件夹 | MelonLoader 根本没被加载。Windows：确认 `version.dll` 在游戏根目录；Linux：确认启动选项那行填了 |
| 启动选项填了还是没反应 | 有些环境 Wine 不认 `version.dll`，把 `version.dll` 改名成 `winhttp.dll`，启动选项同步改成 `WINEDLLOVERRIDES="winhttp=n,b" %command%` |
| 日志里 `Failed to load Hostfxr`，或卡在 `Downloading the Portable .NET Runtime` | 缺 .NET 6 运行时。手动下载 [PortableDotnet 6.0.25 (Windows x64)](https://github.com/LavaGang/PortableDotnet/raw/refs/heads/6.0.25/dotnet6.windows.x86_64.zip)，解压到 `MelonLoader/Dependencies/`（得到 `MelonLoader/Dependencies/dotnet/host/fxr/6.0.25/hostfxr.dll`） |
| 首次启动很久没动静 | 正常，Cpp2IL 在生成代理程序集（几分钟），等它跑完 |
| 以前装过 BepInEx | MelonLoader 和 BepInEx 在同一个游戏里**不能共存**，二选一。要用本 mod 就得把 BepInEx 的 `winhttp.dll` 移走 |
| 游戏更新后 mod 失效 | 删掉 `MelonLoader/Il2CppAssemblies/`，让 MelonLoader 按新版游戏重新生成，再启动一次 |
| 想确认 mod 有没有打上补丁 | 看启动日志那行 `已打补丁 3 处`；显示 0 处就是加载了但补丁没生效 |

---

## 附：`BestDeal.dll` 从哪来

- **推荐**：从本仓库的 [Releases](https://github.com/asinkLuno/bestdeal/releases) 下载（无需自己编译）
- 也可以自己构建，见 [README](README.md) 的「构建」一节（需要 .NET 6 SDK + 已装好 MelonLoader）
