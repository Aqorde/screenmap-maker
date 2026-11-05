using System.Reflection;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
namespace Screenmap.App;

internal sealed class DocumentationWindow:FloatingWindow {
    internal MarkdownGuide Guide {get;}
    internal DocumentationWindow(Editor editor):base(UI.L(editor,"Documentation · Getting started","Документация · Руководство")){
        Width=1000;Height=820;MinWidth=650;MinHeight=440;
        Guide=new MarkdownGuide(ReadGuide(editor.Prefs.Language));Body.Content=Guide;
    }
    internal static string ReadGuide(string language){
        using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream($"Screenmap.App.Guide.{(language=="ru"?"ru":"en")}.md")??throw new InvalidOperationException("Embedded guide missing");
        using var reader=new StreamReader(stream);return reader.ReadToEnd();
    }
}

// A small native renderer for the bundled Markdown dialect. No disk override,
// HTML execution, WebView, remote image loading or Markdown package is needed.
internal sealed class MarkdownGuide:UserControl {
    private readonly ScrollViewer scroll;
    private readonly Dictionary<string,Control> anchors=new(StringComparer.OrdinalIgnoreCase);
    internal int AnchorCount=>anchors.Count;
    internal int LinkCount {get;private set;}
    internal int UnresolvedLinks {get;private set;}
    internal MarkdownGuide(string markdown){
        var panel=new StackPanel{Spacing=10,Margin=new Thickness(28,12,28,28)};
        scroll=new ScrollViewer{Content=panel,HorizontalScrollBarVisibility=Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled};
        var lines=markdown.Replace("\r","").Split('\n');string? pending=null;var targets=new List<string>();
        for(int i=0;i<lines.Length;i++){
            string line=lines[i].TrimEnd();if(string.IsNullOrWhiteSpace(line))continue;
            var anchor=Regex.Match(line,"^<a id=\"([^\"]+)\"></a>$");
            if(anchor.Success){pending=anchor.Groups[1].Value;continue;}
            Control control;
            if(line.StartsWith("```")){
                var code=new List<string>();while(++i<lines.Length&&!lines[i].StartsWith("```"))code.Add(lines[i]);
                var block=UI.Text(string.Join('\n',code),14);block.FontFamily=new FontFamily("Consolas");
                control=UI.Card(block,"Surface2");
            }else if(line.StartsWith('|')){
                var table=new Grid{ColumnSpacing=16,RowSpacing=8};int row=0;
                do{
                    string[] cells=lines[i].Trim().Trim('|').Split('|').Select(x=>x.Trim()).ToArray();
                    if(cells.All(c=>Regex.IsMatch(c,"^:?-+:?$")))continue;
                    if(table.ColumnDefinitions.Count==0)for(int j=0;j<cells.Length;j++)table.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                    table.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                    for(int j=0;j<cells.Length;j++){var cell=Rich(cells[j]);if(row==0)cell.FontWeight=FontWeight.SemiBold;Grid.SetRow(cell,row);Grid.SetColumn(cell,j);table.Children.Add(cell);}row++;
                }while(i+1<lines.Length&&lines[i+1].TrimStart().StartsWith('|')&&++i>0);
                control=UI.Card(table);
            }else{
                var link=Regex.Match(line,"^(?:- )?\\[([^\\]]+)\\]\\(#([^)]+)\\)$");
                var heading=Regex.Match(line,"^(#{1,4}) (.+)$");
                if(link.Success){
                    string id=link.Groups[2].Value;targets.Add(id);LinkCount++;
                    var button=UI.Button(link.Groups[1].Value,()=>Navigate(id));button.Classes.Add("guide-link");control=button;
                }else if(heading.Success){
                    int level=heading.Groups[1].Length;var h=Rich(heading.Groups[2].Value);h.FontSize=level switch{1=>30,2=>24,3=>19,_=>16};h.FontWeight=FontWeight.SemiBold;h.LineHeight=double.NaN;h.Margin=new Thickness(0,level==1?4:18,0,6);control=h;
                    if(level==1){
                        h.Margin=default;h.VerticalAlignment=VerticalAlignment.Center;
                        var header=new Grid{Height=64,ColumnDefinitions=new ColumnDefinitions("64,*"),ColumnSpacing=14};
                        header.Children.Add(new Image{Width=64,Height=64,Source=new Bitmap(AssetLoader.Open(new Uri("avares://ScreenmapMaker/Assets/ScreenmapMaker_64.png")))});
                        Grid.SetColumn(h,1);header.Children.Add(h);control=header;
                    }
                }else if(line.StartsWith("> "))control=UI.Card(Rich(line[2..]),"Surface2");
                else if(line=="---")control=new Separator{Margin=new Thickness(0,8)};
                else if(line.StartsWith("- "))control=Rich("•  "+line[2..]);
                else control=Rich(line);
            }
            if(pending is not null){anchors[pending]=control;pending=null;}panel.Children.Add(control);
        }
        UnresolvedLinks=targets.Count(t=>!anchors.ContainsKey(t));
        var top=UI.Button("↑",()=>Navigate("top"));top.HorizontalAlignment=HorizontalAlignment.Right;ToolTip.SetTip(top,"↑");
        var dock=new DockPanel();DockPanel.SetDock(top,Dock.Bottom);dock.Children.Add(top);dock.Children.Add(scroll);Content=dock;
    }
    internal bool Navigate(string id){
        if(!anchors.TryGetValue(id,out var target))return false;
        Dispatcher.UIThread.Post(()=>{
            if(id=="top"){scroll.Offset=new Vector(0,0);return;}
            if(scroll.Content is Visual content&&target.TranslatePoint(new Point(0,0),content) is Point point)scroll.Offset=new Vector(0,Math.Max(0,point.Y));
        });return true;
    }
    private static TextBlock Rich(string text){
        var block=UI.Text("");block.LineHeight=23;
        foreach(string token in Regex.Split(text,"(\\*\\*.*?\\*\\*|`[^`]+`)")){
            if(token.StartsWith("**")&&token.EndsWith("**")&&token.Length>=4)block.Inlines!.Add(new Bold{Inlines={new Run(token[2..^2])}});
            else if(token.StartsWith('`')&&token.EndsWith('`')&&token.Length>=2)block.Inlines!.Add(new Run(token[1..^1]){FontFamily=new FontFamily("Consolas")});
            else block.Inlines!.Add(new Run(token));
        }
        return block;
    }
}

