# BestDeal — Probably Stolen 顾客推荐 mod

游戏：Probably Stolen 正式版（Steam AppID 4349200 的 Demo 时代已过去；现在基于 Steam AppID 4348910、
Unity Mono 后端）。使用游戏**原生 mod 加载器**（`Mods/` 文件夹 + `manifest.xml` + `IMod`），
不再需要 BepInEx。

## 做什么

顾客走到柜台开始对话后，自动算出**这位顾客会买哪些东西、怎么卖最划算**，把结果列在屏幕左上角：

- 顾客是谁、预算多少、想买哪些类别（要求"不重复"的顾客会标出来）
- 推荐拿上柜台的物品清单：每件物品从哪个库存拿、**游戏给这位顾客的售价**、合计收多少钱
- 显示的价格就是游戏自己的成交价，照着把东西拿上柜台即可

读价时物品会短暂出现在后柜台上，读完立刻自动放回原处，不会改变你的库存归属。

推荐清单会在顾客到店、需求或探价结果变化时自动更新，并始终显示。

## 构建

```bash
dotnet build BestDeal -c Release                   # 构建并自动部署到游戏 Mods/BestDeal/
dotnet build BestDeal -c Release -p:Deploy=false   # 只构建不部署
```

需要 .NET SDK（6.0+）。默认游戏路径 `/run/media/deck/SR256/steamapps/common/Probably Stolen`，
可用 `-p:GameDir=...` 覆盖。构建会同时把 `BestDeal.dll` 和 `manifest.xml` 拷到游戏的
`Mods/BestDeal/` 目录。

## 启用

1. 启动游戏，主菜单打开 **模组** 菜单，选中 BestDeal 点 **启用**，重启游戏生效。
2. 本地反复改代码迭代时，在模组详情面板打开 **开发者模式**，之后每次重新编译重启即可直接加载，
   不用重新审批。
3. 按 **F8** 打开调试控制台，能看到 `[BestDeal 最佳组合报价]: BestDeal v2.1.0 已加载`。
4. 有疑问可在 F8 控制台跑 `mod-scan best_deal` 自查能力扫描结果（本 mod 清单里
   `<Capabilities />` 为空，目标是零能力）。

## 移植说明（v1.x BepInEx → v2.0 原生 mod）

- 游戏从 Demo（IL2CPP）换成正式版（Mono），`Mods/` 原生加载器取代了 BepInEx 6 注入。
- 入口从 `BasePlugin` 换成 `IMod`（Init/OnEnable/OnDisable），HUD 用普通 `MonoBehaviour`。
- Harmony 补丁由游戏加载器自动应用（类上加 `[HarmonyPatch]` 即可），Harmony 用游戏自带的
  `Managed/0Harmony.dll`。
- 旧版为绕过 IL2CPP 的反射工具类已删除：正式版 API 全部是 public 的 C# 类型，直接引用即可
  （`StoreClient`、`EmporiumEntry.backInvinvElement` 等，签名以反编译为准）。

官方 modding 文档：<https://github.com/dragonpan2/probably-stolen-modding>（含中文版 ModdingGuide）。
