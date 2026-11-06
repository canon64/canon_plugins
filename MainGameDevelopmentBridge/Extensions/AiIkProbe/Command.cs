using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MainGameDevelopmentBridge;
using RootMotion.FinalIK;
using UnityEngine;

public sealed class AiIkProbe : IDevelopmentCommand
{
    public string Name => "ai-ik-state";
    public object Execute(Dictionary<string, object> args, DevelopmentContext context)
    {
        return Resources.FindObjectsOfTypeAll<FullBodyBipedIK>().Where(ik => ik.gameObject.scene.IsValid()).Select(ik => new {
            path = Path(ik.transform), enabled = ik.isActiveAndEnabled,
            chains = ik.solver.chain.Select(c => c.nodes.Length).ToArray(),
            effectors = ik.solver.effectors.Select(e => new {
                bone = e.bone == null ? null : Path(e.bone),
                fields = Fields(e),
            }).ToArray()
        }).ToArray();
    }
    static object Fields(object value) => value.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
        .Where(f => f.FieldType == typeof(int) || f.FieldType == typeof(int[]) || f.FieldType == typeof(bool))
        .ToDictionary(f => f.Name, f => f.GetValue(value));
    static string Path(Transform value) => value.parent == null ? value.name : Path(value.parent) + "/" + value.name;
    public void Dispose() { }
}
