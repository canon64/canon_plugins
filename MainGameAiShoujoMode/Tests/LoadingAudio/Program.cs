using System;
using MainGameAiShoujoMode;

internal static class Program
{
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Main()
    {
        bool paused = false, restored = false;
        var loading = new HotelLoadingAudioScope(() => paused, value => { paused = value; if (!value) restored = true; });
        Require(paused && !restored, "Audio was not held throughout loading");
        loading.Dispose(); Require(!paused && restored, "Ready failed to restore playback");
        paused = true; loading.Dispose(); Require(paused, "Repeated cleanup overwrote a later owner");
        using (new HotelLoadingAudioScope(() => paused, value => paused = value)) { Require(paused, "Existing pause changed during entry"); }
        Require(paused, "A pre-existing pause was incorrectly cleared");
        paused = false;
        try { using (new HotelLoadingAudioScope(() => paused, value => paused = value)) { throw new InvalidOperationException(); } }
        catch (InvalidOperationException) { }
        Require(!paused, "Startup failure left playback paused");
        Console.WriteLine("HOTEL_LOADING_AUDIO_PASS heldUntilReady=true restored=true existingPausePreserved=true duplicateCleanup=true failureCleanup=true");
    }
}
