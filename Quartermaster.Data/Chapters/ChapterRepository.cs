using LinqToDB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Quartermaster.Data.Chapters;

public class ChapterRepository {
    private readonly DbContext _context;

    private static readonly Dictionary<string, string> ExternalCodeToShortCode = new() {
        ["BW"] = "bw", ["BY"] = "by", ["BE"] = "be", ["BB"] = "bb",
        ["HB"] = "hb", ["HH"] = "hh", ["HE"] = "he", ["MV"] = "mv",
        ["NI"] = "nds", ["NW"] = "nrw", ["RP"] = "rlp", ["SL"] = "sl",
        ["SN"] = "sn", ["ST"] = "st", ["SH"] = "sh", ["TH"] = "th"
    };

    public ChapterRepository(DbContext context) {
        _context = context;
    }

    public Chapter? Get(Guid id)
        => _context.Chapters.Where(c => c.Id == id).FirstOrDefault();

    public List<Chapter> GetAll()
        => _context.Chapters.OrderBy(c => c.Name).ToList();

    public void Create(Chapter chapter) => _context.Insert(chapter);

    public List<Chapter> GetByExternalCode(string externalCode)
        => _context.Chapters.Where(c => c.ExternalCode == externalCode).ToList();

    public Chapter? FindByExternalCodeAndParent(string externalCode, Guid? parentChapterId)
        => _context.Chapters
            .Where(c => c.ExternalCode == externalCode && c.ParentChapterId == parentChapterId)
            .FirstOrDefault();

    public Chapter? FindForDivision(Guid divisionId, AdministrativeDivisions.AdministrativeDivisionRepository adminDivRepo) {
        var ancestorIds = adminDivRepo.GetAncestorIds(divisionId);
        if (ancestorIds.Count == 0)
            return null;

        var chapters = _context.Chapters
            .Where(c => c.AdministrativeDivisionId != null && ancestorIds.Contains(c.AdministrativeDivisionId.Value))
            .ToList();

        if (chapters.Count == 0)
            return null;

        // Return the chapter whose division appears earliest in ancestor list (most specific)
        foreach (var ancestorId in ancestorIds) {
            var match = chapters.FirstOrDefault(c => c.AdministrativeDivisionId == ancestorId);
            if (match != null)
                return match;
        }

        return chapters[0];
    }

    public List<Guid> GetDescendantIds(Guid chapterId) {
        var result = new List<Guid> { chapterId };
        var queue = new Queue<Guid>();
        queue.Enqueue(chapterId);

        while (queue.Count > 0) {
            var parentId = queue.Dequeue();
            var children = _context.Chapters
                .Where(c => c.ParentChapterId == parentId && c.Id != parentId)
                .Select(c => c.Id)
                .ToList();

            foreach (var childId in children) {
                result.Add(childId);
                queue.Enqueue(childId);
            }
        }

        return result;
    }

