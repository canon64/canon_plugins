using System;
using System.Collections.Generic;
using UnityEngine;

namespace MainGameAiShoujoH.Contracts
{
    public enum SessionPhase { Idle, Loading, Preparing, Ready, Ending, Restoring, Failed }
    public enum EndReason { Completed, Cancelled, PreparationFailed, PermissionRevoked, ModeDestroyed }
    public sealed class KksPoseIdentity
    {
        public string TableBundle { get; set; }
        public string TableAsset { get; set; }
        public int Mode { get; set; }
        public int Id { get; set; }
    }
    public enum NativeHEvent { Hpoint = -1, Normal = 0, Yobai = 1, Bath = 2, Toilet1 = 3, Toilet2 = 4, ShagmiBare = 5, Back = 6, Kitchen = 7, Tachi = 8, Stairs = 9, StairsBare = 10, GyakuYobai = 11, FromFemale = 12, MapBath = 13, KabeanaBack = 14, KabeanaFront = 15, Neonani = 16, TsukueBare = 17 }
    public static class ResultCommitBoundary
    {
        public static bool CanCommit(bool activityActive, bool permitValid, SessionPhase phase) => activityActive && permitValid && phase == SessionPhase.Restoring;
    }
    public sealed class Participant
    {
        public string Identity { get; set; }
        public string Role { get; set; }
        public ChaControl Character { get; set; }
        public bool IsPlayer { get; set; }
    }
    public sealed class StartRequest
    {
        public MonoBehaviour Owner { get; set; }
        public GameObject Island { get; set; }
        public Camera Camera { get; set; }
        public HSceneControls SceneControls { get; set; }
        public Func<Vector3, HPointChoice[]> NearbyHPoints { get; set; }
        public Participant[] Participants { get; set; }
        public string MotionId { get; set; }
        public KksPoseIdentity KksPose { get; set; }
        public bool UseKksOnePoseTrial { get; set; }
        public int EventId { get; set; }
        public int Height { get; set; }
        public int NativePlace { get; set; }
        public Vector3 Position { get; set; }
        public Vector3 OriginalPlayerPosition { get; set; }
        public float SourceWorldScale { get; set; }
        public Quaternion Rotation { get; set; }
        public Action Started { get; set; }
        public Action Ended { get; set; }
        public Action Ready { get; set; }
        public Action<Action> Cover { get; set; }
        public Action<Action> Reveal { get; set; }
        public Func<Action> AcquireControl { get; set; }
        public Results.ResultServices Results { get; set; }
        public Presentation.NativeVoiceContext[] VoiceContexts { get; set; }
        public Integration.NativeHSettings Settings { get; set; }
        public Func<string, int, float> ReadNativeParameter { get; set; }
        public bool SecondFemaleIsPlayer { get; set; }
        public Func<IDisposable> SuspendEndingMusic { get; set; }
        public Func<int> ReadMerchantSpendTotal { get; set; }
        public Func<int, int, bool> HasNativeHItem { get; set; }
        public Action ResolveParticipants { get; set; }
        public Action ReleaseParticipants { get; set; }
        public Action<EndReason, Action, Action<Exception>> RestoreExternalControl { get; set; }
        public Action AbortExternalControl { get; set; }
    }
    public sealed class HPointChoice
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public Vector3 Position { get; set; }
        public Quaternion Rotation { get; set; }
        public int[] Categories { get; set; }
    }
    public sealed class SessionContext
    {
        public Guid Id { get; internal set; }
        public long Generation { get; internal set; }
        public StartRequest Request { get; internal set; }
        public Func<bool> IsCurrent { get; internal set; }
        public Func<bool> CanInteract { get; internal set; }
        public Func<bool> IsMotionPreparation { get; internal set; }
        public Action<string, bool> RequestMotionChange { get; internal set; }
        public Action<string> ReportFailure { get; internal set; }
        public Action<Exception> TraceException { get; internal set; }
    }
    public sealed class Command
    {
        public string Name { get; set; }
        public string Value { get; set; }
        public float Amount { get; set; }
    }
    public interface ISessionModule : IDisposable
    {
        string Name { get; }
        void Prepare(SessionContext context);
        void Execute(Command command);
        void End(EndReason reason);
    }
    public interface IAsyncSessionPreparation
    {
        void PrepareAsync(SessionContext context, Action completed, Action<Exception> failed);
    }
    public interface IAsyncSessionCleanup
    {
        void DisposeAsync(Action completed, Action<Exception> failed);
    }
    public interface IMotionSelectionTransition
    {
        void ValidateMotionSelection(string id, bool automatic);
        void SuspendSelection();
        void ResumeSelection();
    }
    public interface IMotionModule : ISessionModule
    {
        string SelectedMotionId { get; }
        string SelectedState { get; }
        void Select(string motionId);
        void SetSpeed(float speed);
    }
    public interface ISessionFinalizer
    {
        void BeginFinalization(EndReason reason, Action completed, Action<Exception> failed);
    }
    public interface IEndingMotionModule
    {
        void ValidateEndingMotion(int femaleSlot, int posture, int pose);
        void PlayEndingMotion(int femaleSlot, int posture, int pose);
    }
    public interface IKksEndingMotionModule
    {
        void PlayNativeEndingMotion(int femaleSlot, string exactNativeState);
    }
    public interface ISessionEndingStatus
    {
        string EndingScenario { get; }
        bool EndingReceiveSaved { get; }
        bool EndingCompleted { get; }
    }
    public sealed class SessionSnapshot
    {
        public Guid Id { get; internal set; }
        public SessionPhase Phase { get; internal set; }
        public string MotionId { get; internal set; }
        public long Generation { get; internal set; }
        public string MotionState { get; internal set; }
        public string BodyBackend { get; internal set; }
        public string KksTableBundle { get; internal set; }
        public string KksTableAsset { get; internal set; }
        public int? KksMode { get; internal set; }
        public int? KksPoseId { get; internal set; }
        public string[] Participants { get; internal set; }
        public bool Ready { get; internal set; }
        public EndReason? LastEndReason { get; internal set; }
        public bool Restored { get; internal set; }
        public int ResultPendingCount { get; internal set; }
        public int ResultAppliedCount { get; internal set; }
        public string[] Failures { get; internal set; }
        public string EndingScenario { get; internal set; }
        public bool EndingReceiveSaved { get; internal set; }
        public bool EndingCompleted { get; internal set; }
    }
}
