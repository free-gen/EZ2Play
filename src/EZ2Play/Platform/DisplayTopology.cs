using System;
using System.Runtime.InteropServices;

namespace EZ2Play.App
{
    /// <summary>
    /// Reads the Windows display projection topology.
    /// </summary>
    /// <remarks>
    /// Uses the Windows CCD API to identify the projection
    /// mode without relying on monitor names or resolutions.
    /// </remarks>
    internal static class DisplayTopology
    {
        private const uint QdcDatabaseCurrent = 0x00000004;

        private const uint TopologyInternal = 0x00000001;
        private const uint TopologyClone = 0x00000002;
        private const uint TopologyExtend = 0x00000004;
        private const uint TopologyExternal = 0x00000008;

        private const int ErrorSuccess = 0;
        private const int ErrorInsufficientBuffer = 122;

        [StructLayout(LayoutKind.Sequential)]
        private struct Luid
        {
            public uint LowPart;
            public int HighPart;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Rational
        {
            public uint Numerator;
            public uint Denominator;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PathSource
        {
            public Luid AdapterId;
            public uint Id;
            public uint ModeInfoIdx;
            public uint StatusFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PathTarget
        {
            public Luid AdapterId;
            public uint Id;
            public uint ModeInfoIdx;
            public uint OutputTechnology;
            public uint Rotation;
            public uint Scaling;
            public Rational RefreshRate;
            public uint ScanlineOrdering;
            public uint TargetAvailable;
            public uint StatusFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DisplayPath
        {
            public PathSource Source;
            public PathTarget Target;
            public uint Flags;
        }

        [StructLayout(LayoutKind.Explicit, Size = 48)]
        private struct ModeData
        {
            [FieldOffset(0)]
            public ulong PixelRate;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DisplayMode
        {
            public uint InfoType;
            public uint Id;
            public Luid AdapterId;
            public ModeData Data;
        }

        [DllImport("user32.dll")]
        private static extern int GetDisplayConfigBufferSizes(
            uint flags,
            out uint pathCount,
            out uint modeCount);

        [DllImport("user32.dll")]
        private static extern int QueryDisplayConfig(
            uint flags,
            ref uint pathCount,
            [Out] DisplayPath[] paths,
            ref uint modeCount,
            [Out] DisplayMode[] modes,
            out uint topologyId);

        /// <summary>
        /// Determines whether Windows is using the
        /// external-only projection topology.
        /// </summary>
        /// <remarks>
        /// Clone and Extend map to the default choice
        /// in EZ2Play's two-mode display selector.
        /// </remarks>
        public static bool TryGetIsExternal(out bool isExternal)
        {
            isExternal = false;

            try
            {
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    uint pathCount;
                    uint modeCount;

                    if (GetDisplayConfigBufferSizes(
                            QdcDatabaseCurrent,
                            out pathCount,
                            out modeCount) != ErrorSuccess)
                    {
                        return false;
                    }

                    var paths = new DisplayPath[(int)pathCount];
                    var modes = new DisplayMode[(int)modeCount];

                    uint topologyId;

                    int result = QueryDisplayConfig(
                        QdcDatabaseCurrent,
                        ref pathCount,
                        paths,
                        ref modeCount,
                        modes,
                        out topologyId);

                    if (result == ErrorInsufficientBuffer)
                        continue;

                    if (result != ErrorSuccess)
                        return false;

                    switch (topologyId)
                    {
                        case TopologyExternal:
                            isExternal = true;
                            return true;

                        case TopologyInternal:
                        case TopologyClone:
                        case TopologyExtend:
                            return true;

                        default:
                            return false;
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLog.Error(
                    "DisplayTopology",
                    ex,
                    "Failed to read display topology.");
            }

            return false;
        }
    }
}