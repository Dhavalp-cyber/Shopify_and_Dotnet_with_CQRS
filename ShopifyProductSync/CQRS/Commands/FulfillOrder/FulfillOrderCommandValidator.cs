using FluentValidation;

namespace ShopifyProductSync.CQRS.Commands.FulfillOrder
{
    /// <summary>
    /// FluentValidation validator for FulfillOrderCommand.
    /// Validates required fields before the handler is invoked.
    /// Note: Carrier name validation against AllowedTrackingCarriers is done
    /// inside the handler because it requires configuration access.
    /// </summary>
    public class FulfillOrderCommandValidator : AbstractValidator<FulfillOrderCommand>
    {
        public FulfillOrderCommandValidator()
        {
            RuleFor(x => x.OrderId)
                .GreaterThan(0).WithMessage("OrderId is required and must be a positive number.");

            RuleFor(x => x.TrackingNumber)
                .NotEmpty().WithMessage("TrackingNumber is required.");

            RuleFor(x => x.ShippingCarrierName)
                .NotEmpty().WithMessage("ShippingCarrierName is required.");
        }
    }
}
