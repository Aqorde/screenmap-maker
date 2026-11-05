using System.Globalization;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
namespace Screenmap.App;

internal static class Regression052 {
    internal static async Task<int> Run(MainWindow window,string output){
        var vm=window.Editor;var checks=new List<string>();var dir=Path.Combine(output,"v052");Directory.CreateDirectory(dir);
        void Check(bool pass,string name){if(!pass)throw new InvalidOperationException("v0.5.2: "+name);checks.Add(name);}
        bool Fits(Window popup,Control child){var at=child.TranslatePoint(new Point(0,0),popup);return at is Point p&&p.X>=6&&p.Y>=6&&p.X+child.Bounds.Width<=popup.ClientSize.Width-5&&p.Y+child.Bounds.Height<=popup.ClientSize.Height-5;}
        vm.Replace(new());vm.UpdatePreferences(p=>{p.Language="ru";p.Theme="Light";p.BaseSize=14;p.Compact=true;});
        foreach(string theme in new[]{"Light","Dark"})foreach(bool compact in new[]{true,false}){
            vm.UpdatePreferences(p=>{p.Theme=theme;p.Compact=compact;});string suffix=theme+"-"+(compact?"compact":"normal");
            var palette=new ColorPalette("#CDD0DE",_=>{});palette.Show(window);await Verification.Snapshot(palette,Path.Combine(dir,"palette-"+suffix+".png"));
            var rgb=palette.GetVisualDescendants().OfType<TextBlock>().First(t=>t.Text?.StartsWith("RGB ")==true);
            Check(!palette.CanResize&&palette.ResizeEdge(new(1,1)) is null,"palette has no resize affordance "+suffix);
            Check(palette.ClientSize.Width==374&&palette.ClientSize.Height is >400 and <480,"palette tightly fits reference proportions "+suffix);
            Check(Fits(palette,rgb)&&Fits(palette,palette.Surface),"palette content fits without clipping "+suffix);palette.Close();
            var message=new MessageWindow("Ошибка","Укажи новое имя темы");message.Show(window);await Verification.Snapshot(message,Path.Combine(dir,"message-"+suffix+".png"));
            Check(!message.CanResize&&message.ClientSize.Width is >=180 and <=400&&message.ClientSize.Height is >=90 and <=160,"short error uses compact fixed window "+suffix);
            Check(Fits(message,message.Message),"short message remains inside window "+suffix);message.Close();
            var about=new AboutWindow(vm);about.Show(window);await Verification.Snapshot(about,Path.Combine(dir,"about-"+suffix+".png"));
            Check(!about.CanResize&&about.ClientSize.Width==540&&about.ClientSize.Height is >=320 and <=410,"About fits compact reference "+suffix);
            var description=about.GetVisualDescendants().OfType<TextBlock>().Last();Check(Fits(about,description),"About description fits fixed window "+suffix);about.Close();
            await Verification.Snapshot(window,Path.Combine(dir,"main-"+suffix+".png"));
            StateStore.Atomic(Path.Combine(output,"configs","polish-"+suffix+".json"),JsonSerializer.SerializeToUtf8Bytes(new SessionState{Document=vm.Doc,Preferences=vm.Prefs},StateStore.Json));
            var settings=window.CreateSettingsForRender();settings.SelectPage(1);settings.Show(window);await Verification.Snapshot(settings,Path.Combine(dir,"settings-"+suffix+".png"));
            foreach(string name in new[]{"Тема вьюпорта · слои","Настройки глубины","Сохранить тему","Основной шрифт"}){
                var title=settings.GetVisualDescendants().OfType<TextBlock>().First(t=>t.Text==name);var card=title.GetVisualAncestors().OfType<Border>().First(b=>b.Classes.Contains("settings-section"));
                var scroll=card.GetVisualAncestors().OfType<ScrollViewer>().First();var content=(Control)scroll.Content!;
                if(card.TranslatePoint(new(0,0),content) is Point pos)scroll.Offset=new Vector(0,Math.Max(0,pos.Y));Dispatcher.UIThread.RunJobs();
                string section=name switch{"Тема вьюпорта · слои"=>"layers","Настройки глубины"=>"depth","Сохранить тему"=>"theme",_=>"font"};
                await Verification.Snapshot(settings,Path.Combine(dir,$"settings-{section}-{suffix}.png"));
            }
            settings.SelectPage(0);await Verification.Snapshot(settings,Path.Combine(dir,"settings-language-"+suffix+".png"));
            settings.SelectPage(2);await Verification.Snapshot(settings,Path.Combine(dir,"settings-quick-"+suffix+".png"));settings.Close();
        }
        var longMessage=new MessageWindow("Ошибка",string.Join('\n',Enumerable.Repeat("Длинное сообщение: путь или подробности ошибки должны оставаться доступными для чтения.",30)));longMessage.Show(window);await Verification.Snapshot(longMessage,Path.Combine(dir,"message-long.png"));
        var longScroll=longMessage.GetVisualDescendants().OfType<ScrollViewer>().First();
        Check(!longMessage.CanResize&&longMessage.ClientSize.Width<=560&&longMessage.ClientSize.Height<=420,"long error is bounded without resize");
        Check(longScroll.Extent.Height>longScroll.Viewport.Height,"long error retains scroll access");longMessage.Close();
        vm.UpdatePreferences(p=>p.BaseSize=28);var largePalette=new ColorPalette("#CDD0DE",_=>{});largePalette.Show(window);await Verification.Snapshot(largePalette,Path.Combine(dir,"palette-font-28.png"));
        var largeRgb=largePalette.GetVisualDescendants().OfType<TextBlock>().First(t=>t.Text?.StartsWith("RGB ")==true);Check(Fits(largePalette,largeRgb),"fixed palette adapts height to large UI font");largePalette.Close();vm.UpdatePreferences(p=>p.BaseSize=14);
        var timestamp=new DateTimeOffset(2026,11,5,21,34,56,TimeSpan.FromHours(5));var options=MainWindow.ProjectSaveOptions(timestamp);
        Check(options.SuggestedFileName=="scene_20261105_213456.screenmap"&&options.DefaultExtension=="screenmap","save picker suggests scene timestamp pattern");
        var culture=CultureInfo.CurrentCulture;try{CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo("ar-SA");Check(MainWindow.ProjectSaveOptions(timestamp).SuggestedFileName==options.SuggestedFileName,"suggested filename is calendar and culture independent");}finally{CultureInfo.CurrentCulture=culture;}
        var path=Path.Combine(dir,options.SuggestedFileName!);StateStore.SaveProject(path,vm);using(var copy=new Editor()){StateStore.OpenProject(path,copy);Check(copy.Doc.P==vm.Doc.P&&copy.Doc.Q==vm.Doc.Q,"timestamp-named project roundtrip");}
        StateStore.Atomic(Path.Combine(dir,"regression.json"),JsonSerializer.SerializeToUtf8Bytes(new{success=true,assertions=checks.Count,checks,computerUseUsed=false,windowCaptureUsed=false},StateStore.Json));return checks.Count;
    }
}
