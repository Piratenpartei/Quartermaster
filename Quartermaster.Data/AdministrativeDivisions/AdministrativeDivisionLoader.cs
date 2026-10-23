using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Quartermaster.Data.AdministrativeDivisions;

public static class AdministrativeDivisionLoader {
    public static void Load(string baseFilePath, string postcodeFilePath,
        AdministrativeDivisionRepository adminDivRepo) {
        var data = Parse(baseFilePath, postcodeFilePath);
        adminDivRepo.CreateBulk(data.Divisions);
    }

    public static AdministrativeDivisionParseResult Parse(string baseFilePath, string postcodeFilePath) {
        if (!File.Exists(baseFilePath) || !File.Exists(postcodeFilePath))
            throw new InvalidOperationException($"Admin division files not found: {baseFilePath}, {postcodeFilePath}");
        var loadedDivisions = new Dictionary<string, AdminDivision>();
        var otherDivisionsLookup = new Dictionary<string, List<AdminDivision>>();
        var admin3Lookup = new Dictionary<string, List<AdminDivision>>();
        var world = AdminDivision.World;
        loadedDivisions.Add(world.GetAdminCode(), world);

        // Pass 1: Collect base data
        foreach (var line in File.ReadLines(baseFilePath)) {
            var adminDiv = AdminDivision.FromString(line);
            if (adminDiv == null)
                continue;

            // Skip places and city districts whose source row carries no Admin4 code: they'd be parented straight under the
            // state with an AdminCode like "-1+Name", and they have no postcode to be useful anywhere.
            if (adminDiv.Level == AdminLevel.Other && adminDiv.Admin4 == -1)
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

            if (adminDiv.Level == AdminLevel.Other) {
                if (!otherDivisionsLookup.TryGetValue(adminDiv.GetParentAdminCode(), out var list)) {
                    list = [];
                    otherDivisionsLookup.Add(adminDiv.GetParentAdminCode(), list);
                }

                list.Add(adminDiv);
            }

            if (adminDiv.Level == AdminLevel.Admin3)
                admin3Lookup.Add(adminDiv.GetAdminCode(), []);
        }

        // Pass 2: Build Admin3 -> Admin4 Lookup
        foreach (var adminDiv in loadedDivisions.Values) {
            if (adminDiv.Level != AdminLevel.Admin4)
                continue;

            if (!admin3Lookup.TryGetValue(adminDiv.GetParentAdminCode(), out var list))
                continue;

            list.Add(adminDiv);
        }

        // Pass 3: Collect post codes
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

            if (admin3Lookup.TryGetValue(admin3.ToString(), out var admin4s)) {
                foreach (var admin4 in admin4s) {
                    if (!admin4.Names.Contains(name))
                        continue;

                    admin4.PostCodes.Add(postCode);

                    if (otherDivisionsLookup.TryGetValue(admin4.GetAdminCode(), out var list)) {
                        foreach (var ad in list)
                            ad.PostCodes.Add(postCode);
                    }
                }
            }
        }

        foreach (var adminDiv in loadedDivisions.Values) {
            if (adminDiv.Level != AdminLevel.Other || adminDiv.PostCodes.Count > 0)
                continue;
            if (!loadedDivisions.TryGetValue(adminDiv.GetParentAdminCode(), out var a4))
                continue;
            foreach (var pc in a4.PostCodes)
                adminDiv.PostCodes.Add(pc);
        }

        var bulkData = new List<AdministrativeDivision>();
        var origins = new Dictionary<string, AdministrativeDivisionOrigin>();
        foreach (var adminDiv in loadedDivisions.Values.OrderBy(ad => ad.Level)) {
            loadedDivisions.TryGetValue(adminDiv.GetParentAdminCode(), out var parent);
            if (adminDiv.GeoNameId != 0)
                origins[adminDiv.GetAdminCode()] = new AdministrativeDivisionOrigin(adminDiv.GeoNameId, adminDiv.Names[0], adminDiv.IsDistrict);

            bulkData.Add(new AdministrativeDivision {
                Id = adminDiv.Id,
                ParentId = parent?.Id,
                Name = PickGermanName(adminDiv.Names),
                Depth = (int)adminDiv.Level,
                AdminCode = adminDiv.GetAdminCode(),
                PostCodes = string.Join(',', adminDiv.PostCodes)
            });
        }

