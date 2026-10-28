# Steam 创意工坊发布

依据[官方中文版 ModdingGuide：Steam 创意工坊](https://github.com/dragonpan2/probably-stolen-modding/blob/main/ModdingGuide.zh-CN.md#steam-创意工坊)。2026-10-29 核对：正式版 AppID 为 `4348910`，游戏没有上传按钮，通过 SteamCMD 发布；需要拥有游戏的 Steam 账号。试玩版和 playtest 不支持创意工坊。

## 首次上传

1. 安装 [SteamCMD](https://developer.valvesoftware.com/wiki/SteamCMD)。当前开发机的 PATH 中未找到 `steamcmd`。
2. 构建部署：`dotnet build BestDeal -c Release`。上传内容目录根部须直接包含 `manifest.xml` 与 `BestDeal.dll`，不要上传源码仓库或整个 `bin` 目录。
3. 游戏内跑 `mod-scan best_deal` 并检查主要功能。当前清单声明支持 `0.46D`；只填写实际验证过的游戏版本。
4. 准备小于 1 MB 的封面，建议命名 `preview.png`。当前仓库尚无封面。
5. 复制配置示例并按需修改：

   ```bash
   cp workshop/BestDeal.vdf.example workshop/BestDeal.vdf
   ```

   示例的内容路径已指向本机部署目录。准备好封面后取消 `previewfile` 行注释并填写绝对路径。`visibility=2` 是隐藏，先用于上传测试；`0` 是公开，`1` 是仅好友。标题和描述均为可编辑草稿。
6. 执行上传，按提示登录及完成 Steam Guard 验证（不要把密码写进配置或命令）：

   ```bash
   steamcmd +login YOUR_STEAM_ACCOUNT +workshop_build_item "/home/deck/CODE/bestdeal/workshop/BestDeal.vdf" +quit
   ```

7. SteamCMD 会打印物品 ID，并写回 `publishedfileid`。保留这份 VDF，首次还需按 Steam 页面提示接受创意工坊法律协议。
8. 打开该物品页面，检查描述、封面与文件，订阅测试后再通过页面改为公开。

订阅测试时，同 `<ID>` 的本地 `Mods/BestDeal` 会覆盖工坊副本。要真正验证订阅版，先把本地目录移到 `Mods/` 之外，再重启游戏、启用订阅版并再次重启；测试完成后可移回本地目录。

## 后续更新

- 同步 manifest 和加载日志版本，完成构建、扫描及受影响功能的游戏内验收。
- 修改已有 VDF 的 `changenote`，继续使用 Steam 写回的 `publishedfileid`；不要重新复制 ID 为 `0` 的模板，否则会创建另一件物品。
- 检查已有物品的可见性：示例为隐藏，若使用 VDF 管理公开版本，应设置 `visibility=0`。
- 运行相同上传命令；订阅者会收到更新。
- VDF 不含密码，但属于该发布实例的本地配置，已加入忽略列表。请另行备份物品 ID 和 VDF。

当前只准备了配置草稿与流程，尚未上传或创建工坊物品。部署成功也不代表已完成游戏内验收。
