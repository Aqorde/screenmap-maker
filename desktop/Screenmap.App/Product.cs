namespace Screenmap.App;
internal static class Product {
    internal const string Version="0.5.12rc (RC)";
    internal const string Title="aqorde ScreenMap Maker v"+Version;
    internal const string AboutCredit="2026 Alexander Andreev (aqorde)";
    internal static string License(Editor vm)=>UI.L(vm,"MIT License","Лицензия MIT");
}
