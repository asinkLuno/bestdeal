# BestDeal — Probably Stolen Demo 顾客推荐 mod

游戏：Probably Stolen（Demo，Steam AppID 4349200 / Unity IL2CPP）。MelonLoader mod。

> **装 mod 的看 [INSTALL.md](INSTALL.md)**（使用者视角：装 MelonLoader + 放 dll + 排错）。
> 本文件其余部分是开发说明。

## 做什么

顾客走到柜台开始对话后，自动算出**这位顾客会买哪些东西、怎么卖最划算**，把结果列在屏幕左上角：

- 顾客是谁、预算多少、想买哪些类别（要求"不重复"的顾客会标出来）
- 推荐拿上柜台的物品清单：每件物品从哪个库存拿、**游戏给这位顾客的售价**、合计收多少钱
- 显示的价格就是游戏自己的成交价，照着把东西拿上柜台即可

读价时物品会短暂出现在后柜台上，读完立刻自动放回原处，不会改变你的库存归属。

推荐清单会在顾客到店、需求或探价结果变化时自动更新，并始终显示。

## 构建

前置：游戏里已装 MelonLoader 0.7.x，并且**启动过一次游戏**（生成 `MelonLoader/Il2CppAssemblies/`，构建要引用它）。

```bash
dotnet build BestDeal -c Release                   # 构建并自动部署到游戏 Mods/
dotnet build BestDeal -c Release -p:Deploy=false   # 只构建不部署
```

需要 .NET 6 SDK。默认游戏路径 `/home/guozr/.local/share/Steam/steamapps/common/Probably Stolen Demo`，
可用 `-p:GameDir=...` 覆盖。

坑（都踩过）：

- MelonLoader 生成的代理程序集把游戏类型放在 **`Il2Cpp` 命名空间**（BepInEx 版是全局命名空间），
  所以源码里有 `using Il2Cpp;`；Unity 类型仍在 `UnityEngine`。
- MelonLoader 的**自动**打补丁等价于 `Harmony.PatchAll(Assembly)`，要求补丁类带类级 `[HarmonyPatch]`。
  本 mod 的补丁类只有方法级注解 → 自动打补丁会**静默一个都不打**，症状是 HUD 完全不出现。
  所以文件头有 `[assembly: HarmonyDontPatchAll]`，改在 `OnInitializeMelon` 里显式
  `TryPatchAll(typeof(Patches), true)`（即 BepInEx 版语义）；启动日志会报补丁数，掉了就是没打上。