    public void SupplementDefaults(AdministrativeDivisions.AdministrativeDivisionRepository adminDivRepo) {
        if (_context.Chapters.Any())
            return;

        var deDivision = _context.GetTable<AdministrativeDivisions.AdministrativeDivision>()
            .Where(ad => ad.AdminCode == "DE" && ad.Depth == 3)
            .FirstOrDefault();
        if (deDivision == null)
            return;

        var bundesverband = new Chapter {
            Id = Guid.NewGuid(),
            Name = "Piratenpartei Deutschland",
            AdministrativeDivisionId = deDivision.Id,
            ParentChapterId = null,
            ShortCode = "de"
        };
        Create(bundesverband);

        var shortCodes = new Dictionary<string, string> {
            ["Baden-Württemberg"] = "bw", ["Bayern"] = "by", ["Berlin"] = "be",
            ["Brandenburg"] = "bb", ["Bremen"] = "hb", ["Hamburg"] = "hh",
            ["Hessen"] = "he", ["Mecklenburg-Vorpommern"] = "mv",
            ["Niedersachsen"] = "nds", ["Nordrhein-Westfalen"] = "nrw",
            ["Rheinland-Pfalz"] = "rlp", ["Saarland"] = "sl", ["Sachsen"] = "sn",
            ["Sachsen-Anhalt"] = "st", ["Schleswig-Holstein"] = "sh", ["Thüringen"] = "th"
        };

        var states = adminDivRepo.GetChildren(deDivision.Id);
        foreach (var state in states) {
            Create(new Chapter {
                Id = Guid.NewGuid(),
                Name = $"Piratenpartei {state.Name}",
                AdministrativeDivisionId = state.Id,
                ParentChapterId = bundesverband.Id,
                ShortCode = shortCodes.GetValueOrDefault(state.Name)
            });
        }

        // Set ExternalCode on state chapters
        var allChapters = GetAll();
        foreach (var chapter in allChapters) {
            if (chapter.ShortCode != null) {
                var extCode = ExternalCodeToShortCode
                    .FirstOrDefault(kv => kv.Value == chapter.ShortCode).Key;
                if (extCode != null && chapter.ExternalCode == null) {
                    _context.Chapters
                        .Where(c => c.Id == chapter.Id)
                        .Set(c => c.ExternalCode, extCode)
                        .Update();
                    chapter.ExternalCode = extCode;
                }
            }
        }

        // Set ExternalCode "de" on Bundesverband
        _context.Chapters
            .Where(c => c.Id == bundesverband.Id)
            .Set(c => c.ExternalCode, "de")
            .Update();

        // Create Ausland chapter
        var ausland = new Chapter {
            Id = Guid.NewGuid(),
            Name = "Ausland",
            AdministrativeDivisionId = null,
            ParentChapterId = bundesverband.Id,
            ShortCode = null,
            ExternalCode = "Ausland"
        };
        Create(ausland);

        // Seed Bezirk and Kreis chapters from known chapter structure
        SeedSubChapters(bundesverband.Id);
    }

