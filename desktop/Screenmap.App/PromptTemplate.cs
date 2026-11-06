using System.Globalization;
using System.Text;
namespace Screenmap.App;

internal static class PromptTemplate {
    internal static string Create(Editor vm,string imagePath,string language,bool background){
        if(language is not ("ru" or "en"))throw new ArgumentException("Unsupported template language");
        bool ru=language=="ru";var d=vm.Doc;var b=new StringBuilder();
        string L(string en,string russian)=>ru?russian:en;
        string N(double value)=>value.ToString("0.###",CultureInfo.InvariantCulture);
        string name=Path.GetFileName(imagePath);
        string caption=name.Replace("\\","\\\\").Replace("[","\\[").Replace("]","\\]");
        var size=RenderEngine.Dimensions(d,d.ExportWidth);
        b.AppendLine(L("# Scene generation brief","# Задание на генерацию сцены")).AppendLine();
        b.AppendLine(L($"Use [{caption}](<./{Uri.EscapeDataString(name)}>) as the scene composition reference. Attach that image together with this brief.",
            $"Используй [{caption}](<./{Uri.EscapeDataString(name)}>) в качестве референса композиции сцены. Приложи это изображение вместе с заданием.")).AppendLine();
        b.AppendLine(L("## 1. How to read the reference","## 1. Легенда референса")).AppendLine();
        b.AppendLine(L("The screenmap is a directing guide. Its lines, labels, frames and centre marker are annotations; do not draw them as physical objects or include them in the final image. Preserve the composition and apply the scene description below.",
            "Screenmap — вспомогательная карта постановки кадра. Линии, номера, рамки и центральный маркер являются обозначениями: не превращай их в предметы сцены и не переноси в итоговое изображение. Сохрани композицию и примени описание сцены ниже.")).AppendLine();
        if(d.Mode==0){
            b.AppendLine(L("- **Screen zones (2D):** 1 is the central zone; 1.1–1.8 are the inner sectors; 2.1–2.24 are the outer sectors, clockwise as labelled. An object may span several zones. A ratio such as `1.2 : 1.3 = 3 : 2` describes the visible projected area of that one object across those zones; it does not fill either entire zone.",
                "- **Экранные зоны (2D):** 1 — центральная зона; 1.1–1.8 — внутренние секторы; 2.1–2.24 — внешние секторы по часовой стрелке согласно подписям. Объект может занимать несколько зон. Запись `1.2 : 1.3 = 3 : 2` задаёт соотношение видимых площадей одного объекта в этих зонах, а не заполнение зон целиком."));
            b.AppendLine(L("- **Perspective:** the centre of zone 1 is the vanishing point of the manual guide. It is not the mandatory position of the subject, a light source, a horizon or the camera's focus plane. A horizon, if needed, is specified separately in the scene description.",
                "- **Перспектива:** центр зоны 1 — точка схода ручной сетки. Это не обязательное положение объекта, источника света, горизонта или плоскости фокусировки камеры. Горизонт, если он нужен, укажи отдельно в описании сцены."));
        }else{
            var shape=d.PrimitiveShape();
            b.AppendLine(L($"- **Primitive grid (2D):** {shape.Columns} columns × {shape.Rows} rows, sequential cell IDs as labelled. Cell numbers are screen addresses, not depth. Keep a flat composition unless the scene explicitly asks for depth.",
                $"- **Простая сетка (2D):** {shape.Columns} столбцов × {shape.Rows} строк; последовательные номера ячеек указаны на референсе. Номера — экранные адреса, не глубина. Сохраняй плоскую композицию, если в описании сцены не задана глубина."));
        }
        if(d.Layers[1].Visible&&d.Layers[1].Opacity>0){
            b.AppendLine(L("- **Depth D0–D7:** D0 is nearest to the camera; increasing D means farther away; D7 is the farthest named plane. These are relative composition planes, not metres or depth below the water surface. Intermediate positions such as D3–D4 are allowed.",
                "- **Глубина D0–D7:** D0 ближе всего к камере; чем больше D, тем дальше план; D7 — самый дальний именованный план. Это относительные планы композиции, не метры и не глубина погружения под воду. Возможны промежуточные положения, например D3–D4."));
            b.AppendLine(L("- **Depth frames are not containers or clipping masks.** Assigning D5 describes the object's depth only: its silhouette may extend beyond the D5 rectangle in any direction. Do not shrink, centre or squeeze the object into that rectangle. Any screen zone can be combined with any D. Use the specified screen coverage, relative scale, occlusion, light and haze to convey distance. Objects with spatial extent may span several depth planes.",
                "- **Рамки глубины не являются контейнерами или масками обрезки.** D5 задаёт только глубину объекта: его силуэт может выходить за прямоугольник D5 в любую сторону. Не уменьшай, не центрируй и не втискивай объект внутрь этой рамки. Любую экранную зону можно сочетать с любым D. Передавай расстояние через заданное покрытие кадра, относительный масштаб, перекрытия, свет и дымку. Протяжённый объект может пересекать несколько планов глубины."));
        }
        if(d.Layers[2].Visible&&d.Layers[2].Opacity>0)
            b.AppendLine(L("- **Levels:** TOP / MIDTOP / MIDBOTTOM / BOTTOM name vertical bands of the image, not world elevation or distance from the camera.",
                "- **Уровни:** TOP / MIDTOP / MIDBOTTOM / BOTTOM обозначают полосы по высоте изображения, а не высоту в мире и не удалённость от камеры."));
        if(d.Layers[0].Visible&&d.Layers[0].Opacity>0)
            b.AppendLine(d.Layers[1].Bounded
                ? L("- **PGFA:** the subdivided virtual floor, walls and ceiling explain perspective.",
                    "- **ОППС / PGFA:** вспомогательные пол, стены и потолок с делениями поясняют перспективу.")
                : L("- **PGFA:** the upper and lower guide planes show sky and ground with open sides.",
                    "- **ОППС / PGFA:** верхняя и нижняя плоскости показывают небо и землю, а бока открыты."));
        b.AppendLine().AppendLine(L("## 2. This exported map","## 2. Параметры экспортированной карты")).AppendLine();
        b.AppendLine(L($"- Format: **{d.P}:{d.Q}**, **{size.W} × {size.H} px**; mode: **{(d.Mode==0?"Perspective":"Primitive")}**.",
            $"- Формат: **{d.P}:{d.Q}**, **{size.W} × {size.H} px**; режим: **{(d.Mode==0?"Перспектива":"Простая сетка")}**."));
        if(d.Mode==0)b.AppendLine(L($"- Vanishing point: x={N(d.CenterX*100)}%, y={N(d.CenterY*100)}% from the top-left corner; central-zone radius {N(d.Radius*100)}% of the short side.",
            $"- Точка схода: x={N(d.CenterX*100)}%, y={N(d.CenterY*100)}% от верхнего левого угла; радиус центральной зоны {N(d.Radius*100)}% короткой стороны."));
        string transparency=N(d.BackgroundTransparency*100);
        b.AppendLine(d.BackgroundOpacity==0
            ? L("- The exported background is fully transparent.","- Фон экспортирован полностью прозрачным.")
            : background&&vm.Photo is not null
                ? L($"- The exported background picture is part of the reference ({transparency}% transparency). Describe below what to retain or replace.",
                    $"- Экспортированное фоновое фото входит в референс (прозрачность {transparency}%). Ниже укажи, что сохранить или заменить.")
                : L($"- Solid guide background: {d.Background}, {transparency}% transparency. This colour is a guide and does not define the scene.",
                    $"- Однотонная подложка карты: {d.Background}, прозрачность {transparency}%. Цвет подложки служит ориентиром и не задаёт цвет сцены."));
        b.AppendLine().AppendLine(L("| Layer | Exported | Colour | Opacity | Labels enabled |","| Слой | Экспортирован | Цвет | Непрозрачность | Подписи включены |")).AppendLine("| --- | --- | --- | --- | --- |");
        string[] en=["PGFA","Depth","Levels","Zones (IDs)","Grid (lines)"];
        string[] russian=["ОППС","Глубина","Уровни","Зоны (номера)","Сетка (линии)"];
        for(int i=0;i<5;i++){
            var s=d.Layers[i];bool visible=s.Visible&&s.Opacity>0;
            string Yes(bool v)=>v?L("yes","да"):L("no","нет");
            b.AppendLine($"| {(ru?russian[i]:en[i])} | {Yes(visible)} | {s.Color} | {N(s.Opacity*100)}% | {Yes(s.Labels)} |");
        }
        b.AppendLine().AppendLine(L("Read only the layers that are actually exported. Disabled captions are not visible on the image. If depth is hidden, any D assignments in the brief below are textual relative distances, not visible frames. Do not invent missing scene objects or assume exact metric distances from this manual guide.",
            "Считывай только экспортированные слои. Выключенные подписи на изображении не видны. Если глубина скрыта, указания D ниже задают относительное удаление текстом, а не видимые рамки. Не выдумывай недостающие объекты сюжета и не вычисляй точные расстояния в метрах по этой ручной сетке.")).AppendLine();
        b.AppendLine(L("## 3. Scene prompt — fill in before generation","## 3. Промпт сцены — заполни перед генерацией")).AppendLine();
        b.AppendLine(L("Replace the square-bracket placeholders with your description; add or remove object rows as needed.","Замени поля в квадратных скобках своим описанием; добавь или убери строки объектов по необходимости.")).AppendLine();
        b.AppendLine(L("**Scene, mood, visual style:** [describe the setting, story, medium and atmosphere]","**Сюжет, настроение, стиль:** [опиши окружение, действие, способ изображения и атмосферу]")).AppendLine();
        b.AppendLine(L("| Object | Screen zones / coverage | Depth | Pose, orientation and action |","| Объект | Экранные зоны / покрытие | Глубина | Поза, ориентация и действие |")).AppendLine("| --- | --- | --- | --- |");
        b.AppendLine(L("| [main subject] | [zones, area ratios and approximate size in frame] | [D or range; or flat] | [where it faces and what it does] |",
            "| [главный объект] | [зоны, доли площади и примерный размер в кадре] | [D или диапазон; либо плоско] | [куда направлен и что делает] |"));
        b.AppendLine(L("| [supporting object] | [zones / size] | [D / flat] | [pose / action] |","| [второй объект] | [зоны / размер] | [D / плоско] | [поза / действие] |")).AppendLine();
        b.AppendLine(L("**Occlusion and relative scale:** [which object is in front; what overlaps; what must stay visible]","**Перекрытия и масштаб:** [кто перед кем; какие силуэты перекрываются; что обязательно оставить видимым]")).AppendLine();
        b.AppendLine(L("**Light sources:** [for each: screen direction/zone, depth, height, target, colour, softness and intensity]","**Источники света:** [для каждого: направление/зона, глубина, высота, цель, цвет, мягкость и сила]")).AppendLine();
        b.AppendLine(L("**Camera and horizon:** [view direction, camera tilt, horizon position or no visible horizon]","**Камера и горизонт:** [направление взгляда, наклон камеры, положение горизонта либо отсутствие видимого горизонта]")).AppendLine();
        b.AppendLine(L("**Motion / video only:** [timed screen-zone and D waypoints, speed, rotation axis, camera motion; keep object identities]","**Движение / только для видео:** [ключевые позиции по времени: экранная зона и D, скорость, ось вращения, движение камеры; сохраняй идентичность объектов]")).AppendLine();
        b.AppendLine(L("**Must retain / avoid:** [composition constraints, negative prompt, details to preserve]","**Обязательно сохранить / исключить:** [ограничения композиции, негативный промпт, важные детали]")).AppendLine();
        b.AppendLine("---").AppendLine(L($"Template generated by {Product.Title}. Complete section 3 before sending it to an image or video model.",
            $"Шаблон создан в {Product.Title}. Заполни раздел 3 перед отправкой генератору изображений или видео."));
        return b.ToString();
    }
}

