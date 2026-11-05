using System.IO.Compression;
using System.Text.Json;
namespace Screenmap.App;
internal static class StateStore {
    internal static string DefaultConfigDirectory()=>File.Exists(Path.Combine(AppContext.BaseDirectory,"installed-mode"))
        ?Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"aqorde","ScreenmapMaker","config")
        :Path.Combine(AppContext.BaseDirectory,"config");
    internal static readonly JsonSerializerOptions Json=new(){WriteIndented=true,PropertyNameCaseInsensitive=true};
    internal static void Atomic(string path,byte[] data){
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try{File.WriteAllBytes(temp,data);File.Move(temp,path,true);}finally{if(File.Exists(temp))File.Delete(temp);}
    }
    internal static void SaveSession(string directory,Editor vm,double width,double height){
        Editor.Validate(vm.Doc);Editor.ValidatePreferences(vm.Prefs);
        string? photo=null;
        if(vm.Photo is not null){
            photo="photo-"+Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(vm.Photo.Bytes))[..16]+".image";
            Atomic(Path.Combine(directory,photo),vm.Photo.Bytes);
        }
        var state=new SessionState{Document=vm.Doc,Preferences=vm.Prefs,PhotoFile=photo,Width=width,Height=height};
        Atomic(Path.Combine(directory,"state.json"),JsonSerializer.SerializeToUtf8Bytes(state,Json));
    }
    internal static SessionState? LoadSession(string directory,Editor vm,string? applicationDirectory=null){
        string path=Path.Combine(directory,"state.json");
        if(!File.Exists(path)){
            string marker=Path.Combine(applicationDirectory??AppContext.BaseDirectory,"installed-mode");
            if(File.Exists(marker)){
                string language=File.ReadAllText(marker).Trim();
                if(language is "en" or "ru")vm.UpdatePreferences(p=>p.Language=language);
            }
            return null;
        }
        if(new FileInfo(path).Length>4*1024*1024)throw new InvalidDataException("Config exceeds size limit");
        var state=JsonSerializer.Deserialize<SessionState>(File.ReadAllBytes(path),Json)??throw new InvalidDataException("Empty config");
        if(state.Version!=2||!double.IsFinite(state.Width)||!double.IsFinite(state.Height)||state.Width is <800 or >6000||state.Height is <500 or >6000)throw new InvalidDataException("Invalid config");
        Editor.Validate(state.Document);Editor.ValidatePreferences(state.Preferences);
        if(state.PhotoFile is not null){
            if(Path.GetFileName(state.PhotoFile)!=state.PhotoFile)throw new InvalidDataException("Invalid image reference");
            using var test=new PhotoAsset(File.ReadAllBytes(Path.Combine(directory,state.PhotoFile)));
            string id=state.Document.PhotoId??Guid.NewGuid().ToString("N");
            vm.Photos[id]=new PhotoAsset(test.Bytes);state.Document.PhotoId=id;
        }else state.Document.PhotoId=null;
        vm.SetPreferences(state.Preferences);vm.Replace(state.Document);return state;
    }
    internal static void SaveProject(string path,Editor vm){
        Editor.Validate(vm.Doc);
        string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try{
            using(var f=File.Create(temp))using(var archive=new ZipArchive(f,ZipArchiveMode.Create)){
                using(var stream=archive.CreateEntry("manifest.json").Open())JsonSerializer.Serialize(stream,new{format="aqorde-screenmap",version=2,background=vm.Photo is null?null:"assets/background.image"},Json);
                using(var stream=archive.CreateEntry("document.json").Open())JsonSerializer.Serialize(stream,vm.Doc,Json);
                if(vm.Photo is not null)using(var stream=archive.CreateEntry("assets/background.image",CompressionLevel.NoCompression).Open())stream.Write(vm.Photo.Bytes);
            }
            File.Move(temp,path,true);
        }finally{if(File.Exists(temp))File.Delete(temp);}
    }
    private static byte[] Read(ZipArchiveEntry entry,int max){
        if(entry.Length>max)throw new InvalidDataException("Project entry exceeds size limit");
        using var stream=entry.Open();using var output=new MemoryStream();byte[] buffer=new byte[8192];int n;
        while((n=stream.Read(buffer))>0){if(output.Length+n>max)throw new InvalidDataException("Project entry too large");output.Write(buffer,0,n);}return output.ToArray();
    }
    internal static void OpenProject(string path,Editor vm){
        using var archive=ZipFile.OpenRead(path);
        if(archive.Entries.Count>8||archive.Entries.Select(e=>e.FullName).Distinct().Count()!=archive.Entries.Count)throw new InvalidDataException("Invalid project entries");
        using var manifest=JsonDocument.Parse(Read(archive.GetEntry("manifest.json")??throw new InvalidDataException("Missing manifest"),16384));
        if(manifest.RootElement.GetProperty("format").GetString()!="aqorde-screenmap")throw new InvalidDataException("Unknown format");
        int version=manifest.RootElement.GetProperty("version").GetInt32();if(version is not(1 or 2))throw new InvalidDataException("Unsupported version");
        var bytes=Read(archive.GetEntry("document.json")??throw new InvalidDataException("Missing document"),1048576);
        var doc=JsonSerializer.Deserialize<DocumentSettings>(bytes,Json)??throw new InvalidDataException("Empty project");
        if(version==1){doc.SchemaVersion=2;doc.PgfaDensity=Math.Clamp(doc.PgfaDensity,1,6);var defaults=new DocumentSettings();for(int i=0;i<doc.Layers.Length&&i<5;i++){doc.Layers[i].FontSize=defaults.Layers[i].FontSize;doc.Layers[i].Shift=defaults.Layers[i].Shift;}}
        Editor.Validate(doc);using var native=new NativeScene(doc);
        var background=manifest.RootElement.GetProperty("background").GetString();
        if(background is not null){
            if(background!="assets/background.image")throw new InvalidDataException("Invalid background reference");
            var image=Read(archive.GetEntry(background)??throw new InvalidDataException("Missing photo"),64*1024*1024);
            var asset=new PhotoAsset(image);string id=Guid.NewGuid().ToString("N");vm.Photos[id]=asset;doc.PhotoId=id;
        }else doc.PhotoId=null;
        vm.Replace(doc);
    }
}
