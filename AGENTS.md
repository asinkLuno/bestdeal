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
