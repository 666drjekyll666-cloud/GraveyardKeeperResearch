using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace GKFrameSpikeProbe
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class FrameSpikeProbePlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "nikich.gyk.diagnostics.framespikeprobe";
        public const string PluginName = "GK Frame Spike Probe (Diagnostic)";
        public const string PluginVersion = "0.2.0";

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
            ResetSample();

            Logger.LogInfo(
                PluginName + " " + PluginVersion + " loaded. " +
                "Normal-frame work is limited to Stopwatch timestamp + GC.CollectionCount(0..2) + Windows GetThreadTimes for the Unity main thread. " +
                "No object scans, stack traces, hierarchy enumeration, synchronous file I/O, or per-frame log writes are performed. " +
                "threshold_ms=" + _thresholdMs.Value.ToString("0.0") +
                ", arm_delay_s=" + _armDelaySeconds.Value.ToString("0.0") +
                ", main_thread_cpu=" + (_threadCpuAvailable ? "available" : "unavailable") + ".");
        }

        private void Update()
        {
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
                    ", main_thread_cpu=" + (_threadCpuAvailable ? "available" : "unavailable") + ".");
            }

            if (elapsedMs < _thresholdMs.Value)
                return;

            _spikeCount++;
            float elapsed = (float)elapsedMs;
            if (elapsed > _maxSpikeMs) _maxSpikeMs = elapsed;

            long managedBytes = GC.GetTotalMemory(false);
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

            Logger.LogWarning(
                "[FRAME SPIKE #" + _spikeCount + "] " +
                "wall_ms=" + elapsedMs.ToString("0.00") +
                cpuPart +
                " unity_unscaled_dt_ms=" + (Time.unscaledDeltaTime * 1000f).ToString("0.00") +
                " frame=" + Time.frameCount +
                " timeScale=" + Time.timeScale.ToString("0.###") +
                " focused=" + Application.isFocused +
                " gc0=+" + gc0Delta +
                " gc1=+" + gc1Delta +
                " gc2=+" + gc2Delta +
                " managed_mb=" + (managedBytes / (1024.0 * 1024.0)).ToString("0.0") + ".");
        }

        private void OnApplicationFocus(bool focused)
        {
            ResetSample();
        }

        private void OnDestroy()
        {
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
