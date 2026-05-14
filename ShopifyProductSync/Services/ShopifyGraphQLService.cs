using System.Text;
using System.Text.Json;
using ShopifyProductSync.DTOs;

namespace ShopifyProductSync.Services
{
   
    public class ShopifyGraphQLService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ShopifyGraphQLService> _logger;

        //Shopify configuration store
        private readonly string _shopUrl;
        private readonly string _accessToken;
        private readonly string _graphqlEndpoint;

        // Shopify allows max 250 products per page in GraphQL
        private const int PageSize = 250;

        public ShopifyGraphQLService(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<ShopifyGraphQLService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;

            _shopUrl = _configuration["Shopify:ShopUrl"]
                ?? throw new InvalidOperationException("Shopify:ShopUrl is not configured.");

            _accessToken = _configuration["Shopify:AccessToken"]
                ?? throw new InvalidOperationException("Shopify:AccessToken is not configured.");

            // Build the GraphQL endpoint URL
            _graphqlEndpoint = $"https://{_shopUrl}/admin/api/2024-01/graphql.json";
        }


        /// Fetches ALL products from Shopify using GraphQl
        public async Task<List<ShopifyProductResponseDto>> GetAllProductsAsync()
        {
            var allProducts = new List<ShopifyProductResponseDto>();
            string? cursor = null;       //  use for next page fetch in GraphQL
            bool hasNextPage = true;// chack if any product still exist or not
            int pageNumber = 1;

            _logger.LogInformation("Starting GraphQL product fetch from Shopify...");

            //still ittration if pages available
            // in one request shopify return maximum 250 product thats why use loop
            while (hasNextPage)
            {
                _logger.LogInformation("Fetching page {Page} from Shopify GraphQL...", pageNumber);

                // Build the GraphQL query with optional cursor for pagination
                var query = BuildProductsQuery(cursor);

                // Call Shopify GraphQL API
                var response = await ExecuteGraphQLAsync(query);

                if (response?.Data?.Products == null)
                {
                    _logger.LogWarning("Empty or null response from Shopify GraphQL on page {Page}.", pageNumber);
                    break;
                }

                var edges = response.Data.Products.Edges;

                // Map each GraphQL product node to our clean response DTO
                foreach (var edge in edges)
                {
                    if (edge.Node == null) continue;

                    var product = MapToResponseDto(edge.Node);
                    allProducts.Add(product);
                }

                _logger.LogInformation("Page {Page}: fetched {Count} products. Total so far: {Total}",
                    pageNumber, edges.Count, allProducts.Count);

                // Check if there are more pages
                var pageInfo = response.Data.Products.PageInfo;
                hasNextPage = pageInfo?.HasNextPage ?? false;

                // Get the cursor of the last item — used to fetch the next page
                cursor = pageInfo?.EndCursor;

                pageNumber++;
            }

            _logger.LogInformation("GraphQL fetch complete. Total products fetched: {Total}", allProducts.Count);

            return allProducts;
        }

        /// <summary>
        /// Fetches a single product from Shopify by its numeric Shopify Product ID using GraphQL.
        /// Shopify GraphQL requires the ID in GID format: "gid://shopify/Product/1234567890"
        /// Returns null if the product is not found.
        /// </summary>
        public async Task<ShopifyProductResponseDto?> GetProductByShopifyIdAsync(long shopifyProductId)
        {
            _logger.LogInformation("Fetching product {ShopifyId} from Shopify via GraphQL.", shopifyProductId);

            // Shopify GraphQL needs the GID format
            var gid = $"gid://shopify/Product/{shopifyProductId}";

            // Escape the GID for embedding in the query string
            var query = $$"""
            {
                "query": "{ product(id: \"{{gid}}\") { id title vendor status productType createdAt updatedAt variants(first: 1) { edges { node { price } } } } }"
            }
            """;

            var response = await ExecuteGraphQLSingleAsync(query);

            if (response?.Data?.Product == null)
            {
                _logger.LogWarning("Product {ShopifyId} not found in Shopify.", shopifyProductId);
                return null;
            }

            return MapToResponseDto(response.Data.Product);
        }

    

