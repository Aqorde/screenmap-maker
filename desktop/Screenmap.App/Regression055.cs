using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.VisualTree;
namespace Screenmap.App;

internal static class Regression055 {
    internal static async Task<int> Run(MainWindow window,string output){
        var vm=window.Editor;var checks=new List<string>();string dir=System.IO.Path.Combine(output,"v055b");Directory.CreateDirectory(dir);
        void Check(bool value,string message){if(!value)throw new InvalidOperationException("v0.5.5b: "+message);checks.Add(message);}
        vm.Replace(new());vm.UpdatePreferences(p=>{p.Language="ru";p.Theme="Dark";p.Compact=false;});
        vm.Update(d=>{d.ExportWidth=640;d.ExportPrompt=true;d.ExportPromptLanguage="en";d.Layers[0].Visible=true;});
        string scene=System.IO.Path.Combine(dir,"Сцена с пробелами.screenmap");StateStore.SaveProject(scene,vm);
        using(var copy=new Editor()){
            StateStore.OpenProject(scene,copy);Check(copy.Doc.ExportPrompt&&copy.Doc.ExportPromptLanguage=="en","prompt export choices survive project roundtrip");
            string oldJson=JsonSerializer.Serialize(new DocumentSettings()).Replace("\"ExportPromptLanguage\":\"ru\",","").Replace("\"ExportPrompt\":false,","");
            var old=JsonSerializer.Deserialize<DocumentSettings>(oldJson)!;Editor.Validate(old);Check(!old.ExportPrompt&&old.ExportPromptLanguage=="ru","old documents default to optional Russian template");
        }
        foreach(string language in new[]{"ru","en"})foreach(string ext in new[]{"png","svg"}){
            string path=System.IO.Path.Combine(dir,$"Сцена [test] {language}.{ext}");
            ExportService.Export(vm,path,ext,false,true,language);
            string md=System.IO.Path.ChangeExtension(path,".md"),content=File.ReadAllText(md);
            Check(File.Exists(path)&&content.Contains(Uri.EscapeDataString(System.IO.Path.GetFileName(path))),"paired export references actual Unicode filename "+language+ext);
            Check(content.Contains(language=="ru"?"Рамки глубины не являются контейнерами":"Depth frames are not containers"),"depth is not a container "+language+ext);
            Check(content.Contains(language=="ru"?"## 3. Промпт сцены":"## 3. Scene prompt")&&content.Contains(language=="ru"?"Источники света":"Light sources"),"editable scene and multi-light brief "+language+ext);
            File.WriteAllText(md,"Мой заполненный промпт");string? backup=ExportService.Export(vm,path,ext,false,true,language);
            Check(backup is not null&&File.ReadAllText(backup)=="Мой заполненный промпт","re-export preserves an edited sidecar "+language+ext);
            string imageBefore=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path)));string mdBefore=File.ReadAllText(md);bool failed=false;
            using(var locked=File.Open(md,FileMode.Open,FileAccess.Read,FileShare.Read)){
                try{vm.Update(d=>d.CenterX=.4);ExportService.Export(vm,path,ext,false,true,language);}catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){failed=true;}
            }
            Check(failed&&File.ReadAllText(md)==mdBefore&&Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path)))==imageBefore,"locked sidecar rolls image back "+language+ext);
        }
        foreach(string ext in new[]{"png","svg"}){
            string path=System.IO.Path.Combine(dir,"without-template."+ext);ExportService.Export(vm,path,ext,false,false,"ru");
            Check(File.Exists(path)&&!File.Exists(System.IO.Path.ChangeExtension(path,".md")),"unchecked option creates image only "+ext);
        }
        vm.Update(d=>{d.Mode=1;d.Layers[0].Visible=false;d.Layers[1].Visible=false;d.Layers[2].Visible=false;});
        string flat=PromptTemplate.Create(vm,"flat.png","en",false);
        Check(flat.Contains("Primitive grid (2D)")&&!flat.Contains("Depth frames are not containers")&&!flat.Contains("**PGFA:**")&&!flat.Contains("**Levels:**"),"legend follows primitive mode and disabled layers");
        Check(flat.Contains("Solid guide background")&&PromptTemplate.Create(vm,"photo.png","ru",true).Contains("Однотонная подложка карты"),"legend describes the solid background when there is no photo");
        bool rejected=false;try{vm.Update(d=>d.ExportPromptLanguage="invalid",false);}catch(InvalidDataException){rejected=true;}Check(rejected,"invalid prompt language is rejected");
        Check(Program.ProjectArgument(["--config-dir","config.screenmap",scene])==scene&&Program.ProjectArgument(["--verify",scene]) is null,"association launch separates project argument from option values");
        Check(Program.ProjectArgument(["C:\\сцены\\TEST.SCREENMAP"])=="C:\\сцены\\TEST.SCREENMAP","association launch accepts uppercase extension and Cyrillic paths");
        window.OpenProject(scene);vm.Update(d=>d.ExportWidth=800,false);await window.Actions.First(a=>a.Id=="save").Run();
        using(var saved=new Editor()){StateStore.OpenProject(scene,saved);Check(window.ProjectPath==scene&&saved.Doc.ExportWidth==800,"file association open then save writes same project without picker");}
        string malformed=System.IO.Path.Combine(dir,"malformed.screenmap");File.WriteAllText(malformed,"bad");string before=vm.Snapshot();
        try{window.OpenProject(malformed);}catch(InvalidDataException){}
        Check(vm.Snapshot()==before&&window.ProjectPath==scene,"invalid shell project leaves working document and path intact");
        foreach(string language in new[]{"ru","en"})foreach(bool compact in new[]{false,true}){
            vm.UpdatePreferences(p=>{p.Language=language;p.Compact=compact;});
            var guide=new DocumentationWindow(vm);guide.Show(window);await Verification.Snapshot(guide,System.IO.Path.Combine(dir,$"documentation-{language}-{compact}.png"));
            var title=guide.GetVisualDescendants().OfType<TextBlock>().First(t=>t.FontSize==30);var logo=guide.GetVisualDescendants().OfType<Image>().First(i=>i.Width==64);
            Check(title.Bounds.Height>30&&title.Bounds.Height<=64&&logo.Bounds.Height==64,"documentation title and logo fit 64 px row "+language+compact);guide.Close();
            var export=new ExportWindow(vm,window);export.Show(window);await Verification.Snapshot(export,System.IO.Path.Combine(dir,$"export-{language}-{compact}.png"));
            var option=export.PromptOption;var select=export.PromptLanguage;
            Check(option.TranslatePoint(new(0,0),export) is Point a&&select.TranslatePoint(new(0,0),export) is Point c&&c.X>=a.X+option.Bounds.Width&&Math.Abs(a.Y+option.Bounds.Height/2-c.Y-select.Bounds.Height/2)<1,"prompt language sits to checkbox right at same centre "+language+compact);
            option.IsChecked=false;Check(!select.IsEnabled&&!vm.Doc.ExportPrompt,"prompt checkbox persists disabled state "+language+compact);
            option.IsChecked=true;select.SelectedIndex=language=="ru"?1:0;Check(select.IsEnabled&&vm.Doc.ExportPromptLanguage==(language=="ru"?"en":"ru"),"template language independent from UI language "+language+compact);export.Close();
            await Verification.Snapshot(window,System.IO.Path.Combine(dir,$"icons-{language}-{compact}.png"));
            foreach(string buttonName in new[]{"MinimizeButton","MaximizeButton","CloseButton"}){
                var button=window.FindControl<Button>(buttonName)!;var icon=button.GetVisualDescendants().OfType<Viewbox>().First();var at=icon.TranslatePoint(new(0,0),button)!.Value;
                Check(Math.Abs(at.Y+icon.Bounds.Height/2-button.Bounds.Height/2)<1&&Math.Abs(at.X+icon.Bounds.Width/2-button.Bounds.Width/2)<1,"caption icon centred "+buttonName+language+compact);
            }
        }
        string installDir=System.IO.Path.Combine(dir,"installer-language");
        Directory.CreateDirectory(installDir);
        string marker=System.IO.Path.Combine(installDir,"installed-mode");
        string firstRun=System.IO.Path.Combine(installDir,"config");
        foreach(string language in new[]{"en","ru"}){
            File.WriteAllText(marker,language);
            using var installed=new Editor();
            installed.UpdatePreferences(p=>p.Language=language=="en"?"ru":"en");
            Check(StateStore.LoadSession(firstRun,installed,installDir) is null&&installed.Prefs.Language==language,
                "first launch adopts installer language "+language);
        }
        using(var savedLanguage=new Editor()){
            savedLanguage.UpdatePreferences(p=>p.Language="ru");
            StateStore.SaveSession(firstRun,savedLanguage,1200,900);
            File.WriteAllText(marker,"en");
            using var restoredLanguage=new Editor();
            Check(StateStore.LoadSession(firstRun,restoredLanguage,installDir) is not null&&restoredLanguage.Prefs.Language=="ru",
                "saved interface language takes precedence on upgrade");
        }
        Check(!Directory.EnumerateFiles(dir).Any(p=>p.EndsWith(".tmp")||p.EndsWith(".rollback")),"paired export cleans staging files");
        StateStore.Atomic(System.IO.Path.Combine(dir,"regression.json"),JsonSerializer.SerializeToUtf8Bytes(new{success=true,assertions=checks.Count,checks,windowCaptureUsed=false,computerUseUsed=false},StateStore.Json));return checks.Count;
    }
}
