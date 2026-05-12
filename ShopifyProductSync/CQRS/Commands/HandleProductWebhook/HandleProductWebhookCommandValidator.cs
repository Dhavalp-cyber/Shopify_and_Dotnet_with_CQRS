using FluentValidation;

namespace ShopifyProductSync.CQRS.Commands.HandleProductWebhook
{
    /// <summary>
    /// FluentValidation validator for HandleProductWebhookCommand.
    /// Validates that the raw body and HMAC header are present before the handler runs.
    /// </summary>
    public class HandleProductWebhookCommandValidator : AbstractValidator<HandleProductWebhookCommand>
    {
        public HandleProductWebhookCommandValidator()
        {
            RuleFor(x => x.RawBodyBytes)
                .NotNull().WithMessage("Raw body bytes are required.")
                .Must(b => b.Length > 0).WithMessage("Raw body must not be empty.");

            RuleFor(x => x.HmacHeader)
                .NotNull().WithMessage("HMAC header is required.");
        }
    }
}
