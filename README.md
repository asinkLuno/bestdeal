# BestDeal

Probably Stolen 顾客推荐 mod，适用于正式版 Unity Mono（Steam AppID 4348910）。使用游戏原生 mod 加载器和游戏自带的 Harmony。

- **顾客报价**：按需求、预算和不重复约束推荐售卖组合，显示库存位置、成交价及柜台就位标记。
- **真伪提醒**：放大镜检查界面显示香烟、票券等物品的实际状态及标称是否一致，并逐条列出游戏记录的赝品依据（字样、颜色、封条等）；无可读取状态时明确提示无法判断。推荐物品另显示赝品问题，理由使用游戏本地化文本。
- **鉴定提示**：检查台估价面板显示真实价值及当前输入的评级。
- **开店前行情**：按当日新闻提示涨跌类别、适合摆橱窗的库存和收购牌。

价格调用游戏逻辑计算，物品不会被搬动。容器内部子物品的零售加价、大预算算法精度等边界见[系统设计](docs/design.md)。

## 构建与安装

需要 .NET 8 SDK（开发工具与算法检查使用 net8.0；mod 目标框架为 netstandard2.1），以及本机安装的游戏。

```bash
dotnet build BestDeal -c Release -p:Deploy=false  # 仅构建
dotnet build BestDeal -c Release                 # 构建并部署
# 覆盖默认游戏路径（路径有空格时保留引号）
dotnet build BestDeal -c Release -p:GameDir="/path/to/Probably Stolen" -p:Deploy=false
```

默认游戏目录：`/run/media/deck/SR256/steamapps/common/Probably Stolen`。
部署只复制 `BestDeal.dll` 和 `manifest.xml` 至 `Mods/BestDeal/`，不附带游戏或 Harmony 程序集。

启动游戏 → 主菜单「模组」→ 启用 BestDeal → 重启。开发时可在模组详情开启开发者模式。
F8 控制台可查看加载日志，运行 `mod-scan best_deal` 检查能力扫描；清单声明的能力为空。

## 开发导航

- [系统设计与已知边界](docs/design.md)：模块职责、运行时序、算法与游戏适配约束。
- [维护与验证](docs/maintenance.md)：修改入口、游戏升级、回归检查和发布步骤。
- [创意工坊发布](docs/workshop.md)：SteamCMD 配置、隐藏测试及后续更新。
- [诊断工具](tools/README.md)：查看游戏类型签名与 IL。
- [官方 modding 指南](https://github.com/dragonpan2/probably-stolen-modding)：原生加载器和 Harmony 约定；游戏签名以本机 `Assembly-CSharp.dll` 为准。
