using System.IO.Compression;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace LiarsBarEnhanceInstaller;

class Program
{
    public static readonly string[] AllGithubProxy = 
        [
            "https://gh.llkk.cc",
            "https://github.moeyy.xyz",
            "https://ghproxy.cn",
            "https://ghproxy.net",
            "https://ghp.ci"
        ];
    public static string CurrentProxy = "";

    private record pingInfo(string url, long pingTime);
    
    private static void CheckAndUseProxy()
    {
        Log("开始获取Github下载代理");
        using var ping = new Ping();
        var list = new List<pingInfo>();
        foreach (var proxy in AllGithubProxy)
        {
            Log($"正在测试:{proxy}");
            var reply = ping.Send(proxy.Replace("https://", string.Empty));
            if (reply.Status == IPStatus.Success)
                list.Add(new pingInfo(proxy, reply.RoundtripTime));
            Log($"{proxy} PingTime:{reply.RoundtripTime}");
        }

        CurrentProxy = list.MinBy(n => n.pingTime)?.url ?? "";
        Log($"当前代理为:{CurrentProxy}");
    }
    
    
    public const string DefaultSteamPath = @"C:\Program Files (x86)\Steam";

    public const string ModGithubUrl = "https://github.com/dogdie233/LiarsBarEnhance/releases/download/1.0.0/com.github.dogdie233.LiarsBarEnhance.dll";
    public const string BepInExGithubUrl = "https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.2/BepInEx_win_x64_5.4.23.2.zip";

    private static string GetGamePathFormSteamPath()
    {
        var steamPath = GetSteamPathFormSteam();
        Log($"Steam根目录为:{steamPath}");
        var gamePath = Path.Combine(steamPath, "steamapps", "common", "Liar's Bar");
        if (!Directory.Exists(gamePath))
        {
            Log("无法找到游戏根目录");
            return "";
        }
        
        Log("游戏根目录为:" + gamePath);
        return gamePath;
    }

    private static string GetSteamPathFormSteam()
    {
        if (Directory.Exists(DefaultSteamPath))
            return DefaultSteamPath;

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Log("当前系统不支持查询注册表");
            throw new Exception("当前系统不支持查询注册表");
        }

        var LocalMachine = Registry.LocalMachine;
        var steamKey = LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam");
        if (steamKey?.GetValue("InstallPath") is not string path)
            throw new Exception("无法从注册表获取Steam路径");
        
        return path;
    }

    private static async Task<Stream> DownloadFormGithub(string url)
    {
        if (string.IsNullOrEmpty(CurrentProxy))
            CheckAndUseProxy();
        
        var downloadUrl = $"{CurrentProxy}/{url}";
        Log($"Download URL:{downloadUrl}");
        using var client = new HttpClient();
        return await client.GetStreamAsync(downloadUrl);
    }

    private static async Task InstallModTo(string Path)
    {
        await using var modStream = await DownloadFormGithub(ModGithubUrl);
        await using var dllStream = File.Open(Path, FileMode.OpenOrCreate);
        await modStream.CopyToAsync(dllStream);
        Log("安装模组完成");
    }

    private static async Task InstallBepInExTo(string path)
    {
        var bepInExStream = await DownloadFormGithub(BepInExGithubUrl);
        using var zip = new ZipArchive(bepInExStream, ZipArchiveMode.Read);
        zip.ExtractToDirectory(path, true);
        Log("安装BepInEx完成");
    }

    private static async Task CheckInstall(string gamePath)
    {
        var bepInExDir = Path.Combine(gamePath, "BepInEx");
        if (!Directory.Exists(bepInExDir))
        {
            Log("不存在BepInEx,开始安装BepInEx");
            await InstallBepInExTo(gamePath);
        }
        
        var pluginDir = Path.Combine(bepInExDir, "plugins");
        if (!Directory.Exists(pluginDir))
            Directory.CreateDirectory(pluginDir);
        
        var orgDllPath = Path.Combine(pluginDir, "com.github.dogdie233.LiarsBarEnhance.dll");
        var dllPath = Path.Combine(pluginDir, "LiarsBarEnhance.dll");
        if (!File.Exists(dllPath) && !File.Exists(orgDllPath))
        {
            Log("不存在LiarsBarEnhance,开始安装LiarsBarEnhance");
            await InstallModTo(dllPath);
        }
    }

    public static StreamWriter? _Writer;

    public static void Log(string message)
    {
        _Writer?.WriteLine(message);
        Console.WriteLine(message);
    }
    
    
    static async Task Main(string[] args)
    {
        var fileStream = File.Open("./Log.txt", FileMode.OpenOrCreate);
        _Writer = new StreamWriter(fileStream)
        {
            AutoFlush = true
        };
        
        Log("日志已创建");
        Log("开始尝试安装LiarsBarEnhance");
        Log("请输入游戏根目录,如果为空将尝试自动获取");
        
        var path = Console.ReadLine()!;
        if (string.IsNullOrEmpty(path))
            path = GetGamePathFormSteamPath();
        if (string.IsNullOrEmpty(path))
            goto end;
        
        await CheckInstall(path);
        
        end:
        Log("程序运行结束按任意键退出");
        await _Writer.DisposeAsync()!;
        Console.ReadKey();
    }
    
}