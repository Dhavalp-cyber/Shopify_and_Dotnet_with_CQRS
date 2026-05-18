using FluentValidation;

namespace ShopifyProductSync.CQRS.Commands.FulfillItem
{
    /// <summary>
    /// FluentValidation validator for FulfillItemCommand.
    /// Validates all required fields before the handler is invoked.
    /// Carrier name validation against AllowedTrackingCarriers is done
    /// inside the handler because it requires configuration access.
    /// </summary>
    public class FulfillItemCommandValidator : AbstractValidator<FulfillItemCommand>
    {
        public FulfillItemCommandValidator()
        {
            RuleFor(x => x.OrderId)
                .GreaterThan(0).WithMessage("OrderId is required and must be a positive number.");

            RuleFor(x => x.Items)
                .NotEmpty().WithMessage("At least one item is required.");

            RuleForEach(x => x.Items).ChildRules(item =>
            {
                item.RuleFor(i => i.FulfillmentOrderId)
                    .GreaterThan(0).WithMessage("FulfillmentOrderId is required and must be a positive number.");

                item.RuleFor(i => i.FulfillmentOrderLineItemId)
                    .GreaterThan(0).WithMessage("FulfillmentOrderLineItemId is required and must be a positive number.");

                item.RuleFor(i => i.Quantity)
                    .GreaterThan(0).WithMessage("Quantity must be greater than 0.");
            });

            RuleFor(x => x.TrackingNumber)
                .NotEmpty().WithMessage("TrackingNumber is required.");

            RuleFor(x => x.ShippingCarrierName)
                .NotEmpty().WithMessage("ShippingCarrierName is required.");
        }
    }
}
