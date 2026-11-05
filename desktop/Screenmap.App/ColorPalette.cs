using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using SkiaSharp;
namespace Screenmap.App;
internal sealed class ColorPalette:FloatingWindow {
    internal readonly HsvSurface Surface=new();
    internal ColorPalette(string initial,Action<string> changed):base("HEX / RGB",false){
        Width=MinWidth=MaxWidth=374;MinHeight=0;SizeToContent=SizeToContent.Height;
        var hue=new HueStrip(Surface);var hex=new TextBox{Text=initial,PlaceholderText="#RRGGBB"};
        var info=UI.Text("");bool updating=false;
        void Update(){
            var color=Surface.Value;string value=$"#{color.Red:X2}{color.Green:X2}{color.Blue:X2}";
            updating=true;hex.Text=value;updating=false;info.Text=$"RGB {color.Red}, {color.Green}, {color.Blue}";hue.InvalidateVisual();changed(value);
        }
        Surface.Set(initial,false);Surface.Changed+=Update;
        hex.TextChanged+=(_,_)=>{if(!updating&&hex.Text is {Length:7} s&&s[0]=='#'&&s.Skip(1).All(Uri.IsHexDigit))Surface.Set(s,true);};
        Body.Content=new StackPanel{Margin=new Thickness(20,10,20,20),Spacing=16,Children={Surface,hue,hex,info}};
        var c=Surface.Value;info.Text=$"RGB {c.Red}, {c.Green}, {c.Blue}";
    }
}
internal sealed class HsvSurface:Control {
    internal float Hue=0,Saturation=100,ValueLevel=100;
    internal SKColor Value=>SKColor.FromHsv(Hue,Saturation,ValueLevel);
    internal event Action? Changed;
    internal HsvSurface(){Height=240;MinWidth=260;}
    internal void Set(string color,bool notify){SKColor.Parse(color).ToHsv(out Hue,out Saturation,out ValueLevel);InvalidateVisual();if(notify)Changed?.Invoke();}
    internal void SetHue(float hue){Hue=Math.Clamp(hue,0,359.99f);InvalidateVisual();Changed?.Invoke();}
    private static Color A(SKColor c)=>Color.FromRgb(c.Red,c.Green,c.Blue);
    public override void Render(DrawingContext c){
        var rect=new Rect(Bounds.Size);var hue=A(SKColor.FromHsv(Hue,100,100));
        var horizontal=new LinearGradientBrush{StartPoint=new RelativePoint(0,0,RelativeUnit.Relative),EndPoint=new RelativePoint(1,0,RelativeUnit.Relative),GradientStops=[new GradientStop(Colors.White,0),new GradientStop(hue,1)]};
        var vertical=new LinearGradientBrush{StartPoint=new RelativePoint(0,0,RelativeUnit.Relative),EndPoint=new RelativePoint(0,1,RelativeUnit.Relative),GradientStops=[new GradientStop(Colors.Transparent,0),new GradientStop(Colors.Black,1)]};
        c.DrawRectangle(horizontal,null,rect,12,12);c.DrawRectangle(vertical,null,rect,12,12);
        var p=new Point(12.5+Saturation/100*Math.Max(1,Bounds.Width-25),12.5+(1-ValueLevel/100)*Math.Max(1,Bounds.Height-25));
        c.DrawEllipse(new SolidColorBrush(A(Value)),new Pen(Brushes.White,2),p,12.5,12.5);
    }
    private void At(Point p){Saturation=(float)Math.Clamp((p.X-12.5)/Math.Max(1,Bounds.Width-25)*100,0,100);ValueLevel=(float)Math.Clamp((1-(p.Y-12.5)/Math.Max(1,Bounds.Height-25))*100,0,100);InvalidateVisual();Changed?.Invoke();}
    protected override void OnPointerPressed(PointerPressedEventArgs e){base.OnPointerPressed(e);e.Pointer.Capture(this);At(e.GetPosition(this));}
    protected override void OnPointerMoved(PointerEventArgs e){base.OnPointerMoved(e);if(e.Pointer.Captured==this)At(e.GetPosition(this));}
    protected override void OnPointerReleased(PointerReleasedEventArgs e){base.OnPointerReleased(e);e.Pointer.Capture(null);}
}
internal sealed class HueStrip(HsvSurface surface):Control {
    public override void Render(DrawingContext c){
        var stops=new GradientStops();for(int i=0;i<=6;i++){var value=SKColor.FromHsv(i==6?0:i*60,100,100);stops.Add(new GradientStop(Color.FromRgb(value.Red,value.Green,value.Blue),i/6.0));}
        var brush=new LinearGradientBrush{StartPoint=new RelativePoint(0,0,RelativeUnit.Relative),EndPoint=new RelativePoint(1,0,RelativeUnit.Relative),GradientStops=stops};
        c.DrawRectangle(brush,null,new Rect(0,5,Bounds.Width,20),10,10);
        var selected=SKColor.FromHsv(surface.Hue,100,100);
        c.DrawEllipse(new SolidColorBrush(Color.FromRgb(selected.Red,selected.Green,selected.Blue)),new Pen(Brushes.White,2),new Point(12.5+surface.Hue/360*Math.Max(1,Bounds.Width-25),15),12.5,12.5);
    }
    protected override Avalonia.Size MeasureOverride(Avalonia.Size available)=>new(Math.Min(available.Width,300),30);
    private void At(Point p)=>surface.SetHue((float)Math.Clamp((p.X-12.5)/Math.Max(1,Bounds.Width-25)*360,0,359.99));
    protected override void OnPointerPressed(PointerPressedEventArgs e){base.OnPointerPressed(e);e.Pointer.Capture(this);At(e.GetPosition(this));}
    protected override void OnPointerMoved(PointerEventArgs e){base.OnPointerMoved(e);if(e.Pointer.Captured==this)At(e.GetPosition(this));}
    protected override void OnPointerReleased(PointerReleasedEventArgs e){base.OnPointerReleased(e);e.Pointer.Capture(null);}
}
