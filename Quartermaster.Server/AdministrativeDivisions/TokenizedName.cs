namespace Quartermaster.Server.AdministrativeDivisions;

/// <param name="Core">The normalized words that make up the actual name.</param>
/// <param name="Designators">The normalized words naming the kind of division, e.g. "landkreis".</param>
/// <param name="Joined">The core words written together, so "Spreeneisse" still finds "Spree-Neiße".</param>
public record TokenizedName(string[] Core, string[] Designators, string Joined);
