using System.Collections.Generic;
using System.Linq;

namespace Quartermaster.Data.Templates;

/// <summary>
/// Seed hashes of system template texts that shipped before rows stored their seed hash (V9.1). A row without a stored hash whose
/// text matches one of these was never edited and may follow its seed.
/// </summary>
internal static class SystemTemplateLegacySeeds {
    private static readonly Dictionary<string, string[]> _hashes = new() {
        ["templates.support_signatures.cover_letter"] = ["77274365a74feae67b1bc2586faa7577df978dc4da46395a2f99aa784b1e0c91"],
        ["notifications.assembly_motion_cosubmitter_reset.email"] = ["d9ebabfa24cc3148ba8b06ddc05c5cb1473d705bc715035fab1bceeb788c7f3a"],
        ["notifications.assembly_motion_cosubmitter_joined.email"] = ["1c8a7fb5c37f0cbc4bd7da4426974ca424f9a631612b41fd5153c97e8508570a"],
        ["notifications.assembly_motion_cosubmitter_joined.telegram"] = ["ee9900d857ef214cbf1b6b6235667fda86b4bcd50089577122e99c9425c34d5a"],
        ["notifications.assembly_motion_cosubmitter_left.email"] = ["9d08ad4090a9189273a4ec2a425443a27448b27783ce024db10194e32aab43b9"],
        ["notifications.assembly_motion_cosubmitter_left.telegram"] = ["7311c2c60100a7fda2437b7d1b521569dfb451aafef13ddd7dedaaa24c4b4ef5"],
        ["notifications.assembly_motion_collecting_reminder.email"] = ["39dfa8d9df18796af99588d41927fbd99c882f0596d3c858364ad6097ae28b54"],
        ["notifications.assembly_motion_collecting_reminder.telegram"] = ["8ace81d71444e317be21e598e14e2c55ab561acaa79c807f780d650341f48904"],
        ["notifications.assembly_motion_lapsed.email"] = ["cfe89b310a280c5b2d53c298e84a1911355859078de27ddfe69012e58cad6b39"],
        ["notifications.assembly_motion_lapsed.telegram"] = ["4053d99d8cc4e4178158c81a769f0be201273a2b3503f3385f736a619889cebf"],
    };

    public static bool Contains(string identifier, string hash) {
        return _hashes.TryGetValue(identifier, out var hashes) && hashes.Contains(hash);
    }
}
