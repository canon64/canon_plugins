using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class DesktopSmokeEditor
{
    static string Output => Environment.GetEnvironmentVariable("DESKTOP_SMOKE_OUTPUT");
    static DesktopSmokeEditor()
    {
        if(string.IsNullOrEmpty(Output)) return;
        EditorApplication.playModeStateChanged+=Changed;
        EditorApplication.update+=Complete;
        if(!EditorApplication.isPlaying && !File.Exists(Path.Combine(Output,"started"))) EditorApplication.delayCall+=Run;
    }
    public static void Run()
    {
        if(File.Exists(Path.Combine(Output,"started"))) return;
        File.WriteAllText(Path.Combine(Output,"started"),"Unity Play Mode");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        EditorApplication.isPlaying=true;
    }
    static void Changed(PlayModeStateChange state)
    {
        if(state==PlayModeStateChange.EnteredPlayMode)
            new GameObject("DesktopSmokeRunner").AddComponent<DesktopSmokeRunner>().Begin(Output);
    }
    static void Complete()
    {
        string file=Path.Combine(Output,"finished");
        if(!File.Exists(file)) return;
        int code=File.ReadAllText(file)=="PASS" ? 0 : 1;
        EditorApplication.update-=Complete;
        EditorApplication.Exit(code);
    }
}
