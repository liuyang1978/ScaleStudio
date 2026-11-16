# 标签、小票、发票设计与打印

Printing 核心、Labels、Receipts、Invoices 为独立 net8.0 SDK；共享 UI 在 App，Windows 打印适配器在 Desktop。模板为版本化 JSON，使用毫米；支持文字、字体、字号、旋转、变量、线、矩形、Code 128、QR Code。二维码保持正方形，编码库 ZXing.Net 0.16.11（Apache-2.0）。发票四边距裁剪可打印区域以避开孔区；仅提供套打布局，不提供税务开票服务。

## 使用

1. 在主工程底部进入设计器，或运行独立入口：

       dotnet run --project src/ScaleStudio.PrintDesigner

   Desktop 也支持 --designer 参数。
2. 新建标签、小票或发票模板。添加元素，点击选中并拖拽；属性栏调整位置、尺寸、字体、旋转和变量。纸张宽高、发票边距在模板 JSON 编辑，随后点“应用模板 / 更新画布”。
3. 变量框填写 JSON 字典，例如 {"Weight":"12.50 kg","Barcode":"12345678"}。缺少变量会阻止预览/打印。保存和加载模板使用指定路径；加载或新建后更新画布。SVG 导出、画布预览及打印使用同一个变量字典。
4. 小票明细按行输入，点击“小票分页”，按 80 mm 宽、200 mm 最大页高进行固定字号换行分页并重复页眉；打印预览与打印处理全部页。“退出分页”恢复单模板。分页模式的 JSON 保存/SVG 导出仅针对当前第一页，明细需另行保存。字宽按每字最多 4 mm 保守估算，特殊字体仍需预览校验。
5. Windows 点击“刷新打印机”，选择打印机；“关联打印机”将名称写入 JSON，随后保存。预览显示 300 DPI 输出图像。点击发送后需再次确认打印机与页数。

Windows 使用 System.Drawing.Printing.PrintDocument，按模板毫米尺寸设置自定义纸张，补偿设备硬边距，不执行自动缩放；驱动拒绝纸张尺寸会报错。驱动支持的纸型、硬边距与实物套打偏移仍须现场校准。打印成功表示提交到系统打印队列，不能确认纸张已经输出。二维码/条码的物理模块大小应按扫码设备与打印分辨率验证。

Android/macOS/Linux 当前可以使用核心模板与 SVG 导出，没有原生打印适配器。300 DPI 单页及分页总量限制 4000 万像素，超出需减小纸张或分批打印；未做实体打印机验证。SDK IPrintTransport 保留 SVG 传输扩展，实际 Windows 路径使用 IPagePrinter 传输预览同源 PNG 页。

## 验证

    dotnet build ScaleStudio.Desktop.sln -m:1 -p:UseSharedCompilation=false -p:NuGetAudit=false
    dotnet test tests/ScaleStudio.Tests/ScaleStudio.Tests.csproj
    dotnet run --project tests/ScaleStudio.Printing.Checks -p:NuGetAudit=false

本机标准 vstest 因 testhost 连接超时中止；补充检查入口直接执行同一组打印断言（含编码后解码、中文 QR、分页与 JSON 校验），不等同于完整测试套件通过。NuGetAudit=false 只用于本机离线验证，不在工程中关闭漏洞检查。

已实际打开独立设计器，检查二维码、拖拽坐标更新、打印预览及打印机刷新。实体打印及驱动纸张适配尚需硬件验证。
