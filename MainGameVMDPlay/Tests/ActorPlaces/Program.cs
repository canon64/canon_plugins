using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;
using MainGameVMDPlay;

class Program
{
    static int checks;
    static void Check(bool value,string message) { if(!value)throw new Exception(message);checks++; }
    static void Main()
    {
        Console.WriteLine("Testing initial actor location registration...");
        var places=new ActorPlaces();var pos=new float[]{1,2,3};var rot=new float[]{10,20,30};
        Check(ActorPlaceDefaults.EnsureInitial(places,pos,rot),"first ready event creates initial location");
        Check(places.Items.Count==1&&places.Items[0].IsInitial&&places.Selected==0,"initial preset selected on first summon");
        pos[0]=99;rot[0]=99;Check(places.Items[0].Position[0]==1&&places.Items[0].Rotation[0]==10,"spawn data copied independently of later motion");
        Check(!ActorPlaceDefaults.EnsureInitial(places,pos,rot)&&places.Items.Count==1&&places.Items[0].Position[0]==1,"later summons cannot overwrite initial position");
        using(var stream=new MemoryStream()){
            var serializer=new DataContractJsonSerializer(typeof(ActorPlaces));serializer.WriteObject(stream,places);stream.Position=0;
            var loaded=(ActorPlaces)serializer.ReadObject(stream);
            Check(loaded.Items[0].IsInitial&&loaded.Items[0].Position[0]==1&&loaded.Items[0].Rotation[2]==30,"position and rotation survive reload");
            Check(!ActorPlaceDefaults.EnsureInitial(loaded,pos,rot),"initial preset not duplicated after restart");
        }
        using(var stream=new MemoryStream(Encoding.UTF8.GetBytes("{\"Items\":[{\"Name\":\"場所2\",\"Position\":[4,5,6],\"Rotation\":[7,8,9]}],\"Selected\":0}"))){
            var loaded=(ActorPlaces)new DataContractJsonSerializer(typeof(ActorPlaces)).ReadObject(stream);
            Check(ActorPlaceDefaults.EnsureInitial(loaded,pos,rot),"old files receive an initial preset");
            Check(loaded.Items.Count==2&&loaded.Items[1].Name=="場所2"&&loaded.Items[1].Position[0]==4,"existing saved locations preserved");
            Check(loaded.Selected==1,"existing selection preserved when default inserted first");
        }
        var empty=new ActorPlaces();bool rejected=false;try{ActorPlaceDefaults.EnsureInitial(empty,new[]{float.NaN,0,0},new float[3]);}catch(ArgumentException){rejected=true;}
        Check(rejected&&empty.Items.Count==0,"invalid spawn coordinates rejected without corrupting state");
        Console.WriteLine("PASS "+checks+" actor location checks");
    }
}
