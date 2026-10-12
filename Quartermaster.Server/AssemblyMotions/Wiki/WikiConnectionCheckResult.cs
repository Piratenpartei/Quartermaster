namespace Quartermaster.Server.AssemblyMotions.Wiki;

/// <param name="Configured">False when the wiki options are incomplete; no request was made then.</param>
/// <param name="Message">The confirmation when <paramref name="Ok"/>, otherwise a German reason that contains no secret and no response body.</param>
public record WikiConnectionCheckResult(bool Ok, bool Configured, string Message);
