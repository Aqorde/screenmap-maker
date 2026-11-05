using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using System.Text.Json;
using Avalonia.Themes.Fluent;
namespace Screenmap.App;
public partial class App:Application {
    public override void Initialize(){Program.TracePhase("initialize resources");AvaloniaXamlLoader.Load(this);Program.TracePhase("resources loaded");}
    private static string theme="",font="";
    private static double fontSize;
    private static bool? compact;
    internal static void Apply(Preferences p){
        var app=Current!;
        if(theme!=p.Theme){theme=p.Theme;app.RequestedThemeVariant=p.Theme=="Dark"?ThemeVariant.Dark:p.Theme=="Light"?ThemeVariant.Light:ThemeVariant.Default;}
        if(font!=p.FontFamily){font=p.FontFamily;app.Resources["BaseFont"]=new FontFamily(p.FontFamily);app.Resources["ContentControlThemeFontFamily"]=new FontFamily(p.FontFamily);}
        if(fontSize!=p.BaseSize){fontSize=p.BaseSize;app.Resources["BaseSize"]=p.BaseSize;app.Resources["ControlContentThemeFontSize"]=p.BaseSize;}
        if(compact!=p.Compact){compact=p.Compact;
            foreach(var fluent in app.Styles.OfType<FluentTheme>())fluent.DensityStyle=p.Compact?DensityStyle.Compact:DensityStyle.Normal;
            app.Resources["AppButtonPadding"]=p.Compact?new Thickness(8,3):new Thickness(12,7);
            app.Resources["AppButtonMinHeight"]=p.Compact?25d:32d;
        }
    }
    public override void OnFrameworkInitializationCompleted(){
        if(ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop){
            Program.TracePhase("framework initialized");
            var editor=new Editor();
            string directory=Program.Option("--config-dir")??StateStore.DefaultConfigDirectory();
            SessionState? session=null;
            if(Program.Option("--render-config") is string config){
                session=JsonSerializer.Deserialize<SessionState>(File.ReadAllBytes(config),StateStore.Json)??throw new InvalidDataException("Empty render config");
                Editor.Validate(session.Document);Editor.ValidatePreferences(session.Preferences);
                if(session.PhotoFile is not null){if(Path.GetFileName(session.PhotoFile)!=session.PhotoFile)throw new InvalidDataException("Invalid photo reference");string id=session.Document.PhotoId??"render-photo";editor.Photos[id]=new PhotoAsset(File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(config)!,session.PhotoFile)));session.Document.PhotoId=id;}
                editor.SetPreferences(session.Preferences);editor.Replace(session.Document);
            }else if(!Program.Batch){
                try{session=StateStore.LoadSession(directory,editor);}catch(Exception ex){
                    Directory.CreateDirectory(directory);string state=Path.Combine(directory,"state.json");
                    if(File.Exists(state))File.Copy(state,state+".invalid-"+DateTime.UtcNow.ToString("yyyyMMddHHmmss"),true);
                    File.WriteAllText(Path.Combine(directory,"load-error.log"),ex.ToString());
                }
            }
            Apply(editor.Prefs);
            Program.TracePhase("create main window");
            var window=new MainWindow(editor,directory,Program.Batch);
            Program.TracePhase("main window created");
            if(session is not null){window.Width=session.Width;window.Height=session.Height;}
            desktop.MainWindow=window;
            if(Program.ProjectArgument(Program.Arguments) is string project){
                try{window.OpenProject(project);}catch(Exception ex){window.Opened+=(_,_)=>window.ShowMessage(UI.L(editor,"Could not open project","Не удалось открыть проект"),ex.Message);}
            }
            if(Program.Batch)Dispatcher.UIThread.Post(async()=>{
                int exit=0;string output=Program.Option("--verify")??Program.Option("--output")??Path.Combine(AppContext.BaseDirectory,"renders");
                try{
                    Directory.CreateDirectory(output);
                    if(Program.Option("--verify") is not null)await Verification.Run(window,output);
                    else await Verification.RenderOne(window,output);
                }catch(Exception ex){Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"FAIL.txt"),ex.ToString());exit=1;}
                desktop.Shutdown(exit);
            },DispatcherPriority.Background);
        }
        base.OnFrameworkInitializationCompleted();
    }
}
