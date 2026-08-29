using System.ComponentModel;
using System.Runtime.InteropServices;

namespace SharkiDesktopGuardian.Services;

public sealed class NativeMetricsReader
{
    private readonly object _cpuGate = new();
    private ulong _previousIdle;
    private ulong _previousKernel;
    private ulong _previousUser;
    private bool _hasPreviousCpuSample;

    public double? ReadCpuLoadPercent()
    {
        lock (_cpuGate)
        {
            if (!GetSystemTimes(out var idle, out var kernel, out var user))
            {
                return null;
            }

            var idleValue = ToUInt64(idle);
            var kernelValue = ToUInt64(kernel);
            var userValue = ToUInt64(user);

            if (!_hasPreviousCpuSample)
            {
                _previousIdle = idleValue;
                _previousKernel = kernelValue;
                _previousUser = userValue;
                _hasPreviousCpuSample = true;
                return null;
            }

            var idleDelta = idleValue - _previousIdle;
            var kernelDelta = kernelValue - _previousKernel;
            var userDelta = userValue - _previousUser;
            var totalDelta = kernelDelta + userDelta;

            _previousIdle = idleValue;
            _previousKernel = kernelValue;
            _previousUser = userValue;

            if (totalDelta == 0)
            {
                return 0;
            }

            return Math.Clamp((totalDelta - idleDelta) * 100d / totalDelta, 0, 100);
        }
    }

    public (long UsedBytes, long TotalBytes)? ReadMemory()
    {
        var status = new MemoryStatusEx();
        if (!GlobalMemoryStatusEx(ref status))
        {
            return null;
        }

        var total = checked((long)status.TotalPhysical);
        var available = checked((long)status.AvailablePhysical);
        return (Math.Max(0, total - available), total);
    }

    private static ulong ToUInt64(FileTime value) => ((ulong)value.High << 32) | value.Low;

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(out FileTime idleTime, out FileTime kernelTime, out FileTime userTime);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

    [StructLayout(LayoutKind.Sequential)]
    private struct FileTime
    {
        public uint Low;
        public uint High;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhysical;
        public ulong AvailablePhysical;
        public ulong TotalPageFile;
        public ulong AvailablePageFile;
        public ulong TotalVirtual;
        public ulong AvailableVirtual;
        public ulong AvailableExtendedVirtual;

        public MemoryStatusEx()
        {
            Length = (uint)Marshal.SizeOf<MemoryStatusEx>();
        }
    }
}
