using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
namespace Screenmap.App;
[StructLayout(LayoutKind.Sequential)]
internal struct NativeConfig {
    internal uint Size,Abi,Mode,P,Q,Columns,Subdivisions,Rows;
    internal double X,Y,Radius,DepthFont,PrimitiveRadius;
    internal uint DepthShift,Reserved;
}
[StructLayout(LayoutKind.Sequential)] internal struct NativeLine {
    internal const int PgfaSideWall=2, PgfaCrossbar=4, DepthRay=8, PgfaOpenRay=16;
    internal double X1,Y1,X2,Y2;
    internal int Layer,Kind;
}
[StructLayout(LayoutKind.Sequential)] internal struct NativeCircle {internal double X,Y,Rx,Ry;internal int Layer,Filled;}
[StructLayout(LayoutKind.Sequential)] internal unsafe struct NativeLabel {
    internal double X,Y;internal int Layer,Id;internal double FontCap;internal fixed byte Buffer[24];
    internal string Text {get {fixed(byte* p=Buffer)return Marshal.PtrToStringUTF8((nint)p)??"";}}
}
[StructLayout(LayoutKind.Sequential)] internal struct NativeRegion {internal double Ax,Ay,Bx,By,Cx,Cy;internal int Ring,Index;}
internal sealed class SceneHandle(nint value):SafeHandleZeroOrMinusOneIsInvalid(true) {
    protected override bool ReleaseHandle(){Native.sm_destroy(handle);return true;}
    internal void Initialize()=>SetHandle(value);
}
internal static partial class Native {
    private const string Library="ScreenmapCore";
    [LibraryImport(Library)]internal static partial uint sm_abi_version();
    [LibraryImport(Library)]internal static partial uint sm_struct_size(uint kind);
    [LibraryImport(Library)]internal static partial int sm_build(in NativeConfig config,out nint scene);
    [LibraryImport(Library)]internal static partial void sm_destroy(nint scene);
    [LibraryImport(Library)]internal static partial nint sm_last_error();
    [LibraryImport(Library)]internal static partial nint sm_config(SceneHandle scene);
    [LibraryImport(Library)]internal static partial nint sm_lines(SceneHandle scene,out uint count);
    [LibraryImport(Library)]internal static partial nint sm_circles(SceneHandle scene,out uint count);
    [LibraryImport(Library)]internal static partial nint sm_labels(SceneHandle scene,out uint count);
    [LibraryImport(Library)]internal static partial nint sm_regions(SceneHandle scene,out uint count);
    [LibraryImport(Library)]internal static partial int sm_hit_test(SceneHandle scene,double u,double v);
}
internal sealed class NativeScene:IDisposable {
    internal SceneHandle Handle {get;}
    internal NativeConfig Config {get;}
    internal NativeLine[] Lines {get;}
    internal NativeCircle[] Circles {get;}
    internal NativeLabel[] Labels {get;}
    internal NativeRegion[] Regions {get;}
    internal NativeScene(DocumentSettings d) {
        if(Native.sm_abi_version()!=2)throw new InvalidOperationException("ScreenmapCore ABI 2 required");
        int[] sizes=[Marshal.SizeOf<NativeConfig>(),Marshal.SizeOf<NativeLine>(),Marshal.SizeOf<NativeCircle>(),Marshal.SizeOf<NativeLabel>(),Marshal.SizeOf<NativeRegion>()];
        for(uint i=0;i<sizes.Length;i++)if(Native.sm_struct_size(i)!=sizes[i])throw new InvalidOperationException($"ABI structure {i} mismatch");
        var shape=d.PrimitiveShape();
        var c=new NativeConfig{Size=(uint)sizes[0],Abi=2,Mode=(uint)d.Mode,P=(uint)d.P,Q=(uint)d.Q,Columns=(uint)shape.Columns,Rows=(uint)shape.Rows,Subdivisions=(uint)d.PgfaDensity,
            X=d.Mode==0?d.CenterX:.5,Y=d.Mode==0?d.CenterY:.5,Radius=d.Mode==1&&d.PrimitiveCircle?d.PrimitiveRadius:d.Radius,DepthFont=d.Layers[1].FontSize,DepthShift=d.Layers[1].Shift?1u:0,PrimitiveRadius=d.PrimitiveRadius};
        if(Native.sm_build(in c,out var p)!=0)throw new InvalidOperationException(Marshal.PtrToStringUTF8(Native.sm_last_error())??"Native geometry error");
        Handle=new(p);Handle.Initialize();
        try {
            Config=Marshal.PtrToStructure<NativeConfig>(Native.sm_config(Handle));
            p=Native.sm_lines(Handle,out uint n);Lines=Copy<NativeLine>(p,n);
            p=Native.sm_circles(Handle,out n);Circles=Copy<NativeCircle>(p,n);
            p=Native.sm_labels(Handle,out n);Labels=Copy<NativeLabel>(p,n);
            p=Native.sm_regions(Handle,out n);Regions=Copy<NativeRegion>(p,n);
        }catch{Handle.Dispose();throw;}
    }
    private static unsafe T[] Copy<T>(nint p,uint n)where T:unmanaged{
        if(n>20000||(n>0&&p==0))throw new InvalidOperationException("Invalid geometry buffer");
        return new ReadOnlySpan<T>((void*)p,(int)n).ToArray();
    }
    internal string Hit(double x,double y){int id=Native.sm_hit_test(Handle,x,y);return id>=1000?$"{id-1000}":id>=200?$"2.{id-200}":id>=100?$"1.{id-100}":id==1?"1":"—";}
    public void Dispose()=>Handle.Dispose();
}