internal static class ExportService {
    // Prepare both outputs first. Restore original files if the second commit fails.
    // An existing editable prompt gets a separate backup instead of disappearing on re-export.
    internal static string? Export(Editor vm,string path,string extension,bool picture,bool prompt,string language){
        if(extension is not ("png" or "svg"))throw new ArgumentException("Unsupported export format");
        string image=Path.GetFullPath(path),md=Path.ChangeExtension(image,".md"),token=Guid.NewGuid().ToString("N");
        var targets=new List<string>{image};if(prompt)targets.Add(md);
        if(targets.Any(Directory.Exists))throw new IOException("An export target is a directory");
        Directory.CreateDirectory(Path.GetDirectoryName(image)!);
        var original=targets.Select(File.Exists).ToArray();
        var backups=targets.Select(p=>p+"."+token+".rollback").ToArray();
        var prepared=targets.Select(p=>p+"."+token+".tmp").ToArray();
        int committed=0;string? promptBackup=null;
        try{
            if(extension=="png")RenderEngine.Png(vm,prepared[0],vm.Doc.ExportWidth,vm.Doc.ExportDpi,picture);
            else RenderEngine.Svg(vm,prepared[0],vm.Doc.ExportWidth,picture);
            if(prompt)File.WriteAllText(prepared[1],PromptTemplate.Create(vm,image,language,picture),new UTF8Encoding(false));
            for(int i=0;i<targets.Count;i++)if(original[i])File.Copy(targets[i],backups[i]);
            for(int i=0;i<targets.Count;i++){File.Move(prepared[i],targets[i],true);committed++;}
            if(prompt&&original[1]&&!File.ReadAllBytes(backups[1]).SequenceEqual(File.ReadAllBytes(md))){
                promptBackup=md+".previous-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss",CultureInfo.InvariantCulture)+"-"+token[..8]+".bak";
                File.Copy(backups[1],promptBackup);
            }
            return promptBackup;
        }catch{
            for(int i=committed-1;i>=0;i--)if(original[i])File.Copy(backups[i],targets[i],true);else File.Delete(targets[i]);
            throw;
        }finally{
            foreach(var temp in prepared.Concat(backups))if(File.Exists(temp))File.Delete(temp);
        }
    }
}
