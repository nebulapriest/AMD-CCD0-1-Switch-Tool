验证测试源码

以下验证程序以 Windows x64 .NET Framework C# 编译器运行。在 CorePilot-3.2 目录执行：

csc /nologo /out:DiscoveryTests.exe /reference:CorePilot.exe Source\Tests\DiscoveryTests.cs
csc /nologo /out:TopologyVariantsTests.exe /reference:CorePilot.exe /reference:System.Xml.dll Source\Tests\TopologyVariantsTests.cs
csc /nologo /out:RefreshTests.exe /reference:CorePilot.exe /reference:System.Windows.Forms.dll /reference:System.Drawing.dll Source\Tests\RefreshTests.cs

依次运行三个 EXE。测试覆盖本机逐核拓扑交叉核对、模拟不同 CCD 数量与默认选择，以及 UI 列表高频刷新时行、选区、滚动锚点和动画状态的稳定性。临时配置数据写入系统临时目录。


高权限自启路径测试：
csc /nologo /out:StartupInstallTests.exe /reference:CorePilot.exe Source\Tests\StartupInstallTests.cs
StartupInstallTests.exe

