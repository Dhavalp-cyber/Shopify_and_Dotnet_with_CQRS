namespace ShopifyProductSync.Configuration
{
    /// <summary>
    /// Strongly-typed configuration class for Shopify settings.
    /// Bound from the "Shopify" section in appsettings.json.
    /// </summary>
    public class ShopifySettings
    {
        public const string SectionName = "Shopify";

        public string ShopUrl { get; set; } = string.Empty;
        public string AccessToken { get; set; } = string.Empty;
        public string ApiSecretKey { get; set; } = string.Empty;
        public string ApiVersion { get; set; } = "2026-01";

        /// <summary>
        /// List of allowed shipping carrier names.
        /// Loaded from appsettings.json → Shopify:AllowedTrackingCarriers.
        /// Carrier validation uses this list — no hardcoding in code.
        /// </summary>
        public List<string> AllowedTrackingCarriers { get; set; } = new();
    }
}
