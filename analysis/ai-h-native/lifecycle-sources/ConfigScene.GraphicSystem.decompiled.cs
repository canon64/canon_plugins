using System;
using System.Xml;
using UnityEngine;

namespace ConfigScene;

public class GraphicSystem : BaseSystem
{
	public const int MAX_CHARA_NUM = 4;

	public const byte CHARA_GRAPHIC_QUALITY = 0;

	public const byte MAP_GRAPHIC_QUALITY = 0;

	public bool SelfShadow = true;

	public bool Bloom = true;

	public bool SSAO = true;

	public bool SSR = true;

	public bool DepthOfField = true;

	public bool Atmospheric = true;

	public bool Vignette = true;

	public bool Rain = true;

	public byte CharaGraphicQuality;

	public byte MapGraphicQuality;

	public bool FaceLight;

	public bool AmbientLight = true;

	public bool Shield = true;

	public bool SimpleBody;

	public Color SilhouetteColor = Color.blue;

	public Color BackColor = Color.black;

	public int MaxCharaNum = 4;

	public bool[] CharasEntry = new bool[4];

	public GraphicSystem(string elementName)
		: base(elementName)
	{
	}

	public override void Init()
	{
		SelfShadow = true;
		Bloom = true;
		SSAO = true;
		SSR = true;
		DepthOfField = true;
		Atmospheric = true;
		Vignette = true;
		Rain = true;
		CharaGraphicQuality = 0;
		MapGraphicQuality = 0;
		FaceLight = false;
		AmbientLight = true;
		Shield = true;
		SimpleBody = false;
		SilhouetteColor = Color.blue;
		BackColor = Color.black;
		MaxCharaNum = 4;
		for (int i = 0; i < 4; i++)
		{
			CharasEntry[i] = true;
		}
	}

	public override void Read(string rootName, XmlDocument xml)
	{
		try
		{
			XmlNodeList xmlNodeList = null;
			string text = rootName + "/" + elementName + "/";
			xmlNodeList = xml.SelectNodes(text + "SelfShadow");
			if (xmlNodeList != null && xmlNodeList.Item(0) is XmlElement xmlElement)
			{
				SelfShadow = (bool)BaseSystem.Cast(xmlElement.InnerText, SelfShadow.GetType());
			}
			xmlNodeList = xml.SelectNodes(text + "Bloom");
			if (xmlNodeList != null && xmlNodeList.Item(0) is XmlElement xmlElement2)
			{
				Bloom = (bool)BaseSystem.Cast(xmlElement2.InnerText, Bloom.GetType());
			}
			xmlNodeList = xml.SelectNodes(text + "SSAO");
			if (xmlNodeList != null && xmlNodeList.Item(0) is XmlElement xmlElement3)
			{
				SSAO = (bool)BaseSystem.Cast(xmlElement3.InnerText, SSAO.GetType());
			}
			xmlNodeList = xml.SelectNodes(text + "SSR");
			if (xmlNodeList != null && xmlNodeList.Item(0) is XmlElement xmlElement4)
			{
				SSR = (bool)BaseSystem.Cast(xmlElement4.InnerText, SSR.GetType());
			}
			xmlNodeList = xml.SelectNodes(text + "DepthOfField");
			if (xmlNodeList != null && xmlNodeList.Item(0) is XmlElement xmlElement5)
			{
				DepthOfField = (bool)BaseSystem.Cast(xmlElement5.InnerText, DepthOfField.GetType());
			}
			xmlNodeList = xml.SelectNodes(text + "Atmospheric");
			if (xmlNodeList != null && xmlNodeList.Item(0) is XmlElement xmlElement6)
			{
				Atmospheric = (bool)BaseSystem.Cast(xmlElement6.InnerText, Atmospheric.GetType());
			}
			xmlNodeList = xml.SelectNodes(text + "Vignette");
			if (xmlNodeList != null && xmlNodeList.Item(0) is XmlElement xmlElement7)
			{
				Vignette = (bool)BaseSystem.Cast(xmlElement7.InnerText, Vignette.GetType());
			}
			xmlNodeList = xml.SelectNodes(text + "Rain");
			if (xmlNodeList != null && xmlNodeList.Item(0) is XmlElement xmlElement8)
			{
				Rain = (bool)BaseSystem.Cast(xmlElement8.InnerText, Rain.GetType());
			}
			xmlNodeList = xml.SelectNodes(text + "CharaGraphicQuality");
			if (xmlNodeList != null && xmlNodeList.Item(0) is XmlElement xmlElement9)
			{
				CharaGraphicQuality = (byte)BaseSystem.Cast(xmlElement9.InnerText, CharaGraphicQuality.GetType());
			}
			xmlNodeList = xml.SelectNodes(text + "MapGraphicQuality");
			if (xmlNodeList != null && xmlNodeList.Item(0) is XmlElement xmlElement10)
			{
				MapGraphicQuality = (byte)BaseSystem.Cast(xmlElement10.InnerText, MapGraphicQuality.GetType());
			}
			xmlNodeList = xml.SelectNodes(text + "FaceLight");
			if (xmlNodeList != null && xmlNodeList.Item(0) is XmlElement xmlElement11)
			{
				FaceLight = (bool)BaseSystem.Cast(xmlElement11.InnerText, FaceLight.GetType());
			}
			xmlNodeList = xml.SelectNodes(text + "AmbientLight");
			if (xmlNodeList != null && xmlNodeList.Item(0) is XmlElement xmlElement12)
			{
				AmbientLight = (bool)BaseSystem.Cast(xmlElement12.InnerText, AmbientLight.GetType());
			}
			xmlNodeList = xml.SelectNodes(text + "Shield");
			if (xmlNodeList != null && xmlNodeList.Item(0) is XmlElement xmlElement13)
			{
				Shield = (bool)BaseSystem.Cast(xmlElement13.InnerText, Shield.GetType());
			}
			xmlNodeList = xml.SelectNodes(text + "SimpleBody");
			if (xmlNodeList != null && xmlNodeList.Item(0) is XmlElement xmlElement14)
			{
				SimpleBody = (bool)BaseSystem.Cast(xmlElement14.InnerText, SimpleBody.GetType());
			}
			xmlNodeList = xml.SelectNodes(text + "SilhouetteColor");
			if (xmlNodeList != null && xmlNodeList.Item(0) is XmlElement xmlElement15)
			{
				SilhouetteColor = (Color)BaseSystem.Cast(xmlElement15.InnerText, SilhouetteColor.GetType());
			}
			xmlNodeList = xml.SelectNodes(text + "BackColor");
			if (xmlNodeList != null && xmlNodeList.Item(0) is XmlElement xmlElement16)
			{
				BackColor = (Color)BaseSystem.Cast(xmlElement16.InnerText, BackColor.GetType());
			}
			xmlNodeList = xml.SelectNodes(text + "MaxCharaNum");
			if (xmlNodeList != null && xmlNodeList.Item(0) is XmlElement xmlElement17)
			{
				MaxCharaNum = (int)BaseSystem.Cast(xmlElement17.InnerText, MaxCharaNum.GetType());
			}
			for (int i = 0; i < 4; i++)
			{
				xmlNodeList = xml.SelectNodes(text + "CharasEntry" + i);
				if (xmlNodeList != null && xmlNodeList.Item(0) is XmlElement xmlElement18)
				{
					CharasEntry[i] = (bool)BaseSystem.Cast(xmlElement18.InnerText, CharasEntry[i].GetType());
				}
			}
		}
		catch (Exception ex)
		{
			Debug.LogError(ex.Message);
		}
	}

