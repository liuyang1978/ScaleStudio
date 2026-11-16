# ScaleStudio 第一阶段

- .NET 8 / Avalonia 11.3.0；App 是共享 UI，Desktop 和 Android 为平台入口。
- App → Weighing、Configuration、Storage；Configuration → Storage。
- Weighing 不依赖 UI、数据库和平台 SDK。协议由 ProtocolOptions 控制，传输由 IWeightTransport 实现。
- 默认模拟设备；串口实现位于 Desktop。蓝牙、Android USB Host、USB HID 需后续编写真实平台适配器。
- 数据库逐操作打开连接，参数绑定 SQL；重量使用十进制字符串保存精度，排序按时间。
- 配置支持 INI（单节、转义值）和 SQLite。INI 只保证进程内并发写入安全，不适用于多进程共享写入。
- SQLite 的异步 ADO.NET 方法底层仍可能同步执行，UI 应从后台任务调用。
- 示例协议假设 ASCII、换行分帧，sg/wg 稳定含义仅为示例，必须按真实仪表手册配置。
- 不实现法定计量认证；稳定标识不等于经检定的称重结果。



Logging 与 Scanning 为独立 net8.0 模块，App 引用，详见 LoggingScanning.md。
