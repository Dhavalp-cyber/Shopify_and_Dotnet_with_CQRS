using MediatR;
using Microsoft.AspNetCore.Mvc;
using ShopifyProductSync.CQRS.Commands.FulfillItem;
using ShopifyProductSync.CQRS.Commands.FulfillOrder;
using ShopifyProductSync.CQRS.Queries.GetAllOrders;
using ShopifyProductSync.CQRS.Queries.GetUnfulfilledOrders;
using ShopifyProductSync.DTOs;

namespace ShopifyProductSync.Controllers
{
    /// <summary>
    /// Handles Shopify order operations.
    ///
    /// This controller contains NO business logic.
    /// All business logic is inside command/query handlers and services.
    /// </summary>
    [ApiController]
    [Route("api/orders")]
    public class OrdersController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<OrdersController> _logger;

        public OrdersController(IMediator mediator, ILogger<OrdersController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        /// <summary>
        /// Fulfills an existing Shopify order with tracking information.
        ///
        /// Example request body:
        /// {
        ///   "orderId": 123456789,
        ///   "trackingNumber": "TRACK123456",
        ///   "shippingCarrierName": "Aramex",
        ///   "notifyCustomer": true
        /// }
        /// </summary>
        [HttpPost("fulfill")]
        public async Task<IActionResult> FulfillOrder([FromBody] FulfillOrderRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            _logger.LogInformation(
                "POST /api/orders/fulfill — OrderId: {OrderId}, Carrier: {Carrier}",
                request.OrderId, request.ShippingCarrierName);

            try
            {
                var command = new FulfillOrderCommand
                {
                    OrderId = request.OrderId,
                    TrackingNumber = request.TrackingNumber,
                    ShippingCarrierName = request.ShippingCarrierName,
                    NotifyCustomer = request.NotifyCustomer
                };

                var result = await _mediator.Send(command);

                // Handle business-level failure responses
                if (result.Message == "Order not found.")
                    return NotFound(new { message = result.Message });

                if (result.Message == "Order already fulfilled." ||
                    result.Message == "Shipping carrier is invalid." ||
                    result.Message == "No open fulfillment order found for this order.")
                    return BadRequest(new { message = result.Message });

                // Success
                return Ok(new
                {
                    message = result.Message,
                    orderId = result.OrderId,
                    fulfillmentId = result.FulfillmentId,
                    trackingNumber = result.TrackingNumber,
                    shippingCarrierName = result.ShippingCarrierName,
                    notifyCustomer = result.NotifyCustomer
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fulfilling order — OrderId: {OrderId}", request.OrderId);
                return StatusCode(500, new { message = "Failed to fulfill order: " + ex.Message });
            }
        }

        /// <summary>
        /// Fetches all Shopify orders that are unfulfilled or partially fulfilled.
        /// Returns ALL line items for each order, including both fulfilled and unfulfilled items.
        /// Each item includes its own fulfillment status based on fulfillableQuantity.
        /// </summary>
        [HttpGet("unfulfilled-with-items")]
        public async Task<IActionResult> GetUnfulfilledOrdersWithItems()
        {
            _logger.LogInformation(
                "GET /api/orders/unfulfilled-with-items — fetching unfulfilled orders from Shopify.");

            try
            {
                var result = await _mediator.Send(new GetUnfulfilledOrdersQuery());

                return Ok(new
                {
                    totalOrders = result.TotalOrders,
                    orders = result.Orders
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching unfulfilled orders from Shopify.");
                return StatusCode(500, new { message = "Failed to fetch unfulfilled orders: " + ex.Message });
            }
        }

        /// <summary>
        /// Fulfills ONE specific line item from a Shopify order.
        /// Uses fulfillmentOrderId and fulfillmentOrderLineItemId to target exactly one item.
        /// Does NOT fulfill the entire order.
        ///
        /// Example request body:
        /// {
        ///   "orderId": 6726812303404,
        ///   "fulfillmentOrderId": 123456789,
        ///   "fulfillmentOrderLineItemId": 222222222,
        ///   "quantity": 1,
        ///   "trackingNumber": "TRACK-002",
        ///   "shippingCarrierName": "DHL",
        ///   "notifyCustomer": true
        /// }
        /// </summary>
        [HttpPost("fulfill-item")]
        public async Task<IActionResult> FulfillItem([FromBody] FulfillItemRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            _logger.LogInformation(
                "POST /api/orders/fulfill-item — OrderId: {OrderId}, " +
                "FulfillmentOrderId: {FoId}, FulfillmentOrderLineItemId: {FoliId}, " +
                "Quantity: {Qty}, Carrier: {Carrier}",
                request.OrderId, request.FulfillmentOrderId,
                request.FulfillmentOrderLineItemId, request.Quantity,
                request.ShippingCarrierName);

            try
            {
                var command = new FulfillItemCommand
                {
                    OrderId = request.OrderId,
                    FulfillmentOrderId = request.FulfillmentOrderId,
                    FulfillmentOrderLineItemId = request.FulfillmentOrderLineItemId,
                    Quantity = request.Quantity,
                    TrackingNumber = request.TrackingNumber,
                    ShippingCarrierName = request.ShippingCarrierName,
                    NotifyCustomer = request.NotifyCustomer
                };

                var result = await _mediator.Send(command);

                // Handle business-level failure responses
                if (result.Message == "Order not found.")
                    return NotFound(new { message = result.Message });

                if (result.Message == "Shipping carrier is invalid." ||
                    result.Message == "Order is already fully fulfilled.")
                    return BadRequest(new { message = result.Message });

                // Success
                return Ok(new
                {
                    message = result.Message,
                    orderId = result.OrderId,
                    fulfillmentId = result.FulfillmentId,
                    trackingNumber = result.TrackingNumber,
                    shippingCarrierName = result.ShippingCarrierName,
                    notifyCustomer = result.NotifyCustomer
                });
            }
            catch (InvalidOperationException ex)
            {
                // Shopify userErrors — item already fulfilled, invalid quantity, etc.
                _logger.LogWarning(ex,
                    "Shopify rejected fulfill-item — OrderId: {OrderId}, " +
                    "FulfillmentOrderLineItemId: {FoliId}",
                    request.OrderId, request.FulfillmentOrderLineItemId);
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error fulfilling item — OrderId: {OrderId}, " +
                    "FulfillmentOrderLineItemId: {FoliId}",
                    request.OrderId, request.FulfillmentOrderLineItemId);
                return StatusCode(500, new { message = "Failed to fulfill item: " + ex.Message });
            }
        }
    }
}
