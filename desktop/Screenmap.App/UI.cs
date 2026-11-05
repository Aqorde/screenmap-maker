using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
namespace Screenmap.App;
internal static class UI {
    internal static string L(Editor vm,string en,string ru)=>vm.Prefs.Language=="ru"?ru:en;
    internal static string Layer(Editor vm,int i)=>i switch {0=>L(vm,"PGFA","ОППС"),1=>L(vm,"Depth","Глубина"),2=>L(vm,"Levels","Уровни"),3=>L(vm,"Zones","Зоны"),_=>L(vm,"Grid","Сетка")};
    internal static string LayerTip(Editor vm,int i)=>i switch{
        0=>L(vm,"Perspective Grid Field Area: a virtual box with a floor, ceiling and walls. Density subdivides the field.","Область поля перспективной сетки: виртуальная коробка с полом, потолком и стенами. Плотность добавляет деления."),
        1=>L(vm,"Depth planes D0–D7. D0 is nearest; D7 is farthest. Depth is independent of the screen zone.","Планы глубины D0–D7: D0 ближе, D7 дальше. Любую экранную зону можно сочетать с любой глубиной."),
        2=>L(vm,"Four vertical bands: TOP, MIDTOP, MIDBOTTOM, BOTTOM.","Четыре полосы по высоте кадра: TOP, MIDTOP, MIDBOTTOM, BOTTOM."),
        3=>L(vm,"Zone numbers only: 1, 1.1–1.8, 2.1–2.24; sequential cell numbers in Primitive mode.","Только номера зон: 1, 1.1–1.8, 2.1–2.24; в простой сетке — последовательные номера ячеек."),
        _=>L(vm,"Zone boundary lines; rows and columns in Primitive mode. Independent of the numbers.","Линии границ зон; строки и столбцы в простой сетке. Настраиваются независимо от цифр.")};
    internal static Control LayerTitle(Editor vm,int i,bool selected=false){
        var p=Row();p.Spacing=7;
        if(vm.Prefs.LayerDisplay!="Text")p.Children.Add(LayerIcon(i,selected));
        if(vm.Prefs.LayerDisplay!="Icons")p.Children.Add(Text(Layer(vm,i)));
        ToolTip.SetTip(p,Layer(vm,i)+" · "+LayerTip(vm,i));return p;
    }
    internal static Control LayerIcon(int i,bool selected=false){
        string data=i switch{
            0=>"M1 1 L19 1 19 19 1 19 Z M6 6 L14 6 14 14 6 14 Z M1 1 L6 6 M19 1 L14 6 M19 19 L14 14 M1 19 L6 14 M10 1 L10 6 M1 10 L6 10 M19 10 L14 10 M10 19 L10 14",
            1=>"M1 2 L19 2 19 18 1 18 Z M5 6 L15 6 15 14 5 14 Z M8 8 L12 8 12 12 8 12 Z",
            2=>"M2 3 L18 3 M2 8 L18 8 M2 13 L18 13 M2 18 L18 18",
            3=>"M2 2 L18 2 18 18 2 18 Z M2 2 L18 18 M18 2 L2 18 M10 2 L10 18 M2 10 L18 10",
            _=>"M1 1 L19 1 19 19 1 19 Z M7 1 L7 19 M13 1 L13 19 M1 7 L19 7 M1 13 L19 13"};
        var icon=new Avalonia.Controls.Shapes.Path{Data=Geometry.Parse(data),Width=20,Height=20,Stretch=Stretch.Uniform,StrokeThickness=1.4};icon.Bind(Shape.StrokeProperty,new DynamicResourceExtension(selected?"PaletteAccentForegroundBrush":"Ink"));return CentreIcon(icon,20);
    }
    internal static TextBlock Text(string text,double? size=null){var t=new TextBlock{Text=text,TextWrapping=TextWrapping.Wrap,VerticalAlignment=VerticalAlignment.Center};if(size is not null)t.FontSize=size.Value;return t;}
    internal static TextBlock Heading(string text)=>new(){Text=text,FontSize=18,FontWeight=FontWeight.SemiBold,Margin=new Thickness(0,15,0,12)};
    internal static StackPanel Stack(params Control[] children){var p=new StackPanel{Spacing=10};foreach(var c in children)p.Children.Add(c);return p;}
    internal static StackPanel Row(params Control[] children){var p=new StackPanel{Orientation=Orientation.Horizontal,Spacing=10,VerticalAlignment=VerticalAlignment.Center};foreach(var c in children)p.Children.Add(c);return p;}
    internal static Border Card(Control content,string surface="Surface1"){
        var b=new Border{Child=content,CornerRadius=new CornerRadius(12),Padding=new Thickness(16)};
        b.Bind(Border.BackgroundProperty,new DynamicResourceExtension(surface));return b;
    }
    internal static Button Button(string text,Action action){var b=new Button{Content=text};b.Click+=(_,_)=>action();return b;}
    internal static CheckBox Check(string text,bool initial,Action<bool> changed){
        var c=new CheckBox{Content=text,IsChecked=initial};c.IsCheckedChanged+=(_,_)=>changed(c.IsChecked==true);return c;
    }
    internal static NumericUpDown Number(double value,double min,double max,Action<double> changed,double increment=1){
        var n=new NumericUpDown{Value=(decimal)value,Minimum=(decimal)min,Maximum=(decimal)max,Increment=(decimal)increment,ShowButtonSpinner=false,Width=100,FormatString=increment<1?"0.##":"0"};
        n.ValueChanged+=(_,_)=>{if(n.Value is decimal v)changed((double)v);};return n;
    }
    internal static ComboBox Select(string[] items,int index,Action<int> changed){
        var c=new ComboBox{ItemsSource=items,SelectedIndex=index,HorizontalAlignment=HorizontalAlignment.Stretch};
        c.SelectionChanged+=(_,_)=>{if(c.SelectedIndex>=0)changed(c.SelectedIndex);};return c;
    }
    internal static Control Slider(string title,double value,double min,double max,Action<double> changed,bool integer=false){
        var t=Text($"{title}  {value:0.##}");
        var slider=new Slider{Minimum=min,Maximum=max,Value=value,TickFrequency=integer?1:.01,IsSnapToTickEnabled=integer,SmallChange=integer?1:.01};
        Wheel(slider);
        slider.ValueChanged+=(_,_)=>{t.Text=$"{title}  {slider.Value:0.##}";changed(slider.Value);};
        return Stack(t,slider);
    }
    internal static void Wheel(Slider slider){
        slider.AddHandler(InputElement.PointerWheelChangedEvent,(_,e)=>{
            if(!slider.IsEnabled)return;
            double delta=e.Delta.Y!=0?e.Delta.Y:e.Delta.X;
            if(delta==0)return;
            double step=slider.IsSnapToTickEnabled?slider.TickFrequency:slider.SmallChange;
            slider.Value=Math.Clamp(slider.Value+Math.Sign(delta)*step,slider.Minimum,slider.Maximum);
            e.Handled=true;
        },RoutingStrategies.Tunnel,true);
    }
    internal static Button ColorButton(string value,Action click){
        var circle=new Ellipse{Width=25,Height=25,Fill=Brush.Parse(value),Stroke=new SolidColorBrush(Color.FromRgb(96,96,96)),StrokeThickness=2};
        var button=new Button{Content=circle,Padding=new Thickness(4),Background=Brushes.Transparent,BorderThickness=new Thickness(0),Width=35,Height=35};
        ToolTip.SetTip(button,value);button.Click+=(_,_)=>click();return button;
    }
    internal static Control Eye(bool visible){
        string path="M0.75 7.5 Q7.5 0.75 14.25 7.5 Q7.5 14.25 0.75 7.5 Z";
        if(visible)path+=" M9.75 7.5 a2.25 2.25 0 1 0 -4.5 0 a2.25 2.25 0 1 0 4.5 0 Z";
        var p=new Avalonia.Controls.Shapes.Path{Data=Geometry.Parse(path),Width=15,Height=15,Stretch=Stretch.Uniform,StrokeThickness=visible?0:1.1};
        if(visible)p.Bind(Shape.FillProperty,new DynamicResourceExtension("PaletteAccentForegroundBrush"));else p.Bind(Shape.StrokeProperty,new DynamicResourceExtension("Ink"));return CentreIcon(p,15);
    }
    private static Control CentreIcon(Avalonia.Controls.Shapes.Path path,double size){
        // A fixed square view box preserves the drawing origin instead of stretching its ink bounds.
        path.Stretch=Stretch.None;path.Width=path.Height=double.NaN;
        if(path.Data is Geometry geometry)path.RenderTransform=new TranslateTransform(size/2-geometry.Bounds.Center.X,size/2-geometry.Bounds.Center.Y);
        var canvas=new Canvas{Width=size,Height=size,Children={path}};
        return new Viewbox{Width=size,Height=size,Child=canvas,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
    }
    internal static Control CaptionIcon(string kind){
        string data=kind switch{
            "minimize"=>"M2 8 L14 8",
            "maximize"=>"M2.5 2.5 L13.5 2.5 13.5 13.5 2.5 13.5 Z",
            "restore"=>"M5.5 5.5 L13.5 5.5 13.5 13.5 5.5 13.5 Z M2.5 10.5 L2.5 2.5 10.5 2.5 10.5 4",
            "down"=>"M3 5.5 L8 10.5 13 5.5",
            _=>"M3 3 L13 13 M13 3 L3 13"};
        var path=new Avalonia.Controls.Shapes.Path{Data=Geometry.Parse(data),StrokeThickness=1.3};
        path.Bind(Shape.StrokeProperty,new Avalonia.Data.Binding("Foreground"){RelativeSource=new Avalonia.Data.RelativeSource(Avalonia.Data.RelativeSourceMode.FindAncestor){AncestorType=typeof(Button)}});
        return CentreIcon(path,16);
    }
}
internal class FloatingWindow:Window {
    private readonly TextBlock title;
    internal Border Shell {get;}
    internal Grid DragBar {get;}
    internal FloatingWindow(string caption,bool resize=true){
        Title=caption;WindowDecorations=WindowDecorations.None;CanResize=resize;WindowStartupLocation=WindowStartupLocation.CenterOwner;
        Background=Brushes.Transparent;TransparencyLevelHint=[WindowTransparencyLevel.Transparent];TransparencyBackgroundFallback=Brushes.Transparent;
        MinWidth=280;MinHeight=150;
        var shell=new Border{Margin=new Thickness(6),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(12),ClipToBounds=true};Shell=shell;
        shell.Bind(Border.BorderBrushProperty,new DynamicResourceExtension("Hairline"));
        shell.Bind(Border.BackgroundProperty,new DynamicResourceExtension("Surface0"));
        var root=new Grid{Background=Brushes.Transparent};root.Children.Add(shell);Content=root;
        var grid=new Grid{RowDefinitions=new RowDefinitions("42,*")};shell.Child=grid;
        var bar=new Grid{ColumnDefinitions=new ColumnDefinitions("*,42"),Margin=new Thickness(16,0,6,0),Background=Brushes.Transparent};DragBar=bar;
        title=UI.Text(caption);title.FontWeight=FontWeight.SemiBold;bar.Children.Add(title);
        var close=UI.Button("×",Close);close.Content=UI.CaptionIcon("close");close.Padding=new Thickness(0);close.Width=close.Height=30;close.MinHeight=0;close.HorizontalAlignment=HorizontalAlignment.Center;close.VerticalAlignment=VerticalAlignment.Center;ToolTip.SetTip(close,"Close / Закрыть");close.Background=Brushes.Transparent;Grid.SetColumn(close,1);bar.Children.Add(close);
        bar.AddHandler(InputElement.PointerPressedEvent,(_,e)=>{
            if(!e.GetCurrentPoint(bar).Properties.IsLeftButtonPressed||e.GetPosition(bar).X>=bar.Bounds.Width-48)return;
            BeginMoveDrag(e);e.Handled=true;
        },RoutingStrategies.Tunnel);
        grid.Children.Add(bar);
        Body=new ContentControl();Grid.SetRow(Body,1);grid.Children.Add(Body);
        if(resize){
            AddHandler(InputElement.PointerPressedEvent,(_,e)=>{
                if(e.GetCurrentPoint(this).Properties.IsLeftButtonPressed&&ResizeEdge(e.GetPosition(this)) is WindowEdge edge){BeginResizeDrag(edge,e);e.Handled=true;}
            },RoutingStrategies.Tunnel,true);
            PointerMoved+=(_,e)=>{Cursor=new Cursor(ResizeEdge(e.GetPosition(this)) switch{
                WindowEdge.North or WindowEdge.South=>StandardCursorType.SizeNorthSouth,
                WindowEdge.West or WindowEdge.East=>StandardCursorType.SizeWestEast,
                WindowEdge.NorthWest or WindowEdge.SouthEast=>StandardCursorType.TopLeftCorner,
                WindowEdge.NorthEast or WindowEdge.SouthWest=>StandardCursorType.TopRightCorner,_=>StandardCursorType.Arrow});};
        }
    }
    internal WindowEdge? ResizeEdge(Point p){
        if(!CanResize)return null;
        int h=p.X<9?-1:p.X>Bounds.Width-9?1:0,v=p.Y<9?-1:p.Y>Bounds.Height-9?1:0;
        return(h,v) switch{(-1,-1)=>WindowEdge.NorthWest,(1,-1)=>WindowEdge.NorthEast,(-1,1)=>WindowEdge.SouthWest,(1,1)=>WindowEdge.SouthEast,(-1,0)=>WindowEdge.West,(1,0)=>WindowEdge.East,(0,-1)=>WindowEdge.North,(0,1)=>WindowEdge.South,_=>null};
    }
    internal ContentControl Body {get;}
    internal void Rename(string caption){Title=caption;title.Text=caption;}
}
internal sealed class MessageWindow:FloatingWindow {
    internal TextBlock Message {get;}
    internal MessageWindow(string caption,string message):base(caption,false){
        MinWidth=180;MaxWidth=560;MinHeight=100;MaxHeight=420;SizeToContent=SizeToContent.WidthAndHeight;
        Message=UI.Text(message);Message.MaxWidth=470;Message.Margin=new Thickness(0,0,18,0);Message.VerticalAlignment=VerticalAlignment.Top;
        Body.Content=new ScrollViewer{Content=Message,MaxHeight=330,Margin=new Thickness(22,12,22,20),HorizontalScrollBarVisibility=Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,VerticalScrollBarVisibility=Avalonia.Controls.Primitives.ScrollBarVisibility.Auto};
    }
}
