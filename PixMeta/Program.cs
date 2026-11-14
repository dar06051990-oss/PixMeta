using Avalonia;
using System;
namespace PixMeta;
class Program { [STAThread] public static void Main(string[] args) => Build().StartWithClassicDesktopLifetime(args); public static AppBuilder Build()=>AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace(); }
