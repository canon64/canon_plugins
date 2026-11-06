using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace MainGameAiShoujoMode
{
    internal sealed partial class HotelFloorRuntime
    {
        private void PrepareThirdFloorPortal(string content)
        {
            if (Definition.id != "second" || !(Definition.portals ?? Array.Empty<HotelPortalDefinition>()).Any(p => p.destination == "third")) return;
            var cfg = HotelStageSettings.Read(Path.Combine(content, "hotel-stage.json")).portal;
            if (cfg == null) throw new InvalidDataException("3F移動ボタンの設定がありません。");
            var material = new Material(Shader.Find("Standard")) { name = "3F elevator plaque brass", color = new Color(cfg.metallic, cfg.smoothness, cfg.labelSize) };
            material.SetFloat("_Metallic", cfg.metallic); material.SetFloat("_Glossiness", cfg.smoothness); _roomMaterials.Add(material);
            var button = GameObject.CreatePrimitive(PrimitiveType.Cube); button.name = "2F_ThirdFloorButton";
            button.layer = LayerMask.NameToLayer("Map"); button.transform.SetParent(Roots.First().transform, false);
            button.transform.position = HotelStageSettings.Vector(cfg.position); button.transform.localScale = HotelStageSettings.Vector(cfg.size);
            button.GetComponent<Renderer>().sharedMaterial = material;
            var plaque = GameObject.CreatePrimitive(PrimitiveType.Cube); plaque.name = "2F_ThirdFloorPlaque";
            plaque.layer = button.layer; plaque.transform.SetParent(Roots.First().transform, false);
            plaque.transform.position = button.transform.position + HotelStageSettings.Vector(cfg.plaqueOffset);
            plaque.transform.localScale = HotelStageSettings.Vector(cfg.plaqueSize); plaque.GetComponent<Renderer>().sharedMaterial = material;
            var label = new GameObject("2F_ThirdFloorLabel"); label.transform.SetParent(Roots.First().transform, false);
            label.transform.position = button.transform.position + HotelStageSettings.Vector(cfg.labelOffset);
            label.transform.rotation = Quaternion.Euler(default(float), cfg.labelYaw, default(float));
            var text = label.AddComponent<TextMesh>(); text.text = "3F"; text.anchor = TextAnchor.MiddleCenter;
            text.characterSize = cfg.labelSize; text.color = Color.white; text.fontSize = cfg.labelFontSize;
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            label.GetComponent<MeshRenderer>().sharedMaterial = text.font.material;
        }
    }
}
