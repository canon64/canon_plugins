using System;
using System.IO;
using System.Linq;
using MainGameHCharacterMaker;

internal static class CardSaveChecks
{
    internal static void Run(Action<bool,string> check)
    {
        string root=Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(),"_analysis","HCharacterMakerSaveTests",Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(root);
        check(CardSaveIo.DirectoryFor("F:\\kks",1)=="F:\\kks\\UserData\\chara\\female\\freeH", "female cards target the exact freeH subfolder");
        check(CardSaveIo.DirectoryFor("F:\\kks",0)=="F:\\kks\\UserData\\chara\\male\\freeH", "male path is separated from female cards");
        bool rejected=false;
        try { CardSaveIo.DirectoryFor(root,2); } catch(ArgumentOutOfRangeException) { rejected=true; }
        check(rejected,"invalid character sex cannot choose an output directory");
        var utc=new DateTime(2026,10,30,0,0,0,DateTimeKind.Utc);
        string name=CardSaveIo.FileName("../美咲:\\?*",utc,"a1");
        check(Path.GetFileName(name)==name && name.Contains("美咲") && name.Contains("20261030_090000")
            && !name.Contains(':') && !name.Contains('?'),"card names preserve Japanese names and sanitize path characters using JST time");
        check(CardSaveIo.FileName("...",utc,"b1").Contains("キャラ"),"blank or unsafe display names get a usable card name");
        bool called=false; rejected=false;
        try { CardSaveIo.SaveNew(root,"denied.png",()=>false,w=>called=true,p=>called=true); }
        catch(InvalidOperationException) { rejected=true; }
        check(rejected && !called && !File.Exists(Path.Combine(root,"denied.png")),"denied H save never invokes writer or publishes a card");
        bool validated=false;
        string saved=CardSaveIo.SaveNew(root,"card.png",()=>true,w=>w.Write(new byte[]{1,2,3}),p=> {
            validated=File.ReadAllBytes(p).SequenceEqual(new byte[]{1,2,3}) && !File.Exists(Path.Combine(root,"card.png"));
        });
        check(validated && File.ReadAllBytes(saved).SequenceEqual(new byte[]{1,2,3}) && Directory.GetFiles(root,"*.partial").Length==0,
            "completed bytes are validated before final PNG publication and leave no partial file");
        rejected=false;
        try { CardSaveIo.SaveNew(root,"card.png",()=>true,w=>w.Write((byte)9),p=>{}); }
        catch(IOException) { rejected=true; }
        check(rejected && File.ReadAllBytes(saved).SequenceEqual(new byte[]{1,2,3}) && Directory.GetFiles(root,"*.partial").Length==0,
            "filename collision preserves the existing card and removes temporary bytes");
        rejected=false;
        try { CardSaveIo.SaveNew(root,"write-failure.png",()=>true,w=> { w.Write((byte)4); throw new IOException("disk failure"); },p=>{}); }
        catch(IOException) { rejected=true; }
        check(rejected && !File.Exists(Path.Combine(root,"write-failure.png")) && Directory.GetFiles(root,"*.partial").Length==0,
            "native write failure never exposes a broken PNG");
        rejected=false;
        try { CardSaveIo.SaveNew(root,"invalid.png",()=>true,w=>w.Write((byte)4),p=> { throw new InvalidDataException("missing card block"); }); }
        catch(InvalidDataException) { rejected=true; }
        check(rejected && !File.Exists(Path.Combine(root,"invalid.png")) && Directory.GetFiles(root,"*.partial").Length==0,
            "validation failure preserves previous cards and removes rejected output");
        bool allowed=true; called=false; rejected=false;
        try { CardSaveIo.SaveNew(root,"expired.png",()=>allowed,w=> { w.Write((byte)5); allowed=false; },p=>called=true); }
        catch(InvalidOperationException) { rejected=true; }
        check(rejected && !called && !File.Exists(Path.Combine(root,"expired.png")),"permit expiry during native save prevents final card publication");
        allowed=true; rejected=false;
        try { CardSaveIo.SaveNew(root,"late-expiry.png",()=>allowed,w=>w.Write((byte)6),p=>allowed=false); }
        catch(InvalidOperationException) { rejected=true; }
        check(rejected && !File.Exists(Path.Combine(root,"late-expiry.png")),"target replacement before publish prevents stale card publication");
        rejected=false;
        try { CardSaveIo.SaveNew(root,"../escape.png",()=>true,w=>{},p=>{}); }
        catch(ArgumentException) { rejected=true; }
        check(rejected,"card filename cannot escape its freeH directory");
        check(CardSaveIo.FileName("美咲",utc,"id1")!=CardSaveIo.FileName("美咲",utc,"id2"),"repeated saves in the same millisecond have distinct names");
    }
}
