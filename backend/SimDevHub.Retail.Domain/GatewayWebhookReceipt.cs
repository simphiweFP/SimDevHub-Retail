namespace SimDevHub.Retail.Domain;
public sealed class GatewayWebhookReceipt
{
    public Guid Id { get; set; }
    public required string Provider { get; set; }
    public required string BodySha256 { get; set; }
    public required string Status { get; set; }
    public int AttemptCount { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
    public DateTimeOffset? LastClaimedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}