        return new AdministrativeDivisionParseResult(bulkData, origins);
    }

    private static readonly char[] GermanChars = ['ä', 'ö', 'ü', 'ß', 'Ä', 'Ö', 'Ü'];

    private static readonly string[] GermanPrefixes = ["Land ", "Freistaat ", "Freie und Hansestadt ", "Freie Hansestadt "];

    private static string PickGermanName(List<string> names) {
        if (names.Count <= 1)
            return names[0];

        // Collect candidates from official German naming patterns (Land X, Freistaat X, etc.)
        string? lastCandidate = null;
        foreach (var name in names.Skip(1)) {
            foreach (var prefix in GermanPrefixes) {
                if (!name.StartsWith(prefix, StringComparison.Ordinal) || name.Length <= prefix.Length)
                    continue;

                var candidate = name[prefix.Length..];

                // Prefer the variant with German-specific characters (umlauts)
                if (candidate.IndexOfAny(GermanChars) >= 0)
                    return candidate;

                lastCandidate = candidate;
            }
        }

        return lastCandidate ?? names[0];
    }
}

internal class AdminDivision {
    private const string DistrictCode = "ADM5";
    private static readonly HashSet<string> AdminCodes = ["ADM1", "ADM2", "ADM3", "ADM4", DistrictCode];

    public static readonly AdminDivision World = new() {
        CountryCode = "World",
        Level = AdminLevel.World,
        ParentLevel = AdminLevel.Undefined,
        Names = ["World"]
    };

    public Guid Id { get; set; } = Guid.NewGuid();
    public int GeoNameId { get; set; }

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
    public bool IsDistrict { get; set; }

    internal string GetAdminCode(AdminLevel level) {
        return level switch {
            AdminLevel.World => World.CountryCode,
            AdminLevel.Admin1 => Admin1.ToString(),
            AdminLevel.Admin2 => Admin2.ToString(),
            AdminLevel.Admin3 => Admin3.ToString(),
            AdminLevel.Admin4 => Admin4.ToString(),
            AdminLevel.Other => Admin4 + "+" + Names[0],
            _ => CountryCode,
        };
    }

    public string GetAdminCode() => GetAdminCode(Level);
    public string GetParentAdminCode() => GetAdminCode(ParentLevel);

    public static AdminDivision? FromString(string str) {
        var split = str.Split('\t');
        var code = split[7];

        if (!IsLoadedFeature(code))
            return null;

        var adminDiv = new AdminDivision();
        if (int.TryParse(split[0], out var geoNameId))
            adminDiv.GeoNameId = geoNameId;

        adminDiv.IsDistrict = code == DistrictCode;
        adminDiv.Names.Add(adminDiv.IsDistrict ? DistrictName(split[1]) : split[1]);
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

        if (adminDiv.IsPPL || adminDiv.IsDistrict) {
            adminDiv.ParentLevel = adminDiv.Level;
            adminDiv.Level = AdminLevel.Other;
        }

        return adminDiv;
    }

    private static bool IsLoadedFeature(string code) {
        if (AdminCodes.Contains(code))
            return true;

        return code == "PPL" || code.StartsWith("PPLA");
    }

    /// <summary>A city district can share its name with a place in the same city (Stadtbezirk Pieschen, Ortsteil Pieschen).</summary>
    private static string DistrictName(string name) {
        if (name.Contains("bezirk", StringComparison.OrdinalIgnoreCase))
            return name;

        return "Stadtbezirk " + name;
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