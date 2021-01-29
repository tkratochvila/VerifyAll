using System;

namespace InterLayerLib
{
    [Flags]
    public enum VerificationTableCellFlag : short
    {
        None = 0,
        NoDefectAnalysis = 1, // No defect found by given analysis
        DefectAnalysis = 2, // Defect found by given analysis
        Realizable = 4, // Realizable requirement or rule
        Violating = 8, // Violating requirement or rule
        ViolatingNext = 16, // Violating requirement or rule for next defect
        RootUnrealizability = 32, // Root cause of the unrealizability
        Redundant = 64,  // redundant requirement
        Neutral = 128, // Neutral
        NoDefectLimited = 256, // No defect found by limited verification method
        NoDefect = 512, // Does not contain a defect
        Defect = 1024 // Contains a defect
    };
}
