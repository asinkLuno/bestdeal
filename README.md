# BestDeal — Probably Stolen Demo 顾客推荐 mod

游戏：Probably Stolen（Demo，Steam AppID 4349200 / Unity IL2CPP）。BepInEx 6 插件。

## 做什么

顾客走到柜台开始对话后，自动算出**这位顾客会买哪些东西、怎么卖最划算**，把结果列在屏幕左上角：

- 顾客是谁、预算多少、想买哪些类别（要求"不重复"的顾客会标出来）
- 推荐拿上柜台的物品清单：每件物品从哪个库存拿、**游戏给这位顾客的售价**、合计收多少钱
- 显示的价格就是游戏自己的成交价，照着把东西拿上柜台即可

读价时物品会短暂出现在后柜台上，读完立刻自动放回原处，不会改变你的库存归属。

热键：**F8** 重算 · **F9** 显示/隐藏清单。

## 构建

```bash
dotnet build BestDeal -c Release                   # 构建并自动部署到游戏 BepInEx/plugins/
dotnet build BestDeal -c Release -p:Deploy=false   # 只构建不部署
```

需要 .NET 6 SDK。默认游戏路径 `/home/guozr/.local/share/Steam/steamapps/common/Probably Stolen Demo`，
可用 `-p:GameDir=...` 覆盖。
