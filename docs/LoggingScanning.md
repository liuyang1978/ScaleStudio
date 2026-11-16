# Logging 与 Scanning

Logging 为独立 net8.0 库，支持六等级、最低等级过滤、后台单消费者队列、yyyy/MM/dd 目录和日/小时文件。调用 DisposeAsync 完成队列并等待全部写入；文件写入或清理失败会通过该任务传播。队列当前无容量限制，持续高流量需评估内存。清理按文件名日期，在有日志写入的每个新日期执行，只删除可识别的过期 .log 文件。没有独立 FlushAsync。

Scanning 为独立 net8.0 库。Feed(deviceId, text) 接受平台已转换的字符；设备缓存独立，支持多字符结束符、输入间隔超时、定时清理、最大长度保护、设备移除及 ScanReceived 事件。事件在调用线程触发，UI 使用者应切换至 UI 线程。

WindowsRawInput 提供注册、注销和 WM_INPUT 解析入口，宿主需转交消息并提供键盘布局转换。该适配器尚未接入 Avalonia 原生窗口消息，也未经硬件验证。Raw Input 不会拦截 Enter/Space 等普通窗口键盘消息。UI 必须另设显式扫码捕获模式，在隧道路由处理 KeyDown/TextInput 并标记 Handled；不能根据 Raw Input 识别结果声称阻止所有控件操作。桌面主窗口提供 F8 切换捕获模式，捕获期间拦截 KeyDown/TextInput，所有键盘合并为 keyboard 设备；该模式需在扫码前显式开启，仍需实机验证。

Android USB Host 与 HID POS **未实现**，没有设备报告解析支持；不可作为已支持平台宣传。

主工程当前创建日志器与扫码核心，记录启动、扫码事件及桌面关闭；桌面关闭等待日志队列排空。移动端生命周期刷新、实际扫码输入/UI 显示仍待集成。

验证命令：

    dotnet test tests/ScaleStudio.Tests/ScaleStudio.Tests.csproj
    dotnet build ScaleStudio.Desktop.sln
    dotnet build ScaleStudio.sln

完整解决方案需要 Android 工作负载。