        private string BuildProductsQuery(string? cursor)
        {
            // Build the "after" argument only when we have a cursor (page 2+)
            var afterArg = cursor != null ? $", after: \\\"{cursor}\\\"" : "";

            return $$"""
            {
                "query": "{ products(first: {{PageSize}}{{afterArg}}) { edges { cursor node { id title vendor status productType createdAt updatedAt variants(first: 1) { edges { node { price } } } } } pageInfo { hasNextPage endCursor } } }"
            }
            """;
        }

        /// <summary>
        /// Sends the GraphQL query to Shopify and returns the parsed response.
        /// Used for fetching a LIST of products (all products endpoint).
        /// </summary>
        private async Task<ShopifyGraphQLResponse?> ExecuteGraphQLAsync(string queryJson)
        {
            var responseBody = await SendGraphQLRequestAsync(queryJson);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<ShopifyGraphQLResponse>(responseBody, options);
        }

        /// <summary>
        /// Sends the GraphQL query to Shopify and returns the parsed response.
        /// Used for fetching a SINGLE product by ID.
        /// </summary>
        private async Task<ShopifyGraphQLSingleResponse?> ExecuteGraphQLSingleAsync(string queryJson)
        {
            var responseBody = await SendGraphQLRequestAsync(queryJson);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<ShopifyGraphQLSingleResponse>(responseBody, options);
        }

        /// <summary>
        /// Core HTTP method — sends the raw GraphQL JSON body to Shopify and returns the response string.
        /// </summary>
        private async Task<string> SendGraphQLRequestAsync(string queryJson)
        {
            var client = _httpClientFactory.CreateClient("ShopifyGraphQL");

            var request = new HttpRequestMessage(HttpMethod.Post, _graphqlEndpoint);
            request.Headers.Add("X-Shopify-Access-Token", _accessToken);
            request.Content = new StringContent(queryJson, Encoding.UTF8, "application/json");

            var httpResponse = await client.SendAsync(request);

            if (!httpResponse.IsSuccessStatusCode)
            {
                var errorBody = await httpResponse.Content.ReadAsStringAsync();
                _logger.LogError("Shopify GraphQL returned {StatusCode}: {Body}", httpResponse.StatusCode, errorBody);
                throw new Exception($"Shopify GraphQL error {httpResponse.StatusCode}: {errorBody}");
            }

            return await httpResponse.Content.ReadAsStringAsync();
        }

        /// <summary>
        /// Maps a Shopify GraphQL product node to our clean response DTO.
        /// Handles:
        ///   - Parsing the numeric ID from Shopify's GID format: "gid://shopify/Product/1234567890"
        ///   - Extracting price from the first variant
        ///   - Lowercasing the status (Shopify GraphQL returns UPPERCASE)
        /// </summary>
        private ShopifyProductResponseDto MapToResponseDto(ShopifyGraphQLProduct node)
        {
            // Parse numeric ID from "gid://shopify/Product/1234567890"
            long shopifyId = 0;
            if (!string.IsNullOrEmpty(node.Gid))
            {
                var lastSlash = node.Gid.LastIndexOf('/');
                if (lastSlash >= 0)
                    long.TryParse(node.Gid[(lastSlash + 1)..], out shopifyId);
            }

            // Get price from first variant
            decimal price = 0;
            var firstVariant = node.Variants?.Edges?.FirstOrDefault()?.Node;
            if (firstVariant != null)
                decimal.TryParse(firstVariant.Price, out price);

            return new ShopifyProductResponseDto
            {
                ShopifyProductId = shopifyId,
                Title = node.Title,
                Vendor = node.Vendor,
                // Shopify GraphQL returns "ACTIVE" — convert to lowercase "active"
                Status = node.Status.ToLower(),
                ProductType = node.ProductType,
                Price = price,
                CreatedAt = node.CreatedAt,
                UpdatedAt = node.UpdatedAt,
                Source = "Shopify"
            };
        }
    }
}