	public override void Write(XmlWriter writer)
	{
		writer.WriteStartElement(elementName);
		writer.WriteStartElement("SelfShadow");
		writer.WriteValue(BaseSystem.ConvertString(SelfShadow));
		writer.WriteEndElement();
		writer.WriteStartElement("Bloom");
		writer.WriteValue(BaseSystem.ConvertString(Bloom));
		writer.WriteEndElement();
		writer.WriteStartElement("SSAO");
		writer.WriteValue(BaseSystem.ConvertString(SSAO));
		writer.WriteEndElement();
		writer.WriteStartElement("SSR");
		writer.WriteValue(BaseSystem.ConvertString(SSR));
		writer.WriteEndElement();
		writer.WriteStartElement("DepthOfField");
		writer.WriteValue(BaseSystem.ConvertString(DepthOfField));
		writer.WriteEndElement();
		writer.WriteStartElement("Atmospheric");
		writer.WriteValue(BaseSystem.ConvertString(Atmospheric));
		writer.WriteEndElement();
		writer.WriteStartElement("Vignette");
		writer.WriteValue(BaseSystem.ConvertString(Vignette));
		writer.WriteEndElement();
		writer.WriteStartElement("Rain");
		writer.WriteValue(BaseSystem.ConvertString(Rain));
		writer.WriteEndElement();
		writer.WriteStartElement("CharaGraphicQuality");
		writer.WriteValue(BaseSystem.ConvertString(CharaGraphicQuality));
		writer.WriteEndElement();
		writer.WriteStartElement("MapGraphicQuality");
		writer.WriteValue(BaseSystem.ConvertString(MapGraphicQuality));
		writer.WriteEndElement();
		writer.WriteStartElement("FaceLight");
		writer.WriteValue(BaseSystem.ConvertString(FaceLight));
		writer.WriteEndElement();
		writer.WriteStartElement("AmbientLight");
		writer.WriteValue(BaseSystem.ConvertString(AmbientLight));
		writer.WriteEndElement();
		writer.WriteStartElement("Shield");
		writer.WriteValue(BaseSystem.ConvertString(Shield));
		writer.WriteEndElement();
		writer.WriteStartElement("SimpleBody");
		writer.WriteValue(BaseSystem.ConvertString(SimpleBody));
		writer.WriteEndElement();
		writer.WriteStartElement("SilhouetteColor");
		writer.WriteValue(BaseSystem.ConvertString(SilhouetteColor));
		writer.WriteEndElement();
		writer.WriteStartElement("BackColor");
		writer.WriteValue(BaseSystem.ConvertString(BackColor));
		writer.WriteEndElement();
		writer.WriteStartElement("MaxCharaNum");
		writer.WriteValue(BaseSystem.ConvertString(MaxCharaNum));
		writer.WriteEndElement();
		for (int i = 0; i < 4; i++)
		{
			string localName = "CharasEntry" + i;
			writer.WriteStartElement(localName);
			writer.WriteValue(BaseSystem.ConvertString(CharasEntry[i]));
			writer.WriteEndElement();
		}
		writer.WriteEndElement();
	}
}
