# Getting started / 本地运行

Requires Visual Studio 2022 with .NET 8 SDK. Android requires the .NET 8 Android workload, SDK 34 and JDK.

```powershell
git clone https://github.com/liuyang1978/ScaleStudio.git
cd ScaleStudio
dotnet restore ScaleStudio.Desktop.sln
dotnet build ScaleStudio.Desktop.sln
dotnet test ScaleStudio.Desktop.sln
dotnet run --project src/ScaleStudio.Desktop
```

Use ScaleStudio.Desktop.sln if Android workload is not installed.

For Android:
```powershell
dotnet workload install android
dotnet build src/ScaleStudio.Android
```

Open ScaleStudio.sln to work on all platforms.

The UI starts disconnected. Select simulation, start weighing, save a reading and view recent records. Settings are explicitly saved/loaded; the UI defaults to INI. The SQLite configuration backend is available as a library. Desktop serial defaults to 9600 8N1. Change the COM name and regex as needed. Device-specific baud rate, delimiter, poll command, timeout, Android permissions and hardware integration are not yet exposed in the UI.
