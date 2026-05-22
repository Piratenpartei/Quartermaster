namespace Quartermaster.Server.Security;

/// <summary>
/// Configuration for trusting <c>X-Forwarded-For</c> / <c>X-Forwarded-Proto</c> headers from
/// front-line reverse proxies. Both lists are empty by default: until a deployer opts in,
/// forwarded headers are ignored and the connection-level remote IP is authoritative.
/// </summary>
/// <remarks>
/// Untrusted forwarded headers are a known footgun for any code that keys on client IP
/// (rate limiting, lockout, audit). Configure <see cref="KnownProxies"/> with the exact
/// reverse-proxy IPs, or <see cref="KnownNetworks"/> with CIDR ranges if the proxy fleet
/// is dynamically addressed (e.g. Kubernetes ingress controllers). The values bind from
/// the <c>ForwardedHeaders</c> section of <c>appsettings.json</c>.
/// </remarks>
public class ForwardedHeadersSettings {
    public string[] KnownProxies { get; set; } = [];
    public string[] KnownNetworks { get; set; } = [];
}
