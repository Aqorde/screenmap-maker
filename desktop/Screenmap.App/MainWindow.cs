using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
namespace Screenmap.App;
internal sealed record ActionDef(string Id,string English,string Russian,Func<Task> Run,Func<bool>? Selected=null,Func<bool>? Enabled=null) {
    internal string Caption(Editor vm)=>UI.L(vm,English,Russian);
}
public sealed partial class MainWindow:Window {
    internal Editor Editor {get;}
    internal Viewport View {get;}
    internal IReadOnlyList<ActionDef> Actions=>actions;
    internal readonly string ConfigDirectory;
    private readonly bool batch;
    private readonly List<ActionDef> actions=[];
    private readonly List<(MenuItem Item,ActionDef Action)> menus=[];
    private readonly List<(Button Button,ActionDef Action)> quickButtons=[];
    private readonly List<Action> refreshers=[];
    private string language="",quickKey="",sidebarKey="";
    private bool refreshing;
    private SettingsWindow? settings;
    private string? projectPath;
    private string L(string en,string ru)=>UI.L(Editor,en,ru);
    public MainWindow():this(new Editor(),StateStore.DefaultConfigDirectory()){}
    internal MainWindow(Editor editor,string directory,bool batchMode=false){
        AvaloniaXamlLoader.Load(this);Editor=editor;ConfigDirectory=directory;batch=batchMode;
        Icon=new WindowIcon(AssetLoader.Open(new Uri("avares://ScreenmapMaker/Assets/ScreenmapMaker.ico")));
        View=new Viewport(editor);this.FindControl<ContentControl>("ViewportHost")!.Content=View;
        InitChrome();BuildActions();Editor.Changed+=Refresh;KeyDown+=Keys;
        Closing+=(_,e)=>{
            if(batch)return;
            try{StateStore.SaveSession(ConfigDirectory,Editor,Width,Height);}
            catch(Exception ex){e.Cancel=true;ShowMessage(L("Could not save state","Не удалось сохранить состояние"),ex.Message);}
        };
        Closed+=(_,_)=>{Editor.Changed-=Refresh;View.Dispose();Editor.Dispose();};
        Refresh();
    }
    private void InitChrome(){
        this.FindControl<Button>("MinimizeButton")!.Content=UI.CaptionIcon("minimize");
        this.FindControl<Button>("MaximizeButton")!.Content=UI.CaptionIcon("maximize");
        this.FindControl<Button>("CloseButton")!.Content=UI.CaptionIcon("close");
        this.FindControl<Button>("MinimizeButton")!.Click+=(_,_)=>WindowState=WindowState.Minimized;
        this.FindControl<Button>("MaximizeButton")!.Click+=(_,_)=>ToggleMaximize();
        this.FindControl<Button>("CloseButton")!.Click+=(_,_)=>Close();
        var bar=this.FindControl<Grid>("TitleBar")!;
        // Native ElementRole handles the Win32 non-client region. The routed
        // fallback also supports headless and platforms with client-side input.
        bar.PointerPressed+=(_,e)=>{
            if(!e.GetCurrentPoint(bar).Properties.IsLeftButtonPressed||e.GetPosition(bar).X>=bar.Bounds.Width-138)return;
            if(e.ClickCount==2)ToggleMaximize();else BeginMoveDrag(e);e.Handled=true;
        };
        PropertyChanged+=(_,e)=>{if(e.Property==WindowStateProperty)this.FindControl<Button>("MaximizeButton")!.Content=UI.CaptionIcon(WindowState==WindowState.Maximized?"restore":"maximize");};
    }
    private void ToggleMaximize()=>WindowState=WindowState==WindowState.Maximized?WindowState.Normal:WindowState.Maximized;
    private static Task Sync(Action action){action();return Task.CompletedTask;}
    private void Add(string id,string en,string ru,Action run,Func<bool>? selected=null,Func<bool>? enabled=null)=>actions.Add(new(id,en,ru,()=>Sync(run),selected,enabled));
    private void AddAsync(string id,string en,string ru,Func<Task> run)=>actions.Add(new(id,en,ru,run));
    private void BuildActions(){
        Add("new","New","Новый",()=>{Editor.Replace(new());projectPath=null;});
        AddAsync("open","Open…","Открыть…",Open);AddAsync("save","Save…","Сохранить…",Save);
        Add("export","Export…","Экспорт…",OpenExport);Add("settings","Settings…","Настройки…",OpenSettings);
        Add("undo","Undo","Отменить",Editor.Undo);Add("redo","Redo","Повторить",Editor.Redo);
        Add("perspective","Move perspective","Изменить перспективу",()=>{if(Editor.Doc.Mode!=0)return;Editor.PerspectiveEditing=!Editor.PerspectiveEditing;Editor.Notify();},()=>Editor.Doc.Mode==0&&Editor.PerspectiveEditing,()=>Editor.Doc.Mode==0);
        Add("planePerspective","Perspective","Перспектива",()=>Editor.Update(d=>d.Mode=0),()=>Editor.Doc.Mode==0);
        Add("planePrimitive","Primitive","Простая сетка",()=>Editor.Update(d=>d.Mode=1),()=>Editor.Doc.Mode==1);
        Add("ar169","16:9","16:9",()=>SetAspect(16,9),()=>Math.Max(Editor.Doc.P,Editor.Doc.Q)==16);
        Add("ar34","3:4","3:4",()=>SetAspect(3,4),()=>Math.Max(Editor.Doc.P,Editor.Doc.Q)==4);
        Add("ar11","1:1","1:1",()=>SetAspect(1,1),()=>Editor.Doc.P==Editor.Doc.Q);
        Add("orientation","Orientation","Ориентация",()=>Editor.Update(d=>(d.P,d.Q)=(d.Q,d.P)),()=>Editor.Doc.P<Editor.Doc.Q);
        Add("portrait","Portrait","Портрет",()=>Editor.Update(d=>{if(d.P>d.Q)(d.P,d.Q)=(d.Q,d.P);}),()=>Editor.Doc.P<Editor.Doc.Q);
        Add("landscape","Landscape","Альбом",()=>Editor.Update(d=>{if(d.P<d.Q)(d.P,d.Q)=(d.Q,d.P);}),()=>Editor.Doc.P>=Editor.Doc.Q);
        for(int i=0;i<5;i++){
            int n=i;string[] ens=["PGFA","Depth","Levels","Zones","Grid"],rus=["ОППС","Глубина","Уровни","Зоны","Сетка"];
            Add($"layer{i}",ens[i],rus[i],()=>Editor.Update(d=>d.Layers[n].Visible=!d.Layers[n].Visible,false),()=>Editor.Doc.Layers[n].Visible);
            Add($"caption{i}",ens[i]+" captions","Подписи: "+rus[i],()=>Editor.Update(d=>d.Layers[n].Labels=!d.Layers[n].Labels,false),()=>Editor.Doc.Layers[n].Labels);
            Add($"opacity{i}",ens[i]+" opacity","Прозрачность: "+rus[i],()=>OpenSlider(ens[n],Editor.Doc.Layers[n].Opacity,0,1,v=>Editor.Update(d=>d.Layers[n].Opacity=v,false)));
        }
        Add("boundDepth","Bound depth planes","Ограничить глубину",
            ()=>Editor.Update(d=>d.Layers[1].Bounded=!d.Layers[1].Bounded,false),
            ()=>Editor.Doc.Layers[1].Bounded);
        Add("radius","Perspective radius","Радиус перспективы",()=>OpenSlider(L("Radius","Радиус"),Editor.Doc.Radius,.006,.28,v=>Editor.Update(d=>d.Radius=v)));
        Add("pgfaDensity","PGFA density","Плотность ОППС",()=>OpenSlider(UI.Layer(Editor,0),Editor.Doc.PgfaDensity,1,6,v=>Editor.Update(d=>d.PgfaDensity=(int)Math.Round(v)),true));
        Add("primitiveDensity","Primitive density","Плотность простой сетки",()=>OpenSlider(L("Density step","Шаг плотности"),Editor.Doc.PrimitiveStep,0,Editor.Doc.PrimitiveSteps().Length-1,v=>Editor.Update(d=>d.SetPrimitiveStep((int)v)),true));
        Add("centre","Centre point","Центральная точка",()=>Editor.Update(d=>d.PrimitiveCircle=!d.PrimitiveCircle),()=>Editor.Doc.PrimitiveCircle);
        Add("hollow","Hollow circle","Окружность",()=>Editor.Update(d=>d.PrimitiveHollow=!d.PrimitiveHollow,false),()=>Editor.Doc.PrimitiveHollow);
        Add("size","Centre point size","Размер центральной точки",()=>OpenSlider(L("Size","Размер"),Editor.Doc.PrimitiveRadius,.005,.2,v=>Editor.Update(d=>d.PrimitiveRadius=v)));
        AddAsync("photo","Background picture…","Фото для фона…",OpenPhoto);
        Add("removePhoto","Remove picture","Убрать фото",()=>Editor.Update(d=>d.PhotoId=null,false));
        Add("photoOpacity","Picture opacity","Непрозрачность фото",()=>{if(Editor.Photo is not null)OpenSlider(L("Picture opacity","Непрозрачность фото"),Editor.Doc.PhotoOpacity,0,1,v=>Editor.Update(d=>d.PhotoOpacity=v,false));});
        Add("photoFit","Fill / fit picture","Заполнить / вписать фото",()=>Editor.Update(d=>d.PhotoFill=!d.PhotoFill,false),()=>Editor.Doc.PhotoFill);
        Add("photoX","Picture crop X","Кадрирование фото X",()=>OpenSlider("X",Editor.Doc.PhotoX,0,1,v=>Editor.Update(d=>d.PhotoX=v,false)));
        Add("photoY","Picture crop Y","Кадрирование фото Y",()=>OpenSlider("Y",Editor.Doc.PhotoY,0,1,v=>Editor.Update(d=>d.PhotoY=v,false)));
        Add("theme","Global theme","Тема приложения",()=>Editor.UpdatePreferences(p=>p.Theme=p.Theme=="Dark"?"Light":p.Theme=="Light"?"System":"Dark"));
        Add("language","Language","Язык",()=>Editor.UpdatePreferences(p=>p.Language=p.Language=="ru"?"en":"ru"));
        Add("screenmapTheme","Screenmap themes","Темы Screenmap",()=>{OpenSettings();settings!.SelectPage(1);});
        Add("documentation","Documentation","Документация",Documentation);
        Add("about","About","О программе",()=>new AboutWindow(Editor).Show(this));
    }
    private void SetAspect(int p,int q){
        bool portrait=Editor.Doc.P<Editor.Doc.Q;
        Editor.Update(d=>{d.P=portrait?Math.Min(p,q):Math.Max(p,q);d.Q=portrait?Math.Max(p,q):Math.Min(p,q);});
    }
    private ActionDef Find(string id)=>actions.First(a=>a.Id==id);
    private MenuItem MenuAction(string id){
        var a=Find(id);var menu=new MenuItem{Header=a.Caption(Editor)};menu.Click+=async(_,_)=>await GuardAsync(a.Run);menus.Add((menu,a));return menu;
    }
    private void BuildMenus(){
        menus.Clear();var file=new MenuItem{Header=L("_File","_Файл")};
        file.ItemsSource=new object[]{MenuAction("new"),MenuAction("save"),MenuAction("open"),new Separator(),MenuAction("export"),new Separator(),MenuAction("settings")};
        var plane=new MenuItem{Header=L("Plane","Плоскость"),ItemsSource=new[]{MenuAction("planePerspective"),MenuAction("planePrimitive")}};
        var aspect=new MenuItem{Header=L("Aspect ratio","Соотношение сторон"),ItemsSource=new[]{MenuAction("ar169"),MenuAction("ar34"),MenuAction("ar11")}};
        var orientation=new MenuItem{Header=L("Orientation","Ориентация"),ItemsSource=new[]{MenuAction("portrait"),MenuAction("landscape")}};
        var edit=new MenuItem{Header=L("_Edit","_Правка"),ItemsSource=new object[]{MenuAction("undo"),MenuAction("redo"),new Separator(),plane,aspect,orientation}};
        var help=new MenuItem{Header=L("_Help","_Помощь"),ItemsSource=new[]{MenuAction("documentation"),MenuAction("about")}};
        this.FindControl<Menu>("MainMenu")!.ItemsSource=new[]{file,edit,help};
    }
    private void BuildQuick(){
        var bar=this.FindControl<WrapPanel>("QuickBar")!;bar.Children.Clear();quickButtons.Clear();
        foreach(string id in Editor.Prefs.QuickMenu.Distinct()){
            var a=actions.FirstOrDefault(a=>a.Id==id);if(a is null)continue;
            Button button=a.Selected is not null?new ToggleButton():new Button();
            button.Content=a.Caption(Editor);button.Click+=async(_,_)=>await GuardAsync(a.Run);bar.Children.Add(button);quickButtons.Add((button,a));
        }
        bar.IsVisible=bar.Children.Count>0;
    }
    private void BuildSidebar(){
        refreshers.Clear();var panel=this.FindControl<StackPanel>("Sidebar")!;panel.Children.Clear();
        panel.Spacing=Editor.Prefs.Compact?7:12;panel.Margin=Editor.Prefs.Compact?new Thickness(12,4,30,12):new Thickness(16,4,30,16);
        this.FindControl<Grid>("WorkArea")!.ColumnDefinitions[1].Width=new GridLength(Editor.Prefs.Compact?270:284);
        for(int i=0;i<5;i++){
            int n=i;var s=Editor.Doc.Layers[i];var eye=new ToggleButton{Content=UI.Eye(s.Visible),Width=30,Height=30,Padding=new Thickness(5)};
            eye.Click+=(_,_)=>{if(!refreshing)Editor.Update(d=>d.Layers[n].Visible=eye.IsChecked==true,false);};
            var caption=UI.Check(L("Caption","Подписи"),s.Labels,v=>{if(!refreshing)Editor.Update(d=>d.Layers[n].Labels=v,false);});caption.FontSize=12;
            var row=new Grid{ColumnDefinitions=new ColumnDefinitions("34,*,Auto"),ColumnSpacing=5};
            row.Children.Add(eye);var name=UI.LayerTitle(Editor,i);Grid.SetColumn(name,1);row.Children.Add(name);Grid.SetColumn(caption,2);row.Children.Add(caption);panel.Children.Add(row);
            var opacity=new Slider{Minimum=0,Maximum=1,Value=s.Opacity,SmallChange=.02};
            opacity.ValueChanged+=(_,_)=>{if(!refreshing)Editor.Update(d=>d.Layers[n].Opacity=opacity.Value,false);};Gesture(opacity);UI.Wheel(opacity);ToolTip.SetTip(opacity,UI.LayerTip(Editor,i));panel.Children.Add(opacity);
            refreshers.Add(()=>{var style=Editor.Doc.Layers[n];eye.IsChecked=style.Visible;eye.Content=UI.Eye(style.Visible);caption.IsChecked=style.Labels;opacity.Value=style.Opacity;});
        }
        panel.Children.Add(UI.Heading(Editor.Doc.Mode==0?L("Perspective","Перспектива"):L("Primitive","Простая сетка")));
        if(Editor.Doc.Mode==0)Slider(panel,L("Radius","Радиус"),.006,.28,()=>Editor.Doc.Radius,v=>Editor.Update(d=>d.Radius=v));
        else{
            Slider(panel,L("Density","Плотность"),0,Editor.Doc.PrimitiveSteps().Length-1,()=>Editor.Doc.PrimitiveStep,v=>Editor.Update(d=>d.SetPrimitiveStep((int)v)),true,()=>{var s=Editor.Doc.PrimitiveShape();return $"{s.Columns} × {s.Rows}";});
            var center=UI.Check(L("Centre point","Центральная точка"),Editor.Doc.PrimitiveCircle,v=>{if(!refreshing)Editor.Update(d=>d.PrimitiveCircle=v);});panel.Children.Add(center);refreshers.Add(()=>center.IsChecked=Editor.Doc.PrimitiveCircle);
            var hollow=UI.Check(L("Hollow circle","Окружность"),Editor.Doc.PrimitiveHollow,v=>{if(!refreshing)Editor.Update(d=>d.PrimitiveHollow=v,false);});panel.Children.Add(hollow);refreshers.Add(()=>{hollow.IsChecked=Editor.Doc.PrimitiveHollow;hollow.IsEnabled=Editor.Doc.PrimitiveCircle;});
            Slider(panel,L("Size","Размер"),.005,.2,()=>Editor.Doc.PrimitiveRadius,v=>Editor.Update(d=>d.PrimitiveRadius=v));
        }
        Slider(panel,L("PGFA density","Плотность ОППС"),1,6,()=>Editor.Doc.PgfaDensity,v=>Editor.Update(d=>d.PgfaDensity=(int)Math.Round(v)),true);
        panel.Children.Add(UI.Heading(L("Background","Фон")));
        panel.Children.Add(UI.Row(UI.Button(L("Picture…","Фото…"),()=>_ = GuardAsync(OpenPhoto)),UI.Button(L("Remove","Убрать"),()=>Editor.Update(d=>d.PhotoId=null,false))));
        var imageOpacity=Slider(panel,L("Opacity","Непрозрачность"),0,1,()=>Editor.Doc.PhotoOpacity,v=>Editor.Update(d=>d.PhotoOpacity=v,false));imageOpacity.IsEnabled=Editor.Photo is not null;
        var fit=UI.Select([L("Fill","Заполнить"),L("Fit","Вписать")],Editor.Doc.PhotoFill?0:1,i=>{if(!refreshing)Editor.Update(d=>d.PhotoFill=i==0,false);});fit.IsEnabled=Editor.Photo is not null;panel.Children.Add(fit);refreshers.Add(()=>fit.SelectedIndex=Editor.Doc.PhotoFill?0:1);
        var x=Slider(panel,L("Crop X","Кадрирование X"),0,1,()=>Editor.Doc.PhotoX,v=>Editor.Update(d=>d.PhotoX=v,false));x.IsEnabled=Editor.Photo is not null;
        var y=Slider(panel,L("Crop Y","Кадрирование Y"),0,1,()=>Editor.Doc.PhotoY,v=>Editor.Update(d=>d.PhotoY=v,false));y.IsEnabled=Editor.Photo is not null;
    }
    private void Gesture(Slider s){s.AddHandler(InputElement.PointerPressedEvent,(_,_)=>Editor.BeginGesture(),RoutingStrategies.Tunnel,true);s.AddHandler(InputElement.PointerReleasedEvent,(_,_)=>Editor.EndGesture(),RoutingStrategies.Tunnel,true);s.PointerCaptureLost+=(_,_)=>Editor.EndGesture();}
    private Slider Slider(Panel panel,string text,double min,double max,Func<double> get,Action<double> set,bool integer=false,Func<string>? display=null){
        var label=UI.Text(text);panel.Children.Add(label);
        var slider=new Slider{Minimum=min,Maximum=max,TickFrequency=integer?1:.001,IsSnapToTickEnabled=integer,SmallChange=integer?1:.005,Value=get()};
        slider.ValueChanged+=(_,_)=>{if(!refreshing)Guard(()=>set(slider.Value));};Gesture(slider);UI.Wheel(slider);panel.Children.Add(slider);
        refreshers.Add(()=>{slider.Value=get();label.Text=display is not null?$"{text}  {display()}":integer?$"{text}  {get():0}":$"{text}  {get()*100:0.#}%";});return slider;
    }
    private void Refresh(){
        App.Apply(Editor.Prefs);refreshing=true;
        try{
            if(language!=Editor.Prefs.Language){language=Editor.Prefs.Language;BuildMenus();}
            this.FindControl<TextBlock>("ViewportTitle")!.Text=L("Viewport","Вьюпорт");
            this.FindControl<TextBlock>("LayersTitle")!.Text=L("Layers","Слои");
            ToolTip.SetTip(this.FindControl<Button>("MinimizeButton")!,L("Minimize","Свернуть"));
            ToolTip.SetTip(this.FindControl<Button>("MaximizeButton")!,L("Maximize / restore","Развернуть / восстановить"));
            ToolTip.SetTip(this.FindControl<Button>("CloseButton")!,L("Close","Закрыть"));
            string q=Editor.Prefs.Language+Editor.Prefs.LayerDisplay+string.Join('|',Editor.Prefs.QuickMenu);if(q!=quickKey){quickKey=q;BuildQuick();}
            string side=$"{Editor.Prefs.Language}:{Editor.Prefs.LayerDisplay}:{Editor.Prefs.Compact}:{Editor.Doc.Mode}:{Editor.Doc.P}:{Editor.Doc.Q}:{Editor.Photo is not null}";
            if(side!=sidebarKey){sidebarKey=side;BuildSidebar();}
            foreach(var (menu,a)in menus){menu.Header=(a.Selected?.Invoke()==true?"✓  ":"")+a.Caption(Editor);menu.IsEnabled=a.Enabled?.Invoke()??true;if(a.Id=="undo")menu.IsEnabled=Editor.CanUndo;if(a.Id=="redo")menu.IsEnabled=Editor.CanRedo;}
            foreach(var (button,a)in quickButtons){
                button.Content=a.Id=="orientation"?L(Editor.Doc.P<Editor.Doc.Q?"Portrait":"Landscape",Editor.Doc.P<Editor.Doc.Q?"Портрет":"Альбом"):a.Caption(Editor);
                if(button is ToggleButton t)t.IsChecked=a.Selected?.Invoke()==true;
                button.IsEnabled=a.Enabled?.Invoke()??true;
                if(a.Id.StartsWith("layer")&&int.TryParse(a.Id.AsSpan(5),out int layer)){button.Content=UI.LayerTitle(Editor,layer,Editor.Doc.Layers[layer].Visible);ToolTip.SetTip(button,UI.LayerTip(Editor,layer));}
                if(a.Id=="photoOpacity")button.IsEnabled=Editor.Photo is not null;
            }
            foreach(var update in refreshers)update();
        }finally{refreshing=false;}
    }
    internal void OpenSettings(){if(settings is {IsVisible:true}){settings.Activate();return;}settings=new SettingsWindow(Editor,this);settings.Show(this);}
    internal SettingsWindow CreateSettingsForRender(){var w=new SettingsWindow(Editor,this);return w;}
    internal void OpenExport(){var w=new ExportWindow(Editor,this);w.Show(this);}
    internal void OpenPalette(string initial,Action<string> changed){var w=new ColorPalette(initial,changed);w.Show(settings is {IsVisible:true}?settings:this);}
    private void OpenSlider(string title,double value,double min,double max,Action<double> changed,bool integer=false){
        var w=new FloatingWindow(title){Width=340,Height=170};w.Body.Content=new StackPanel{Margin=new Thickness(20),Children={UI.Slider(title,value,min,max,changed,integer)}};w.Show(this);
    }
    private async Task OpenPhoto(){
        var files=await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions{Title=L("Background picture","Фоновое изображение"),AllowMultiple=false,FileTypeFilter=[FilePickerFileTypes.ImageAll]});
        if(files.Count==0)return;await using var s=await files[0].OpenReadAsync();using var m=new MemoryStream();await s.CopyToAsync(m);Editor.AddPhoto(m.ToArray());
    }
    internal void OpenProject(string path){
        var full=Path.GetFullPath(path);StateStore.OpenProject(full,Editor);projectPath=full;
    }
    internal string? ProjectPath=>projectPath;
    private async Task Open(){
        var files=await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions{AllowMultiple=false,FileTypeFilter=[new FilePickerFileType("Screenmap"){Patterns=["*.screenmap"]}]});
        if(files.Count==0)return;string path=files[0].Path.LocalPath;OpenProject(path);
    }
    private async Task Save(){
        if(projectPath is not null){StateStore.SaveProject(projectPath,Editor);return;}
        var file=await StorageProvider.SaveFilePickerAsync(ProjectSaveOptions(DateTimeOffset.Now));
        if(file is null)return;StateStore.SaveProject(file.Path.LocalPath,Editor);projectPath=file.Path.LocalPath;
    }
    internal static FilePickerSaveOptions ProjectSaveOptions(DateTimeOffset timestamp)=>new(){
        SuggestedFileName="scene_"+timestamp.ToString("yyyyMMdd_HHmmss",System.Globalization.CultureInfo.InvariantCulture)+".screenmap",
        DefaultExtension="screenmap",FileTypeChoices=[new FilePickerFileType("Screenmap"){Patterns=["*.screenmap"]}]
    };
    private void Keys(object? sender,KeyEventArgs e){
        if(e.KeyModifiers!=KeyModifiers.Control)return;
        if(FocusManager?.GetFocusedElement() is TextBox && e.Key is Key.Z or Key.Y)return;
        string? id=e.Key switch{Key.N=>"new",Key.O=>"open",Key.S=>"save",Key.Z=>"undo",Key.Y=>"redo",Key.E=>"export",_=>null};
        if(id is not null){e.Handled=true;_ = GuardAsync(Find(id).Run);}
    }
    internal void Guard(Action action){try{action();}catch(Exception ex){ShowMessage(L("Error","Ошибка"),ex.Message);}}
    private async Task GuardAsync(Func<Task> action){try{await action();}catch(Exception ex){ShowMessage(L("Error","Ошибка"),ex.Message);}}
    internal void ShowMessage(string title,string text){var w=new MessageWindow(title,text);w.Show(settings is {IsVisible:true}?settings:this);}
    private void Documentation()=>new DocumentationWindow(Editor).Show(this);
}
