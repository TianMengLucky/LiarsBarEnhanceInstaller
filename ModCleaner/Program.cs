using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace ModCleaner;

internal static class Program
{
    private static void Main(string[] args)
    {
        Console.WriteLine("请输入游戏根目录,如果为空将尝试自动获取");
        var input = Console.ReadLine()!;
        var path = string.IsNullOrEmpty(input) ? GetGamePathFormSteamPath() : input;
        if (string.IsNullOrEmpty(path))
        { 
            Console.WriteLine("无法找到游戏根目录"); 
            goto end;
        }
        
        foreach (var name in UnInstallFileList)
        {
            var itemPath = Path.Combine(path, name);
            if (File.Exists(itemPath))
            {
                File.Delete(itemPath);
                Console.WriteLine($"清除文件:{name} {itemPath}");
            }

            if (Directory.Exists(itemPath))
            {
                Directory.Delete(itemPath, true);
                Console.WriteLine($"清除文件夹:{name} {itemPath}");
            }
        }
        
        end:  
        Console.WriteLine("输入任意键退出");
        Console.ReadKey();
    }
    private static string GetGamePathFormSteamPath()
    {
        var steamPath = GetSteamPathFormSteam();
        Console.WriteLine($"Steam根目录为:{steamPath}");
        var gamePath = Path.Combine(steamPath, "steamapps", "common", "Liar's Bar");
        if (!Directory.Exists(gamePath))
        {
            Console.WriteLine("无法找到游戏根目录");
            return "";
        }

        Console.WriteLine("游戏根目录为:" + gamePath);
        return gamePath;
    }
    
    public const string DefaultSteamPath = @"C:\Program Files (x86)\Steam";
    
    
    private static string GetSteamPathFormSteam()
    {
        if (Directory.Exists(DefaultSteamPath))
            return DefaultSteamPath;

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Console.WriteLine("当前系统不支持查询注册表");
            throw new Exception("当前系统不支持查询注册表");
        }

        var LocalMachine = Registry.LocalMachine;
        var steamKey = LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam");
        if (steamKey?.GetValue("InstallPath") is not string path)
            throw new Exception("无法从注册表获取Steam路径");

        return path;
    }
    
    public static readonly string[] UnInstallFileList =
    [
        ".doorstop_version",
        "changelog.txt",
        "doorstop_config.ini",
        "winhttp.dll",
        "BepInEx"
    ];
}