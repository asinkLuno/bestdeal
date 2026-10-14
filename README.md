# skdp — Probably Stolen Demo mod 工程

游戏：Probably Stolen Demo（Unity 2021.3.45f2 / IL2CPP / Steam AppID 4349200 / Linux+Proton）

## 布局
- `BestDeal/` — BepInEx 6 IL2CPP 插件：顾客上门读取需求（类别/预算），算出预算内最佳售卖组合并显示在 HUD（F8 重算 / F9 显隐）。
  **价格一律来自游戏自己**：把候选物品逐件快速摆上柜台（游戏这时才会按当前顾客算出最终成交价）读走价格再立刻收回，详见下面「逆向结论」
- `tools/dump_types.py` — .NET 元数据 dumper（venv: `.venv/bin/python -I tools/dump_types.py <dll> <pattern>`）

## 构建
```bash
dotnet build BestDeal -c Release        # 构建并自动部署到游戏 BepInEx/plugins/
dotnet build BestDeal -c Release -p:Deploy=false   # 只构建不部署
```

## 游戏侧路径
- 游戏根：`/home/guozr/.local/share/Steam/steamapps/common/Probably Stolen Demo/`
- 日志：`BepInEx/LogOutput.log`（游戏退出后会完整落盘；运行中看 compatdata 下的 Player.log）
- 存档：`compatdata/4349200/pfx/.../LocalLow/Questing Goose Studio/Probably Stolen/`
- winhttp 覆盖已写入该 prefix 的 `user.reg`（备份 `user.reg.bak-skdp`）

## 逆向结论（关键 hook 点）
- `StoreClient` — 顾客：`clientBuyingTagList/IdList`（类别）、`clientBlack*`、`clientBudget/clientCash`、
  `buyPriceModifier`、`IsClientBuyingThisItem(GameItem)`、`HasBudgetLeftToBuy()`、`OnItemPlaced/OnItemSold/OnDealAccepted`
- **需求是到店之后才填的**：`StartMainDialogue` 那一刻 `clientBuyingTagList` 为空、`isRequireVariety=False`，
  几秒后才有值 → 必须轮询需求指纹并刷新 HUD，否则类别行一直显示「无限制」。
- **价格只在物品被摆给当前顾客看时才算**（`GameItem.GetNegociatedValue()`：背包里=原值 55，
  摆上柜台=最终价 64/37，等于游戏实付）；`GetRefreshedValue/GetBaseValue/OnAddedToWeightedArea` 等
  "零搬运重算"都不可靠（实测 6 件只 1 件变化且值不对），所以只能真摆一下再收回。
- **价格数值示例（实测）**：净水药片 37、试剂盒 21、空瓶装水 8/4/4、废金属 12、垃圾 3、螺丝 16、
  熔炉 50、模组 18；游戏按"每个节点各自加成/折扣并各自舍入"算（瓶身 1 + 9 片药片×7 = 64）。
- **顾客特性**：`clientItemFeatureBuying` 是**特性 id 字符串**列表（商人 `["discountedBuy"]`、
  多样化顾客 `["varietyBuyerBonus"]`），对应 `ItemFeatureList.<Id>()` 工厂。
- **要求不重复**：`isRequireVariety`（到店后才置位）+ `IsItemInVarietyList(item)` + `varietyTrackingList`；
  摆上柜台会让顾客"见过"物品 → 探测一轮结束后要把因此新增的记录删掉。
- **中文名是现成的**：`LocHelper.GetLocalizedMechanic("type_household_good")` → 日用品、
  `GetLocalizedItem("item_scrap_metal_name")` → 废金属（本地化表也能用 UnityPy 从
  `StreamingAssets/aa/**/localization-string-tables-chinese(simplified)(zh)_assets_all.bundle` 抽出来）。
- `GameItem` — `identifier/uniqueId/name/unitCount/unitValue`；物品类型 `GetGameItemType()`（HOUSEHOLD_GOOD/WATER/…）
- `GameInventory.childItems` — 库存物品枚举；`EmporiumEntry.Instance` — 店内所有柜台/展示位（后柜台 identifier=`backInv`）
- 存档是明文 JSON（Easy Save 3，`compatdata/.../Probably Stolen/save_8.es3`），可离线查店内每件物品与标签；
  游戏自带 `GameLog_*.txt` 有 `tracked price`、`Display Case` 等行
- `StoreRequest` — 派系订单板（另一套系统）
- 官方 ModLoader 在 Demo 被禁用（Player.log: `[ModLoader] Limited build: mod loading disabled.`）

## 版本
- v1.4.1 **修复探测一步都没跑**：v1.4.0 里 `CollectCandidates` 复用 `Collect`，而 `Collect` 只收"已探到价"的物品
  → 探测队列永远为空、一件也没摆上柜台（先有鸡还是先有蛋）。现在 `Collect(c, requireVariety, needPrice:)`，
  探测排队用 `needPrice:false`（不要求已探到价），HUD 推荐仍只认探到真价的物品。
  另外：轮询改由 HUD 自己的 `Update` 驱动（不再只靠游戏的 `StoreClientCounter.Update`），
  HUD 显示探测进度（`价格探测中 N/M`），找不到后柜台时告警并留待重试。
