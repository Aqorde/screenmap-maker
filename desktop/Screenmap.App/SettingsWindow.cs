using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
namespace Screenmap.App;
internal sealed class SettingsWindow:FloatingWindow {
    private readonly Editor vm;
    private readonly MainWindow owner;
    private readonly ContentControl content=new();
    private readonly StackPanel nav=new(){Spacing=6};
    private int page;
    private string language;
    private CheckBox? boundDepth;
    internal SettingsWindow(Editor model,MainWindow parent):base(UI.L(model,"Settings","Настройки"),true){
        vm=model;owner=parent;language=vm.Prefs.Language;Width=1100;Height=840;MinWidth=940;MinHeight=540;
        var grid=new Grid{ColumnDefinitions=new ColumnDefinitions("190,*"),Margin=new Thickness(16,8,16,16),ColumnSpacing=20};
        var sidebar=UI.Card(nav);sidebar.Classes.Add("settings-navigation");grid.Children.Add(sidebar);Grid.SetColumn(content,1);grid.Children.Add(content);Body.Content=grid;
        vm.Changed+=Changed;Closed+=(_,_)=>vm.Changed-=Changed;BuildNav();BuildPage();
    }
    private string L(string en,string ru)=>UI.L(vm,en,ru);
    private void Changed(){
        if(language!=vm.Prefs.Language){
            language=vm.Prefs.Language;Rename(L("Settings","Настройки"));BuildNav();BuildPage();
        }
        if(boundDepth is not null)boundDepth.IsChecked=vm.Doc.Layers[1].Bounded;
    }
    private void BuildNav(){
        nav.Children.Clear();string[] names=[L("Language","Язык"),L("Appearance","Внешний вид"),L("Quick menu","Быстрое меню")];
        for(int i=0;i<3;i++){int n=i;var b=UI.Button(names[i],()=>{page=n;BuildPage();});b.HorizontalAlignment=HorizontalAlignment.Stretch;nav.Children.Add(b);}
    }
    private void BuildPage(){
        boundDepth=null;
        var sections=new StackPanel{Spacing=14,Margin=new Thickness(0,0,30,15)};
        content.Content=new ScrollViewer{Content=sections,HorizontalScrollBarVisibility=Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,VerticalScrollBarVisibility=Avalonia.Controls.Primitives.ScrollBarVisibility.Auto};
        StackPanel Section(string heading){
            var body=new StackPanel{Spacing=12};var title=UI.Heading(heading);title.Margin=new Thickness(0,0,0,4);body.Children.Add(title);
            var card=UI.Card(body);card.Classes.Add("settings-section");sections.Children.Add(card);return body;
        }
        StackPanel panel;
        if(page==0){
            panel=Section(L("Language","Язык"));
            panel.Children.Add(UI.Select(["Русский","English"],vm.Prefs.Language=="ru"?0:1,i=>vm.UpdatePreferences(p=>p.Language=i==0?"ru":"en")));
            return;
        }
        if(page==2){
            panel=Section(L("Viewport quick menu","Быстрое меню вьюпорта"));
            panel.Children.Add(UI.Text(L("Choose which actions appear above the viewport.","Отметь действия, которые нужны над кадром.")));
            foreach(var action in owner.Actions){
                var row=new Grid{ColumnDefinitions=new ColumnDefinitions("*,Auto"),Margin=new Thickness(0,2)};
                row.Children.Add(UI.Text(action.Caption(vm)));
                var check=UI.Check("",vm.Prefs.QuickMenu.Contains(action.Id),enabled=>vm.UpdatePreferences(p=>{p.QuickMenu.Remove(action.Id);if(enabled)p.QuickMenu.Add(action.Id);}));
                Grid.SetColumn(check,1);row.Children.Add(check);panel.Children.Add(row);
            }
            return;
        }
        panel=Section(L("Global theme","Тема приложения"));
        panel.Children.Add(UI.Select([L("Light","Светлая"),L("Dark","Тёмная"),L("System","Системная")],vm.Prefs.Theme switch{"Light"=>0,"Dark"=>1,_=>2},i=>vm.UpdatePreferences(p=>p.Theme=i switch{0=>"Light",1=>"Dark",_=>"System"})));
        panel.Children.Add(UI.Check(L("Compact view","Компактный вид"),vm.Prefs.Compact,v=>vm.UpdatePreferences(p=>p.Compact=v)));
        panel=Section(L("Layer buttons","Отображение слоёв"));
        panel.Children.Add(UI.Select([L("Icons","Пиктограммы"),L("Text","Надписи"),L("Icons and text","Пиктограммы и надписи")],vm.Prefs.LayerDisplay switch{"Icons"=>0,"Text"=>1,_=>2},i=>vm.UpdatePreferences(p=>p.LayerDisplay=i switch{0=>"Icons",1=>"Text",_=>"Both"})));
        panel=Section(L("Viewport theme · layers","Тема вьюпорта · слои"));
        const string widths="*,115,110,40";
        var columns=new Grid{ColumnDefinitions=new ColumnDefinitions(widths),ColumnSpacing=8};
        columns.Children.Add(UI.Text(L("Layer","Слой")));
        var lineHeader=UI.Text(L("Line width, px","Толщина линий, пикс."));Grid.SetColumn(lineHeader,1);columns.Children.Add(lineHeader);
        var fontHeader=UI.Text(L("Font size","Размер шрифта"));Grid.SetColumn(fontHeader,2);columns.Children.Add(fontHeader);
        var colorHeader=UI.Text(L("Color","Цвет"));colorHeader.HorizontalAlignment=HorizontalAlignment.Center;Grid.SetColumn(colorHeader,3);columns.Children.Add(colorHeader);
        panel.Children.Add(columns);
        for(int i=0;i<5;i++){
            int index=i;var style=vm.Doc.Layers[i];
            var row=new Grid{ColumnDefinitions=new ColumnDefinitions(widths),ColumnSpacing=8,VerticalAlignment=VerticalAlignment.Center};
            var layerName=UI.Row(UI.Text(UI.Layer(vm,i)));ToolTip.SetTip(layerName,UI.LayerTip(vm,i));
            row.Children.Add(layerName);
            var lineWidth=UI.Number(style.LineWidth,.25,12,v=>vm.Update(d=>d.Layers[index].LineWidth=v,false),.25);lineWidth.Width=105;Grid.SetColumn(lineWidth,1);row.Children.Add(lineWidth);
            var size=UI.Number(style.FontSize,8,72,v=>vm.Update(d=>d.Layers[index].FontSize=v,index==1));size.Width=100;Grid.SetColumn(size,2);row.Children.Add(size);
            Button color=null!;color=UI.ColorButton(style.Color,()=>owner.OpenPalette(vm.Doc.Layers[index].Color,hex=>{vm.Update(d=>d.Layers[index].Color=hex,false);((Avalonia.Controls.Shapes.Ellipse)color.Content!).Fill=Brush.Parse(hex);}));
            Grid.SetColumn(color,3);row.Children.Add(color);panel.Children.Add(row);
        }
        panel=Section(L("Depth settings","Настройки глубины"));
        panel.Children.Add(UI.Check(L("Depth shift","Сдвиг глубины"),vm.Doc.Layers[1].Shift,v=>vm.Update(d=>d.Layers[1].Shift=v)));
        boundDepth=UI.Check(L("Bound depth planes","Ограничить глубину"),vm.Doc.Layers[1].Bounded,v=>{
            if(v!=vm.Doc.Layers[1].Bounded)vm.Update(d=>d.Layers[1].Bounded=v,false);
        });
        ToolTip.SetTip(boundDepth,L("Off: open the side walls and extend PGFA horizontals to the frame edges. Depth extensions are dotted.",
            "Сними флажок, чтобы раскрыть стены. Горизонтали ОППС продлятся сплошными линиями, а горизонтали глубины — точками."));
        panel.Children.Add(boundDepth);
        panel=Section(L("Background","Фон"));
        Button background=null!;background=UI.ColorButton(vm.Doc.Background,()=>owner.OpenPalette(vm.Doc.Background,hex=>{vm.Update(d=>d.Background=hex,false);((Avalonia.Controls.Shapes.Ellipse)background.Content!).Fill=Brush.Parse(hex);}));
        panel.Children.Add(UI.Row(UI.Text(L("Background color","Цвет фона")),background));
        panel=Section(L("Save theme","Сохранить тему"));
        var themeNames=vm.Prefs.ScreenmapThemes.Select(t=>t.Name).ToArray();
        var name=new TextBox{PlaceholderText=L("Theme name","Название темы"),Text=vm.Prefs.SelectedScreenmapTheme,HorizontalAlignment=HorizontalAlignment.Stretch,BorderThickness=new Thickness(0),Background=Brushes.Transparent};
        var combo=new Grid{ColumnDefinitions=new ColumnDefinitions("*,36")};combo.Children.Add(name);
        var dropdown=UI.Button("⌄",()=>{
            var menu=new ContextMenu{ItemsSource=themeNames.Select(theme=>{
                var item=new MenuItem{Header=theme};item.Click+=(_,_)=>{vm.ApplyTheme(theme);BuildPage();};return item;
            }).ToArray()};menu.Open(name);
        });dropdown.Content=UI.CaptionIcon("down");dropdown.Padding=new Thickness(4);Grid.SetColumn(dropdown,1);combo.Children.Add(dropdown);
        var input=UI.Card(combo,"Surface2");input.Padding=new Thickness(3);input.CornerRadius=new CornerRadius(7);
        var themeRow=new Grid{ColumnDefinitions=new ColumnDefinitions("*,60"),ColumnSpacing=8};themeRow.Children.Add(input);
        var saveTheme=UI.Button("OK",()=>owner.Guard(()=>{string text=(name.Text??"").Trim();if(text is "Dark" or "Light")throw new ArgumentException(L("Choose a new theme name","Укажи новое имя темы"));vm.SaveTheme(text);BuildPage();}));
        Grid.SetColumn(saveTheme,1);themeRow.Children.Add(saveTheme);panel.Children.Add(themeRow);
        panel=Section(L("Base font","Основной шрифт"));
        var fonts=FontManager.Current.SystemFonts.Select(f=>f.Name).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.CurrentCultureIgnoreCase).ToList();
        if(!fonts.Contains(vm.Prefs.FontFamily))fonts.Insert(0,vm.Prefs.FontFamily);
        var family=UI.Select(fonts.ToArray(),fonts.IndexOf(vm.Prefs.FontFamily),i=>vm.UpdatePreferences(p=>p.FontFamily=fonts[i]));panel.Children.Add(family);
        panel.Children.Add(UI.Row(UI.Text(L("Base size","Базовый размер")),UI.Number(vm.Prefs.BaseSize,10,28,v=>vm.UpdatePreferences(p=>p.BaseSize=v))));
    }
    internal void SelectPage(int n){page=n;BuildPage();}
}
