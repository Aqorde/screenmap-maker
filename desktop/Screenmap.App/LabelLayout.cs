using SkiaSharp;
namespace Screenmap.App;

internal sealed record PlacedLabel(int Layer,int Id,string Text,float Size,float X,float Y,SKRect Bounds,SKPoint? Leader=null);

internal static class LabelLayout {
    internal static bool Overlap(SKRect a,SKRect b,float gap=0)=>a.Left<b.Right+gap&&a.Right>b.Left-gap&&a.Top<b.Bottom+gap&&a.Bottom>b.Top-gap;
    private static bool Contains(SKRect outer,SKRect inner)=>inner.Left>=outer.Left-.01f&&inner.Right<=outer.Right+.01f&&inner.Top>=outer.Top-.01f&&inner.Bottom<=outer.Bottom+.01f;
    private static SKRect Shift(SKRect r,float x,float y)=>new(r.Left+x,r.Top+y,r.Right+x,r.Bottom+y);
    private static SKRect Glyph(SKTypeface face,string text,float size){using var font=new SKFont(face,size);using var path=font.GetTextPath(text,new SKPoint(0,0));return path.Bounds;}
    private static float Size(DocumentSettings d,NativeLabel l,float scale){
        double size=l.Layer==1&&d.Layers[1].Shift?Math.Max(8,d.Layers[1].FontSize-2*l.Id):d.Layers[l.Layer].FontSize;
        if(l.FontCap>0)size=Math.Min(size,l.FontCap);return(float)size*scale;
    }
    private static PlacedLabel At(NativeLabel l,SKRect glyph,float size,SKPoint center,SKPoint? leader=null){
        float x=center.X-glyph.MidX,y=center.Y-glyph.MidY;
        return new(l.Layer,l.Id,l.Text,size,x,y,Shift(glyph,x,y),leader);
    }
    private static SKRect Frame(NativeScene s,int id,float w,float h){
        var lines=s.Lines.Where(l=>l.Layer==1&&l.Kind==id).ToArray();
        return new((float)lines.Min(l=>Math.Min(l.X1,l.X2))*w,(float)lines.Min(l=>Math.Min(l.Y1,l.Y2))*h,(float)lines.Max(l=>Math.Max(l.X1,l.X2))*w,(float)lines.Max(l=>Math.Max(l.Y1,l.Y2))*h);
    }
    private static IEnumerable<SKPoint> Candidates(SKRect band,SKRect glyph,SKPoint preferred,float step){
        float minX=band.Left+glyph.Width/2,maxX=band.Right-glyph.Width/2,minY=band.Top+glyph.Height/2,maxY=band.Bottom-glyph.Height/2;
        if(minX>maxX||minY>maxY)yield break;
        float x=Math.Clamp(preferred.X,minX,maxX),y=Math.Clamp(preferred.Y,minY,maxY);
        yield return new(x,y);
        // Search along the long axis of the available band, then across it.
        bool horizontal=maxX-minX>=maxY-minY;
        var along=new List<float>{horizontal?x:y,horizontal?minX:minY,horizontal?maxX:maxY};
        float low=horizontal?minX:minY,high=horizontal?maxX:maxY;
        for(float v=low;v<=high;v+=Math.Max(step,horizontal?glyph.Width*.55f:glyph.Height*.8f))along.Add(v);
        float[] across=horizontal?[y,minY,(minY+maxY)/2,maxY]:[x,minX,(minX+maxX)/2,maxX];
        foreach(float a in along.Distinct().OrderBy(v=>Math.Abs(v-(horizontal?x:y))))foreach(float b in across.Distinct())yield return horizontal?new(a,b):new(b,a);
    }
    internal static PlacedLabel[] Build(DocumentSettings d,NativeScene scene,string family,float w,float h){
        float unit=Math.Min(w,h)/720;
        return Solve(d,scene,family,w/unit,h/unit).Select(p=>p with{
            Size=p.Size*unit,X=p.X*unit,Y=p.Y*unit,
            Bounds=new(p.Bounds.Left*unit,p.Bounds.Top*unit,p.Bounds.Right*unit,p.Bounds.Bottom*unit),
            Leader=p.Leader is SKPoint leader?new(leader.X*unit,leader.Y*unit):null
        }).ToArray();
    }
    private static PlacedLabel[] Solve(DocumentSettings d,NativeScene scene,string family,float w,float h){
        using var face=SKTypeface.FromFamilyName(family);float scale=Math.Min(w,h)/720,gap=3*scale;
        var result=new List<PlacedLabel>();
        bool Enabled(NativeLabel l)=>d.Layers[l.Layer].Visible&&d.Layers[l.Layer].Labels&&d.Layers[l.Layer].Opacity>0;
        // Screen-region IDs retain their native positions; Depth must avoid them.
        foreach(int layer in new[]{3,2,4,0})foreach(var label in scene.Labels.Where(l=>l.Layer==layer&&Enabled(l))){
            float size=Size(d,label,scale);var glyph=Glyph(face,label.Text,size);
            var center=new SKPoint(Math.Clamp((float)label.X*w,glyph.Width/2+2*scale,Math.Max(glyph.Width/2+2*scale,w-glyph.Width/2-2*scale)),Math.Clamp((float)label.Y*h,glyph.Height/2+2*scale,Math.Max(glyph.Height/2+2*scale,h-glyph.Height/2-2*scale)));
            result.Add(At(label,glyph,size,center));
        }
        foreach(var label in scene.Labels.Where(l=>l.Layer==1&&Enabled(l)).OrderBy(l=>l.Id)){
            var outer=label.Id==0?new SKRect(0,0,w,h):Frame(scene,label.Id-1,w,h);var inner=Frame(scene,label.Id,w,h);
            SKRect[] bands=[new(outer.Left,outer.Top,outer.Right,inner.Top),new(outer.Left,inner.Bottom,outer.Right,outer.Bottom),new(outer.Left,inner.Top,inner.Left,inner.Bottom),new(inner.Right,inner.Top,outer.Right,inner.Bottom)];
            var preferred=new SKPoint((float)label.X*w,(float)label.Y*h);float initial=Size(d,label,scale);
            PlacedLabel? chosen=null;
            bool Free(SKRect box)=>!result.Any(p=>Overlap(box,p.Bounds,gap));
            // Prefer the original band and size. Only reduce Depth, never zone IDs.
            for(float size=initial;size>=Math.Min(initial,5*scale)-.001f&&chosen is null;size-=Math.Max(scale,(initial-5*scale)/5)){
                var glyph=Glyph(face,label.Text,size);
                foreach(var band in bands.OrderBy(b=>DistanceTo(b,preferred))){
                    foreach(var center in Candidates(band,glyph,preferred,6*scale)){
                        var candidate=At(label,glyph,size,center);
                        if(Contains(band,candidate.Bounds)&&Free(candidate.Bounds)){chosen=candidate;break;}
                    }
                    if(chosen is not null)break;
                }
            }
            if(chosen is null){
                // Crowded custom fonts: use a free callout with a leader to its D band.
                float size=Math.Min(initial,5*scale);var glyph=Glyph(face,label.Text,size);
                var frame=new SKRect(2*scale,2*scale,w-2*scale,h-2*scale);
                foreach(var center in Candidates(frame,glyph,preferred,6*scale)){
                    var candidate=At(label,glyph,size,center,preferred);
                    if(Free(candidate.Bounds)){chosen=candidate;break;}
                }
                if(chosen is null){
                    for(float y=frame.Top+glyph.Height/2;y<frame.Bottom-glyph.Height/2&&chosen is null;y+=Math.Max(4*scale,glyph.Height+gap))
                    for(float x=frame.Left+glyph.Width/2;x<frame.Right-glyph.Width/2;x+=Math.Max(4*scale,glyph.Width+gap)){
                        var candidate=At(label,glyph,size,new(x,y),preferred);if(Free(candidate.Bounds)){chosen=candidate;break;}
                    }
                }
            }
            if(chosen is null){
                // Oversized cell IDs can cover almost the entire frame. The
                // outer 2px margin remains available for an emergency callout.
                float size=Math.Min(initial,2*scale);var glyph=Glyph(face,label.Text,size);
                foreach(var center in Candidates(new(0,0,w,h),glyph,new(w/2,0),2*scale)){
                    var candidate=At(label,glyph,size,center,preferred);
                    if(!result.Any(p=>Overlap(candidate.Bounds,p.Bounds,.15f*scale))){chosen=candidate;break;}
                }
            }
            if(chosen is null)throw new InvalidOperationException("No free position for depth caption "+label.Text);
            result.Add(chosen);
        }
        return result.ToArray();
    }
    private static float DistanceTo(SKRect r,SKPoint p){float x=Math.Clamp(p.X,r.Left,r.Right),y=Math.Clamp(p.Y,r.Top,r.Bottom);return Math.Abs(p.X-x)+Math.Abs(p.Y-y);}
}
