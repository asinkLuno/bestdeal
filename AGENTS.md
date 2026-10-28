# AGENTS.md

## 官方 modding 文档

- 游戏官方 mod 编写指南（GitHub 仓库）：<https://github.com/dragonpan2/probably-stolen-modding>
  - 含中文版 ModdingGuide。原生 mod 加载器（`Mods/` 文件夹 + `manifest.xml` + `IMod`）、
    Harmony 补丁约定等权威说明以这个仓库为准。
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
  编译检查优先使用 `-p:Deploy=false`，避免整理代码时覆盖正在使用的 mod。
