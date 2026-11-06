using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
namespace Screenmap.App;
internal sealed class ExportWindow:FloatingWindow {
    private readonly Editor vm;
    internal CheckBox BackgroundOption {get;}
    internal CheckBox PromptOption {get;}
    internal ComboBox PromptLanguage {get;}
    internal ExportWindow(Editor editor,MainWindow owner):base(UI.L(editor,"Export","Экспорт")){
        vm=editor;Width=650;Height=550;MinWidth=600;MinHeight=340;
        var panel=new StackPanel{Margin=new Thickness(24,12,24,24),Spacing=14};
        var format=UI.Select(["PNG","SVG"],0,_=>{});
        var dimensions=UI.Text("");Button? exportButton=null;
        void Info(){try{var size=RenderEngine.Dimensions(vm.Doc,vm.Doc.ExportWidth);dimensions.Text=$"{size.W} × {size.H} px";if(exportButton is not null)exportButton.IsEnabled=true;}
            catch(ArgumentException){dimensions.Text=UI.L(vm,"Maximum: 8192 px per side / 64 MP","Максимум: 8192 px на сторону / 64 MP");if(exportButton is not null)exportButton.IsEnabled=false;}}
        panel.Children.Add(UI.Row(UI.Text(UI.L(vm,"Format","Формат")),format));
        panel.Children.Add(UI.Row(UI.Text(UI.L(vm,"Width, px","Ширина, px")),UI.Number(vm.Doc.ExportWidth,64,8192,v=>{vm.Update(d=>d.ExportWidth=(int)v,false);Info();})));
        panel.Children.Add(dimensions);Info();
        panel.Children.Add(UI.Row(UI.Text("DPI · PNG"),UI.Number(vm.Doc.ExportDpi,36,1200,v=>vm.Update(d=>d.ExportDpi=(int)v,false))));
        BackgroundOption=UI.Check(UI.L(vm,"Export with background picture","Экспорт с фоновым изображением"),vm.Photo is not null&&vm.Doc.ExportBackground,v=>vm.Update(d=>d.ExportBackground=v,false));
        BackgroundOption.IsEnabled=vm.Photo is not null;panel.Children.Add(BackgroundOption);
        PromptOption=UI.Check(UI.L(vm,"Export with prompt template","Экспортировать с шаблоном промпта"),vm.Doc.ExportPrompt,v=>{vm.Update(d=>d.ExportPrompt=v,false);if(PromptLanguage is not null)PromptLanguage.IsEnabled=v;});
        PromptLanguage=UI.Select(["Русский","English"],vm.Doc.ExportPromptLanguage=="en"?1:0,i=>vm.Update(d=>d.ExportPromptLanguage=i==0?"ru":"en",false));
        PromptLanguage.IsEnabled=PromptOption.IsChecked==true;PromptLanguage.MinWidth=110;
        var promptRow=new Grid{ColumnDefinitions=new ColumnDefinitions("*,Auto"),ColumnSpacing=12};promptRow.Children.Add(PromptOption);Grid.SetColumn(PromptLanguage,1);promptRow.Children.Add(PromptLanguage);panel.Children.Add(promptRow);
        panel.Children.Add(UI.Text(UI.L(vm,"A matching .md file is saved beside the image. On re-export, an edited template is preserved in a .md.previous-….bak file.","Рядом с изображением сохраняется одноимённый .md. При повторном экспорте изменённый шаблон сохраняется в .md.previous-….bak.")));
        var export=UI.Button(UI.L(vm,"Export…","Экспортировать…"),async()=>{
            try{
                string ext=format.SelectedIndex==0?"png":"svg";
                var file=await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions{SuggestedFileName="screenmap."+ext,DefaultExtension=ext,FileTypeChoices=[new FilePickerFileType(ext.ToUpperInvariant()){Patterns=["*."+ext]}]});
                if(file is null)return;
                bool picture=vm.Photo is not null&&BackgroundOption.IsChecked==true;
                ExportService.Export(vm,file.Path.LocalPath,ext,picture,PromptOption.IsChecked==true,vm.Doc.ExportPromptLanguage);
                Close();
            }catch(Exception ex){owner.ShowMessage(UI.L(vm,"Export error","Ошибка экспорта"),ex.Message);}
        });exportButton=export;panel.Children.Add(export);Body.Content=new ScrollViewer{Content=panel};
        void Changed(){BackgroundOption.IsEnabled=vm.Photo is not null;if(vm.Photo is null&&BackgroundOption.IsChecked==true)BackgroundOption.IsChecked=false;Info();}
        vm.Changed+=Changed;Closed+=(_,_)=>vm.Changed-=Changed;Changed();
    }
}
