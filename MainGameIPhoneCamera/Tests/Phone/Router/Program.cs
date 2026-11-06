using System;
using UnityEngine;

namespace MainGameIPhoneCamera
{
    public sealed partial class Plugin
    {
        private static int _checks;
        private static void Check(bool value, string label) { if (!value) throw new Exception(label); _checks++; }
        private sealed class Extra : IPhoneApp
        {
            public string Id => "test-extra";
            public string Title => "Extra";
            public Sprite Icon => null;
            public int Opens, Closes, Confirms, Triggers;
            public void Open(RectTransform root) { Opens++; }
            public void Close() { Closes++; }
            public void Navigate(int x, int y) { }
            public void Confirm() { Confirms++; }
            public void Trigger() { Triggers++; }
        }
        public static void Main()
        {
            Console.WriteLine("[RouterTests] Executing production phone screen routing with UI/Unity boundary doubles.");
            var p = new Plugin(); p.CreatePhoneShell(new Transform());
            Check(p._phone.Page == PhonePage.Locked && !p._phoneCamera.enabled && !p._cameraUi.Visible, "phone starts locked with camera disabled");
            p.IsHeld = true; p.PhoneGrabbed();
            p.TriggerPhone();
            Check(p._phone.Page == PhonePage.Camera && p.Shots == 0 && p._phoneCamera.enabled && p._cameraUi.Visible && !p._shellUi.Visible, "lock trigger opens camera without taking a photo");
            p.TriggerPhone(); Check(p.Shots == 1, "next trigger operates camera");
            p.BackPhone(); Check(p._phone.Page == PhonePage.Home && !p._phoneCamera.enabled && p._shellUi.Visible, "B returns camera to menu and disables live camera");
            Check(p._homeApps.Count == 3 && p._homeApps[0].Id == "photos" && p._homeApps[1].Id == "camera" && p._homeApps[2].Id == "settings", "default three apps");
            p._homeSelection = 0; p.ConfirmPhone();
            Check(p._phone.Page == PhonePage.Gallery && p.RefreshedGallery == 1, "A opens photo app and refreshes library");
            p.ConfirmPhone(); Check(p._phone.Page == PhonePage.Viewer && p.OpenedMedia == 0, "A opens selected photo/video");
            p.NavigatePhone(1, 0); p.ConfirmPhone(); Check(p._controlSelection == 1 && p.ViewerActions == 1, "viewer D-pad selection and A command dispatch");
            p._controlSelection = 2;
            for (int i = 0; i < 3; i++)
            {
                p.OpenGalleryItem(1);
                Check(p._controlSelection == 2, "next media keeps next button selected across repeated loads");
            }
            p._controlSelection = 1; p.OpenGalleryItem(0);
            Check(p._controlSelection == 1, "previous media keeps previous button selected");
            p.OpenGalleryItem(0); Check(p._controlSelection == 1, "first media boundary keeps previous button selected");
            p.ShowPhonePage(PhonePage.Gallery);p.OpenGalleryItem(0);
            Check(p._controlSelection == 0, "opening a viewer from the library starts on its first control");
            p._galleryPlaying = true; p.UpdatePhoneMediaActivity();
            p._viewerUiVisible = false; p._shellUi.ViewerUi(false);
            p.BackPhone(); Check(!p._galleryPlaying && !p._viewerUiVisible && !p._shellUi.Moving, "B exits media playback and clears viewer input even with hidden overlay");
            Check(p._phone.Page == PhonePage.Gallery, "B from viewer returns to library");
            p.BackPhone(); Check(p._phone.Page == PhonePage.Home, "B from library returns to menu");
            p.ShowPhonePage(PhonePage.Locked); p.ConfirmPhone(); Check(p._phone.Page == PhonePage.Home, "locked A opens menu");
            p._homeSelection = 1; p.ConfirmPhone(); p._app.Select(CaptureMode.Video); p._app.BeginRecording(); p._movie = new object(); p.UpdatePhoneMediaActivity();
            var recording = p._movie;
            p.IsHeld = false; p.PhoneReleased();
            Check(p._movie == recording && p.Stops == 0 && p._app.Phase == CapturePhase.Recording, "release keeps the same recording running");
            Check(!p._phone.Deadline.HasValue && !p._phone.Expire(DateTime.UtcNow.AddMinutes(5)) && p._phone.Page == PhonePage.Camera, "unheld recording keeps camera display awake beyond idle timeout");
            p.IsHeld = true; p.PhoneGrabbed();
            Check(p._movie == recording && p._phone.Page == PhonePage.Camera, "pickup during recording preserves camera and recording");
            p.BackPhone(); Check(p.Stops == 1 && p._movie == null && p._app.Phase == CapturePhase.Saving && p._phone.Page == PhonePage.Home, "B stops recording and returns home while saving");
            p._homeSelection = 2; p.ConfirmPhone(); Check(p._phone.Page == PhonePage.Settings && p.OpenedSettings == 1, "settings icon opens native configuration bridge");
            p.BackPhone(); Check(p._phone.Page == PhonePage.Home && p.ClosedSettings == 1, "B closes owned settings bridge");
            p.ShowPhonePage(PhonePage.Settings); p.IsHeld = false; p._phone.Release(DateTime.UtcNow.AddSeconds(-61)); p._phone.Expire(DateTime.UtcNow);
            p.ShowPhonePage(p._phone.Page, false);
            Check(p._phone.Page == PhonePage.Locked && p.ClosedSettings == 2, "automatic lock also closes old settings view");
            var extension = new Extra(); using var registration = IPhoneAppApi.Register(extension); p._mainThread.Drain();
            p.ShowPhonePage(PhonePage.Home); p._homeSelection = 3; p.ConfirmPhone(); p.ConfirmPhone(); p.TriggerPhone();
            Check(p._phone.Page == PhonePage.Extension && extension.Opens == 1 && extension.Confirms == 1 && extension.Triggers == 1, "registered extension receives permitted app input");
            p.BackPhone(); Check(extension.Closes == 1 && p._phone.Page == PhonePage.Home, "B closes extension once and returns menu");
            int results = 0; var stale = p.CapturePhonePoster(); p.ShowPhonePage(PhonePage.Gallery); stale(() => results++); p._mainThread.Drain();
            Check(results == 0, "old page result rejected after navigation");
            var current = p.CapturePhonePoster(); current(() => results++); p._mainThread.Drain(); Check(results == 1, "current page result applied");
            var revoked = p.CapturePhonePoster(); p._permit.IsValid = false; revoked(() => results++); p._mainThread.Drain(); Check(results == 1, "revoked permit rejects result");
            p._permit = new Permit(); var oldPhone = p.CapturePhonePoster(false); ++p._captureEpoch; oldPhone(() => results++); p._mainThread.Drain(); Check(results == 1, "old phone generation rejected");
            p.ShowPhonePage(PhonePage.Home); var canceled = p.CapturePhonePoster(false); p._captureLifetime.Cancel(); canceled(() => results++); p._mainThread.Drain(); Check(results == 1, "canceled phone lifetime rejects result");
            p.CanAct = false; p.ConfirmPhone(); p.BackPhone(); p.TriggerPhone(); Check(p._phone.Page == PhonePage.Home && p.Shots == 1, "inactive phone entrypoints rejected");
            p.DestroyPhoneShell();
            CheckViewer();
            CheckOrientationClock();
            Console.WriteLine("PASS " + _checks + " production router checks (native rendering/audio not executed).");
        }
        private static void CheckOrientationClock()
        {
            var context = new QueueContext(); int ticks = 0;
            using var clock = new PhoneOrientationClock(context, () => { ticks++; return true; });
            clock.SetActive(true);
            System.Threading.Thread.Sleep(450); context.Drain();
            Check(ticks == 0, "orientation is not polled every frame or before one second");
            clock.SetActive(true);
            System.Threading.Thread.Sleep(700); context.Drain();
            Check(ticks == 1, "active page changes do not postpone the one-second check");
            System.Threading.Thread.Sleep(1150);
            Check(ticks == 1, "orientation Unity work waits for main-thread dispatch");
            context.Drain(); Check(ticks == 2, "orientation checks repeat once per deadline");
            System.Threading.Thread.Sleep(1150); clock.SetActive(false); context.Drain();
            Check(ticks == 2, "screen off rejects even an already queued orientation check");
            clock.SetActive(true); System.Threading.Thread.Sleep(1150); clock.Dispose(); context.Drain();
            Check(ticks == 2, "phone destruction cancels queued orientation work");
            using var invalid = new PhoneOrientationClock(context, () => { ticks++; return false; });
            invalid.SetActive(true); System.Threading.Thread.Sleep(1150); context.Drain();
            System.Threading.Thread.Sleep(1150); context.Drain();
            Check(ticks == 3, "revoked session terminates orientation checks without rescheduling");
        }
    }
}