- v1.4.0 **价格改为直读游戏**：新增 `PriceProbe`——顾客到店、需求填好后，把候选物品（按游戏估值从高到低，≤16 件）
  逐件快速摆上柜台（每件停 0.25 秒）读走游戏算好的最终价、立刻收回（源库存取物品自己的 `parentInventory`，
  每步校验指针；被顾客买走就不碰；收回失败即停手告警；顾客离场先收回）。HUD 只推荐"探到真价"的物品。
  删除此前自算价格的全部代码（加价公式/银行家舍入/容器逐层估值/顾客加成特性/`retailMarkup` 读取）。
  （历史：v1.1.0~v1.3.0 曾按"逐层加价 + 顾客特性加成"估算，虽然能对上简单物品，
  但面对商人折价、派系声望、容器内容物时反复出错，故整体废弃。）
- v1.0.1 **发布收尾：移除全部诊断能力**——删除成交对账守卫（OnItemBought/OnItemSold 钩子）与所有诊断日志
  （顾客/候选/组合/交易结束），HUD 去掉计价行；仅保留 HUD 功能与必要的失败告警
- v1.0.0 **清理调试代码**（移除现金差/工厂/特性 dump 等探针）；
  `GetNegociatedValue()` 从计价链路剔除（只返回未加价原值）
- v0.15.3 **整数定点计价**：售价 = 单价×(1000+加成×10)/1000 精确银行家舍入，修掉浮点误差
  （15×1.3 浮点算成 19.4999...96 被舍成 19，游戏实付 20）；多样化加成改读
  `VarietyBuyerBonus().GetCurrentValueMod()`=15（**实测**，`GetActualValueModifier` 对工厂对象返回 0）
- v0.15.1 **修复闪退**：撤销对 `GameItem.AddFeatureRelatedToBuyer`/`AddClientItemBuyingFeature` 的 patch
  （启动/物品初始化阶段被调用，postfix 里调 il2cpp 方法 → AccessViolation，见 BepInEx/ErrorLog.log）；
  特性观察移到成交钩子（已验证的安全时机），且只在未学到多样化数值时 dump 一次
- v0.15 现金差对账证实公式：普客 305→368（+63=17+17+29）、多样客 368→420（+52=20+32）。
  多样加成的真实值不在 `VarietyBuyerBonus()` 工厂对象里（读出 0）→ 改为挂钩 `GameItem.AddFeatureRelatedToBuyer`
  /`AddClientItemBuyingFeature`，物品被挂买方特性时读清单学习实际数值；取值链：特性实测→工厂→成交反推 15
  （带标注）。对账钩子改为双方向：`OnItemBought`=玩家卖货（主用途）、`OnItemSold`=顾客卖货
- v0.14 **售价 = 单价 × (1 + 零售加价% + 多样化卖家加成%)，Math.Round 银行家舍入**。多样化加成读
  `ItemFeatureList.VarietyBuyerBonus()`（ItemFeature.valueModifier），仅对 `isRequireVariety` 顾客叠加。
  数据验证：普客 15×1.15=17.25→17、25×1.15=28.75→29；要求不重复顾客 15×1.30=19.5→20、25×1.30=32.5→32
  （加法叠加，且只有银行家舍入能同时解释 19.5→20 和 32.5→32）
- v0.13 **售价 = 单价 × (1 + 零售加价)**：加价读游戏自己的 `PlayerStore.retailMarkup`，是否接受加价读
  `StoreClient.acceptRetailMarkup`。取证钩子：`OnItemSold(GameItem, int)` 的 int=实付金额，每次成交自动打
  `[成交] ... 实付=N 系数=... 预测=...` 可离线校验；`OnItemPlaced` 记录上柜时 neg 变化
- v0.12 删掉 1.16 兜底（本来就是蒙的）——`GetNegociatedValue()` 取不到价就跳过该物品不参与计算
- v0.11 价格改用游戏自己的 `GameItem.GetNegociatedValue()`（议价后单价，自动含声望/多样化加成——
  `ItemFeatureList.VarietyBuyerBonus` 证实变价机制）
- v0.10 砍掉「已在柜台」检测（多方案均不可靠，用户决定移除）；**重复商品约束**：读游戏自己的
  `StoreClient.isRequireVariety`（每顾客开关）与 `IsItemInVarietyList(item)`（游戏自身重复判定），
  要求不重复的顾客用分组背包每类限 1 件；已跟踪的同类直接跳过
- v0.9 柜台判定改用游戏权威 API `EmporiumEntry.GetCountersItems()`（放弃观察式学习——游戏成交/拖拽也走
  UncheckedAccept，学习目标会漂移）；扫描排除 sold/trash/drain/hidden/response/swap/afterhour 等非售卖区
  （修「买走的东西还算」）；CompleteBusiness 清顾客改用指针比较（interop 代理引用永不相同）
- v0.8 **只读模式**：彻底移除自动搬运（曾因学习目标漂移把 Expel+UncheckedAccept 打进临时库存导致物品丢失）；
  保留柜台识别（Ctrl+点击一次，仅识别）+ HUD「✓已在柜台 / →需拿上柜台」标记
- v0.7 顾客离场清 CurrentClient（防学习目标被顾客间的整理污染）；推荐区分柜台/库存
- v0.6 自动上架 v2（学习式）——已废弃：目标漂移导致丢物品
- v0.5 修背包回溯 bug（DP 对但只回溯出 1 件）；价格 = floor(unitValue×count×1.16)，系数可配置；
  候选按 il2cpp 指针去重
- v0.4 砍掉自动上架第一版；全店扫描无排除
- v0.3 自动上架 v1 + 学习钩子（验证了 UncheckedAccept 可 hook、拖拽多跳）
- v0.2 顾客上门自动计算 + 游戏内悬浮窗（F8 重算 / F9 显隐）
- v0.1 日志输出
