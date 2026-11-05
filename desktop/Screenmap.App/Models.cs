using System.Text.Json;
using SkiaSharp;
namespace Screenmap.App;
public sealed class LayerSettings {
    public bool Visible {get;set;}=true;
    public string Color {get;set;}="#C2CDD9";
    public double Opacity {get;set;}=1;
    public bool Labels {get;set;}=true;
    public double FontSize {get;set;}=18;
    public bool Shift {get;set;}
    public double LineWidth {get;set;}=1.25;
    public bool Bounded {get;set;}=true;
}
public sealed class DocumentSettings {
    public int SchemaVersion {get;set;}=2;
    public string ZoneNumbering {get;set;}="screenmap-zones-v1";
    public int Mode {get;set;}
    public int P {get;set;}=16;
    public int Q {get;set;}=9;
    public double CenterX {get;set;}=.5;
    public double CenterY {get;set;}=.5;
    public double Radius {get;set;}=.075;
    public double PrimitiveRadius {get;set;}=.025;
    public bool PrimitiveCircle {get;set;}
    public bool PrimitiveHollow {get;set;}
    public Dictionary<string,double> PrimitiveDensities {get;set;}=new(){{"16:9",.45},{"3:4",.5},{"1:1",.3}};
    public int PgfaDensity {get;set;}=1;
    public string Background {get;set;}="#464646";
    public string? PhotoId {get;set;}
    public double PhotoOpacity {get;set;}=1;
    public bool PhotoFill {get;set;}=true;
    public double PhotoX {get;set;}=.5;
    public double PhotoY {get;set;}=.5;
    public int ExportWidth {get;set;}=1920;
    public int ExportDpi {get;set;}=96;
    public bool ExportBackground {get;set;}
    public bool ExportPrompt {get;set;}
    public string ExportPromptLanguage {get;set;}="ru";
    public LayerSettings[] Layers {get;set;}=[
        new(){Visible=false,Color="#91A1B5",Opacity=.8,Labels=false,FontSize=14,LineWidth=.85},
        new(){Color="#EF8A8A",FontSize=24,Shift=true,LineWidth=1.8},
        new(){Color="#95DBA5",Opacity=.9,FontSize=36,LineWidth=3.3},
        new(){Color="#CAD3E0",Opacity=.85,FontSize=18},
        new(){Color="#CAD3E0",Opacity=.85,Labels=false,FontSize=18}];
    public string AspectKey=>Math.Max(P,Q)==16?"16:9":P==Q?"1:1":"3:4";
    public (int Columns,int Rows)[] PrimitiveSteps() {
        int maxC=(int)Math.Round(16.0*P/Math.Max(P,Q)),maxR=(int)Math.Round(16.0*Q/Math.Max(P,Q));
        // Every detent changes the cell count. Retain the old normalized value in files.
        var points=new SortedSet<double>{0,1};
        for(int i=1;i<maxC;i++)points.Add((i-.5)/(maxC-1));
        for(int i=1;i<maxR;i++)points.Add((i-.5)/(maxR-1));
        var values=points.ToArray();var result=new List<(int,int)>();
        for(int i=0;i<values.Length-1;i++){
            double t=(values[i]+values[i+1])/2;
            var shape=((int)Math.Round(1+t*(maxC-1),MidpointRounding.AwayFromZero),(int)Math.Round(1+t*(maxR-1),MidpointRounding.AwayFromZero));
            if(result.Count==0||result[^1]!=shape)result.Add(shape);
        }
        if(result[^1]!=(maxC,maxR))result.Add((maxC,maxR));return result.ToArray();
    }
    public int PrimitiveStep=>Math.Clamp((int)Math.Round(PrimitiveDensities.GetValueOrDefault(AspectKey,.4)*(PrimitiveSteps().Length-1),MidpointRounding.AwayFromZero),0,PrimitiveSteps().Length-1);
    public (int Columns,int Rows) PrimitiveShape()=>PrimitiveSteps()[PrimitiveStep];
    public void SetPrimitiveStep(int step)=>PrimitiveDensities[AspectKey]=(double)Math.Clamp(step,0,PrimitiveSteps().Length-1)/(PrimitiveSteps().Length-1);
}
public sealed class ScreenmapTheme {
    public string Name {get;set;}="";
    public string Background {get;set;}="#464646";
    public LayerSettings[] Layers {get;set;}=[];
}
public sealed class Preferences {
    public string Language {get;set;}="ru";
    public string Theme {get;set;}="System";
    public string FontFamily {get;set;}="Calibri";
    public double BaseSize {get;set;}=14;
    public bool Compact {get;set;}
    public string LayerDisplay {get;set;}="Both";
    public List<string> QuickMenu {get;set;}=["perspective","boundDepth","perspectiveReset"];
    public List<ScreenmapTheme> ScreenmapThemes {get;set;}=[];
    public string SelectedScreenmapTheme {get;set;}="Dark";
}
public sealed class SessionState {
    public int Version {get;set;}=2;
    public DocumentSettings Document {get;set;}=new();
    public Preferences Preferences {get;set;}=new();
    public string? PhotoFile {get;set;}
    public double Width {get;set;}=1440;
    public double Height {get;set;}=940;
}
internal sealed class PhotoAsset : IDisposable {
    internal byte[] Bytes {get;}
    internal SKImage Image {get;}
    internal PhotoAsset(byte[] bytes) {
        if(bytes.Length>64*1024*1024)throw new InvalidDataException("Image exceeds 64 MB");
        Bytes=bytes;
        using var data=SKData.CreateCopy(bytes);
        Image=SKImage.FromEncodedData(data)??throw new InvalidDataException("Invalid image");
        if((long)Image.Width*Image.Height>64000000){Image.Dispose();throw new InvalidDataException("Image exceeds 64 MP");}
    }
    public void Dispose()=>Image.Dispose();
}
internal sealed class Editor : IDisposable {
    internal DocumentSettings Doc {get;private set;}=new();
    internal Preferences Prefs {get;private set;}=new();
    internal NativeScene Scene {get;private set;}
    internal Dictionary<string,PhotoAsset> Photos {get;}=[];
    internal PhotoAsset? Photo=>Doc.PhotoId is not null&&Photos.TryGetValue(Doc.PhotoId,out var asset)?asset:null;
    internal bool PerspectiveEditing {get;set;}
    internal event Action? Changed;
    private readonly Stack<string> undo=new(),redo=new();
    private string? gesture;
    internal bool CanUndo=>undo.Count>0;
    internal bool CanRedo=>redo.Count>0;
    internal Editor(){Scene=new NativeScene(Doc); EnsureDefaultThemes();}
    internal string Snapshot()=>JsonSerializer.Serialize(Doc);
    internal static T Clone<T>(T value)=>JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;
    internal void Notify()=>Changed?.Invoke();
    internal void Update(Action<DocumentSettings> action,bool geometry=true,bool record=true) {
        string before=Snapshot();
        try {
            action(Doc); Validate(Doc);
            if(geometry)Rebuild();
            if(before!=Snapshot()&&record&&gesture is null){undo.Push(before);redo.Clear();Trim();}
            Notify();
        } catch {Doc=JsonSerializer.Deserialize<DocumentSettings>(before)!;throw;}
    }
    private void Trim(){if(undo.Count>80){var keep=undo.Take(80).Reverse().ToArray();undo.Clear();foreach(var s in keep)undo.Push(s);}}
    private void Rebuild(){
        var next=new NativeScene(Doc);var old=Scene;Scene=next;old.Dispose();
        if(Doc.Mode==0){Doc.CenterX=next.Config.X;Doc.CenterY=next.Config.Y;}
        if(Doc.Mode==0)Doc.Radius=next.Config.Radius;
    }
    internal void BeginGesture()=>gesture??=Snapshot();
    internal void EndGesture(bool cancel=false){
        if(gesture is null)return;var start=gesture;gesture=null;
        if(cancel){Doc=JsonSerializer.Deserialize<DocumentSettings>(start)!;Rebuild();}
        else if(start!=Snapshot()){undo.Push(start);redo.Clear();Trim();}
        Notify();
    }
    internal void Undo(){if(!CanUndo)return;redo.Push(Snapshot());Doc=JsonSerializer.Deserialize<DocumentSettings>(undo.Pop())!;Rebuild();Notify();}
    internal void Redo(){if(!CanRedo)return;undo.Push(Snapshot());Doc=JsonSerializer.Deserialize<DocumentSettings>(redo.Pop())!;Rebuild();Notify();}
    internal void Replace(DocumentSettings document){
        Validate(document);using var test=new NativeScene(document);Doc=document;Rebuild();undo.Clear();redo.Clear();Notify();
    }
    internal void SetPreferences(Preferences preferences){Prefs=preferences;EnsureDefaultThemes();Notify();}
    internal void UpdatePreferences(Action<Preferences> action){action(Prefs);ValidatePreferences(Prefs);Notify();}
    internal string AddPhoto(byte[] bytes){
        var asset=new PhotoAsset(bytes);string key=Guid.NewGuid().ToString("N");Photos.Add(key,asset);
        Update(d=>d.PhotoId=key,false);return key;
    }
    internal void EnsureDefaultThemes(){
        foreach(bool dark in new[]{true,false}) {
            string name=dark?"Dark":"Light"; if(Prefs.ScreenmapThemes.Any(t=>t.Name==name))continue;
            var doc=new DocumentSettings();
            if(!dark){doc.Background="#EDF1F5";doc.Layers[0].Color="#73859C";doc.Layers[1].Color="#AD4253";doc.Layers[2].Color="#39754C";doc.Layers[3].Color="#4D596A";doc.Layers[4].Color="#4D596A";}
            Prefs.ScreenmapThemes.Insert(dark?0:Math.Min(1,Prefs.ScreenmapThemes.Count),new(){Name=name,Background=doc.Background,Layers=Clone(doc.Layers)});
        }
    }
    internal void ApplyTheme(string name){
        var t=Prefs.ScreenmapThemes.FirstOrDefault(t=>t.Name==name);if(t is null)return;
        Update(d=>{d.Background=t.Background;d.Layers=Clone(t.Layers);});
        Prefs.SelectedScreenmapTheme=name;Notify();
    }
    internal void SaveTheme(string name){
        name=name.Trim();if(name.Length is <1 or >48)throw new ArgumentException("Theme name: 1–48 characters");
        if(name is "Dark" or "Light")throw new ArgumentException("Choose a new theme name");
        var t=new ScreenmapTheme{Name=name,Background=Doc.Background,Layers=Clone(Doc.Layers)};
        Prefs.ScreenmapThemes.RemoveAll(t=>t.Name==name);Prefs.ScreenmapThemes.Add(t);Prefs.SelectedScreenmapTheme=name;Notify();
    }
    internal static void ValidatePreferences(Preferences p){
        if(p.Language is not ("ru" or "en")||p.Theme is not ("Dark" or "Light" or "System")||p.LayerDisplay is not ("Both" or "Icons" or "Text")||p.BaseSize is <10 or >28||!double.IsFinite(p.BaseSize)||string.IsNullOrWhiteSpace(p.FontFamily)||p.FontFamily.Length>200||p.QuickMenu is null||p.QuickMenu.Count>100||p.ScreenmapThemes is null||p.ScreenmapThemes.Count>100)throw new InvalidDataException("Invalid preferences");
        foreach(var t in p.ScreenmapThemes){if(t.Name.Length>48||t.Layers.Length!=5||!Color(t.Background))throw new InvalidDataException("Invalid saved theme");foreach(var layer in t.Layers)ValidateLayer(layer);}
    }
    private static bool Color(string? v)=>v is {Length:7}&&v[0]=='#'&&v.Skip(1).All(Uri.IsHexDigit);
    private static void ValidateLayer(LayerSettings s){
        if(s is null||!Color(s.Color)||!double.IsFinite(s.Opacity)||s.Opacity is <0 or >1||!double.IsFinite(s.LineWidth)||s.LineWidth is <.25 or >12||!double.IsFinite(s.FontSize)||s.FontSize is <8 or >72)throw new InvalidDataException("Invalid layer");
    }
    internal static void Validate(DocumentSettings d){
        if(d.ExportPromptLanguage is not ("ru" or "en"))throw new InvalidDataException("Invalid prompt language");
        if(d.SchemaVersion!=2||d.ZoneNumbering!="screenmap-zones-v1"||d.Layers is null||d.Layers.Length!=5||!Color(d.Background)||d.PrimitiveDensities is null||d.PrimitiveDensities.Values.Any(v=>!double.IsFinite(v)||v is <0 or >1)||d.PhotoId?.Length>100||!double.IsFinite(d.PhotoOpacity)||d.PhotoOpacity is <0 or >1||d.ExportWidth is <64 or >8192||d.ExportDpi is <36 or >1200||!double.IsFinite(d.PhotoX)||!double.IsFinite(d.PhotoY)||d.PhotoX is <0 or >1||d.PhotoY is <0 or >1)throw new InvalidDataException("Invalid project");
        foreach(var l in d.Layers)ValidateLayer(l);
    }
    public void Dispose(){Scene.Dispose();foreach(var p in Photos.Values)p.Dispose();}
}
