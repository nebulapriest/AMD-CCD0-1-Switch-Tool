拓扑检测器使用 .NET 10 自包含 x64 发布，不要求目标机器安装 .NET。

运行顺序：读 AMD CPUID 品牌与逐逻辑处理器 CPUID 8000001E/8000001D；通过 SetThreadGroupAffinity 固定检测线程；再将处理器组、逻辑编号、物理核心和末级缓存与 GetSystemCpuSetInformation、GetLogicalProcessorInformationEx 的 Windows 记录逐项比较。

型号参照来自 Reference/AMD-processors.xml，对支持单个 CCD 对应单个 L3 的芯片系列作严格名单判断。APU、旧款多 CCX CCD、虚拟机、未知型号、混合不匹配的拓扑都停止自动推断。CCD 编号按硬件 L3 域的 CPUID 排序生成，不等于已验证的 BIOS 标签。超过一个 Windows 处理器组时，当前亲和性应用模块会暂停。

拓扑关系资料：AMD AMD64 Architecture Programmer’s Manual Volume 2，以及微软 GetLogicalProcessorInformationEx / CACHE_RELATIONSHIP。详见程序 Reference/sources.json。
