using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Quartermaster.Data.AdministrativeDivisions;

//AdministrativeDivisionLoader.Load("DE_Base.txt", "DE_PostCodes.txt");
public static class AdministrativeDivisionLoader {
    public static void Load(string baseFilePath, string postcodeFilePath) {
        if (!File.Exists(baseFilePath) || !File.Exists(postcodeFilePath))
            throw new InvalidOperationException();

        var loadedDivisions = new Dictionary<string, AdminDivision>();
        var world = AdminDivision.World;
        loadedDivisions.Add(world.GetAdminCode(), world);

        // Pass 1: Collect base data
        foreach (var line in File.ReadLines(baseFilePath)) {
            var adminDiv = AdminDivision.FromString(line);
            if (adminDiv == null)
                continue;

            if (!loadedDivisions.ContainsKey(adminDiv.CountryCode)) {
                loadedDivisions.Add(adminDiv.CountryCode, new AdminDivision {
                    CountryCode = adminDiv.CountryCode,
                    Names = [adminDiv.CountryCode],
                    Level = AdminLevel.Country,
                    ParentLevel = AdminLevel.World
                });
            }

            if (!loadedDivisions.ContainsKey(adminDiv.GetAdminCode()))
                loadedDivisions.Add(adminDiv.GetAdminCode(), adminDiv);
        }

        // Pass 2: Collect post codes
        foreach (var line in File.ReadLines(postcodeFilePath)) {
            var split = line.Split('\t');
            var postCodeStr = split[1];
            if (!int.TryParse(postCodeStr, out var postCode) || postCode == 0)
                continue;

            var admin3Str = split[8];
            if (!int.TryParse(admin3Str, out var admin3) || admin3 == 0)
                continue;

            var name = split[2];

            if (loadedDivisions.TryGetValue(admin3.ToString(), out var adminDiv))
                adminDiv.PostCodes.Add(postCode);
            if (loadedDivisions.TryGetValue(admin3 + "_" + name, out adminDiv))
                adminDiv.PostCodes.Add(postCode);
        }

        File.WriteAllText(baseFilePath + ".json", JsonSerializer.Serialize(loadedDivisions));

        var eberholzen = loadedDivisions.Values.FirstOrDefault(d
            => d.Names.Any(n => n.Contains("Griedel")));
        if (eberholzen == null) {
            Console.WriteLine("Eberholzen is null");
            return;
        }

        var hierarchy = new List<AdminDivision> {
            eberholzen
        };

        var currentAdminDiv = eberholzen;
        while (currentAdminDiv.Level != AdminLevel.World) {
            if (!loadedDivisions.TryGetValue(currentAdminDiv.GetParentAdminCode(), out var parent))
                break;

            currentAdminDiv = parent;
            hierarchy.Add(currentAdminDiv);

            if (currentAdminDiv.Level == AdminLevel.Undefined)
                break;
        }

        hierarchy.Reverse();
        foreach (var adminDiv in hierarchy) {
            Console.WriteLine($"Name: {adminDiv.Names[0]}, Level: {adminDiv.Level}, " +
                $"PLZ: {(adminDiv.PostCodes.Count > 0 ? adminDiv.PostCodes.FirstOrDefault() : "N/A")}");
        }
    }
}

internal class AdminDivision {
    public static readonly AdminDivision World = new AdminDivision() {
        CountryCode = "World",
        Level = AdminLevel.World,
        ParentLevel = AdminLevel.Undefined,
        Names = ["World"]
    };

    public List<string> Names { get; internal set; } = [];
    public HashSet<int> PostCodes { get; } = [];

    public AdminLevel Level { get; set; }
    public AdminLevel ParentLevel { get; set; }

    public string CountryCode { get; set; } = "Null Island";

    public int Admin1 { get; set; } = -1;
    public int Admin2 { get; set; } = -1;
    public int Admin3 { get; set; } = -1;
    public int Admin4 { get; set; } = -1;

    public bool IsPPL { get; set; }

    internal string GetAdminCode(AdminLevel level) {
        return level switch {
            AdminLevel.World => World.CountryCode,
            AdminLevel.Admin1 => Admin1.ToString(),
            AdminLevel.Admin2 => Admin2.ToString(),
            AdminLevel.Admin3 => Admin3.ToString(),
            AdminLevel.Admin4 => Admin4.ToString(),
            AdminLevel.Other => Admin3 + "_" + Names[0],
            _ => CountryCode,
        };
    }

    public string GetAdminCode() => GetAdminCode(Level);
    public string GetParentAdminCode() => GetAdminCode(ParentLevel);

    public static AdminDivision? FromString(string str) {
        var split = str.Split('\t');
        var code = split[7];

        if (code != "ADM1" && code != "ADM2" && code != "ADM3" && code != "ADM4" && code != "PPL" && !code.StartsWith("PPLA"))
            return null;

        var adminDiv = new AdminDivision();

        adminDiv.Names.Add(split[1]);
        adminDiv.Names.AddRange(split[3].Split(',', StringSplitOptions.RemoveEmptyEntries));

        adminDiv.CountryCode = split[8];
        if (!string.IsNullOrEmpty(adminDiv.CountryCode)) {
            adminDiv.ParentLevel = AdminLevel.World;
            adminDiv.Level = AdminLevel.Country;
        }

        if (int.TryParse(split[10], out var a1) && a1 != 0) {
            adminDiv.Admin1 = a1;

            if (a1 != -1) {
                adminDiv.ParentLevel = adminDiv.Level;
                adminDiv.Level = AdminLevel.Admin1;
            }
        }

        if (int.TryParse(split[11], out var a2) && a2 != 0) {
            adminDiv.Admin2 = a2;

            if (a2 != -1) {
                adminDiv.ParentLevel = adminDiv.Level;
                adminDiv.Level = AdminLevel.Admin2;
            }
        }

        if (int.TryParse(split[12], out var a3) && a3 != 0) {
            adminDiv.Admin3 = a3;

            if (a3 != -1) {
                adminDiv.ParentLevel = adminDiv.Level;
                adminDiv.Level = AdminLevel.Admin3;
            }
        }

        if (int.TryParse(split[13], out var a4) && a4 != 0) {
            adminDiv.Admin4 = a4;

            if (a4 != -1) {
                adminDiv.ParentLevel = adminDiv.Level;
                adminDiv.Level = AdminLevel.Admin4;
            }
        }

        adminDiv.IsPPL = code == "PPL" || code.StartsWith("PPLA");

        if (adminDiv.IsPPL) {
            adminDiv.ParentLevel = adminDiv.Level;
            adminDiv.Level = AdminLevel.Other;
        }

        return adminDiv;
    }
}

internal enum AdminLevel {
    Undefined = 0,
    World = 1,
    Continent = 2,
    Country = 3,
    Admin1 = 4,
    Admin2 = 5,
    Admin3 = 6,
    Admin4 = 7,
    Other = 8
}