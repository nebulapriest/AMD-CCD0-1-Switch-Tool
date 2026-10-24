# CorePilot

CorePilot 是一款面向 Windows 的 AMD 多 CCD 程序调度工具。它可以把程序及其子进程绑定到指定 CCD 或逻辑处理器，设置进程优先级，并在后台持续应用已保存的规则。

## 功能

- 自动发现 AMD 处理器拓扑，并交叉核对 CPUID、Windows CPU Sets、物理核心和 L3 缓存信息。
- 为程序组保存 CCD、全部 CPU、自定义核心和任务优先级规则，可选择继承到子进程。
- 为没有专属规则的普通程序设置默认 CCD；遇到未知型号或不一致拓扑时停止猜测并要求人工核对。
- 支持托盘后台运行、Windows 计划任务登录启动，以及暂停、退出时恢复此前保存的进程设置。
- 窗口右上角关闭会留在托盘；使用“退出并恢复”按钮或托盘菜单退出。若仍有进程待恢复，程序每 5 秒重试，恢复完成后自动退出，也可取消退出继续托盘运行。
- 每 5 秒自动检查一次程序状态；运行页提供“立即刷新”按钮。刷新期间按钮会暂时禁用，避免重复扫描。
- 活动记录仅在打开页面时读取最近记录，避免后台持续扫描日志。

## 使用与安全

支持 Windows 10 和 Windows 11 x64。应用清单包含微软用于 Windows 10/11 的兼容性标识；CPU Sets 与系统处理器拓扑接口均由 Windows 10 及之后版本提供。亲和性控制需要足够权限；建议使用随包提供的 `Install-CorePilot.ps1` 安装到受保护的 `Program Files\\CorePilot` 目录，再由 Windows 计划任务以最高权限在登录后静默启动。安装脚本会收紧目录 ACL；程序拒绝从普通用户可写目录启用高权限自启。关闭窗口会缩到托盘；需要结束后台程序时，点击窗口中的“退出并恢复”或使用托盘菜单。恢复暂未完成时，程序会持续重试并在恢复完成后自动退出。

CorePilot 不会关闭 Windows 核心调度器。受保护的系统进程、内核、驱动和中断仍由 Windows 管理；当前执行器仅支持一个 Windows 处理器组。硬件 CCD 顺序不保证与 BIOS 标签一致。仅在型号和拓扑校验通过，或用户完成手动核对后才允许按 CCD 绑定。性能收益依工作负载和系统配置而异。

## 构建

主界面和定位工具使用 Windows x64 与 .NET Framework 4.x 编译器：

```bat
Source\\Build.cmd
```

拓扑检测器使用 .NET 10 SDK 构建为自包含 x64 程序：

```powershell
dotnet publish Source/TopologyProbe/TopologyProbe.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o TopologyRuntime
```

构建结果应放在项目根目录的 `CorePilot.exe`、`CorePilot.Locator.exe` 和 `TopologyRuntime` 中。不要将个人配置、活动记录或本机验证映射打入发行包；详细步骤见 [源码构建说明](Source/构建说明.txt)。

## 开源项目与依赖

| 项目 | 用途 | 许可证 / 使用情况 |
| --- | --- | --- |
| [.NET Runtime](https://github.com/dotnet/runtime) | 自包含拓扑检测器使用 .NET 运行时和基础类库 | MIT；发布版本应保留对应运行时的许可与第三方声明 |
| [.NET Windows Forms](https://github.com/dotnet/winforms) | 主程序基于 Windows Forms 桌面 UI 技术 | MIT；主程序调用 Windows/.NET Framework 自带的 WinForms API，没有复制其仓库源码 |

项目没有第三方 NuGet 包依赖。主程序使用 Windows/.NET Framework 自带的 WinForms、System.Drawing 与 Windows API；拓扑检测器通过 .NET 10 SDK 的框架引用构建。发行文件中的 .NET 运行时遵循其随包提供的许可和声明。

## 官方资料与处理器目录

`Reference/AMD-processors.xml` 是 AMD 官方处理器规格目录的快照，来源和抓取日期记录在 `Reference/sources.json`。AMD 的公开规格表没有完整列出每个型号的 CCD 数量；CorePilot 只将它作为型号参照，兼容名单与拓扑推断属于本项目的保守判断。

拓扑实现参考 [AMD64 架构程序员手册](https://www.amd.com/en/search/documentation/hub.html) 和微软的 [GetLogicalProcessorInformationEx](https://learn.microsoft.com/windows/win32/api/sysinfoapi/nf-sysinfoapi-getlogicalprocessorinformationex)、[GetSystemCpuSetInformation](https://learn.microsoft.com/windows/win32/api/winbase/nf-winbase-getsystemcpusetinformation) 文档。

## 许可证

本项目按 MIT License 发布，见 [LICENSE](LICENSE)。第三方运行时和框架保留各自的版权与许可声明。
