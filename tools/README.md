# 游戏 API 诊断工具

日常维护优先使用 `DumpCs`，输出本机 `Assembly-CSharp.dll` 的字段、方法签名，以及按需输出 IL。只用于诊断，不是 mod 的构建依赖，也不参与部署。

```bash
dotnet run --project tools/DumpCs -- "/path/to/Probably Stolen/Probably Stolen_Data/Managed" StoreClient
dotnet run --project tools/DumpCs -- "/path/to/Probably Stolen/Probably Stolen_Data/Managed" GameItem --il OnAddedToWeightedArea
```

依赖 .NET 8 SDK，程序集从传入的 Managed 目录解析。工具通过反射读取元数据和方法体，不调用游戏业务方法；IL 输出是辅助信息，复杂指令请用完整反编译器核对。

`dump_types.py` 是旧版元数据快速筛选脚本，保留用于不方便加载程序集时的类型名、字段名和方法名检索。依赖 Python 3 和 `dnfile`，不输出完整签名：

```bash
python3 -m venv .venv
.venv/bin/pip install dnfile
.venv/bin/python tools/dump_types.py "/path/to/Assembly-CSharp.dll" StoreClient
```

不要提交游戏 DLL、反编译全文或本地诊断输出。
