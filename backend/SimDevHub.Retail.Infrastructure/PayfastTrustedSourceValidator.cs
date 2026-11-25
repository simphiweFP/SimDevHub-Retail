using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Configuration;
using SimDevHub.Retail.Application;

namespace SimDevHub.Retail.Infrastructure;

/// <summary>
/// Fail-closed network-peer IP validator. Intended ONLY for direct TLS traffic
/// to the API with no reverse proxy. Never trusts X-Forwarded-For or Referer.
/// Operators must review the published Payfast egress ranges regularly.
/// </summary>
public sealed class PayfastTrustedSourceValidator : IPayfastSourceValidator
{
    private readonly (IPAddress Address, int Prefix)[] _ranges;
    public bool IsConfigured => _ranges.Length > 0;

    public PayfastTrustedSourceValidator(IConfiguration config)
    {
        var raw = config["Payments:PayfastSandbox:TrustedSourceCidrs"] ?? "";
        var entries = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (entries.Length == 0 || entries.Length > 32)
        {
            _ranges = [];
            return;
        }

        var parsed = new List<(IPAddress Address, int Prefix)>();
        foreach (var item in entries)
        {
            var tokens = item.Split('/');
            if (tokens.Length is < 1 or > 2 || !IPAddress.TryParse(tokens[0], out var ip))
            {
                _ranges = [];
                return;
            }
            if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
            var max = ip.AddressFamily == AddressFamily.InterNetwork ? 32 : 128;
            var prefix = max;
            if (tokens.Length == 2 && (!int.TryParse(tokens[1], out prefix) ||
                                       prefix < 0 || prefix > max))
            {
                _ranges = [];
                return;
            }
            // Single-host ranges only, or sensible bounded CIDRs. Never accept
            // broad networks even if deployment config is accidentally wrong.
            if (prefix < (max == 32 ? 24 : 120) || ip.Equals(IPAddress.Any) ||
                ip.Equals(IPAddress.IPv6Any) || IPAddress.IsLoopback(ip))
            {
                _ranges = [];
                return;
            }
            parsed.Add((ip, prefix));
        }
        _ranges = parsed.ToArray();
    }

    public Task<bool> IsTrustedSourceAsync(GatewayNotification notification, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var source = notification.SourceAddress;
        if (source is null || _ranges.Length == 0)
            return Task.FromResult(false);
        if (source.IsIPv4MappedToIPv6) source = source.MapToIPv4();
        if (IPAddress.IsLoopback(source))
            return Task.FromResult(false);

        var bytes = source.GetAddressBytes();
        foreach (var (network, prefix) in _ranges)
        {
            var networkBytes = network.GetAddressBytes();
            if (bytes.Length != networkBytes.Length) continue;
            var full = prefix / 8;
            var bits = prefix % 8;
            if (!bytes.AsSpan(0, full).SequenceEqual(networkBytes.AsSpan(0, full)))
                continue;
            if (bits == 0 || (bytes[full] & (0xff << (8 - bits))) ==
                             (networkBytes[full] & (0xff << (8 - bits))))
                return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }
}
