using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using MainGameAiShoujoMode.Life;

internal static class LifeActivityChecks
{
    private static void Require(bool value,string message) { if (!value) throw new Exception(message); }
    internal static void Run(string root)
    {
        Console.WriteLine("AI_LIFE_ACTIVITY_CHECK_START productionControllerAndAtomicSave=true");
        var content=Path.GetFullPath(Path.Combine(root,"../../MainGameAiShoujoMode/Package/Content"));
        var settings=AiLifeRuntimeSettings.LoadContent(content);
        settings.savePath=Path.Combine(root,"activity-checks",Guid.NewGuid().ToString("N"),"world.json");
        AiLifeRuntime Open()
        {
            var value=new AiLifeRuntime(settings,()=>DateTime.Now,null);value.LoadItems(content);value.LoadActivities(content);return value;
        }
        var runtime=Open();var life=new AiLifeController(runtime,Guid.NewGuid().ToString("N"),"activity-check");
        var food=runtime.Items.items.First(v=>!v.system&&v.food);var one=runtime.Activities.one;
        int Count() => runtime.Store.Read().inventories.Single(v=>v.Key==runtime.PlayerInventoryKey).items.Sum(v=>v.Count);
        var revision=runtime.Store.Read().revision;
        try { life.QueueActivity("eat",Guid.NewGuid().ToString("N"));throw new Exception("Food shortage accepted"); } catch(InvalidOperationException) { }
        Require(runtime.Store.Read().revision==revision,"Food shortage changed state");
        runtime.Store.Commit(runtime.Store.Read(),new AiLifeStateStore.Request { id=Guid.NewGuid().ToString("N"),action="acquire",destination=runtime.PlayerInventoryKey,category=food.category,item=food.id,count=one+one },out _,out _);
        var id=Guid.NewGuid().ToString("N");life.BeginConversation();life.QueueActivity("eat",id,runtime.PlayerInventoryKey,food.category,food.id);
        Require(Count()==one+one&&life.State.activity.stage=="queued","Queue consumed stock");
        revision=runtime.Store.Read().revision;life.QueueActivity("eat",id,runtime.PlayerInventoryKey,food.category,food.id);
        Require(runtime.Store.Read().revision==revision,"Double click duplicated queued action");
        Require(!life.CompleteActivity(id),"Conversation permitted completion");life.EndConversation();
        Require(!life.CompleteActivity(id),"Unperformed activity completed");
        life.ActivityDestination(id,"trial-point",settings.rules.temperature,settings.rules.minimum,settings.rules.mood);life.ActivityArrived(id);
        var reopened=new AiLifeController(Open(),life.ResidentId,"activity-check");
        Require(reopened.State.activity.id==id&&reopened.State.activity.stage=="performing"&&!reopened.Conversing,"Reload lost persisted activity or restored conversation");
        reopened.BeginH();Require(!reopened.CompleteActivity(id)&&Count()==one+one,"H callback consumed food");reopened.EndH();
        Require(reopened.CompleteActivity(id)&&Count()==one,"Completion failed to consume once");
        revision=runtime.Store.Read().revision;Require(!reopened.CompleteActivity(id)&&runtime.Store.Read().revision==revision,"Completion replay changed state");
        Require(reopened.State.activity==null&&reopened.State.action=="normal"&&reopened.State.fullness==settings.rules.fullness,"Completion lost return action or invented an effect");
        var cancel=Guid.NewGuid().ToString("N");reopened.QueueActivity("eat",cancel);reopened.CancelActivity("test cancel");
        Require(!reopened.CompleteActivity(cancel)&&Count()==one,"Cancelled action consumed food");
        var rest=Guid.NewGuid().ToString("N");reopened.QueueActivity("rest",rest);reopened.ActivityDestination(rest,"rest-point",default,default,default);reopened.ActivityArrived(rest);
        Require(!reopened.CompleteActivity(cancel)&&reopened.State.activity.id==rest,"Old callback affected newer activity");
        Require(reopened.CompleteActivity(rest)&&Count()==one,"Rest consumed food or failed to complete");
        var changed=Guid.NewGuid().ToString("N");reopened.QueueActivity("eat",changed);reopened.ActivityDestination(changed,"point",default,default,default);reopened.ActivityArrived(changed);
        runtime.Store.Commit(runtime.Store.Read(),new AiLifeStateStore.Request { id=Guid.NewGuid().ToString("N"),action="consume",sources=new[]{runtime.PlayerInventoryKey},category=food.category,item=food.id,count=one },out _,out _);
        revision=runtime.Store.Read().revision;
        try { reopened.CompleteActivity(changed);throw new Exception("Completion stock shortage accepted"); } catch(InvalidOperationException) { }
        Require(runtime.Store.Read().revision==revision&&reopened.State.activity.id==changed,"Completion shortage partially committed");
        reopened.CancelActivity("stock gone",changed);reopened.QueueActivity("rest",Guid.NewGuid().ToString("N"));reopened.Execute("follow");
        Require(reopened.State.activity==null&&reopened.State.action=="follow","Follow command failed to cancel pending activity");
        var bedTarget="bed:"+Guid.NewGuid().ToString("N")+"/native-sleep-slot";
        var bedRequest=Guid.NewGuid().ToString("N");reopened.QueueActivity("rest",bedRequest,furnitureTarget:bedTarget);
        Require(new AiLifeController(Open(),life.ResidentId,"activity-check").State.activity.furnitureTarget==bedTarget,"Reload lost exact bed target");
        revision=runtime.Store.Read().revision;reopened.QueueActivity("rest",bedRequest,furnitureTarget:bedTarget);
        Require(runtime.Store.Read().revision==revision,"Duplicate bed request mutated state");
        try{reopened.QueueActivity("rest",bedRequest,furnitureTarget:bedTarget+"other");throw new Exception("Bed operation was silently retargeted");}catch(InvalidOperationException){}
        var fatigue=reopened.State.fatigue;reopened.CancelActivity("bed blocked",bedRequest);
        Require(!reopened.CompleteActivity(bedRequest)&&reopened.State.fatigue==fatigue,"Failed bed granted effects");
        var newerBed=Guid.NewGuid().ToString("N");reopened.QueueActivity("rest",newerBed,furnitureTarget:bedTarget);
        reopened.ActivityDestination(newerBed,bedTarget,default,default,default);reopened.ActivityArrived(newerBed);
        Require(!reopened.CompleteActivity(bedRequest)&&reopened.State.activity.id==newerBed,"Late bed callback completed newer request");
        Require(reopened.CompleteActivity(newerBed),"Bed rest failed to complete");revision=runtime.Store.Read().revision;
        Require(!reopened.CompleteActivity(newerBed)&&runtime.Store.Read().revision==revision,"Bed rest effect applied twice");
        File.WriteAllText(Path.Combine(root,"activity-checks.json"),JsonSerializer.Serialize(new { passed=true,queueNoConsumption=true,duplicateQueue=true,arrivalRequired=true,reload=true,hCallbackRejected=true,consumeOnce=true,cancelNoConsumption=true,staleCallbackRejected=true,shortageRollback=true,effectsRemainUnspecified=true,gameVerified=false }));
        Console.WriteLine("AI_LIFE_ACTIVITY_CHECK_PASS queue shortage duplicate arrival reload H atomicOnce cancel staleCallback rollback noInventedEffects");
    }
}
