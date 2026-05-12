using System.Security.Cryptography;
using System.Text;

namespace ShopifyProductSync.Services
{
    /// <summary>
    /// Handles Shopify webhook HMAC verification.
    /// 
    /// How HMAC verification works:
    /// 1. Shopify sends a header: X-Shopify-Hmac-Sha256
    /// 2. That header contains a Base64-encoded HMAC-SHA256 hash
    /// 3. The hash is computed using your Shopify API Secret Key + the raw request body
    /// 4. We compute the same hash on our side and compare
    /// 5. If they match → webhook is genuine from Shopify
    /// 6. If they don't match → reject the request (could be fake/tampered)
    /// 
    /// WHERE TO PUT THE SECRET KEY:
    /// In appsettings.json under "Shopify:ApiSecretKey"
    /// You can find this key in your Shopify Partner Dashboard:
    ///   Apps → Your App → API credentials → API secret key
    /// </summary>
    public class ShopifyWebhookService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<ShopifyWebhookService> _logger;

        public ShopifyWebhookService(IConfiguration configuration, ILogger<ShopifyWebhookService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>
        /// Verifies the HMAC signature sent by Shopify.
        /// Returns true if valid, false if invalid.
        /// </summary>
        /// <param name="requestBody">The raw request body bytes (must be raw, not parsed)</param>
        /// <param name="shopifyHmacHeader">The value of X-Shopify-Hmac-Sha256 header</param>
        public bool IsValidWebhook(byte[] requestBody, string shopifyHmacHeader)
        {
            var apiSecretKey = _configuration["Shopify:ApiSecretKey"];

            if (string.IsNullOrEmpty(apiSecretKey))
            {
                _logger.LogWarning("Shopify:ApiSecretKey is not configured. Skipping HMAC verification.");
                // If no secret key is configured, allow the request (useful during development)
                // In production, you should always have the secret key configured
                return true;
            }

            if (string.IsNullOrEmpty(shopifyHmacHeader))
            {
                _logger.LogWarning("X-Shopify-Hmac-Sha256 header is missing.");
                return false;
            }

            try
            {
                // Compute HMAC-SHA256 using the secret key and raw body
                var secretKeyBytes = Encoding.UTF8.GetBytes(apiSecretKey);

                using var hmac = new HMACSHA256(secretKeyBytes);
                var computedHashBytes = hmac.ComputeHash(requestBody);
                var computedHash = Convert.ToBase64String(computedHashBytes);

                // Compare our computed hash with Shopify's hash
                // Use constant-time comparison to prevent timing attacks
                var isValid = CryptographicEquals(computedHash, shopifyHmacHeader);

                if (!isValid)
                {
                    _logger.LogWarning("HMAC verification failed. Possible tampered webhook.");
                }

                return isValid;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during HMAC verification.");
                return false;
            }
        }

        /// <summary>
        /// Constant-time string comparison to prevent timing attacks.
        /// Normal string comparison can leak information about how many characters matched.
        /// </summary>
        private static bool CryptographicEquals(string a, string b)
        {
            if (a.Length != b.Length)
                return false;

            var result = 0;
            for (int i = 0; i < a.Length; i++)
            {
                result |= a[i] ^ b[i];
            }
            return result == 0;
        }
    }
}
