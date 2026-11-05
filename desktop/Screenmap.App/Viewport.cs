using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using SkiaSharp;
namespace Screenmap.App;
internal sealed class Viewport:Control,IDisposable {
    private readonly Editor vm;
    private RenderPlan plan;
    private bool dragging,radiusDrag;
    private IPointer? activePointer;
    internal Rect Frame {get;private set;}
    internal Viewport(Editor editor){vm=editor;Focusable=true;ClipToBounds=true;plan=new(vm);vm.Changed+=Changed;}
    private void Changed(){var next=new RenderPlan(vm);var old=plan;plan=next;old.Dispose();InvalidateVisual();}
    public override void Render(DrawingContext context){
        base.Render(context);double w=Math.Max(1,Bounds.Width-56),h=Math.Max(1,Bounds.Height-56);
        double aspect=(double)vm.Doc.P/vm.Doc.Q;w=Math.Min(w,h*aspect);h=w/aspect;
        Frame=new Rect((Bounds.Width-w)/2,(Bounds.Height-h)/2,w,h);
        context.Custom(new DrawOperation(Frame,plan.Acquire()));
        if(vm.PerspectiveEditing&&vm.Doc.Mode==0){
            var circle=vm.Scene.Circles.First();
            var center=new Point(Frame.X+circle.X*w,Frame.Y+circle.Y*h);
            var pen=new Pen(Brushes.DeepSkyBlue,1.5);context.DrawEllipse(null,pen,center,8,8);
            context.DrawLine(pen,center-new Vector(13,0),center+new Vector(13,0));
            context.DrawLine(pen,center-new Vector(0,13),center+new Vector(0,13));
            context.DrawEllipse(Brushes.DeepSkyBlue,null,new Point(center.X+circle.Rx*w,center.Y),5,5);
        }
    }
    private sealed class DrawOperation(Rect bounds,RenderPlan plan):ICustomDrawOperation {
        public Rect Bounds=>bounds;
        public bool HitTest(Point p)=>bounds.Contains(p);
        public bool Equals(ICustomDrawOperation? other)=>false;
        public void Dispose()=>plan.Dispose();
        public void Render(ImmediateDrawingContext context){
            var feature=context.TryGetFeature<ISkiaSharpApiLeaseFeature>();if(feature is null)return;
            using var lease=feature.Lease();var c=lease.SkCanvas;c.Save();c.Translate((float)bounds.X,(float)bounds.Y);c.Scale((float)bounds.Width/plan.Width,(float)bounds.Height/plan.Height);c.DrawPicture(plan.Picture);c.Restore();
        }
    }
    protected override void OnPointerPressed(PointerPressedEventArgs e){
        base.OnPointerPressed(e);if(!vm.PerspectiveEditing||vm.Doc.Mode!=0||!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed||!Frame.Contains(e.GetPosition(this)))return;
        Focus();var c=vm.Scene.Circles[0];var p=e.GetPosition(this);var handle=new Point(Frame.X+(c.X+c.Rx)*Frame.Width,Frame.Y+c.Y*Frame.Height);
        radiusDrag=Math.Pow(p.X-handle.X,2)+Math.Pow(p.Y-handle.Y,2)<=169;activePointer=e.Pointer;dragging=true;vm.BeginGesture();e.Pointer.Capture(this);UpdatePointer(p);e.Handled=true;
    }
    private void UpdatePointer(Point p){
        double x=(p.X-Frame.X)/Frame.Width,y=(p.Y-Frame.Y)/Frame.Height;
        if(radiusDrag){double dx=(x-vm.Doc.CenterX)*Frame.Width,dy=(y-vm.Doc.CenterY)*Frame.Height;vm.Update(d=>d.Radius=Math.Sqrt(dx*dx+dy*dy)/Math.Min(Frame.Width,Frame.Height),record:false);}
        else vm.Update(d=>{d.CenterX=x;d.CenterY=y;},record:false);
    }
    protected override void OnPointerMoved(PointerEventArgs e){base.OnPointerMoved(e);if(dragging)UpdatePointer(e.GetPosition(this));}
    protected override void OnPointerReleased(PointerReleasedEventArgs e){base.OnPointerReleased(e);if(!dragging)return;dragging=false;e.Pointer.Capture(null);activePointer=null;vm.EndGesture();e.Handled=true;}
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e){base.OnPointerCaptureLost(e);if(dragging){dragging=false;vm.EndGesture();}}
    protected override void OnKeyDown(KeyEventArgs e){base.OnKeyDown(e);if(e.Key==Key.Escape&&dragging){dragging=false;activePointer?.Capture(null);activePointer=null;vm.EndGesture(true);e.Handled=true;}}
    public void Dispose(){vm.Changed-=Changed;plan.Dispose();}
}