    public (List<Chapter> Items, int TotalCount) Search(string? query, int page, int pageSize) {
        var q = _context.Chapters.AsQueryable();

        if (!string.IsNullOrWhiteSpace(query)) {
            q = q.Where(c => c.Name.Contains(query)
                || (c.ShortCode != null && c.ShortCode.Contains(query))
                || (c.ExternalCode != null && c.ExternalCode.Contains(query)));
        }

        var totalCount = q.Count();
        var items = q.OrderBy(c => c.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return (items, totalCount);
    }

    public List<Chapter> GetRoots()
        => _context.Chapters.Where(c => c.ParentChapterId == null).OrderBy(c => c.Name).ToList();

    public List<Chapter> GetChildren(Guid parentId)
        => _context.Chapters.Where(c => c.ParentChapterId == parentId && c.Id != parentId).OrderBy(c => c.Name).ToList();

    public List<Chapter> GetAncestorChain(Guid chapterId) {
        var chain = new List<Chapter>();
        var current = Get(chapterId);
        while (current != null) {
            chain.Add(current);
            if (current.ParentChapterId == null || current.ParentChapterId == current.Id)
                break;
            current = Get(current.ParentChapterId.Value);
        }
        return chain;
    }

    private void SeedSubChapters(Guid bundesverbandId) {
        var stateChapters = _context.Chapters
            .Where(c => c.ParentChapterId == bundesverbandId && c.ExternalCode != null && c.ExternalCode != "Ausland")
            .ToList()
            .ToDictionary(c => c.ExternalCode!);

        var subChapters = new List<(string Lv, string? Bezirk, string? Kreis)> {
            // Baden-Württemberg
            ("BW", "BW.FR", null),
            ("BW", "BW.KA", null), ("BW", "BW.KA", "BW.KA.BAD"), ("BW", "BW.KA", "BW.KA.FDS"), ("BW", "BW.KA", "BW.KA.HD"),
            ("BW", "BW.S", null), ("BW", "BW.S", "BW.S.S"),
            ("BW", "BW.TÜ", null), ("BW", "BW.TÜ", "BW.TÜ.UL"),
            ("BW", "BzV Südwürtemberg", null),
            // Bayern
            ("BY", "Bezirksverband Mittelfranken", null), ("BY", "Bezirksverband Mittelfranken", "Kreisverband Nürnberg"),
            ("BY", "Bezirksverband Mittelfranken", "Kreisverband Nürnberger Land"),
            ("BY", "Bezirksverband Mittelfranken", "KV Erlangen und Erlangen-Höchstadt"),
            ("BY", "Bezirksverband Mittelfranken", "KV Weißenburg-Gunzenhausen"),
            ("BY", "Bezirksverband Niederbayern", null), ("BY", "Bezirksverband Niederbayern", "KV Landshut"),
            ("BY", "Bezirksverband Oberbayern", null), ("BY", "Bezirksverband Oberbayern", "Kreisverband Berchtesgadener Land"),
            ("BY", "Bezirksverband Oberbayern", "Kreisverband Ebersberg"), ("BY", "Bezirksverband Oberbayern", "Kreisverband Ingolstadt"),
            ("BY", "Bezirksverband Oberbayern", "Kreisverband Landsberg am Lech"),
            ("BY", "Bezirksverband Oberbayern", "Kreisverband Mühldorf am Inn"),
            ("BY", "Bezirksverband Oberbayern", "Kreisverband München-Land"),
            ("BY", "Bezirksverband Oberbayern", "Kreisverband München-Stadt"),
            ("BY", "Bezirksverband Oberbayern", "Kreisverband Rosenheim"),
            ("BY", "Bezirksverband Oberbayern", "Kreisverband Starnberg"),
            ("BY", "Bezirksverband Oberbayern", "Kreisverband Traunstein"),
            ("BY", "Bezirksverband Oberfranken", null), ("BY", "Bezirksverband Oberfranken", "Kreisverband Bamberg"),
            ("BY", "Bezirksverband Oberfranken", "Kreisverband Bayreuth"),
            ("BY", "Bezirksverband Oberfranken", "Kreisverband Hof-Wunsiedel"),
            ("BY", "Bezirksverband Oberfranken", "Kreisverband Kronach"),
            ("BY", "Bezirksverband Oberpfalz", null), ("BY", "Bezirksverband Oberpfalz", "Kreisverband Neumarkt in der Oberpfalz"),
            ("BY", "Bezirksverband Oberpfalz", "Kreisverband Regensburg"),
            ("BY", "Bezirksverband Oberpfalz", "Kreisverband Schwandorf"),
            ("BY", "Bezirksverband Schwaben", null), ("BY", "Bezirksverband Schwaben", "Kreisverband Allgäu-Bodensee"),
            ("BY", "Bezirksverband Schwaben", "Kreisverband Kaufbeuren-Ostallgäu"),
            ("BY", "Bezirksverband Schwaben", "Kreisverband Neu-Ulm"),
            ("BY", "Bezirksverband Schwaben", "KV Günzburg"),
            ("BY", "Bezirksverband Unterfranken", null),
            // Berlin
            ("BE", "B-Charlottenburg-Wilmersdorf", null), ("BE", "B-Friedrichshain-Kreuzberg", null),
            ("BE", "B-Lichtenberg", null), ("BE", "B-Marzahn-Hellersdorf", null),
            ("BE", "B-Mitte", null), ("BE", "B-Neukölln", null),
            ("BE", "B-Pankow", null), ("BE", "B-Reinickendorf", null),
            ("BE", "B-Spandau", null), ("BE", "B-Steglitz-Zehlendorf", null),
            ("BE", "B-Tempelhof-Schöneberg", null), ("BE", "B-Treptow-Köpenick", null),
            // Brandenburg
            ("BB", null, "Märkisch-Oderland"), ("BB", null, "Potsdam"),
            ("BB", null, "RV DOS"), ("BB", null, "RV NORD"),
            ("BB", null, "RV SÜD"), ("BB", null, "RV WEST"),
            ("BB", null, "Teltow-Fläming"),
            // Bremen
            ("HB", null, "Bremerhaven"),
            // Hamburg
            ("HH", "HH-Altona", null), ("HH", "HH-Bergedorf", null),
            ("HH", "HH-Eimsbüttel", null), ("HH", "HH-Harburg", null),
            ("HH", "HH-Mitte", null), ("HH", "HH-Nord", null),
            ("HH", "HH-Wandsbek", null),
            // Hessen
            ("HE", "Mittelhessen", null), ("HE", "Mittelhessen", "Gießen"),
            ("HE", "Mittelhessen", "Lahn-Dill-Kreis"), ("HE", "Mittelhessen", "Limburg-Weilburg"),
            ("HE", "Mittelhessen", "Marburg-Biedenkopf"), ("HE", "Mittelhessen", "Vogelsbergkreis"),
            ("HE", "Nordhessen", null), ("HE", "Nordhessen", "Fulda"),
            ("HE", "Nordhessen", "Kassel"), ("HE", "Nordhessen", "Kassel Stadt"),
            ("HE", "Nordhessen", "Schwalm-Eder-Kreis"), ("HE", "Nordhessen", "Waldeck-Frankenberg"),
            ("HE", "Nordhessen", "Werra-Meißner-Kreis"),
            ("HE", "Südhessen", null), ("HE", "Südhessen", "Bergstraße"),
            ("HE", "Südhessen", "Darmstadt-Dieburg"), ("HE", "Südhessen", "Darmstadt Stadt"),
            ("HE", "Südhessen", "Frankfurt Stadt"), ("HE", "Südhessen", "Groß-Gerau"),
            ("HE", "Südhessen", "Hochtaunuskreis"), ("HE", "Südhessen", "Main-Kinzig-Kreis"),
            ("HE", "Südhessen", "Main-Taunus-Kreis"), ("HE", "Südhessen", "Odenwaldkreis"),
            ("HE", "Südhessen", "Offenbach"), ("HE", "Südhessen", "Rheingau-Taunus-Kreis"),
            ("HE", "Südhessen", "Wetteraukreis"), ("HE", "Südhessen", "Wiesbaden"),
            // Niedersachsen
            ("NI", null, "Cloppenburg"), ("NI", null, "Diepholz"),
            ("NI", null, "Göttingen"), ("NI", null, "Hameln-Pyrmont"),
            ("NI", null, "Nienburg-Schaumburg"), ("NI", null, "Nordost"),
            ("NI", null, "Oldenburg"), ("NI", null, "Osnabrück"),
            ("NI", null, "Region Hannover"), ("NI", null, "Stade"),
            ("NI", null, "Stadt Braunschweig"), ("NI", null, "Stadt Oldenburg"),
            ("NI", null, "Stadt Wolfsburg"), ("NI", null, "Südheide"),
            ("NI", null, "Wolfenbüttel-Salzgitter"),
            // Nordrhein-Westfalen
            ("NW", "RB Arnsberg", null), ("NW", "RB Arnsberg", "Bochum"),
            ("NW", "RB Arnsberg", "Dortmund"), ("NW", "RB Arnsberg", "Ennepe-Ruhr-Kreis"),
            ("NW", "RB Arnsberg", "Hagen"), ("NW", "RB Arnsberg", "Hamm"),
            ("NW", "RB Arnsberg", "Herne"), ("NW", "RB Arnsberg", "Hochsauerlandkreis"),
            ("NW", "RB Arnsberg", "Märkischer Kreis"), ("NW", "RB Arnsberg", "Olpe"),
            ("NW", "RB Arnsberg", "Siegen-Wittgenstein"), ("NW", "RB Arnsberg", "Soest"),
            ("NW", "RB Arnsberg", "Unna"),
            ("NW", "RB Detmold", null), ("NW", "RB Detmold", "Bielefeld"),
            ("NW", "RB Detmold", "Gütersloh"), ("NW", "RB Detmold", "Herford"),
            ("NW", "RB Detmold", "Höxter"), ("NW", "RB Detmold", "Lippe"),
            ("NW", "RB Detmold", "Minden-Lübbecke"), ("NW", "RB Detmold", "Paderborn"),
            ("NW", "RB Düsseldorf", null), ("NW", "RB Düsseldorf", "Duisburg"),
            ("NW", "RB Düsseldorf", "Düsseldorf"), ("NW", "RB Düsseldorf", "Essen"),
            ("NW", "RB Düsseldorf", "Kleve"), ("NW", "RB Düsseldorf", "Krefeld"),
            ("NW", "RB Düsseldorf", "Mettmann"), ("NW", "RB Düsseldorf", "Mönchengladbach"),
            ("NW", "RB Düsseldorf", "Mülheim"), ("NW", "RB Düsseldorf", "Oberhausen"),
            ("NW", "RB Düsseldorf", "Remscheid"), ("NW", "RB Düsseldorf", "Rhein-Kreis Neuss"),
            ("NW", "RB Düsseldorf", "Solingen"), ("NW", "RB Düsseldorf", "Viersen"),
            ("NW", "RB Düsseldorf", "Wesel"), ("NW", "RB Düsseldorf", "Wuppertal"),
            ("NW", "RB Köln", null), ("NW", "RB Köln", "Aachen"),
            ("NW", "RB Köln", "Bonn"), ("NW", "RB Köln", "Düren"),
            ("NW", "RB Köln", "Euskirchen"), ("NW", "RB Köln", "Heinsberg"),
            ("NW", "RB Köln", "Köln"), ("NW", "RB Köln", "Leverkusen"),
            ("NW", "RB Köln", "Oberbergischer Kreis"), ("NW", "RB Köln", "Rhein-Erft-Kreis"),
            ("NW", "RB Köln", "Rheinisch-Bergischer Kreis"), ("NW", "RB Köln", "Rhein-Sieg-Kreis"),
            ("NW", "RB Münster", null), ("NW", "RB Münster", "Borken"),
            ("NW", "RB Münster", "Bottrop"), ("NW", "RB Münster", "Coesfeld"),
            ("NW", "RB Münster", "Gelsenkirchen"), ("NW", "RB Münster", "Münster"),
            ("NW", "RB Münster", "Recklinghausen"), ("NW", "RB Münster", "Steinfurt"),
            ("NW", "RB Münster", "Warendorf"),
            // Rheinland-Pfalz
            ("RP", null, "KV Koblenz/Mayen-Koblenz"), ("RP", null, "KV Rhein-Pfalz"),
            ("RP", null, "KV Südpfalz"), ("RP", null, "KV Südwestpfalz"),
            ("RP", null, "KV Trier/Trier-Saarburg"),
            ("RP", null, "vKV Ahrweiler"), ("RP", null, "vKV Altenkirchen"),
            ("RP", null, "vKV Alzey-Worms"), ("RP", null, "vKV Bad Dürkheim"),
            ("RP", null, "vKV Bad Kreuznach"), ("RP", null, "vKV Bernkastel-Wittlich"),
            ("RP", null, "vKV Birkenfeld"), ("RP", null, "vKV Cochem-Zell"),
            ("RP", null, "vKV Donnersbergkreis"), ("RP", null, "vKV Kaiserslautern"),
            ("RP", null, "vKV Kusel"), ("RP", null, "vKV Mainz"),
            ("RP", null, "vKV Mainz-Bingen"), ("RP", null, "vKV Neustadt"),
            ("RP", null, "vKV Neuwied"), ("RP", null, "vKV Rhein-Hunsrück-Kreis"),
            ("RP", null, "vKV Rhein-Lahn-Kreis"), ("RP", null, "vKV St. Kaiserslautern"),
            ("RP", null, "vKV Westerwaldkreis"), ("RP", null, "vKV Worms"),
            // Saarland
            ("SL", null, "Merzig-Wadern"), ("SL", null, "Neunkirchen"),
            ("SL", null, "Saarbrücken"), ("SL", null, "Saarlouis"),
            ("SL", null, "Saarpfalz-Kreis"), ("SL", null, "St. Wendel"),
            // Schleswig-Holstein
            ("SH", null, "KV Kiel"),
            ("SH", null, "vKV Dithmarschen"), ("SH", null, "vKV Neumünster"),
            ("SH", null, "vKV Nordfriesland"), ("SH", null, "vKV Pinneberg"),
            ("SH", null, "vKV Plön"), ("SH", null, "vKV Rendsburg-Eckernförde"),
            ("SH", null, "vKV Segeberg"), ("SH", null, "vKV Südholstein"),
            // Sachsen
            ("SN", null, "Chemnitz"), ("SN", null, "Dresden"),
            ("SN", null, "Leipzig"), ("SN", null, "Meißen"),
            ("SN", null, "vKV Bautzen"), ("SN", null, "vKV Erzgebirge"),
            ("SN", null, "vKV Görlitz"), ("SN", null, "vKV Leipziger Land"),
            ("SN", null, "vKV Mittelsachsen"), ("SN", null, "vKV Nordsachsen"),
            ("SN", null, "vKV Sächsische Schweiz"), ("SN", null, "vKV Vogtland"),
            ("SN", null, "vKV Zwickau"),
            // Sachsen-Anhalt
            ("ST", null, "KV Börde"),
            ("ST", null, "RV Altmark"),
            ("ST", null, "VKV Anhalt-Bitterfeld"), ("ST", null, "VKV Burgenlandkreis"),
            ("ST", null, "VKV Dessau"), ("ST", null, "VKV Halle"),
            ("ST", null, "VKV Harz"), ("ST", null, "VKV Jerichower Land"),
            ("ST", null, "VKV Magdeburg"), ("ST", null, "VKV Mansfeld-Südharz"),
            ("ST", null, "VKV Saalekreis"), ("ST", null, "VKV Salzlandkreis"),
            ("ST", null, "VKV Wittenberg"),
            // Thüringen
            ("TH", "Mitte-Thüringen", null), ("TH", "Mitte-Thüringen", "Erfurt"),
            ("TH", "Mitte-Thüringen", "Ilm-Kreis"), ("TH", "Mitte-Thüringen", "vKV SRU"),
            ("TH", "Mitte-Thüringen", "Weimar"),
            ("TH", "Nord-Thüringen", null), ("TH", "Nord-Thüringen", "vKV KYF"),
            ("TH", "Nord-Thüringen", "vKV NDH"), ("TH", "Nord-Thüringen", "vKV SÖM"),
            ("TH", "Nord-Thüringen", "vKV UH"),
            ("TH", "Ost-Thüringen", null), ("TH", "Ost-Thüringen", "Gera"),
            ("TH", "Ost-Thüringen", "Jena"), ("TH", "Ost-Thüringen", "vKV Greiz"),
            ("TH", "Ost-Thüringen", "vKV SHKSOK"),
            ("TH", "Süd-Thüringen", null), ("TH", "Süd-Thüringen", "Schmalkalden-Meiningen"),
            ("TH", "Süd-Thüringen", "vKV SHLHibuSON"),
            ("TH", "West-Thüringen", null), ("TH", "West-Thüringen", "Gotha"),
            ("TH", "West-Thüringen", "Wartburgkreis"),
            // Mecklenburg-Vorpommern
            ("MV", null, "WM"),
            // Hessen (some Kreis entries appear without Bezirk in the data)
            ("HE", null, "Frankfurt Stadt"), ("HE", null, "Gießen"),
            ("HE", null, "Kassel Stadt"), ("HE", null, "Limburg-Weilburg"),
            ("HE", null, "Rheingau-Taunus-Kreis"), ("HE", null, "Vogelsbergkreis"),
        };

        // Track created Bezirk chapters to avoid duplicates
        var createdBezirke = new Dictionary<string, Chapter>();

        foreach (var (lv, bezirk, kreis) in subChapters) {
            if (!stateChapters.TryGetValue(lv, out var stateChapter))
                continue;

            Chapter? parentForKreis = stateChapter;

            // Create Bezirk if specified and not yet created
            if (bezirk != null) {
                var bezirkKey = $"{lv}|{bezirk}";
                if (!createdBezirke.TryGetValue(bezirkKey, out var bezirkChapter)) {
                    // Check if already exists in DB (idempotent)
                    bezirkChapter = FindByExternalCodeAndParent(bezirk, stateChapter.Id);
                    if (bezirkChapter == null) {
                        bezirkChapter = new Chapter {
                            Id = Guid.NewGuid(),
                            Name = bezirk,
                            ParentChapterId = stateChapter.Id,
                            ExternalCode = bezirk
                        };
                        Create(bezirkChapter);
                    }
                    createdBezirke[bezirkKey] = bezirkChapter;
                }
                parentForKreis = bezirkChapter;
            }

            // Create Kreis if specified
            if (kreis != null) {
                var existing = FindByExternalCodeAndParent(kreis, parentForKreis.Id);
                if (existing == null) {
                    Create(new Chapter {
                        Id = Guid.NewGuid(),
                        Name = kreis,
                        ParentChapterId = parentForKreis.Id,
                        ExternalCode = kreis
                    });
                }
            }
        }
    }
}
