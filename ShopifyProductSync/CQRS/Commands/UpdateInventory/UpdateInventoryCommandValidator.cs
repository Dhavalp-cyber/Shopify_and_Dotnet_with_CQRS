using FluentValidation;

namespace ShopifyProductSync.CQRS.Commands.UpdateInventory
{
    /// <summary>
    /// FluentValidation validator for UpdateInventoryCommand.
    /// Ensures all required IDs are positive and quantity is non-negative.
    /// </summary>
    public class UpdateInventoryCommandValidator : AbstractValidator<UpdateInventoryCommand>
    {
        public UpdateInventoryCommandValidator()
        {
            RuleFor(x => x.ShopifyProductId)
                .GreaterThan(0).WithMessage("ShopifyProductId must be a positive number.");

            RuleFor(x => x.ShopifyLocationId)
                .GreaterThan(0).WithMessage("ShopifyLocationId must be a positive number.");

            RuleFor(x => x.Quantity)
                .GreaterThanOrEqualTo(0).WithMessage("Quantity must be zero or greater.");
        }
    }
}
