using System.Buffers.Binary;
using System.Text;
using System.Xml.Linq;
using SkiaSharp;
namespace Screenmap.App;
internal sealed class RenderPlan:IDisposable {
    private int references=1;
    internal SKPicture Picture {get;}
    internal float Width {get;}
    internal float Height {get;}
    internal RenderPlan(Editor vm,bool background=true) {
        Width=(float)(720.0*vm.Doc.P/Math.Min(vm.Doc.P,vm.Doc.Q));Height=(float)(720.0*vm.Doc.Q/Math.Min(vm.Doc.P,vm.Doc.Q));
        using var recorder=new SKPictureRecorder();
        var canvas=recorder.BeginRecording(new SKRect(0,0,Width,Height));
        RenderEngine.Draw(canvas,vm.Doc,vm.Scene,vm.Prefs.FontFamily,vm.Photo,Width,Height,background);
        Picture=recorder.EndRecording();
    }
    internal RenderPlan Acquire(){Interlocked.Increment(ref references);return this;}
    public void Dispose(){if(Interlocked.Decrement(ref references)==0)Picture.Dispose();}
}
internal static class RenderEngine {
    internal readonly record struct GuideStroke(double X1,double Y1,double X2,double Y2,bool Dotted);
    internal static readonly int[] Order=[0,4,2,5,3,1];
    internal static readonly string[] Names=["pgfa","depth","levels","zones","grid","centre-point"];
    internal static SKColor Color(string value,double opacity=1){
        var color=SKColor.Parse(value);return color.WithAlpha((byte)Math.Clamp((int)Math.Round(opacity*255),0,255));
    }
    internal static void Draw(SKCanvas canvas,DocumentSettings doc,NativeScene scene,string family,PhotoAsset? photo,float w,float h,bool background){
        canvas.Save();canvas.ClipRect(new SKRect(0,0,w,h));
        if(background)DrawBackground(canvas,doc,photo,w,h);
        var labels=LabelLayout.Build(doc,scene,family,w,h);
        foreach(int layer in Order)if(Visible(doc,layer))DrawGeometry(canvas,doc,scene,layer,w,h);
        foreach(int layer in Order)if(layer!=5&&Visible(doc,layer))DrawLabels(canvas,doc,family,labels,layer,w,h);
        canvas.Restore();
    }
    internal static void DrawBackground(SKCanvas c,DocumentSettings d,PhotoAsset? photo,float w,float h){
        c.Clear(Color(d.Background));
        if(photo is null)return;
        float scale=d.PhotoFill?Math.Max(w/photo.Image.Width,h/photo.Image.Height):Math.Min(w/photo.Image.Width,h/photo.Image.Height);
        float iw=photo.Image.Width*scale,ih=photo.Image.Height*scale;
        var rect=SKRect.Create((float)((w-iw)*d.PhotoX),(float)((h-ih)*d.PhotoY),iw,ih);
        using var paint=new SKPaint{IsAntialias=true,Color=SKColors.White.WithAlpha((byte)Math.Round(d.PhotoOpacity*255))};
        c.DrawImage(photo.Image,rect,new SKSamplingOptions(SKFilterMode.Linear),paint);
    }
    private static bool Visible(DocumentSettings d,int layer)=>layer==5?d.Mode==1&&d.PrimitiveCircle&&d.Layers[4].Visible:d.Layers[layer].Visible;
    internal static IEnumerable<GuideStroke> Strokes(NativeScene scene,int layer,bool bounded){
        foreach(var line in scene.Lines.Where(l=>l.Layer==layer)){
            bool horizontal=Math.Abs(line.Y1-line.Y2)<1e-10;
            if(bounded&&layer==0&&(line.Kind&NativeLine.PgfaOpenRay)!=0)continue;
            if(!bounded&&layer==0){
                if((line.Kind&NativeLine.PgfaSideWall)!=0)continue;
                if((line.Kind&NativeLine.PgfaCrossbar)!=0){
                    yield return new(0,line.Y1,1,line.Y2,false);
                    continue;
                }
            }
            if(!bounded&&layer==1&&!horizontal&&line.Kind!=NativeLine.DepthRay)continue;
            yield return new(line.X1,line.Y1,line.X2,line.Y2,false);
            if(layer!=1||bounded||!horizontal||line.Kind==NativeLine.DepthRay)continue;
            double left=Math.Min(line.X1,line.X2),right=Math.Max(line.X1,line.X2);
            if(left>0)yield return new(0,line.Y1,left,line.Y1,true);
            if(right<1)yield return new(right,line.Y1,1,line.Y1,true);
        }
    }
    internal static void DrawGeometry(SKCanvas c,DocumentSettings d,NativeScene scene,int layer,float w,float h){
        if(layer==5){
            var dot=scene.Circles.First(p=>p.Layer==4);
            using var fill=new SKPaint{IsAntialias=true,Style=d.PrimitiveHollow?SKPaintStyle.Stroke:SKPaintStyle.Fill,StrokeWidth=(float)(d.Layers[4].LineWidth*Math.Min(w,h)/720),Color=Color(d.Layers[4].Color,d.Layers[4].Opacity)};
            c.DrawOval((float)dot.X*w,(float)dot.Y*h,(float)dot.Rx*w,(float)dot.Ry*h,fill);return;
        }
        var s=d.Layers[layer];float scale=Math.Min(w,h)/720;
        using var paint=new SKPaint{IsAntialias=true,Color=Color(s.Color,s.Opacity),Style=SKPaintStyle.Stroke,StrokeWidth=scale*(float)s.LineWidth};
        using var dotted=SKPathEffect.CreateDash([Math.Max(.5f,paint.StrokeWidth*.25f),Math.Max(3,paint.StrokeWidth*2.5f)],0);
        using var circlePath=new SKPath();var circle=scene.Circles.First();
        circlePath.AddOval(SKRect.Create((float)((circle.X-circle.Rx)*w),(float)((circle.Y-circle.Ry)*h),(float)(circle.Rx*2*w),(float)(circle.Ry*2*h)));
        var strokes=Strokes(scene,layer,d.Layers[1].Bounded).ToArray();
        bool dim=d.Mode==1&&d.PrimitiveCircle&&d.PrimitiveHollow&&d.Layers[4].Visible;
        bool mask=d.Mode==0&&d.Layers[4].Visible&&(layer!=1||!d.Layers[1].Shift);
        void Pass(bool inside,float opacity){
            c.Save();c.ClipRect(new SKRect(0,0,w,h));
            if(dim||mask)c.ClipPath(circlePath,inside?SKClipOperation.Intersect:SKClipOperation.Difference,true);
            paint.Color=Color(s.Color,s.Opacity*opacity);
            foreach(var line in strokes){
                paint.PathEffect=line.Dotted?dotted:null;paint.StrokeCap=line.Dotted?SKStrokeCap.Round:SKStrokeCap.Butt;
                c.DrawLine((float)line.X1*w,(float)line.Y1*h,(float)line.X2*w,(float)line.Y2*h,paint);
            }
            c.Restore();
        }
        Pass(false,1);if(dim)Pass(true,.5f);
        paint.PathEffect=null;paint.StrokeCap=SKStrokeCap.Butt;paint.Color=Color(s.Color,s.Opacity);
        foreach(var outline in scene.Circles.Where(p=>p.Layer==layer)){
            if(d.Mode==1||outline.Filled!=0)continue;
            c.DrawOval((float)outline.X*w,(float)outline.Y*h,(float)outline.Rx*w,(float)outline.Ry*h,paint);
        }
    }
    internal static void DrawLabels(SKCanvas c,DocumentSettings d,string family,PlacedLabel[] labels,int layer,float w,float h){
        var s=d.Layers[layer];float scale=Math.Min(w,h)/720;
        using var paint=new SKPaint{IsAntialias=true,Color=Color(s.Color,s.Opacity),Style=SKPaintStyle.Fill};
        c.Save();c.ClipRect(new SKRect(0,0,w,h));
        using var typeface=SKTypeface.FromFamilyName(family);
        foreach(var label in labels.Where(l=>l.Layer==layer)){
            if(label.Leader is SKPoint anchor){
                c.Save();foreach(var other in labels){var box=other.Bounds;box.Inflate(scale,scale);c.ClipRect(box,SKClipOperation.Difference,true);}
                paint.Style=SKPaintStyle.Stroke;paint.StrokeWidth=.6f*scale;paint.Color=Color(s.Color,s.Opacity*.65);
                c.DrawLine(anchor,new SKPoint(label.Bounds.MidX,label.Bounds.MidY),paint);c.Restore();paint.Style=SKPaintStyle.Fill;paint.Color=Color(s.Color,s.Opacity);
            }
            using var font=new SKFont(typeface,label.Size){Edging=SKFontEdging.Antialias};
            using var path=font.GetTextPath(label.Text,new SKPoint(label.X,label.Y));
            c.DrawPath(path,paint);
        }
        c.Restore();
    }
    internal static (int W,int H) Dimensions(DocumentSettings d,int width){
        width=Math.Max(d.P,(int)Math.Round((double)width/d.P)*d.P);int height=checked(width/d.P*d.Q);
        if(width<1||height<1||width>8192||height>8192||(long)width*height>64000000)throw new ArgumentException("Output must fit 8192 px / 64 MP");
        return(width,height);
    }
    internal static void Png(Editor vm,string path,int width,int dpi,bool photo){
        var (w,h)=Dimensions(vm.Doc,width);if(dpi is <36 or >1200)throw new ArgumentException("DPI: 36–1200");
        using var surface=SKSurface.Create(new SKImageInfo(w,h,SKColorType.Bgra8888,SKAlphaType.Premul))??throw new InvalidOperationException("Unable to allocate image");
        surface.Canvas.Clear(SKColors.Transparent);
        Draw(surface.Canvas,vm.Doc,vm.Scene,vm.Prefs.FontFamily,vm.Photo,w,h,photo&&vm.Photo is not null);
        using var image=surface.Snapshot();using var data=image.Encode(SKEncodedImageFormat.Png,100);
        StateStore.Atomic(path,WithDpi(data.ToArray(),dpi));
    }
    private static byte[] WithDpi(byte[] png,int dpi){
        byte[] chunk=new byte[21];BinaryPrimitives.WriteUInt32BigEndian(chunk.AsSpan(0,4),9);
        Encoding.ASCII.GetBytes("pHYs").CopyTo(chunk,4);
        uint ppm=(uint)Math.Round(dpi/0.0254);BinaryPrimitives.WriteUInt32BigEndian(chunk.AsSpan(8,4),ppm);BinaryPrimitives.WriteUInt32BigEndian(chunk.AsSpan(12,4),ppm);chunk[16]=1;
        uint crc=0xffffffff;for(int i=4;i<17;i++){crc^=chunk[i];for(int k=0;k<8;k++)crc=(crc&1)!=0?0xedb88320^(crc>>1):crc>>1;}
        BinaryPrimitives.WriteUInt32BigEndian(chunk.AsSpan(17,4),~crc);
        using var output=new MemoryStream();output.Write(png,0,33);output.Write(chunk);output.Write(png,33,png.Length-33);return output.ToArray();
    }
    internal static void Svg(Editor vm,string path,int width,bool photo){
        var (w,h)=Dimensions(vm.Doc,width);XNamespace ns="http://www.w3.org/2000/svg";
        var root=new XElement(ns+"svg",new XAttribute("width",w),new XAttribute("height",h),new XAttribute("viewBox",$"0 0 {w} {h}"),
            new XElement(ns+"title",Product.Title),
            new XElement(ns+"desc","Any screen zone may be combined with any D. D0 is near; D7 is far. Guide fields are not a metric depth map."));
        var labels=LabelLayout.Build(vm.Doc,vm.Scene,vm.Prefs.FontFamily,w,h);
        var passes=new[]{(Layer:-1,Text:false)}.Concat(Order.Select(l=>(Layer:l,Text:false))).Concat(Order.Where(l=>l!=5).Select(l=>(Layer:l,Text:true)));
        foreach(var pass in passes){int layer=pass.Layer;
            if(layer==-1&&!(photo&&vm.Photo is not null)||layer>=0&&!Visible(vm.Doc,layer))continue;
            using var memory=new MemoryStream();
            using(var stream=new SKManagedWStream(memory,false))
            using(var c=SKSvgCanvas.Create(new SKRect(0,0,w,h),stream)){
                c.Save();c.ClipRect(new SKRect(0,0,w,h));
                if(layer==-1)DrawBackground(c,vm.Doc,vm.Photo,w,h);else if(pass.Text)DrawLabels(c,vm.Doc,vm.Prefs.FontFamily,labels,layer,w,h);else DrawGeometry(c,vm.Doc,vm.Scene,layer,w,h);
                c.Restore();c.Flush();
            }
            var sub=XDocument.Parse(Encoding.UTF8.GetString(memory.ToArray()));
            string name=layer<0?"background":Names[layer]+(pass.Text?"-labels":"");
            var ids=sub.Descendants().Attributes("id").ToDictionary(a=>a.Value,a=>name+"-"+a.Value);
            foreach(var element in sub.Descendants())foreach(var a in element.Attributes().ToArray()){
                if(a.Name.LocalName=="id")a.Value=ids[a.Value];
                else foreach(var id in ids){a.Value=a.Value.Replace("url(#"+id.Key+")","url(#"+id.Value+")");if(a.Value=="#"+id.Key)a.Value="#"+id.Value;}
            }
            root.Add(new XElement(ns+"g",new XAttribute("id",name),sub.Root!.Elements()));
        }
        StateStore.Atomic(path,Encoding.UTF8.GetBytes(new XDocument(root).ToString()));
    }
}
