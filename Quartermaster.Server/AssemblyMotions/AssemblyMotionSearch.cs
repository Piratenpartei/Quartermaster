using System;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Data.AssemblyMotions;

namespace Quartermaster.Server.AssemblyMotions;

public static class AssemblyMotionSearch {
    public static bool Matches(AssemblyMotion motion, AssemblyMotionType type, string? search) {
        if (string.IsNullOrEmpty(search))
            return true;
        if (motion.Title.Contains(search, StringComparison.OrdinalIgnoreCase))
            return true;
        if (motion.Number == null)
            return false;
        if (string.Equals(AssemblyMotionNumber.Format(type.Code, motion.Number.Value), search, StringComparison.OrdinalIgnoreCase))
            return true;
        return string.Equals(type.Code + motion.Number, search, StringComparison.OrdinalIgnoreCase);
    }
}
