# 标签、小票、发票打印模块

四个独立 net8.0 SDK 工程：Printing 为公共模板、JSON 持久化、验证、SVG 渲染和 IPrintTransport 接口；Labels、Receipts、Invoices 分别提供标签、小票、发票默认模板。核心不依赖 Avalonia，可被其他程序嵌入。

主界面底部提供模板 JSON 编辑器，支持新建、保存、加载、变量预览与 SVG 导出。所有尺寸以毫米表示，支持纸张尺寸、文字位置/字体/字号/旋转、线及矩形。变量通过字典严格绑定，缺失变量报错；SVG 转义文本。发票四边距定义可打印区域，通过裁剪避开孔区。

PrinterName 随模板保存。PrintJob 要求明确选择打印机，由宿主提供 IPrintTransport；当前没有 Windows/Android 原生打印实现，不会自动发送实体打印任务。SVG 可在浏览器中预览并手动按实际尺寸打印，打印驱动缩放需自行校准。

当前为第一版基础实现，**尚未实现**拖拽设计、条码、二维码、原生打印预览/打印机枚举、连续小票自动分页、独立设计器程序。发票模块只负责布局/套打，不提供税务开票能力。未进行打印机硬件验证。

验证：dotnet test tests/ScaleStudio.Tests/ScaleStudio.Tests.csproj；dotnet build ScaleStudio.Desktop.sln。
