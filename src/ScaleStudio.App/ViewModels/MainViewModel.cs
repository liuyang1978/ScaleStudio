using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Avalonia.Threading;
using ScaleStudio.Configuration;
using ScaleStudio.Storage;
using ScaleStudio.Weighing;

namespace ScaleStudio.App.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged, IAsyncDisposable
{
    private readonly WeightRecordStore _records;
    private readonly ConfigurationService _config;
    private readonly Func<string,IWeightTransport>? _serialFactory;
    private WeighingService? _service;
    private WeightReading? _reading;
    private bool _busy;
    private bool _disposed;
    private string _status="未连接", _port="COM1", _weight="--", _stable="未知";
    private string _device="模拟电子秤";
    private bool _poll;
    private string _pattern=new ProtocolOptions().Pattern;
    private string _history="";
    private readonly List<AsyncCommand> _commands = new();

    public MainViewModel(WeightRecordStore records, ConfigurationService config, Func<string,IWeightTransport>? serialFactory)
    {
        _records=records; _config=config; _serialFactory=serialFactory;
        Devices = serialFactory == null ? ["模拟电子秤"] : ["模拟电子秤","串口"];
        StartCommand=Command(StartAsync); StopCommand=Command(StopAsync);
        SaveCommand=Command(SaveAsync); SettingsCommand=Command(SaveSettingsAsync);
        LoadCommand=Command(LoadSettingsAsync); HistoryCommand=Command(HistoryAsync);
    }
    public event PropertyChangedEventHandler? PropertyChanged;
    public string[] Devices { get; }
    public string Device { get=>_device; set=>Set(ref _device,value); }
    public string Port { get=>_port; set=>Set(ref _port,value); }
    public bool Poll { get=>_poll; set=>Set(ref _poll,value); }
    public string Pattern { get=>_pattern; set=>Set(ref _pattern,value); }
    public string Status { get=>_status; private set=>Set(ref _status,value); }
    public string Weight { get=>_weight; private set=>Set(ref _weight,value); }
    public string Stable { get=>_stable; private set=>Set(ref _stable,value); }
    public string History { get=>_history; private set=>Set(ref _history,value); }

    public ICommand StartCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand SettingsCommand { get; }
    public ICommand LoadCommand { get; }
    public ICommand HistoryCommand { get; }

    private void Set<T>(ref T field,T value,[CallerMemberName] string? name=null)
    {
        if (EqualityComparer<T>.Default.Equals(field,value)) return;
        field=value; PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(name));
    }

    private ICommand Command(Func<Task> action)
    {
        var command=new AsyncCommand(async () =>
        {
            if (_busy || _disposed) return;
            _busy=true; Refresh();
            try { await action(); }
            catch(Exception ex) { Status=ex.Message; }
            finally { _busy=false; Refresh(); }
        },()=>!_busy&&!_disposed);
        _commands.Add(command); return command;
    }
    private void Refresh() { foreach(var c in _commands) c.Refresh(); }

    private async Task StartAsync()
    {
        await StopAsync();
        var port=Port;
        Func<IWeightTransport> create=Device=="串口" && _serialFactory!=null
            ? ()=>_serialFactory(port) : ()=>new SimulationTransport();
        var options=new ProtocolOptions { Pattern=Pattern, Mode=Poll?ProtocolMode.Poll:ProtocolMode.ReceiveOnly };
        var service=new WeighingService(create,options);
        _service=service;
        service.ReadingReceived += r=>Dispatcher.UIThread.Post(()=>
        {
            if (_service!=service) return;
            _reading=r; Weight=$"{r.Weight:F2} {r.Unit}";
            Stable=r.IsStable==true?"稳定":r.IsStable==false?"不稳定":"未知";
        });
        service.ConnectionChanged += error=>Dispatcher.UIThread.Post(()=>
        {
            if (_service!=service) return;
            Status=error==null?"已连接":"连接异常，自动重试："+error.Message;
            if (error!=null) { _reading=null; Weight="--"; Stable="未知"; }
        });
        Status="连接中...";
        await service.StartAsync();
    }
    private async Task StopAsync()
    {
        var service=_service; _service=null;
        _reading=null; Weight="--"; Stable="未知";
        if(service!=null) await service.DisposeAsync();
        Status="未连接";
    }
    private async Task SaveAsync()
    {
        var r=_reading;
        if(r==null) { Status="暂无称重数据"; return; }
        await Task.Run(async ()=>
        {
            await _records.InitializeAsync();
            await _records.InsertAsync(new WeightRecord(Guid.NewGuid().ToString("N"),r.Timestamp,r.Weight,r.Unit,r.IsStable,r.Raw));
        });
        Status="称重记录已保存";
    }
    private async Task HistoryAsync()
    {
        var list=await Task.Run(async ()=> { await _records.InitializeAsync(); return await _records.QueryRecentAsync(30); });
        History=string.Join(Environment.NewLine,list.Select(r=>$"{r.Timestamp.ToLocalTime():yyyy-MM-dd HH:mm:ss}    {r.Weight} {r.Unit}"));
    }
    private async Task SaveSettingsAsync()
    {
        _=new WeightParser(new ProtocolOptions { Pattern=Pattern });
        await _config.SetAsync("scale",new UserSettings(Device,Port,Poll,Pattern));
        Status="配置已保存，下次启动称重时生效";
    }
    private async Task LoadSettingsAsync()
    {
        var s=await _config.GetAsync("scale",new UserSettings(Device,Port,Poll,Pattern));
        Device=Devices.Contains(s.Device)?s.Device:Devices[0];
        Port=s.Port; Poll=s.Poll; Pattern=s.Pattern; Status="配置已读取";
    }
    public async ValueTask DisposeAsync()
    {
        _disposed=true; Refresh(); await StopAsync();
    }
    public sealed record UserSettings(string Device,string Port,bool Poll,string Pattern);
}

internal sealed class AsyncCommand(Func<Task> action,Func<bool> canExecute) : ICommand
{
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter)=>canExecute();
    public async void Execute(object? parameter) { if(CanExecute(parameter)) await action(); }
    public void Refresh()=>CanExecuteChanged?.Invoke(this,EventArgs.Empty);
}
