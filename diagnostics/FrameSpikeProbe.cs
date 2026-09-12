using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.Scripting;

namespace GKFrameSpikeProbe
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class FrameSpikeProbePlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "nikich.gyk.diagnostics.framespikeprobe";
        public const string PluginName = "GK Frame Spike Probe (Diagnostic)";
        public const string PluginVersion = "0.4.0";

        private const KeyCode GcHoldKey = KeyCode.F6;
        private const float GcHoldSeconds = 15f;

        [StructLayout(LayoutKind.Sequential)]
        private struct FileTime
        {
            public uint Low;
            public uint High;
        }

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentThread();

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetThreadTimes(
            IntPtr threadHandle,
            out FileTime creationTime,
            out FileTime exitTime,
            out FileTime kernelTime,
            out FileTime userTime);

        private ConfigEntry<float> _thresholdMs;
        private ConfigEntry<float> _armDelaySeconds;
        private long _lastTicks;
        private bool _haveSample;
        private bool _armedLogged;
        private float _armAt;
        private int _lastGc0;
        private int _lastGc1;
        private int _lastGc2;
        private bool _threadCpuAvailable;
        private long _lastThreadCpu100Ns;
        private long _spikeCount;
        private float _maxSpikeMs;
        private bool _unityGcIncremental;
        private ulong _unityGcSliceNs;
        private GarbageCollector.Mode _unityGcMode;

        private bool _gcHoldActive;
        private float _gcHoldUntil;
        private GarbageCollector.Mode _gcHoldRestoreMode;
        private long _gcHoldStartManagedBytes;
        private int _gcHoldStartCycle;

        private void Awake()
        {
            _thresholdMs = Config.Bind(
                "Diagnostics",
                "Spike Threshold (ms)",
                50f,
                new ConfigDescription(
                    "Log only frame-to-frame wall-clock intervals at or above this threshold.",
                    new AcceptableValueRange<float>(20f, 1000f)));

            _armDelaySeconds = Config.Bind(
                "Diagnostics",
                "Arm Delay After Plugin Load (seconds)",
                30f,
                new ConfigDescription(
                    "Ignore startup/loading activity for this many realtime seconds after the plugin loads.",
                    new AcceptableValueRange<float>(0f, 300f)));

            _armAt = Time.realtimeSinceStartup + _armDelaySeconds.Value;
            _threadCpuAvailable = TryReadCurrentThreadCpu100Ns(out _lastThreadCpu100Ns);
            _unityGcIncremental = GarbageCollector.isIncremental;
            _unityGcSliceNs = GarbageCollector.incrementalTimeSliceNanoseconds;
            _unityGcMode = GarbageCollector.GCMode;
            ResetSample();

            Logger.LogInfo(
                PluginName + " " + PluginVersion + " loaded. " +
                "Normal-frame work is limited to Stopwatch timestamp + GC.CollectionCount(0..2) + Windows GetThreadTimes for the Unity main thread + one F6 key-state check. " +
                "Unity incremental-GC state is queried only at startup and on logged spikes; CollectIncremental(0) performs no collection work. " +
                "F6 starts a bounded 15-second diagnostic GC hold by setting GarbageCollector.GCMode=Disabled; it auto-restores the previous GC mode and never calls GC.Collect on restore. " +
                "No object scans, stack traces, hierarchy enumeration, synchronous file I/O, or per-frame log writes are performed. " +
                "threshold_ms=" + _thresholdMs.Value.ToString("0.0") +
                ", arm_delay_s=" + _armDelaySeconds.Value.ToString("0.0") +
                ", main_thread_cpu=" + (_threadCpuAvailable ? "available" : "unavailable") +
                ", unity_gc_incremental=" + _unityGcIncremental +
                ", unity_gc_mode=" + _unityGcMode +
                ", unity_gc_slice_ns=" + _unityGcSliceNs +
                ", gc_hold_key=" + GcHoldKey +
                ", gc_hold_seconds=" + GcHoldSeconds.ToString("0") + ".");
        }

        private void Update()
        {
            HandleGcHoldControl();

            long nowTicks = Stopwatch.GetTimestamp();
            int gc0 = GC.CollectionCount(0);
            int gc1 = GC.CollectionCount(1);
            int gc2 = GC.CollectionCount(2);
            long threadCpu100Ns = 0;
            bool threadCpuRead = _threadCpuAvailable && TryReadCurrentThreadCpu100Ns(out threadCpu100Ns);

            if (_threadCpuAvailable && !threadCpuRead)
            {
                _threadCpuAvailable = false;
                Logger.LogWarning("Main-thread CPU sampling became unavailable; wall/GC spike detection remains active.");
            }

            if (!_haveSample)
            {
                _lastTicks = nowTicks;
                _lastGc0 = gc0;
                _lastGc1 = gc1;
                _lastGc2 = gc2;
                if (threadCpuRead) _lastThreadCpu100Ns = threadCpu100Ns;
                _haveSample = true;
                return;
            }

            double elapsedMs = (nowTicks - _lastTicks) * 1000.0 / Stopwatch.Frequency;
            int gc0Delta = gc0 - _lastGc0;
            int gc1Delta = gc1 - _lastGc1;
            int gc2Delta = gc2 - _lastGc2;
            double mainThreadCpuMs = threadCpuRead
                ? Math.Max(0.0, (threadCpu100Ns - _lastThreadCpu100Ns) / 10000.0)
                : -1.0;

            _lastTicks = nowTicks;
            _lastGc0 = gc0;
            _lastGc1 = gc1;
            _lastGc2 = gc2;
            if (threadCpuRead) _lastThreadCpu100Ns = threadCpu100Ns;

            if (Time.realtimeSinceStartup < _armAt)
                return;

            if (!_armedLogged)
            {
                _armedLogged = true;
                Logger.LogInfo(
                    "FRAME SPIKE PROBE ARMED | threshold_ms=" +
                    _thresholdMs.Value.ToString("0.0") +
                    ", main_thread_cpu=" + (_threadCpuAvailable ? "available" : "unavailable") +
                    ", unity_gc_incremental=" + _unityGcIncremental +
                    ", unity_gc_mode=" + _unityGcMode +
                    ", unity_gc_slice_ns=" + _unityGcSliceNs +
                    ", gc_hold_key=" + GcHoldKey +
                    ", gc_hold_seconds=" + GcHoldSeconds.ToString("0") + ".");
            }

            if (elapsedMs < _thresholdMs.Value)
                return;

            _spikeCount++;
            float elapsed = (float)elapsedMs;
            if (elapsed > _maxSpikeMs) _maxSpikeMs = elapsed;

            long managedBytes = GC.GetTotalMemory(false);
            string incrementalPendingText = QueryIncrementalPendingText();

            string cpuPart;
            if (mainThreadCpuMs >= 0.0)
            {
                double nonCpuWallMs = Math.Max(0.0, elapsedMs - mainThreadCpuMs);
                double cpuSharePct = elapsedMs > 0.0 ? Math.Min(100.0, mainThreadCpuMs * 100.0 / elapsedMs) : 0.0;
                cpuPart =
                    " main_thread_cpu_ms=" + mainThreadCpuMs.ToString("0.00") +
                    " non_cpu_wall_ms=" + nonCpuWallMs.ToString("0.00") +
                    " cpu_share_pct=" + cpuSharePct.ToString("0.0");
            }
            else
            {
                cpuPart = " main_thread_cpu_ms=NA non_cpu_wall_ms=NA cpu_share_pct=NA";
            }

            float holdRemaining = _gcHoldActive ? Math.Max(0f, _gcHoldUntil - Time.realtimeSinceStartup) : 0f;

            Logger.LogWarning(
                "[FRAME SPIKE #" + _spikeCount + "] " +
                "wall_ms=" + elapsedMs.ToString("0.00") +
                cpuPart +
                " unity_unscaled_dt_ms=" + (Time.unscaledDeltaTime * 1000f).ToString("0.00") +
                " frame=" + Time.frameCount +
                " timeScale=" + Time.timeScale.ToString("0.###") +
                " focused=" + Application.isFocused +
                " gc_cycle_delta=" + gc0Delta +
                " gc0=+" + gc0Delta +
                " gc1=+" + gc1Delta +
                " gc2=+" + gc2Delta +
                " unity_gc_incremental=" + GarbageCollector.isIncremental +
                " unity_gc_mode=" + GarbageCollector.GCMode +
                " incremental_pending=" + incrementalPendingText +
                " unity_gc_slice_ns=" + GarbageCollector.incrementalTimeSliceNanoseconds +
                " gc_hold_active=" + _gcHoldActive +
                " gc_hold_remaining_s=" + holdRemaining.ToString("0.0") +
                " managed_mb=" + (managedBytes / (1024.0 * 1024.0)).ToString("0.0") + ".");
        }

        private void HandleGcHoldControl()
        {
            bool keyDown = Input.GetKeyDown(GcHoldKey);

            if (_gcHoldActive)
            {
                if (keyDown)
                {
                    EndGcHold("manual");
                }
                else if (Time.realtimeSinceStartup >= _gcHoldUntil)
                {
                    EndGcHold("timeout");
                }

                return;
            }

            if (!keyDown || Time.realtimeSinceStartup < _armAt)
                return;

            StartGcHold();
        }

        private void StartGcHold()
        {
            if (GarbageCollector.GCMode == GarbageCollector.Mode.Disabled)
            {
                Logger.LogWarning("[GC HOLD NOT STARTED] GarbageCollector.GCMode is already Disabled; diagnostic did not change global GC state.");
                return;
            }

            GarbageCollector.Mode previousMode = GarbageCollector.GCMode;
            string pendingBefore = QueryIncrementalPendingText();
            long managedBefore = GC.GetTotalMemory(false);
            int cycleBefore = GC.CollectionCount(0);

            try
            {
                GarbageCollector.GCMode = GarbageCollector.Mode.Disabled;
            }
            catch (Exception ex)
            {
                Logger.LogError("[GC HOLD FAILED] Could not disable GC: " + ex.GetType().Name + ": " + ex.Message);
                return;
            }

            if (GarbageCollector.GCMode != GarbageCollector.Mode.Disabled)
            {
                Logger.LogError("[GC HOLD FAILED] Requested Disabled but runtime reports " + GarbageCollector.GCMode + ".");
                return;
            }

            _gcHoldRestoreMode = previousMode;
            _gcHoldStartManagedBytes = managedBefore;
            _gcHoldStartCycle = cycleBefore;
            _gcHoldUntil = Time.realtimeSinceStartup + GcHoldSeconds;
            _gcHoldActive = true;

            Logger.LogWarning(
                "[GC HOLD START] duration_s=" + GcHoldSeconds.ToString("0") +
                " restore_mode=" + previousMode +
                " incremental_pending_before=" + pendingBefore +
                " gc_cycle=" + cycleBefore +
                " managed_mb=" + (managedBefore / (1024.0 * 1024.0)).ToString("0.0") +
                ". Cross the target area now; F6 ends the hold early. GC will restore automatically.");

            ResetSample();
        }

        private void EndGcHold(string reason)
        {
            if (!_gcHoldActive)
                return;

            GarbageCollector.Mode restoreMode = _gcHoldRestoreMode;
            long managedBeforeRestore = GC.GetTotalMemory(false);
            int cycleBeforeRestore = GC.CollectionCount(0);

            try
            {
                GarbageCollector.GCMode = restoreMode;
            }
            catch (Exception ex)
            {
                Logger.LogError("[GC HOLD RESTORE FAILED] Could not restore GC mode to " + restoreMode + ": " + ex.GetType().Name + ": " + ex.Message);
                return;
            }

            _gcHoldActive = false;
            _gcHoldUntil = 0f;

            Logger.LogWarning(
                "[GC HOLD END] reason=" + reason +
                " restored_mode=" + GarbageCollector.GCMode +
                " gc_cycle_delta=" + (cycleBeforeRestore - _gcHoldStartCycle) +
                " managed_mb_start=" + (_gcHoldStartManagedBytes / (1024.0 * 1024.0)).ToString("0.0") +
                " managed_mb_end=" + (managedBeforeRestore / (1024.0 * 1024.0)).ToString("0.0") +
                " managed_growth_mb=" + ((managedBeforeRestore - _gcHoldStartManagedBytes) / (1024.0 * 1024.0)).ToString("0.0") +
                ". No forced collection was issued on restore.");

            ResetSample();
        }

        private string QueryIncrementalPendingText()
        {
            if (!_unityGcIncremental || GarbageCollector.GCMode == GarbageCollector.Mode.Disabled)
                return "NA";

            try
            {
                return GarbageCollector.CollectIncremental(0) ? "True" : "False";
            }
            catch (Exception ex)
            {
                return "ERR:" + ex.GetType().Name;
            }
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused && _gcHoldActive)
                EndGcHold("focus_lost");

            ResetSample();
        }

        private void OnDestroy()
        {
            if (_gcHoldActive)
                EndGcHold("plugin_destroy");

            Logger.LogInfo(
                "FRAME SPIKE PROBE STOPPED | spikes=" + _spikeCount +
                " max_ms=" + _maxSpikeMs.ToString("0.00") + ".");
        }

        private void ResetSample()
        {
            _lastTicks = Stopwatch.GetTimestamp();
            _lastGc0 = GC.CollectionCount(0);
            _lastGc1 = GC.CollectionCount(1);
            _lastGc2 = GC.CollectionCount(2);

            long threadCpu100Ns;
            if (_threadCpuAvailable && TryReadCurrentThreadCpu100Ns(out threadCpu100Ns))
                _lastThreadCpu100Ns = threadCpu100Ns;

            _haveSample = true;
        }

        private static bool TryReadCurrentThreadCpu100Ns(out long total100Ns)
        {
            total100Ns = 0;
            try
            {
                FileTime creation;
                FileTime exit;
                FileTime kernel;
                FileTime user;
                if (!GetThreadTimes(GetCurrentThread(), out creation, out exit, out kernel, out user))
                    return false;

                total100Ns = ToInt64(kernel) + ToInt64(user);
                return true;
            }
            catch (DllNotFoundException)
            {
                return false;
            }
            catch (EntryPointNotFoundException)
            {
                return false;
            }
        }

        private static long ToInt64(FileTime value)
        {
            return ((long)value.High << 32) | value.Low;
        }
    }
}