internal sealed class AboutWindow:FloatingWindow {
    internal AboutWindow(Editor vm):base(UI.L(vm,"About","О программе"),false){
        Width=MinWidth=MaxWidth=540;MinHeight=0;SizeToContent=SizeToContent.Height;
        var logo=new Image{Source=new Bitmap(AssetLoader.Open(new Uri("avares://ScreenmapMaker/Assets/ScreenmapMaker_128.png"))),Width=128,Height=128,HorizontalAlignment=HorizontalAlignment.Center};
        var title=UI.Text("aqorde ScreenMap Maker",26);title.FontWeight=FontWeight.SemiBold;title.HorizontalAlignment=HorizontalAlignment.Center;
        var version=UI.Text("v"+Product.Version,18);version.HorizontalAlignment=HorizontalAlignment.Center;
        var text=UI.Text(UI.L(vm,"Framing and composition grid tool for AI image and video generation.","Инструмент построения сеток для режиссуры кадра и сцены при генерации изображений и видео."));text.TextAlignment=TextAlignment.Center;
        var license=UI.Text(Product.License(vm));license.TextAlignment=TextAlignment.Center;
        var copyright=UI.Text(Product.Copyright,12);copyright.TextAlignment=TextAlignment.Center;
        Body.Content=new ScrollViewer{Content=new StackPanel{Spacing=12,Margin=new Thickness(28,10,28,24),Children={logo,title,version,text,license,copyright}}};
    }
}
