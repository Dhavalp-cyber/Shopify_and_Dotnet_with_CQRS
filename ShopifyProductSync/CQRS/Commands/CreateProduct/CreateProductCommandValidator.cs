using FluentValidation;

namespace ShopifyProductSync.CQRS.Commands.CreateProduct
{
    /// <summary>
    /// FluentValidation validator for CreateProductCommand.
    /// Validates all required fields before the handler is invoked.
    /// </summary>
    public class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
    {
        public CreateProductCommandValidator()
        {
            RuleFor(x => x.Title)
                .NotEmpty().WithMessage("Title is required.")
                .MaximumLength(500).WithMessage("Title must not exceed 500 characters.");

            RuleFor(x => x.Vendor)
                .NotEmpty().WithMessage("Vendor is required.")
                .MaximumLength(255).WithMessage("Vendor must not exceed 255 characters.");

            RuleFor(x => x.ProductType)
                .NotEmpty().WithMessage("ProductType is required.")
                .MaximumLength(255).WithMessage("ProductType must not exceed 255 characters.");

            RuleFor(x => x.Status)
                .NotEmpty().WithMessage("Status is required.")
                .Must(s => s == "active" || s == "draft" || s == "archived")
                .WithMessage("Status must be 'active', 'draft', or 'archived'.");

            RuleFor(x => x.Price)
                .GreaterThanOrEqualTo(0).WithMessage("Price must be zero or greater.");
        }
    }
}
