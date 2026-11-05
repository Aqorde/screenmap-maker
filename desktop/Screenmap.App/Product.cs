namespace Screenmap.App;
internal static class Product {
    internal const string Version="0.5.9b (beta)";
    internal const string Title="aqorde ScreenMap Maker v"+Version;
    internal const string Copyright="© 2026 Alexander Andreev (aqorde)";
    internal static string License(Editor vm)=>UI.L(vm,"MIT License","Лицензия MIT");
}
