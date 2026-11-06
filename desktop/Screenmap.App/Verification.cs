using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using SkiaSharp;
using System.Text.Json;
using System.Buffers.Binary;
using System.IO.Compression;
using System.Xml.Linq;
namespace Screenmap.App;
internal static class Verification {
    internal static async Task Snapshot(Window w,string path,double scale=1){
        w.Show();await Task.Delay(35);Dispatcher.UIThread.RunJobs();
        var size=w.ClientSize;w.Measure(size);w.Arrange(new Rect(size));
        using var bitmap=new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(size.Width*scale),(int)Math.Ceiling(size.Height*scale)),new Vector(96*scale,96*scale));
        bitmap.Render(w);bitmap.Save(path,new PngBitmapEncoderOptions());
    }
    internal static async Task RenderOne(MainWindow window,string output){
        await Snapshot(window,Path.Combine(output,"viewport.png"));
        using var plan=new RenderPlan(window.Editor);
        RenderEngine.Png(window.Editor,Path.Combine(output,"screenmap.png"),1280,96,window.Editor.Photo is not null);
        var settings=window.CreateSettingsForRender();settings.SelectPage(1);settings.Show(window);await Snapshot(settings,Path.Combine(output,"settings.png"));settings.Close();
        var fixedPopups=new List<object>();
        async Task FixedPopup(Window popup,string name){popup.Show(window);await Snapshot(popup,Path.Combine(output,name+".png"));fixedPopups.Add(new{kind=name,canResize=popup.CanResize,width=popup.ClientSize.Width,height=popup.ClientSize.Height});popup.Close();}
        await FixedPopup(new ColorPalette("#CDD0DE",_=>{}),"palette");
        await FixedPopup(new MessageWindow(UI.L(window.Editor,"Error","Ошибка"),UI.L(window.Editor,"Choose a new theme name","Укажи новое имя темы")),"message");
        await FixedPopup(new AboutWindow(window.Editor),"about");
        StateStore.Atomic(Path.Combine(output,"window.json"),JsonSerializer.SerializeToUtf8Bytes(new{
            title=window.Title,decorations=window.WindowDecorations.ToString(),extendedClientArea=window.IsExtendedIntoWindowDecorations,
            renderScaling=window.RenderScaling,fixedPopups,windowCaptureUsed=false,computerUseUsed=false
        },StateStore.Json));
    }
    internal static async Task Run(MainWindow window,string output){
        int assertions=0;var vm=window.Editor;
        void Check(bool value,string message){assertions++;if(!value)throw new InvalidOperationException(message);}
        string configDir=Path.Combine(output,"configs");Directory.CreateDirectory(configDir);
        async Task Case(string name,Action<DocumentSettings> mutate,string theme="Dark"){
            vm.Replace(new DocumentSettings());vm.UpdatePreferences(p=>{p.Theme=theme;p.Language="en";p.QuickMenu=["perspective","orientation"];});vm.Update(mutate);
            var session=new SessionState{Document=vm.Doc,Preferences=vm.Prefs};
            StateStore.Atomic(Path.Combine(configDir,name+".json"),JsonSerializer.SerializeToUtf8Bytes(session,StateStore.Json));
            await Snapshot(window,Path.Combine(output,name+".png"));
            RenderEngine.Svg(vm,Path.Combine(output,name+".svg"),1280,false);
        }
        await Case("01-perspective-dark",_=>{});
        await Case("02-perspective-light",_=>{},"Light");
        await Case("03-radius-large",d=>d.Radius=.23);
        await Case("04-centre-offset",d=>{d.CenterX=.3;d.CenterY=.62;d.Radius=.13;});
        await Case("05-depth-unshifted",d=>{d.Layers[1].Shift=false;d.Layers[1].FontSize=24;});
        await Case("06-pgfa-sparse",d=>{foreach(var l in d.Layers)l.Visible=false;d.Layers[0].Visible=true;d.PgfaDensity=1;});
        await Case("07-pgfa-dense",d=>{foreach(var l in d.Layers)l.Visible=false;d.Layers[0].Visible=true;d.PgfaDensity=4;});
        await Case("08-primitive-all-layers",d=>{d.Mode=1;d.PrimitiveDensities["16:9"]=1;d.PrimitiveCircle=true;d.Layers[0].Visible=true;});
        Check(vm.Doc.PrimitiveShape()==(16,9),"Primitive maximum");
        Check(vm.Scene.Labels.Count(l=>l.Layer==3)==144,"Primitive numbered cells");
        Check(new[]{0,1,2,3,4}.All(id=>vm.Scene.Lines.Any(l=>l.Layer==id)||vm.Scene.Labels.Any(l=>l.Layer==id)),"Primitive preserves all layers");
        await Case("09-primitive-single",d=>{d.Mode=1;d.PrimitiveDensities["16:9"]=0;});
        Check(vm.Doc.PrimitiveShape()==(1,1),"Single cell");
        await Case("10-portrait",d=>{d.P=3;d.Q=4;d.Mode=1;d.PrimitiveDensities["3:4"]=1;});
        Check(vm.Doc.PrimitiveShape()==(12,16),"Portrait density");
        Check(vm.Scene.Labels.Count(l=>l.Layer==3)==192,"Portrait numbered cells");
        await Case("11-square",d=>{d.P=1;d.Q=1;d.Mode=1;d.PrimitiveDensities["1:1"]=.4;});
        vm.Replace(new DocumentSettings());vm.UpdatePreferences(p=>{p.Theme="Dark";p.Language="en";});
        await Snapshot(window,Path.Combine(output,"12-drag-before.png"));
        vm.PerspectiveEditing=true;vm.Notify();await Task.Delay(30);Dispatcher.UIThread.RunJobs();
        var center=window.View.TranslatePoint(window.View.Frame.Center,window)!.Value;
        double initial=vm.Doc.CenterX;
        window.MouseDown(center,MouseButton.Left);window.MouseMove(center+new Vector(90,35));window.MouseUp(center+new Vector(90,35),MouseButton.Left);
        Check(vm.Doc.CenterX>initial+.02,"Headless pointer moves perspective");
        vm.Undo();Check(Math.Abs(vm.Doc.CenterX-initial)<1e-8,"Pointer gesture is one Undo");
        var circle=vm.Scene.Circles[0];
        var handle=window.View.TranslatePoint(new Point(window.View.Frame.X+(circle.X+circle.Rx)*window.View.Frame.Width,window.View.Frame.Y+circle.Y*window.View.Frame.Height),window)!.Value;
        double radius=vm.Doc.Radius;
        window.MouseDown(handle,MouseButton.Left);window.MouseMove(handle+new Vector(25,0));window.MouseUp(handle+new Vector(25,0),MouseButton.Left);
        Check(vm.Doc.Radius>radius+.01,"Radius handle expands D7");vm.Undo();
        window.MouseDown(center,MouseButton.Left);window.MouseMove(center+new Vector(55,20));window.KeyPress(Key.Escape,RawInputModifiers.None,PhysicalKey.Escape,null);window.KeyRelease(Key.Escape,RawInputModifiers.None,PhysicalKey.Escape,null);window.MouseUp(center+new Vector(55,20),MouseButton.Left);
        Check(Math.Abs(vm.Doc.CenterX-initial)<1e-8,"Escape restores gesture and releases capture");
        var outside=window.View.TranslatePoint(new Point(window.View.Frame.Left-14,window.View.Frame.Center.Y+20),window)!.Value;
        window.MouseDown(center,MouseButton.Left);window.MouseMove(outside);window.MouseUp(outside,MouseButton.Left);
        Check(vm.Doc.CenterX<0&&vm.Doc.CenterY==.5,"perspective drag goes outside and keeps its horizon");
        var edgeMarker=window.View.TranslatePoint(window.View.PerspectiveMarker,window)!.Value;
        double beforeEdgeDrag=vm.Doc.CenterX;
        window.MouseDown(edgeMarker,MouseButton.Left);window.MouseMove(edgeMarker-new Vector(8,0));window.MouseUp(edgeMarker-new Vector(8,0),MouseButton.Left);
        Check(vm.Doc.CenterX<beforeEdgeDrag,"off-frame marker can be dragged without jumping to the edge");
        var sidebar=window.FindControl<StackPanel>("Sidebar")!;
        var perspectiveX=sidebar.Children.OfType<Slider>().Single(s=>s.Minimum==-1&&s.Maximum==2);
        perspectiveX.Value=1.35;
        Check(vm.Doc.CenterX==1.35&&vm.Doc.CenterY==.5,"Perspective X moves the point right of the frame along the horizon");
        var offFrameProject=Path.Combine(output,"off-frame.screenmap");StateStore.SaveProject(offFrameProject,vm);
        using(var reopenedPerspective=new Editor()){
            StateStore.OpenProject(offFrameProject,reopenedPerspective);
            Check(reopenedPerspective.Doc.CenterX==1.35,"off-frame perspective round-trips through a project");
        }
        var reset=sidebar.Children.OfType<Button>().Single(b=>Equals(b.Content,"Perspective Reset"));
        var resetRadius=vm.Doc.Radius;
        reset.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Check(vm.Doc.CenterX==.5&&vm.Doc.CenterY==.5&&vm.Doc.Radius==resetRadius,"Perspective Reset button centres the point and retains radius");
        vm.Undo();
        Check(vm.Doc.CenterX==1.35,"Perspective Reset can be undone");
        vm.Redo();
        var settings=window.CreateSettingsForRender();settings.SelectPage(1);settings.Show(window);
        await Snapshot(settings,Path.Combine(output,"settings-appearance.png"));
        settings.Height=920;await Snapshot(settings,Path.Combine(output,"settings-appearance-tall.png"));
        settings.SelectPage(2);await Snapshot(settings,Path.Combine(output,"settings-quick-menu.png"));settings.Close();
        var palette=new ColorPalette("#4389CD",_=>{});palette.Show(window);await Snapshot(palette,Path.Combine(output,"palette.png"));palette.Close();
        int transparencyLabel=sidebar.Children.IndexOf(sidebar.Children.OfType<TextBlock>().Single(t=>t.Text?.StartsWith("Transparency  ")==true));
        var transparencySlider=(Slider)sidebar.Children[transparencyLabel+1];
        Check(transparencySlider.IsEnabled&&transparencySlider.Value==0,"Background transparency starts at zero and is editable without a photo");
        transparencySlider.Value=.5;
        Check(vm.Doc.BackgroundOpacity==.5,"Transparency slider inversely controls the opacity of the background");
        vm.Undo();Check(vm.Doc.BackgroundTransparency==0&&transparencySlider.Value==0,"Background transparency is undoable");
        var export=new ExportWindow(vm,window);Check(!export.BackgroundOption.IsEnabled,"Only the photo option is disabled without photo");export.Show(window);await Snapshot(export,Path.Combine(output,"export-no-photo.png"));export.Close();
        using var imageSurface=SKSurface.Create(new SKImageInfo(640,360));
        imageSurface.Canvas.Clear(SKColors.CornflowerBlue);using(var p=new SKPaint{Color=SKColors.Gold})imageSurface.Canvas.DrawCircle(320,180,100,p);
        using var image=imageSurface.Snapshot();using var encoded=image.Encode(SKEncodedImageFormat.Png,100);
        XNamespace photoNs="http://www.w3.org/2000/svg";
        foreach(string mode in new[]{"solid","photo-excluded","photo-included"}){
            if(mode=="photo-excluded")vm.AddPhoto(encoded.ToArray());
            bool includePhoto=mode=="photo-included";
            foreach(int percent in new[]{0,50,100}){
                vm.Update(d=>{d.BackgroundTransparency=percent/100.0;d.P=1;d.Q=1;d.PhotoFill=false;d.ExportWidth=256;foreach(var l in d.Layers)l.Visible=false;});
                string path=Path.Combine(output,$"background-{mode}-{percent}");
                ExportService.Export(vm,path+".png","png",includePhoto,true,"en");
                ExportService.Export(vm,path+".svg","svg",includePhoto,false,"en");
                using var decoded=SKBitmap.Decode(path+".png");
                var middle=decoded.GetPixel(128,128);
                var matte=decoded.GetPixel(128,16);
                int alpha=(int)Math.Round((1-percent/100.0)*255);
                Check(Math.Abs(middle.Alpha-alpha)<=1&&Math.Abs(matte.Alpha-alpha)<=1,$"PNG applies transparency to both picture and matte: {mode} {percent}");
                if(percent<100){
                    Check(Math.Abs(middle.Red-(includePhoto?255:70))<=1&&Math.Abs(matte.Red-70)<=1,$"PNG retains the expected photo or colour: {mode} {percent}");
                }
                var group=XDocument.Load(path+".svg").Descendants(photoNs+"g").SingleOrDefault(e=>(string?)e.Attribute("id")=="background");
                Check((group is null)==(percent==100),$"SVG emits a background only when visible: {mode} {percent}");
                if(group is not null){
                    Check(((double?)group.Attribute("opacity")??1)==1-percent/100.0,$"SVG applies one opacity to the entire background group: {mode} {percent}");
                    Check(group.Descendants(photoNs+"image").Any(e=>e.Attributes().Any(a=>a.Value.StartsWith("data:image/")))==includePhoto,$"SVG embeds photo only when requested: {mode} {percent}");
                }
                var brief=File.ReadAllText(path+".md");
                Check(brief.Contains(percent==100?"background is fully transparent":includePhoto?"exported background picture":"Solid guide background"),$"Prompt reflects actual background: {mode} {percent}");
            }
        }
        var withPhoto=new ExportWindow(vm,window);Check(withPhoto.BackgroundOption.IsEnabled,"Photo export enabled with photo");withPhoto.Show(window);await Snapshot(withPhoto,Path.Combine(output,"export-with-photo.png"));withPhoto.Close();
        vm.Update(d=>{d.Layers[1].Visible=true;d.Layers[3].Visible=true;d.BackgroundOpacity=.55;d.Mode=1;});
        string project=Path.Combine(output,"roundtrip.screenmap");StateStore.SaveProject(project,vm);
        using var other=new Editor();StateStore.OpenProject(project,other);
        Check(other.Photo is not null&&other.Photo.Bytes.SequenceEqual(vm.Photo!.Bytes),"Embedded photo roundtrip");
        Check(other.Doc.BackgroundOpacity==.55&&other.Doc.Mode==1,"Document roundtrip");
        var legacyOpacity=JsonSerializer.Deserialize<DocumentSettings>("""{"PhotoOpacity":0.25}""");
        Check(legacyOpacity?.BackgroundTransparency==.75,"Existing PhotoOpacity values load on the inverted transparency scale");
        vm.UpdatePreferences(p=>{p.Language="ru";p.Theme="Light";p.BaseSize=16;p.QuickMenu=["orientation","layer1","export"];});
        vm.SaveTheme("Custom test");vm.Update(d=>d.Layers[1].Color="#123456",false);vm.SaveTheme("Custom test");
        Check(vm.Prefs.ScreenmapThemes.Count(t=>t.Name=="Custom test")==1&&vm.Prefs.ScreenmapThemes.First(t=>t.Name=="Custom test").Layers[1].Color=="#123456","Custom theme updates in place");
        string sessionDir=Path.Combine(output,"restored-session");StateStore.SaveSession(sessionDir,vm,1440,940);
        using var restored=new Editor();var session=StateStore.LoadSession(sessionDir,restored);
        Check(session is not null&&restored.Prefs.Language=="ru"&&restored.Prefs.Theme=="Light"&&restored.Prefs.BaseSize==16,"Preferences restored");
        Check(restored.Prefs.QuickMenu.SequenceEqual(vm.Prefs.QuickMenu)&&restored.Prefs.ScreenmapThemes.Any(t=>t.Name=="Custom test"),"Quick menu and custom theme restored");
        Check(restored.Photo is not null&&restored.Doc.BackgroundOpacity==.55,"Background restored");
        var closeEditor=new Editor();closeEditor.UpdatePreferences(p=>{p.Language="en";p.Theme="Dark";p.QuickMenu=["orientation"];});closeEditor.Update(d=>d.Radius=.14);
        string closeDir=Path.Combine(output,"normal-close");
        var closeWindow=new MainWindow(closeEditor,closeDir,false);closeWindow.Show(window);closeWindow.Close();
        using var reopened=new Editor();StateStore.LoadSession(closeDir,reopened);
        Check(Math.Abs(reopened.Doc.Radius-.14)<1e-8&&reopened.Prefs.QuickMenu.SequenceEqual(new[]{"orientation"}),"Closing window persists state");
        vm.Replace(new DocumentSettings());vm.Update(d=>d.PrimitiveDensities["16:9"]=.1);
        vm.Update(d=>{d.P=3;d.Q=4;d.PrimitiveDensities["3:4"]=.8;});
        vm.Update(d=>{d.P=16;d.Q=9;});
        Check(vm.Doc.PrimitiveDensities["16:9"]==.1&&vm.Doc.PrimitiveDensities["3:4"]==.8,"Primitive density is independent per AR");
        vm.Replace(new DocumentSettings());vm.Update(d=>d.BackgroundTransparency=1,false);vm.UpdatePreferences(p=>{p.Language="en";p.Theme="Dark";p.BaseSize=14;});
        var a=Path.Combine(output,"dpi-96.png");var b=Path.Combine(output,"dpi-300.png");
        RenderEngine.Png(vm,a,800,96,false);RenderEngine.Png(vm,b,800,300,false);
        using var ba=SKBitmap.Decode(a);using var bb=SKBitmap.Decode(b);
        Check(ba.Width==800&&ba.Height==450,"PNG dimensions");
        var pngBytes=File.ReadAllBytes(b);
        Check(System.Text.Encoding.ASCII.GetString(pngBytes,37,4)=="pHYs"&&Math.Abs(BinaryPrimitives.ReadUInt32BigEndian(pngBytes.AsSpan(41,4))*.0254-300)<.1,"PNG DPI metadata");
        var legacy=Path.Combine(output,"legacy-v1.screenmap");
        using(var legacyFile=File.Create(legacy))using(var archive=new ZipArchive(legacyFile,ZipArchiveMode.Create)){
            using(var stream=archive.CreateEntry("manifest.json").Open())JsonSerializer.Serialize(stream,new{format="aqorde-screenmap",version=1,background=(string?)null});
            using(var stream=archive.CreateEntry("document.json").Open())JsonSerializer.Serialize(stream,new{SchemaVersion=1,ZoneNumbering="screenmap-zones-v1",Mode=0,P=16,Q=9,CenterX=.5,CenterY=.5,Radius=.065,Density=8,PgfaDensity=2,Layers=new DocumentSettings().Layers});
        }
        using var old=new Editor();StateStore.OpenProject(legacy,old);
        Check(old.Doc.SchemaVersion==2&&old.Scene.Config.Abi==2,"Legacy v0.3 projects migrate");
        Check(old.Doc.Layers[1].FontSize==24&&old.Doc.Layers[1].Shift,"Legacy captions receive v0.4 defaults");Check(ba.Bytes.SequenceEqual(bb.Bytes),"DPI does not change pixels");
        Check(ba.Bytes.Where((_,i)=>i%4==3).Any(v=>v==0)&&ba.Bytes.Where((_,i)=>i%4==3).Any(v=>v>0),"Transparent background retains visible grid pixels");
        string svg=Path.Combine(output,"verified.svg");RenderEngine.Svg(vm,svg,800,false);XNamespace ns="http://www.w3.org/2000/svg";var xml=XDocument.Load(svg);
        Check(xml.Root?.Attribute("viewBox")?.Value=="0 0 800 450","SVG dimensions");
        Check(xml.Descendants(ns+"g").Any(g=>g.Attribute("id")?.Value=="depth"),"SVG named layers");
        Check(xml.Descendants(ns+"path").Any()&&!xml.Descendants(ns+"text").Any(),"SVG glyph outlines");
        await Snapshot(window,Path.Combine(output,"final-dark.png"));
        await Snapshot(window,Path.Combine(output,"final-dark-150.png"),1.5);
        await Snapshot(window,Path.Combine(output,"final-dark-200.png"),2);
        assertions+=await Regression050.Run(window,output);
        assertions+=await Regression051.Run(window,output);
        assertions+=await Regression052.Run(window,output);
        assertions+=await Regression055.Run(window,output);
        StateStore.Atomic(Path.Combine(output,"verification.json"),JsonSerializer.SerializeToUtf8Bytes(new{success=true,assertions,ui="Avalonia 12.1.4 / Skia",nativeAbi=Native.sm_abi_version(),windowCaptureUsed=false,computerUseUsed=false,inputBackend="Avalonia.Headless synthetic events",realWindowsDpiTest=false,configs=configDir},StateStore.Json));
    }
}
