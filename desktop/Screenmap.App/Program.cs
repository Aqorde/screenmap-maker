using Avalonia;
using Avalonia.Headless;
namespace Screenmap.App;
internal static class Program {
    internal static string[] Arguments=[];
    internal static bool Batch=>Arguments.Contains("--verify")||Arguments.Contains("--render-config");
    internal static string? Option(string key){int i=Array.IndexOf(Arguments,key);return i>=0&&i+1<Arguments.Length?Arguments[i+1]:null;}
    internal static string? ProjectArgument(string[] args){
        for(int i=0;i<args.Length;i++){
            if(args[i] is "--config-dir" or "--output" or "--verify" or "--render-config"){i++;continue;}
            if(!args[i].StartsWith("--",StringComparison.Ordinal)&&Path.GetExtension(args[i]).Equals(".screenmap",StringComparison.OrdinalIgnoreCase))return args[i];
        }return null;
    }
    internal static void TracePhase(string message){if(!Batch)return;string? dir=Option("--verify")??Option("--output");if(dir is null)return;Directory.CreateDirectory(dir);File.AppendAllText(Path.Combine(dir,"phases.log"),DateTime.UtcNow.ToString("O")+" "+message+Environment.NewLine);}
    [STAThread]
    public static int Main(string[] args){
        Arguments=args;
        try{
            TracePhase("configure");
            var builder=AppBuilder.Configure<App>();
            if(Batch&&!args.Contains("--desktop-render"))builder=builder.UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions{UseHeadlessDrawing=false});
            else builder=builder.UsePlatformDetect();
            TracePhase("start lifetime");return builder.LogToTrace().StartWithClassicDesktopLifetime(args);
        }catch(Exception ex){
            string dir=Option("--verify")??Option("--output")??AppContext.BaseDirectory;
            Directory.CreateDirectory(dir);File.WriteAllText(Path.Combine(dir,"startup-error.log"),ex.ToString());return 1;
        }
    }
}
