using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SkiaSharp;
using System.Text.Json;
using System.Xml.Linq;
namespace Screenmap.App;

internal static class Regression050 {
    internal static async Task<int> Run(MainWindow window,string output){
        var vm=window.Editor;var checks=new List<string>();
        void Check(bool pass,string name){if(!pass)throw new InvalidOperationException("v0.5 regression: "+name);checks.Add(name);}
        void Reset(){vm.Replace(new DocumentSettings());vm.UpdatePreferences(p=>{p.Language="ru";p.Theme="Dark";p.Compact=false;p.LayerDisplay="Both";p.QuickMenu=["perspective","layer0","layer1","layer2","layer3","layer4"];});}
        var dir=Path.Combine(output,"v050");Directory.CreateDirectory(dir);
        async Task Capture(string name){
            StateStore.Atomic(Path.Combine(output,"configs",name+".json"),JsonSerializer.SerializeToUtf8Bytes(new SessionState{Document=vm.Doc,Preferences=vm.Prefs},StateStore.Json));
            await Verification.Snapshot(window,Path.Combine(dir,name+".png"));
            RenderEngine.Svg(vm,Path.Combine(dir,name+".svg"),1280,false);
        }
        Reset();
        foreach(var (p,q) in new[]{(16,9),(9,16),(4,3),(3,4),(1,1)}){
            vm.Update(d=>{d.P=p;d.Q=q;d.Mode=1;d.PrimitiveDensities[d.AspectKey]=1;});
            var steps=vm.Doc.PrimitiveSteps();var max=steps[^1];
            Check(steps[0]==(1,1)&&Math.Max(max.Columns,max.Rows)==16,$"density endpoints {p}:{q}");
            Check(steps.Distinct().Count()==steps.Length,$"no repeated density detents {p}:{q}");
            Check(Enumerable.Range(1,steps.Length-1).All(i=>steps[i].Columns>=steps[i-1].Columns&&steps[i].Rows>=steps[i-1].Rows),$"monotonic density {p}:{q}");
            for(int j=0;j<steps.Length;j++){
                vm.Update(d=>d.SetPrimitiveStep(j));Check(vm.Doc.PrimitiveShape()==steps[j],$"detent maps exactly {p}:{q}/{j}");
            }
            await Capture($"density-{p}-{q}");
        }
        Reset();vm.Update(d=>{d.Mode=1;d.PrimitiveCircle=true;d.PrimitiveRadius=.04;});
        double Far()=>vm.Scene.Lines.First(l=>l.Layer==1&&l.Kind==7).X1;
        double small=Far();vm.Update(d=>d.PrimitiveRadius=.16);Check(Far()<small-.05,"enabled primitive marker drives depth");
        vm.Update(d=>d.PrimitiveCircle=false);double disabled=Far();vm.Update(d=>d.PrimitiveRadius=.08);Check(Math.Abs(Far()-disabled)<1e-10,"disabled primitive marker does not drive depth");
        Check(Math.Abs(vm.Doc.Radius-.075)<1e-10,"primitive edits preserve perspective radius");
        vm.Update(d=>{d.BackgroundTransparency=1;d.PrimitiveCircle=true;d.PrimitiveRadius=.12;foreach(var l in d.Layers)l.Visible=false;d.Layers[4].Visible=true;d.SetPrimitiveStep(2);});
        string png=Path.Combine(dir,"filled.png");RenderEngine.Png(vm,png,1280,96,false);
        using(var image=SKBitmap.Decode(png))Check(image.GetPixel(image.Width/2,image.Height/2).Alpha>0,"filled centre is opaque");
        vm.Update(d=>d.PrimitiveHollow=true,false);png=Path.Combine(dir,"hollow.png");RenderEngine.Png(vm,png,1280,96,false);
        using(var image=SKBitmap.Decode(png))Check(image.GetPixel(image.Width/2,image.Height/2).Alpha is >0 and <220,"hollow centre keeps dimmed lines without a background patch");
        await Capture("primitive-hollow-ru");
        Check(window.Actions.First(a=>a.Id=="perspective").Enabled?.Invoke()==false,"perspective action disabled in Primitive");
        Check(window.GetVisualDescendants().OfType<TextBlock>().Any(t=>t.Text=="Простая сетка"),"Russian primitive section title");
        vm.Update(d=>d.SetPrimitiveStep(5));
        await Verification.Snapshot(window,Path.Combine(dir,"wheel-before.png"));
        var density=window.GetVisualDescendants().OfType<Slider>().First(s=>s.IsSnapToTickEnabled&&s.Maximum>6);
        density.BringIntoView();Dispatcher.UIThread.RunJobs();
        var parent=density.GetVisualAncestors().OfType<ScrollViewer>().First();var offset=parent.Offset;int old=vm.Doc.PrimitiveStep;
        var position=density.TranslatePoint(new Point(density.Bounds.Width/2,density.Bounds.Height/2),window)!.Value;
        window.MouseMove(position);window.MouseWheel(position,new Vector(0,1));Dispatcher.UIThread.RunJobs();
        Check(vm.Doc.PrimitiveStep==old+1,"wheel advances one density detent");Check(parent.Offset==offset,"wheel over slider does not scroll sidebar");
        Reset();vm.Update(d=>d.BackgroundTransparency=1,false);foreach(int layer in new[]{3,4}){
            vm.Update(d=>{foreach(var l in d.Layers)l.Visible=false;d.Layers[layer].Visible=true;d.Layers[layer].Opacity=1;},false);
            png=Path.Combine(dir,$"only-{layer}.png");RenderEngine.Png(vm,png,1280,96,false);
            using(var image=SKBitmap.Decode(png))Check(image.Bytes.Where((_,i)=>i%4==3).Any(a=>a>0),$"layer {layer} renders independently");
            vm.Update(d=>d.Layers[layer].Opacity=0,false);RenderEngine.Png(vm,png,1280,96,false);
            using(var image=SKBitmap.Decode(png))Check(image.Bytes.Where((_,i)=>i%4==3).All(a=>a==0),$"layer {layer} opacity controls only its own contents");
        }
        Check(!vm.Scene.Lines.Any(l=>l.Layer==3)&&vm.Scene.Lines.Any(l=>l.Layer==4),"zone numbers and boundary lines have separate layers");
        Reset();vm.Update(d=>{d.BackgroundTransparency=1;d.Layers[1].Bounded=false;},false);await Capture("depth-unbounded");
        // Skia flattens the dash effect into vector paths rather than dash attributes.
        var xml=XDocument.Load(Path.Combine(dir,"depth-unbounded.svg"));XNamespace ns="http://www.w3.org/2000/svg";
        var depth=xml.Root!.Elements(ns+"g").First(g=>g.Attribute("id")?.Value=="depth");
        vm.Update(d=>d.Layers[1].Bounded=true,false);string bounded=Path.Combine(dir,"bounded.svg");RenderEngine.Svg(vm,bounded,1280,false);
        var plain=XDocument.Load(bounded).Root!.Elements(ns+"g").First(g=>g.Attribute("id")?.Value=="depth");
        Check(depth.ToString().Length>plain.ToString().Length,"SVG encodes dotted horizontal extensions as vector outlines");
        vm.Update(d=>{d.Layers[1].Bounded=false;foreach(var l in d.Layers)l.Visible=false;d.Layers[1].Visible=true;},false);
        png=Path.Combine(dir,"extended-horizontal.png");RenderEngine.Png(vm,png,1280,96,false);
        using(var image=SKBitmap.Decode(png)){
            var alphas=Enumerable.Range(2,50).Select(x=>image.GetPixel(x,(int)Math.Round(image.Height*.055)).Alpha).ToArray();
            Check(alphas.Any(a=>a==0)&&alphas.Any(a=>a>0),"unbounded depth horizontal extension is dotted");
        }
        vm.Update(d=>{d.Layers[2].Visible=true;d.Layers[3].Visible=true;d.Layers[4].Visible=true;},false);
        vm.Update(d=>d.Layers[1].Shift=false);await Capture("depth-unshifted");
        vm.Update(d=>{d.Layers[1].LineWidth=5;d.Layers[4].LineWidth=3;},false);await Capture("thick-lines");
        string project=Path.Combine(dir,"v050.screenmap");StateStore.SaveProject(project,vm);using(var copy=new Editor()){
            StateStore.OpenProject(project,copy);Check(copy.Doc.Layers[1].LineWidth==5&&!copy.Doc.Layers[1].Bounded&&!copy.Doc.Layers[1].Shift,"new layer modifiers roundtrip");
        }
        Reset();
        foreach(string lang in new[]{"ru","en"}){
            vm.UpdatePreferences(p=>p.Language=lang);var guide=new DocumentationWindow(vm);guide.Show(window);await Verification.Snapshot(guide,Path.Combine(dir,$"guide-{lang}.png"));
            Check(guide.Guide.AnchorCount>=17&&guide.Guide.LinkCount>=32&&guide.Guide.UnresolvedLinks==0,$"embedded Markdown anchors resolve {lang}");
            Check(guide.Guide.Navigate(lang=="ru"?"orcas":"orcas"),$"guide section navigation {lang}");Dispatcher.UIThread.RunJobs();await Verification.Snapshot(guide,Path.Combine(dir,$"guide-example-{lang}.png"));
            Check(guide.Guide.Navigate("top"),$"guide back to top {lang}");guide.Close();
            var about=new AboutWindow(vm);about.Show(window);await Verification.Snapshot(about,Path.Combine(dir,$"about-{lang}.png"));
            Check(about.GetVisualDescendants().OfType<Image>().Any(i=>i.Source is not null),$"About has supplied logo {lang}");about.Close();
        }
        foreach(string theme in new[]{"Dark","Light"})foreach(bool compact in new[]{false,true}){
            vm.UpdatePreferences(p=>{p.Theme=theme;p.Compact=compact;p.Language="ru";});await Capture($"theme-{theme}-{(compact?"compact":"normal")}");
            var settings=window.CreateSettingsForRender();settings.SelectPage(1);settings.Show(window);await Verification.Snapshot(settings,Path.Combine(dir,$"settings-{theme}-{compact}.png"));
            Check(settings.CanResize&&settings.ResizeEdge(new Point(1,1))==WindowEdge.NorthWest&&settings.ResizeEdge(new Point(settings.Bounds.Width-1,100))==WindowEdge.East,$"floating resize edges {theme}/{compact}");
            Check(settings.DragBar.Background is not null,$"title drag region participates in hit testing {theme}/{compact}");
            bool dispatched=false;settings.AddHandler(InputElement.PointerPressedEvent,(_,e)=>dispatched=e.Handled,RoutingStrategies.Bubble,true);
            var bar=settings.DragBar.TranslatePoint(new Point(70,20),settings)!.Value;
            settings.MouseDown(bar,MouseButton.Left);settings.MouseUp(bar,MouseButton.Left);
            Check(dispatched,$"pointer dispatch reaches title move handler {theme}/{compact}");
            dispatched=false;settings.MouseDown(new Point(2,2),MouseButton.Left);settings.MouseUp(new Point(2,2),MouseButton.Left);
            Check(dispatched,$"pointer dispatch reaches window resize handler {theme}/{compact}");
            Check(settings.Background is ISolidColorBrush brush&&brush.Color.A==0,$"floating window background transparent {theme}/{compact}");
            using(var image=SKBitmap.Decode(Path.Combine(dir,$"settings-{theme}-{compact}.png")))Check(image.GetPixel(7,7).Alpha==0&&image.GetPixel(40,40).Alpha>0,$"no opaque square corner {theme}/{compact}");
            settings.Close();
            var variant=theme=="Dark"?ThemeVariant.Dark:ThemeVariant.Light;
            Check(Application.Current!.TryGetResource("Ink",variant,out var ink)&&ink is ISolidColorBrush ib&&ib.Color.ToString().EndsWith(theme=="Dark"?"d5dbe3":"1b2a3a",StringComparison.OrdinalIgnoreCase),$"gallery palette applied {theme}/{compact}");
            Check(Application.Current!.TryGetResource("ToggleButtonForegroundChecked",variant,out var selectedInk)&&selectedInk is ISolidColorBrush si&&si.Color==Color.Parse(theme=="Dark"?"#D5DBE3":"#FFFFFF"),$"selected button text has contrasting foreground {theme}/{compact}");
        }
        vm.UpdatePreferences(p=>{p.LayerDisplay="Icons";p.Compact=true;});await Capture("icons-only");
        vm.UpdatePreferences(p=>p.LayerDisplay="Text");await Capture("text-only");
        vm.UpdatePreferences(p=>p.LayerDisplay="Both");
        string config=Path.Combine(dir,"session");StateStore.SaveSession(config,vm,1440,940);
        using(var restored=new Editor()){StateStore.LoadSession(config,restored);Check(restored.Prefs.Compact&&restored.Prefs.LayerDisplay=="Both","compact and layer display preferences persist");}
        StateStore.Atomic(Path.Combine(dir,"regression.json"),JsonSerializer.SerializeToUtf8Bytes(new{success=true,assertions=checks.Count,checks,osWindowDragAndResize="manual acceptance remains; headless tests cover hit regions and render",computerUseUsed=false},StateStore.Json));
        return checks.Count;
    }
}
