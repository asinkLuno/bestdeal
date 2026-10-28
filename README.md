# BestDeal — Probably Stolen 顾客推荐 mod

游戏：Probably Stolen 正式版（Steam AppID 4349200 的 Demo 时代已过去；现在基于 Steam AppID 4348910、
Unity Mono 后端）。使用游戏**原生 mod 加载器**（`Mods/` 文件夹 + `manifest.xml` + `IMod`），
不再需要 BepInEx。

## 做什么

顾客走到柜台开始对话后，自动算出**这位顾客会买哪些东西、怎么卖最划算**，把结果列在屏幕左上角：

- 顾客是谁、预算多少、想买哪些类别（要求"不重复"的顾客会标出来）
- 推荐拿上柜台的物品清单：每件物品从哪个库存拿、**游戏给这位顾客的售价**、合计收多少钱
- 显示的价格就是游戏自己的成交价，照着把东西拿上柜台即可
- 已经摆上柜台的推荐物品会标 ✔（同款有多件时，柜台上**任意一件**同款都算已就位）；赝品点会标 ⚠ 并给出**游戏自己的本地化理由**
  （直接复用顾客识破赝品时的对话文本，跟随游戏语言；已被识破的会注明）
- **鉴定估价提示**：鉴定顾客上门、你把物品拿到检查台弹出估价面板时，单独一框显示物品的**真实价值**
  （游戏自己的判定值，照抄就是完美评级）和当前输入的评级（评级公式与阈值同游戏，名称复用游戏自己的
  `appraisal_quality_*` 本地化表）。报价离谱会被拒并大幅消耗顾客耐心，两次就翻脸走人——照抄一次搞定
- **开店前行情提示**：还没开店时显示当日新闻的涨跌价（读游戏自己的 StoreEvent，涨跌幅就是游戏实际
  使用的百分比）：**涨价**类别会扫一遍库存，有现货就提示"摆橱窗：xxx ×n"，没有现货则提示改摆
  **库存里最多的种类**；**降价**类别提示把对应类别的**收购牌**挂上橱窗（水/食物/医疗/材料/家居/
  奢侈品/武器，名称用游戏自己的本地化）。一开 shutter 营业这个面板就自动消失，换回顾客推荐清单

读价不需要搬动物品：游戏只在物品摆上柜台时才把顾客的加价/折扣特征挂上去算出最终价，mod 在内存里
复刻这条游戏自己的挂特征链路（挂上 → 读 `GetNegociatedValue` → 按快照还原），物品根本不会被搬动。

推荐清单会在顾客到店、需求或柜台内容变化时自动更新，并始终显示。

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
3. 按 **F8** 打开调试控制台，能看到 `[BestDeal 最佳组合报价]: BestDeal v2.6.0 已加载`。
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
