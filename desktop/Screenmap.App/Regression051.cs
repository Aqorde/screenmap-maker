using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Chrome;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SkiaSharp;
using System.Diagnostics;
using System.Text.Json;
using System.Xml.Linq;
namespace Screenmap.App;

internal static class Regression051 {
    internal static async Task<int> Run(MainWindow window,string output){
        var vm=window.Editor;var checks=new List<string>();var timer=Stopwatch.StartNew();
        void Check(bool pass,string name){if(!pass)throw new InvalidOperationException("v0.5.1: "+name);checks.Add(name);}
        var dir=Path.Combine(output,"v051");Directory.CreateDirectory(dir);
        void Reset(){vm.Replace(new());vm.PerspectiveEditing=false;vm.UpdatePreferences(p=>{p.Language="ru";p.Theme="Dark";p.Compact=false;p.BaseSize=14;p.LayerDisplay="Both";p.QuickMenu=["perspective","orientation","export"];});}
        async Task Capture(string name){
            StateStore.Atomic(Path.Combine(output,"configs",name+".json"),JsonSerializer.SerializeToUtf8Bytes(new SessionState{Document=vm.Doc,Preferences=vm.Prefs,Width=window.Width,Height=window.Height},StateStore.Json));
            await Verification.Snapshot(window,Path.Combine(dir,name+".png"));
            RenderEngine.Png(vm,Path.Combine(dir,name+"-grid.png"),1280,96,false);RenderEngine.Svg(vm,Path.Combine(dir,name+".svg"),1280,false);
        }
        Reset();
        // Independently measure the paths actually drawn, rather than accepting
        // the layout's own collision predicate as its proof.
        using var face=SKTypeface.FromFamilyName("Calibri");
        int scenes=0,leaders=0;
        foreach(var (p,q) in new[]{(16,9),(9,16),(4,3),(3,4),(1,1)})
        foreach(var (x,y) in new[]{(.08,.08),(.23,.31),(.3,.62),(.75,.72),(.92,.92)})
        foreach(int style in new[]{0,1,2})foreach(bool shift in new[]{false,true}){
            var d=new DocumentSettings{P=p,Q=q,CenterX=x,CenterY=y,Radius=style switch{0=>.006,1=>.075,_=>.23}};
            d.Layers[3].FontSize=style switch{0=>18,1=>30,_=>72};d.Layers[2].FontSize=style==2?72:36;d.Layers[1].FontSize=style==2?72:24;d.Layers[1].Shift=shift;
            using var scene=new NativeScene(d);float w=720f*p/Math.Min(p,q),h=720f*q/Math.Min(p,q);
            var layout=LabelLayout.Build(d,scene,"Calibri",w,h);
            SKRect Actual(PlacedLabel l){using var f=new SKFont(face,l.Size);using var path=f.GetTextPath(l.Text,new SKPoint(l.X,l.Y));return path.Bounds;}
            var depth=layout.Where(l=>l.Layer==1).Select(l=>(Label:l,Bounds:Actual(l))).ToArray();
            var other=layout.Where(l=>l.Layer!=1).Select(Actual).ToArray();
            Check(depth.Length==8&&depth.Select(l=>l.Label.Id).SequenceEqual(Enumerable.Range(0,8)),$"B2 retains all D captions {scenes}");
            Check(layout.Count(l=>l.Layer==3)==33,$"B2 retains all zone IDs {scenes}");
            bool Separate(SKRect a,SKRect b)=>Math.Min(a.Right,b.Right)-Math.Max(a.Left,b.Left)<=.01f||Math.Min(a.Bottom,b.Bottom)-Math.Max(a.Top,b.Top)<=.01f;
            Check(depth.All(a=>other.All(b=>Separate(a.Bounds,b))),$"B2 depth paths do not overlap zone/level paths {scenes}");
            Check(depth.SelectMany((a,i)=>depth.Skip(i+1).Select(b=>Separate(a.Bounds,b.Bounds))).All(v=>v),$"depth captions do not overlap each other {scenes}");
            Check(depth.All(a=>a.Bounds.Left>=-.02&&a.Bounds.Top>=-.02&&a.Bounds.Right<=w+.02&&a.Bounds.Bottom<=h+.02),$"depth captions stay inside frame {scenes}");
            leaders+=depth.Count(a=>a.Label.Leader is not null);scenes++;
        }
        foreach(var (p,q) in new[]{(16,9),(9,16),(4,3),(3,4),(1,1)})foreach(int size in new[]{18,72}){
            var d=new DocumentSettings{Mode=1,P=p,Q=q};d.PrimitiveDensities[d.AspectKey]=1;d.Layers[3].FontSize=size;
            using var scene=new NativeScene(d);var layout=LabelLayout.Build(d,scene,"Calibri",720f*p/Math.Min(p,q),720f*q/Math.Min(p,q));
            var depth=layout.Where(l=>l.Layer==1).ToArray();var zones=layout.Where(l=>l.Layer==3).ToArray();
            Check(depth.Length==8&&zones.Length==d.PrimitiveShape().Columns*d.PrimitiveShape().Rows,$"dense primitive keeps all captions {p}:{q}/{size}");
            Check(depth.All(a=>zones.All(b=>!LabelLayout.Overlap(a.Bounds,b.Bounds))),$"dense primitive depth avoids zone IDs {p}:{q}/{size}");
            leaders+=depth.Count(a=>a.Leader is not null);scenes++;
        }
        Reset();vm.Update(d=>{d.CenterX=.3;d.CenterY=.62;d.Radius=.13;});await Capture("b2-offset-left");
        var one=LabelLayout.Build(vm.Doc,vm.Scene,"Calibri",1280,720);var half=LabelLayout.Build(vm.Doc,vm.Scene,"Calibri",640,360);
        Check(one.Length==half.Length&&one.Zip(half).All(v=>Math.Abs(v.First.X-v.Second.X*2)<.02&&Math.Abs(v.First.Y-v.Second.Y*2)<.02&&Math.Abs(v.First.Size-v.Second.Size*2)<.01),"viewport and export label positions scale consistently");
        var zone=one.First(l=>l.Layer==3&&l.Id==202);var d1=one.First(l=>l.Layer==1&&l.Id==1);
        Check(!LabelLayout.Overlap(zone.Bounds,d1.Bounds),"original B2 D1 versus 2.2 regression");
        vm.Update(d=>{d.CenterX=.23;d.CenterY=.31;d.Radius=.045;d.PgfaDensity=2;d.Layers[0].Visible=true;});await Capture("b2-original-top-left");
        vm.Update(d=>{d.CenterX=.75;d.CenterY=.72;d.Radius=.1;});await Capture("b2-offset-right");
        Reset();vm.Update(d=>{foreach(var l in d.Layers)l.Visible=false;d.Layers[1].Visible=true;d.Layers[1].Labels=false;d.Layers[1].Bounded=false;});
        var strokes=RenderEngine.Strokes(vm.Scene,1,false).ToArray();var solid=strokes.Where(s=>!s.Dotted).ToArray();
        Check(solid.Length==20,"open depth keeps 16 horizontals and four corner rays");
        Check(strokes.Where(s=>s.Dotted).All(s=>Math.Abs(s.Y1-s.Y2)<1e-10&&(s.X1==0||s.X2==1)),"only horizontal continuations to frame edge are dotted");
        Check(solid.Count(s=>Math.Abs(s.Y1-s.Y2)>1e-10)==4,"only the four corner rays remain outside the depth horizontals");
        await Capture("depth-dotted-extensions");
        using(var png=SKBitmap.Decode(Path.Combine(dir,"depth-dotted-extensions-grid.png"))){
            int row=(int)Math.Round(png.Height*.055);var extension=Enumerable.Range(2,50).Select(x=>png.GetPixel(x,row).Alpha).ToArray();
            Check(extension.Any(a=>a>0)&&extension.Any(a=>a==0),"PNG horizontal extension has dots and gaps");
            Check(Enumerable.Range(100,400).All(x=>png.GetPixel(x,row).Alpha>0),"PNG original horizontal remains continuous");
            int col=(int)Math.Round(png.Width*.055);Check(png.GetPixel(col,png.Height/2).Alpha==0,"PNG omits depth vertical at the side");
        }
        Reset();
        vm.Update(d=>{
            foreach(var layer in d.Layers)layer.Visible=false;
            d.Layers[0].Visible=true;d.PgfaDensity=4;
        });
        vm.UpdatePreferences(p=>p.QuickMenu.Add("boundDepth"));
        var quickBar=window.FindControl<WrapPanel>("QuickBar")!;
        var depthToggle=quickBar.Children.OfType<ToggleButton>().Single(b=>Equals(b.Content,"Ограничить глубину"));
        var depthSettings=window.CreateSettingsForRender();depthSettings.SelectPage(1);depthSettings.Show(window);
        await Verification.Snapshot(depthSettings,Path.Combine(dir,"depth-settings-closed.png"));
        var boundOption=depthSettings.GetVisualDescendants().OfType<CheckBox>().Single(c=>Equals(c.Content,"Ограничить глубину"));
        string closedPgfa=Path.Combine(dir,"pgfa-closed.png");
        RenderEngine.Png(vm,closedPgfa,1280,96,false);
        depthToggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(!vm.Doc.Layers[1].Bounded&&depthToggle.IsChecked==false&&boundOption.IsChecked==false,"quick action opens PGFA and syncs Appearance even with depth hidden");
        string openPgfa=Path.Combine(dir,"pgfa-open.png");
        RenderEngine.Png(vm,openPgfa,1280,96,false);
        using(var closed=SKBitmap.Decode(closedPgfa))
        using(var open=SKBitmap.Decode(openPgfa)){
            int col=(int)Math.Round(open.Width*.055),mid=open.Height/2;
            Check(closed.GetPixel(col,mid).Alpha>0&&open.GetPixel(col,mid).Alpha==0,"PGFA PNG drops vertical wall edges");
            Check(closed.GetPixel(open.Width/2,(int)Math.Round(open.Height*.055)).Alpha>0
                &&open.GetPixel(open.Width/2,(int)Math.Round(open.Height*.055)).Alpha>0
                &&open.GetPixel(open.Width/2,(int)Math.Round(open.Height*.945)).Alpha>0,"PGFA PNG retains upper and lower planes");
            var crossbar=vm.Scene.Lines.First(l=>l.Layer==0&&l.Kind==NativeLine.PgfaCrossbar);
            int subdivisionRow=(int)(open.Height*crossbar.Y1);
            Check(closed.GetPixel(2,subdivisionRow).Alpha==0
                &&Enumerable.Range(0,open.Width).All(x=>open.GetPixel(x,subdivisionRow).Alpha>0)
                &&open.GetPixel(2,(int)(open.Height*.945)).Alpha>0,
                "PGFA PNG extends solid subdivision and major horizontals to the image edges");
        }
        boundOption.IsChecked=true;
        Check(vm.Doc.Layers[1].Bounded&&depthToggle.IsChecked==true,"Appearance closes the sides and syncs the quick action");
        depthSettings.Close();
        vm.Update(d=>{
            d.P=4;d.Q=3;
            d.Layers[1].Visible=true;d.Layers[1].Bounded=false;
        });
        var centred=new[]{0,1}.Select(layer=>RenderEngine.Strokes(vm.Scene,layer,false).ToArray()).ToArray();
        vm.Update(d=>{d.CenterX=.42;d.CenterY=.47;});
        foreach(int layer in new[]{0,1}){
            var pairs=centred[layer].Zip(RenderEngine.Strokes(vm.Scene,layer,false)).ToArray();
            Check(pairs.Any(pair=>!pair.First.Dotted&&pair.First.Y1==pair.First.Y2
                    &&Math.Abs(pair.First.Y1-pair.Second.Y1)>.001)
                &&pairs.Any(pair=>!pair.First.Dotted&&pair.First.Y1!=pair.First.Y2
                    &&Math.Abs(pair.First.X2-pair.Second.X2)>.001),
                $"open layer {layer} updates horizontals and diagonals with the perspective centre");
        }
        var openRays=RenderEngine.Strokes(vm.Scene,0,false)
            .Where(s=>!s.Dotted&&s.Y1!=s.Y2&&(s.X1==0||s.X1==1)).ToArray();
        var closedPgfaStrokes=RenderEngine.Strokes(vm.Scene,0,true).ToHashSet();
        foreach(double edge in new[]{0.0,1.0}){
            var sideRays=openRays.Where(ray=>ray.X1==edge).ToArray();
            Check(sideRays.Any(ray=>ray.Y1<ray.Y2&&ray.Y2<vm.Doc.CenterY)
                &&sideRays.Any(ray=>ray.Y1>ray.Y2&&ray.Y2>vm.Doc.CenterY)
                &&sideRays.All(ray=>ray.X2>0&&ray.X2<1&&!closedPgfaStrokes.Contains(ray)),
                $"PGFA open diagonals enter both planes from image edge {edge}");
        }
        await Capture("sky-and-ground");
        Reset();vm.Update(d=>{d.Mode=1;d.PrimitiveCircle=true;d.PrimitiveHollow=true;d.PrimitiveRadius=.19;d.P=1;d.Q=1;d.SetPrimitiveStep(2);foreach(var l in d.Layers)l.Visible=false;d.Layers[3].Visible=true;d.Layers[3].Opacity=1;d.Layers[3].FontSize=36;d.Layers[4].Visible=true;d.Layers[4].Opacity=1;});
        await Capture("hollow-visible-numbers");
        using(var png=SKBitmap.Decode(Path.Combine(dir,"hollow-visible-numbers-grid.png"))){
            int mid=png.Width/2;var text=from xx in Enumerable.Range(mid-15,30) from yy in Enumerable.Range(mid-20,40) select png.GetPixel(xx,yy).Alpha;
            Check(text.Any(a=>a>245),"cell number inside hollow circle remains fully visible");
        }
        vm.Update(d=>d.SetPrimitiveStep(1));RenderEngine.Png(vm,Path.Combine(dir,"hollow-cross.png"),720,96,false);
        using(var png=SKBitmap.Decode(Path.Combine(dir,"hollow-cross.png")))Check(png.GetPixel(360,360).Alpha is >0 and <230,"lines under hollow centre remain visible but dimmed");
        Reset();
        foreach(string theme in new[]{"Dark","Light"}){
            vm.UpdatePreferences(p=>{p.Theme=theme;p.Compact=true;});await Capture("ui-"+theme);
            var variant=theme=="Dark"?ThemeVariant.Dark:ThemeVariant.Light;
            Application.Current!.TryGetResource("Surface0",variant,out var background);Application.Current!.TryGetResource("Surface1",variant,out var card);
            Check(background is ISolidColorBrush bg&&bg.Color==Color.Parse(theme=="Dark"?"#262A31":"#F7FAFF"),"swapped window background "+theme);
            Check(card is ISolidColorBrush fg&&fg.Color==Color.Parse(theme=="Dark"?"#1F2227":"#E6F0FF"),"swapped block background "+theme);
            var viewportTitle=window.FindControl<TextBlock>("ViewportTitle")!;
            Check(viewportTitle.GetVisualAncestors().Contains(window.FindControl<Border>("ViewportCard")!),"viewport heading is inside viewport card "+theme);
            var header=window.FindControl<TextBlock>("LayersTitle")!;var scroll=window.FindControl<ScrollViewer>("SidebarScroll")!;
            Check(!header.GetVisualAncestors().OfType<ScrollViewer>().Any(),"Layers heading is outside scrolling content "+theme);
            var before=header.TranslatePoint(new(0,0),window);scroll.Offset=new Vector(0,200);Dispatcher.UIThread.RunJobs();
            Check(header.TranslatePoint(new(0,0),window)==before,"Layers heading remains fixed while sidebar scrolls "+theme);
            Check(window.FindControl<StackPanel>("Sidebar")!.Margin.Right>=28,"sidebar reserves scrollbar gutter "+theme);
            await Verification.Snapshot(window,Path.Combine(dir,"sidebar-scrolled-"+theme+".png"));scroll.Offset=new Vector(0,0);
            var settings=window.CreateSettingsForRender();settings.SelectPage(1);settings.Show(window);await Verification.Snapshot(settings,Path.Combine(dir,"settings-"+theme+".png"));
            var texts=settings.GetVisualDescendants().OfType<TextBlock>().ToArray();
            Check(texts.Any(t=>t.Text=="Настройки глубины"),"separate Depth settings section "+theme);
            Check(!texts.Any(t=>t.Text=="Сохранить тему Screenmap"),"save-theme heading drops Screenmap "+theme);
            var save=texts.First(t=>t.Text=="Сохранить тему");save.BringIntoView();Dispatcher.UIThread.RunJobs();await Verification.Snapshot(settings,Path.Combine(dir,"settings-save-"+theme+".png"));
            var saveScroll=save.GetVisualAncestors().OfType<ScrollViewer>().First();Check(saveScroll.Content is Control saveContent&&saveContent.Margin.Right>=28,"settings reserve scrollbar gutter "+theme);
            settings.SelectPage(2);var quick=settings.GetVisualDescendants().OfType<ScrollViewer>().First();quick.Offset=new Vector(0,350);Dispatcher.UIThread.RunJobs();await Verification.Snapshot(settings,Path.Combine(dir,"settings-quick-scrolled-"+theme+".png"));settings.Close();
        }
        Check(window.WindowDecorations==WindowDecorations.BorderOnly&&window.ExtendClientAreaToDecorationsHint,"main window uses themed client-side title bar");
        Check(WindowDecorationProperties.GetElementRole(window.FindControl<Grid>("TitleBar")!)==WindowDecorationsElementRole.TitleBar,"native title-bar role is registered");
        var max=window.FindControl<Button>("MaximizeButton")!;max.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(window.WindowState==WindowState.Maximized,"caption maximize button works");max.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(window.WindowState==WindowState.Normal,"caption restore button works");
        window.FindControl<Button>("MinimizeButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(window.WindowState==WindowState.Minimized,"caption minimize button works");window.WindowState=WindowState.Normal;
        var about=new AboutWindow(vm);about.Show(window);await Verification.Snapshot(about,Path.Combine(dir,"about.png"));Check(!about.GetVisualDescendants().OfType<TextBlock>().Any(t=>t.Text?.Contains("C++ Core")==true),"About omits architecture line");about.Close();
        window.Width=1000;window.Height=680;await Capture("minimum-window");window.Width=1440;window.Height=940;
        var xml=XDocument.Load(Path.Combine(dir,"b2-offset-left.svg"));XNamespace ns="http://www.w3.org/2000/svg";var groups=xml.Root!.Elements(ns+"g").Select(g=>g.Attribute("id")?.Value??"").ToArray();
        Check(Array.IndexOf(groups,"depth")<Array.IndexOf(groups,"zones-labels")&&Array.IndexOf(groups,"depth-labels")>Array.IndexOf(groups,"zones-labels"),"SVG places all captions above geometry using shared layout");
        StateStore.Atomic(Path.Combine(dir,"regression.json"),JsonSerializer.SerializeToUtf8Bytes(new{success=true,assertions=checks.Count,b2Scenes=scenes,b2Callouts=leaders,elapsedSeconds=timer.Elapsed.TotalSeconds,checks,windowCaptureUsed=false,computerUseUsed=false},StateStore.Json));
        return checks.Count;
    }
}
