using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace MainGameRoundedLimbClothes
{
    [DataContract]
    public sealed class PositionSettings
    {
        [DataMember] public float LeftArm=50, RightArm=50, LeftLeg=50, RightLeg=50;
        [DataMember] public bool LinkArms=true, LinkLegs=true;
        public PositionSettings Copy() => (PositionSettings)MemberwiseClone();
        public bool Valid => ValidPosition(LeftArm)&&ValidPosition(RightArm)&&ValidPosition(LeftLeg)&&ValidPosition(RightLeg);
        static bool ValidPosition(float x) => !float.IsNaN(x)&&!float.IsInfinity(x)&&x>=0&&x<=100;
        [OnDeserializing] void Defaults(StreamingContext context) {LeftArm=RightArm=LeftLeg=RightLeg=50;LinkArms=LinkLegs=true;}
        public float Get(int index) => index==0?LeftArm:index==1?RightArm:index==2?LeftLeg:RightLeg;
        public void Set(int index,float value)
        {
            if(!ValidPosition(value))throw new ArgumentOutOfRangeException(nameof(value));
            if(index==0||index==1){if(LinkArms)LeftArm=RightArm=value;else if(index==0)LeftArm=value;else RightArm=value;}
            else{if(LinkLegs)LeftLeg=RightLeg=value;else if(index==2)LeftLeg=value;else RightLeg=value;}
        }
    }
    [DataContract]
    public sealed class SavedSettings
    {
        [DataMember] public int Version=1;
        [DataMember] public Dictionary<int,PositionSettings> Coordinates=new Dictionary<int,PositionSettings>();
        public static string Encode(SavedSettings state)
        {
            using(var stream=new MemoryStream()){new DataContractJsonSerializer(typeof(SavedSettings)).WriteObject(stream,state);return Encoding.UTF8.GetString(stream.ToArray());}
        }
        public static SavedSettings Decode(string value)
        {
            if(value==null||value.Length>65536)throw new SerializationException("保存データのサイズが不正です。");
            using(var stream=new MemoryStream(Encoding.UTF8.GetBytes(value)))
            {
                var result=(SavedSettings)new DataContractJsonSerializer(typeof(SavedSettings)).ReadObject(stream);
                if(result==null||result.Version!=1||result.Coordinates==null||result.Coordinates.Count>64||
                    !System.Linq.Enumerable.All(result.Coordinates,x=>x.Key>=0&&x.Key<64&&x.Value!=null&&x.Value.Valid))throw new SerializationException("未対応または不正な保存データです。");
                return result;
            }
        }
    }
}
