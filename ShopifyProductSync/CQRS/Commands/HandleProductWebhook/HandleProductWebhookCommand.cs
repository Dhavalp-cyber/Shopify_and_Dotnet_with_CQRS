using MediatR;

namespace ShopifyProductSync.CQRS.Commands.HandleProductWebhook
{
    /// <summary>
    /// Command to process an incoming Shopify product/create webhook payload.
    /// Carries the raw body bytes and HMAC header for verification, plus parsed product fields.
    /// </summary>
    public class HandleProductWebhookCommand : IRequest<HandleProductWebhookResult>
    {
        public byte[] RawBodyBytes { get; set; } = Array.Empty<byte>();
        public string HmacHeader { get; set; } = string.Empty;
    }

    /// <summary>
    /// Result returned after processing a webhook.
    /// </summary>
    public class HandleProductWebhookResult
    {
        public bool AlreadyExists { get; set; }
        public bool HmacValid { get; set; }
        public string Message { get; set; } = string.Empty;
        public int? LocalId { get; set; }
    }
}
