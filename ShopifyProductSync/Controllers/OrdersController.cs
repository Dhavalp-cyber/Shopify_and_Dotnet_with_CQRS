using MediatR;
using Microsoft.AspNetCore.Mvc;
using ShopifyProductSync.CQRS.Commands.AddOrderNoteAttributes;
using ShopifyProductSync.CQRS.Commands.FulfillItem;
using ShopifyProductSync.CQRS.Commands.FulfillOrder;
using ShopifyProductSync.CQRS.Queries.GetAllOrders;
using ShopifyProductSync.CQRS.Queries.GetOrderNote;
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
        /// <summary>
        /// Fetches the note and note_attributes for a Shopify order from the local database.
        /// Returns 404 if the order has not been synced locally yet —
        /// use POST /api/orders/{orderId}/note-attributes to create it first.
        /// </summary>
        [HttpGet("{orderId}/note")]
        public async Task<IActionResult> GetOrderNote(string orderId)
        {
            _logger.LogInformation(
                "GET /api/orders/{OrderId}/note", orderId);

            try
            {
                var result = await _mediator.Send(new GetOrderNoteQuery { OrderId = orderId });

                if (result == null)
                    return NotFound(new
                    {
                        message = $"Order '{orderId}' not found locally. " +
                                  "POST to /api/orders/{orderId}/note-attributes to sync it first."
                    });

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching order note — OrderId: {OrderId}", orderId);
                return StatusCode(500, new { message = "Failed to fetch order note: " + ex.Message });
            }
        }

        /// <summary>
        /// Appends note_attributes to a Shopify order and optionally updates the note text.
        /// Additive: existing attributes are preserved; new keys are inserted; existing keys updated.
        /// Syncs the merged result to Shopify via the orderUpdate GraphQL mutation.
        ///
        /// Example request body:
        /// {
        ///   "note": "Handle with care",
        ///   "noteAttributes": [
        ///     { "name": "gift_message", "value": "Happy Birthday!" },
        ///     { "name": "source",       "value": "mobile_app" }
        ///   ]
        /// }
        /// </summary>
        [HttpPost("{orderId}/note-attributes")]
        public async Task<IActionResult> AddOrderNoteAttributes(
            string orderId,
            [FromBody] AddOrderNoteAttributesRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            _logger.LogInformation(
                "POST /api/orders/{OrderId}/note-attributes — AttributeCount: {Count}",
                orderId, request.NoteAttributes.Count);

            try
            {
                var command = new AddOrderNoteAttributesCommand
                {
                    OrderId = orderId,
                    Note = request.Note,
                    NoteAttributes = request.NoteAttributes
                };

                var result = await _mediator.Send(command);

                return Ok(new
                {
                    message = result.Message,
                    order = result.Order
                });
            }
            catch (InvalidOperationException ex)
            {
                // Shopify userErrors — e.g. order not found in Shopify, invalid attributes
                _logger.LogWarning(ex,
                    "Shopify rejected note-attributes update — OrderId: {OrderId}", orderId);
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error updating order note attributes — OrderId: {OrderId}", orderId);
                return StatusCode(500, new { message = "Failed to update note attributes: " + ex.Message });
            }
        }
    }
}
