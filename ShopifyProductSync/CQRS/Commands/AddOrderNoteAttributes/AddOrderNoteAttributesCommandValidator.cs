using FluentValidation;

namespace ShopifyProductSync.CQRS.Commands.AddOrderNoteAttributes
{
    /// <summary>
    /// FluentValidation validator for AddOrderNoteAttributesCommand.
    /// Validates required fields before the handler is invoked.
    /// </summary>
    public class AddOrderNoteAttributesCommandValidator
        : AbstractValidator<AddOrderNoteAttributesCommand>
    {
        public AddOrderNoteAttributesCommandValidator()
        {
            RuleFor(x => x.OrderId)
                .NotEmpty().WithMessage("OrderId is required.")
                .Must(BeValidOrderId)
                .WithMessage("OrderId must be a positive numeric ID or a valid Shopify GID " +
                             "(e.g. gid://shopify/Order/6726812303404).");

            RuleForEach(x => x.NoteAttributes)
                .ChildRules(attr =>
                {
                    attr.RuleFor(a => a.Name)
                        .NotEmpty().WithMessage("NoteAttribute Name is required.")
                        .MaximumLength(255).WithMessage("NoteAttribute Name must not exceed 255 characters.");

                    attr.RuleFor(a => a.Value)
                        .NotNull().WithMessage("NoteAttribute Value must not be null.")
                        .MaximumLength(5000).WithMessage("NoteAttribute Value must not exceed 5000 characters.");
                });
        }

        private static bool BeValidOrderId(string orderId)
        {
            if (string.IsNullOrWhiteSpace(orderId)) return false;

            // Accept GID format
            if (orderId.StartsWith("gid://", StringComparison.OrdinalIgnoreCase))
            {
                var lastSlash = orderId.LastIndexOf('/');
                return lastSlash >= 0 && long.TryParse(orderId[(lastSlash + 1)..], out var gidVal) && gidVal > 0;
            }

            // Accept plain numeric
            return long.TryParse(orderId, out var numVal) && numVal > 0;
        }
    }
}
