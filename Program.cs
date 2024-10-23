using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Newtonsoft.Json;
using JsonSerializer = Newtonsoft.Json.JsonSerializer;

namespace LiarsBarEnhanceInstaller;

static class Program
{
    public static readonly Version CurrentVersion = new(1, 0, 1);
    
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

    public static readonly DownloadInfo DefaultDownloadInfo = new()
    {
        BepInEx_Version = Default_BepInEx_Version,
        LiarsBarEnhance_Version = Default_LiarsBarEnhance_Version,
        LiarsBarEnhance_Name = Default_LiarsBarEnhance_Name,
        BepInEx_Name = Default_BepInEx_Name
    };
    
    public const string Default_BepInEx_Version = "5.4.23.2";
    public const string Default_LiarsBarEnhance_Version = "1.0.0";
    public const string Default_LiarsBarEnhance_Name = "com.github.dogdie233.LiarsBarEnhance.dll";
    public const string Default_BepInEx_Name = "BepInEx_win_{arch}_{version}.zip";

    public const string ModGithubUrl = "https://github.com/dogdie233/LiarsBarEnhance/releases/download/{version}/{name}";
    public const string BepInExGithubUrl = "https://github.com/BepInEx/BepInEx/releases/download/v{version}/{name}";
    public const string InstallerGithubUrl = "https://github.com/TianMengLucky/LiarsBarEnhanceInstaller/releases/download/{version}/LiarsBarEnhanceInstaller.exe";
    public const string InfoUrl = "https://raw.githubusercontent.com/TianMengLucky/LiarsBarEnhanceInstaller/refs/heads/main/DownloadInfo.json";

    public static DownloadInfo? CurrentInfo;
    public static string Arch = "x64";

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

    private static async Task CheckAndUpdateInstaller(DownloadInfo info)
    {
        if (info.LatestInstallVersion.CompareTo(CurrentVersion) > 0)
        {
            Log("发现新版本, 开始下载");
            await using var stream = await DownloadFormGithub(InstallerGithubUrl.Replace("{version}", info.LatestInstallVersion.ToString()));
            await using var fileStream = new FileStream("LiarsBarEnhanceInstaller_New.exe", FileMode.Create);
            await stream.CopyToAsync(fileStream);

            Process.Start("LiarsBarEnhanceInstaller_New.exe");
            Environment.Exit(0);
        }
    }

    private static async Task<DownloadInfo?> GetDownloadInfo()
    {
        Log("尝试获得下载信息");
        try
        {
            await using var infoStream = await DownloadFormGithub(InfoUrl);
            return JsonSerializer.CreateDefault().Deserialize<DownloadInfo>(new JsonTextReader(new StreamReader(infoStream)));
        }
        catch (Exception e)
        {
            Log("无法获得下载信息:" + e.Message);
        }

        return null;
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

    private static bool Has<T>(this T @enum, params T[] values) where T : Enum
    { 
        foreach (var value in values)
        {
            if (@enum.HasFlag(value))
                return true;
        }

        return false;
    }

    private static async Task InstallModTo(string Path, DownloadInfo info)
    {
        await using var modStream = await DownloadFormGithub(
            ModGithubUrl
                .Replace("{version}", info.LiarsBarEnhance_Version)
                .Replace("{name}", info.LiarsBarEnhance_Name)
            );
        await using var dllStream = File.Open(Path, FileMode.OpenOrCreate);
        await modStream.CopyToAsync(dllStream);
        Log("安装模组完成");
    }

    private static async Task InstallBepInExTo(string path, DownloadInfo info)
    {
        Arch = RuntimeInformation.ProcessArchitecture.HasFlag(Architecture.X64) ? "x64" : "x86";
        Log("Arch:" + Arch);
        
        var bepInExStream = await DownloadFormGithub(
            BepInExGithubUrl
                .Replace("{version}", info.BepInEx_Version)
                .Replace("{name}", info.BepInEx_Name)
                .Replace("{arch}", Arch)
            );
        
        using var zip = new ZipArchive(bepInExStream, ZipArchiveMode.Read);
        zip.ExtractToDirectory(path, true);
        Log("安装BepInEx完成");
    }


    private static async Task CheckInstall(string gamePath, DownloadInfo info)
    {
        var bepInExDir = Path.Combine(gamePath, "BepInEx");
        if (!Directory.Exists(bepInExDir))
        {
            Log("不存在BepInEx,开始安装BepInEx");
            await InstallBepInExTo(gamePath, info);
        }
        
        var pluginDir = Path.Combine(bepInExDir, "plugins");
        if (!Directory.Exists(pluginDir))
            Directory.CreateDirectory(pluginDir);
        
        var orgDllPath = Path.Combine(pluginDir, "com.github.dogdie233.LiarsBarEnhance.dll");
        var dllPath = Path.Combine(pluginDir, "LiarsBarEnhance.dll");
        
        CheckDllVersion(orgDllPath, info);
        CheckDllVersion(dllPath, info);
        
        if (!File.Exists(dllPath) && !File.Exists(orgDllPath))
        {
            Log("不存在LiarsBarEnhance,开始安装LiarsBarEnhance");
            await InstallModTo(dllPath, info);
        }
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "<Pending>")]
    private static void CheckDllVersion(string path, DownloadInfo info)
    {
        if (!File.Exists(path)) return;
        var assembly = Assembly.LoadFrom(path);
        var version = assembly.GetName().Version;
        if (version == null || version < new Version(info.LiarsBarEnhance_Version))
            File.Delete(path);
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
        Log("从Github获取下载信息");
        CurrentInfo = await GetDownloadInfo();
        if (CurrentInfo == null)
        {
            Log("获取信息失败使用默认下载");
        }
        else
        {
            Log("获取信息成功");
            await CheckAndUpdateInstaller(CurrentInfo);
            Log($"下载BepInEx版本:{CurrentInfo.BepInEx_Version}");
            Log($"下载LiarsBarEnhance版本:{CurrentInfo.LiarsBarEnhance_Version}");
        }
        
        Log("请输入游戏根目录,如果为空将尝试自动获取");
        var path = Console.ReadLine()!;
        if (string.IsNullOrEmpty(path))
            path = GetGamePathFormSteamPath();
        if (string.IsNullOrEmpty(path))
            goto end;
        
        await CheckInstall(path, CurrentInfo ?? DefaultDownloadInfo);
        
        end:
        Log("程序运行结束按任意键退出");
        await _Writer.DisposeAsync()!;
        Console.ReadKey();
    }
}

public class DownloadInfo
{
    [JsonProperty("BepInExVersion")]
    public string BepInEx_Version { get; set; }
    
    [JsonProperty("LiarsBarEnhanceVersion")]
    public string LiarsBarEnhance_Version { get; set; }
    
    [JsonProperty("BepInExName")]
    public string BepInEx_Name { get; set; }
    
    [JsonProperty("LiarsBarEnhanceName")]
    public string LiarsBarEnhance_Name { get; set; }
    
    public Version LatestInstallVersion { get; set; }
}