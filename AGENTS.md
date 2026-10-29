# AGENTS.md

## 官方 modding 文档

- 官方 modding 入门指南（中文版 README）：
  <https://github.com/dragonpan2/probably-stolen-modding/blob/main/README.zh-CN.md>
  - 面向初学者的项目结构、`manifest.xml`、`IMod`、构建与安装说明；阅读后继续看下面的完整制作指南。
- 官方模组制作指南仓库：<https://github.com/dragonpan2/probably-stolen-modding>
  - 原生 mod 加载器（`Mods/` 文件夹 + `manifest.xml` + `IMod`）、Harmony 补丁约定等权威说明以该仓库为准。
  - 游戏 API 的具体用法优先查该仓库；签名细节以反编译
    `Probably Stolen_Data/Managed/Assembly-CSharp.dll` 为准。

## 项目速览

- 本仓库是游戏 Probably Stolen（Steam AppID 4348910，Unity Mono 正式版）的顾客推荐 mod **BestDeal**。
- 用游戏原生 mod 加载器（不用 BepInEx）；Harmony 用游戏自带的 `Managed/0Harmony.dll`，
  补丁类加 `[HarmonyPatch]` 即可，由游戏加载器自动应用。
- 构建：`dotnet build BestDeal -c Release`（自动部署到游戏 `Mods/BestDeal/`）；
  `-p:Deploy=false` 只构建不部署；`-p:GameDir=...` 覆盖游戏路径。

## 维护导航

- 系统设计和已知限制：`docs/design.md`；验证与发布流程：`docs/maintenance.md`。
- 代码按 `BestDeal/Runtime`、`Features`、`Game`、`Presentation` 分目录，统一使用 `BestDeal` 命名空间。
- 分组背包在 `Features/Quotes/GroupedKnapsack.cs`，保持不依赖游戏类型；候选收集和展示规则在 `QuoteOptimizer.cs`。
- 算法检查：`dotnet run --project tests/BestDeal.AlgorithmChecks -c Release`（无需游戏）；
  每次更新完成后默认执行 Release 构建并部署到游戏，无需再次确认；仅中途编译检查使用 `-p:Deploy=false`。

## Steam 创意工坊发布

- 发布方式以官方 ModdingGuide 的 Steam 创意工坊章节为准；详细流程见 `docs/workshop.md`。
- 现有 BestDeal 工坊条目 ID：`3828971953`，AppID：`4348910`。
  更新时保留 `publishedfileid`，不要设为 `0` 或重新复制首次发布模板，否则会创建新条目。
- 更新已有条目使用本地配置 `workshop/BestDeal.update.vdf`。
  `workshop/BestDeal.vdf` 保留了首次上传配置且 `visibility=2`（隐藏），不要直接拿它更新公开条目。
  更新配置只指定条目 ID、内容目录和更新说明，避免覆盖标题、描述、封面和可见性。
- 发布前同步 `BestDeal/manifest.xml` 的 `ModVersion` 和
  `BestDeal/Runtime/BestDealMod.cs` 的加载日志版本；`GameVersions` 仅填写实际验证过的版本。
- 执行算法检查与 Release 构建部署，完成受影响功能的游戏内验收及 `mod-scan best_deal`。
  当前机器若 PATH 中没有 `dotnet`，使用 `/home/deck/.dotnet/dotnet`：

  ```bash
  /home/deck/.dotnet/dotnet run --project tests/BestDeal.AlgorithmChecks -c Release
  /home/deck/.dotnet/dotnet build BestDeal -c Release
  ```

- 上传内容来自 `/run/media/deck/SR256/steamapps/common/Probably Stolen/Mods/BestDeal/`，
  根目录须直接包含 `manifest.xml` 和 `BestDeal.dll`；不上传源码仓库或整个 `bin` 目录。
- 修改 `workshop/BestDeal.update.vdf` 的 `changenote`，准确说明本次改动，然后用原发布账号上传：

  ```bash
  steamcmd +login YOUR_STEAM_ACCOUNT +workshop_build_item "/home/deck/CODE/bestdeal/workshop/BestDeal.update.vdf" +quit
  ```

  密码和 Steam Guard 验证由用户在终端完成，不写进配置、命令或仓库。
  本地部署成功不等于工坊上传成功；须确认 SteamCMD 的上传成功结果，并检查
  <https://steamcommunity.com/sharedfiles/filedetails/?id=3828971953>。
- 验证订阅版时，先把本地 `Mods/BestDeal/` 移到 `Mods/` 之外，否则同 ID 的本地副本会覆盖工坊副本。
  重启游戏、启用订阅版并按游戏要求再次重启；测试完成后可恢复本地目录。
- 本地 VDF 属于发布配置，另行备份，不覆盖已有条目 ID。`docs/workshop.md` 中“尚未上传”等
  初次准备记录可能过时，判断现状应核对本地 VDF 和实际上传结果。
