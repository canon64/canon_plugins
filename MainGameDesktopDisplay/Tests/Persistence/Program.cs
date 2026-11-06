using System;
using System.Collections.Generic;
using System.IO;
using BepInEx.Configuration;
using MainGameDesktopDisplay;

static class Program
{
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);Console.WriteLine("PASS "+message);}
    static int Main()
    {
        string path=Path.Combine(@"F:\kks\work\plugins\canon_plugins\MainGameDesktopDisplay\verification","config-roundtrip-"+Guid.NewGuid().ToString("N")+".cfg");
        try
        {
            Console.WriteLine("Testing the installed BepInEx ConfigFile against an isolated verification file.");
            var config=new ConfigFile(path,false);
            config.Bind("Desktop","Monitor","","").Value="DISPLAY1";
            config.Bind("Clip.__._DISPLAY1","EdgesPercent","","").Value="0,30.9237251,6.767307,70.79644";
            var preset=new DisplayPreset{Name="縦画面・切り取り",Monitor="DISPLAY1",Width=.3475971f};
            preset.Edges[1]=30.9237251f;preset.Edges[2]=6.767307f;preset.Edges[3]=70.79644f;
            string data=DisplayPreset.Encode(new List<DisplayPreset>{preset});
            config.Bind("Presets","Screens","","").Value=data;
            config.Bind("Presets","LastName","","").Value=preset.Name;
            string assignment=Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(preset.Name));
            config.Bind("Preset.DISPLAY1","SelectedName","","").Value=assignment;
            config.Save();
            for(int n=0;n<3;n++)
            {
                var read=new ConfigFile(path,false);
                Check(read.Bind("Desktop","Monitor","","").Value=="DISPLAY1","saved monitor survives actual config reload "+n);
                Check(read.Bind("Clip.__._DISPLAY1","EdgesPercent","","").Value=="0,30.9237251,6.767307,70.79644","existing crop section survives actual config reload "+n);
                string encoded=read.Bind("Presets","Screens","","").Value;
                var restored=DisplayPreset.Decode(encoded);
                Check(encoded==data&&restored.Count==1&&restored[0].Name==preset.Name&&restored[0].Monitor==preset.Monitor&&restored[0].Width==preset.Width&&restored[0].Edges[3]==preset.Edges[3],"named monitor/crop/width preset survives actual config reload "+n);
                Check(read.Bind("Presets","LastName","","").Value==preset.Name,"last preset selection survives actual config reload "+n);
                Check(read.Bind("Preset.DISPLAY1","SelectedName","","").Value==assignment,"per-monitor assigned preset survives actual config reload "+n);
                read.Save();
            }
            return 0;
        }
        catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
        finally{if(File.Exists(path))File.Delete(path);}
    }
}
