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
- `StoreClient` — 顾客：`clientBuyingTagList/IdList`（类别）、`clientBlack*`、`GetBudget()`/`clientBudget`、
  `IsClientBuyingThisItem(GameItem)`、`IsItemInVarietyList(GameItem)`、`isRequireVariety`、
  `GetTradeRepMultiplier()`、`CompleteBusiness()`（离场）。
  ⚠ `OnItemPlaced` 不会被触发，别指望它；成交钩子 `OnItemBought/OnItemSold/OnDealAccepted` 已被移除
  （v1.0.1 起不再需要，价格直接读游戏）
- **需求是到店之后才填的**：`StartMainDialogue` 那一刻 `clientBuyingTagList` 为空、`isRequireVariety=False`，
  几秒后才有值 → 必须轮询需求指纹并刷新 HUD，否则类别行一直显示「无限制」。
- **价格只在物品被摆给当前顾客看时才算**（`GameItem.GetNegociatedValue()`：背包里=原值 55，
  摆上柜台=最终价 64/37，等于游戏实付）；`GetRefreshedValue/GetBaseValue/OnAddedToWeightedArea` 等
  "零搬运重算"都不可靠（实测 6 件只 1 件变化且值不对），所以只能真摆一下再收回。
- **价格数值示例（实测）**：净水药片 37、试剂盒 21、空瓶装水 8/4/4、废金属 12、垃圾 3、螺丝 16、
  熔炉 50、模组 18；游戏按"每个节点各自加成/折扣并各自舍入"算（瓶身 1 + 9 片药片×7 = 64）。
- **顾客特性**：`clientItemFeatureBuying` 是**特性 id 字符串**列表（商人 `["discountedBuy"]`、
  多样化顾客 `["varietyBuyerBonus"]`）——只用来做需求指纹（价格已经由游戏自己算，不用管它是怎么加成的）。
- **要求不重复**：`isRequireVariety`（到店后才置位）+ `IsItemInVarietyList(item)` + `varietyTrackingList`；
  摆上柜台会让顾客"见过"物品 → 探测一轮结束后要把因此新增的记录删掉。
- **中文名是现成的**：`LocHelper.GetLocalizedMechanic("type_household_good")` → 日用品、
  `GetLocalizedItem("item_scrap_metal_name")` → 废金属（本地化表也能用 UnityPy 从
  `StreamingAssets/aa/**/localization-string-tables-chinese(simplified)(zh)_assets_all.bundle` 抽出来）。
- `GameItem` — `identifier/uniqueId/name/unitCount/unitValue`；物品类型 `GetGameItemType()`（HOUSEHOLD_GOOD/WATER/…）
- `GameInventory.childItems` — 库存物品枚举；`EmporiumEntry.Instance` — 店内所有柜台/展示位（后柜台 identifier=`backInv`）
- 存档是明文 JSON（Easy Save 3，`compatdata/.../Probably Stolen/save_<槽位>.es3`），可离线查店内每件物品与标签；
  游戏自带 `GameLog_*.txt` 有 `tracked price`、`Display Case` 等行
- `StoreRequest` — 派系订单板（另一套系统）
- 官方 ModLoader 在 Demo 被禁用（Player.log: `[ModLoader] Limited build: mod loading disabled.`）

## 版本
- v1.4.2 轮询只在店内场景跑：`EmporiumEntry.Instance` 为空（主菜单/读盘）就跳过扫描，修掉"每秒一条
  `EmporiumEntry.Instance 为空` 警告"；告警文案里也不再提 F8（探测早就自动化了）
- v1.4.1 **修复探测一步都没跑**：v1.4.0 里 `CollectCandidates` 复用 `Collect`，而 `Collect` 只收"已探到价"的物品
  → 探测队列永远为空、一件也没摆上柜台（先有鸡还是先有蛋）。现在 `Collect(c, requireVariety, needPrice:)`，
  探测排队用 `needPrice:false`（不要求已探到价），HUD 推荐仍只认探到真价的物品。
  另外：轮询改由 HUD 自己的 `Update` 驱动（不再只靠游戏的 `StoreClientCounter.Update`），
  HUD 显示探测进度（`价格探测中 N/M`），找不到后柜台时告警并留待重试。
- v1.4.0 **价格改为直读游戏**：新增 `PriceProbe`——顾客到店、需求填好后，把候选物品（按游戏估值从高到低，≤16 件）
  逐件快速摆上柜台（每件停 0.25 秒）读走游戏算好的最终价、立刻收回（源库存取物品自己的 `parentInventory`，
  每步校验指针；被顾客买走就不碰；收回失败即停手告警；顾客离场先收回）。HUD 只推荐"探到真价"的物品。
  删除此前自算价格的全部代码（加价公式/银行家舍入/容器逐层估值/顾客加成特性/`retailMarkup` 读取）。
- v1.3.0 及更早（**已全部废弃，勿再走**）：整条线是"自己算售价"——`unitValue×加价`、银行家舍入、
  容器逐层估值、`ItemFeatureList.VarietyBuyerBonus()` 加成、`retailMarkup`、按 `isRequireVariety` 叠 15%……
  简单物品能对上（15→17、25→29），遇到容器内容物（瓶身 1 vs 实际 64）、商人折价、派系声望时就错，
  已整体删除，细节见 git 历史。
- 另外两条死路（**勿再走**）：
  - **自动搬运**：学习式目标会漂移，曾把物品 `Expel` 进临时库存导致丢物品。现在只做"探测即收回"，
    源库存一律取物品自己的 `parentInventory`，每步校验指针，被顾客买走就不碰。
  - **多方案"已在柜台"检测**：都不靠谱，已移除。
  - **危险路径**：patch `GameItem.AddFeatureRelatedToBuyer` / `AddClientItemBuyingFeature`
    ——启动/物品初始化阶段就会被调用，postfix 里调 il2cpp 方法 → AccessViolation 闪退。
