using FluentValidation;
using Microsoft.EntityFrameworkCore;
using OrderFlow.Api.DTOs;
using OrderFlow.Infrastructure.Persistence;

namespace OrderFlow.Api.Validators;

public class CreateOrderRequestValidator : AbstractValidator<CreateOrderRequest>
{
    public CreateOrderRequestValidator(OrderFlowDbContext context)
    {
        RuleFor(x => x.ClienteNombre)
            .NotEmpty().WithMessage("El nombre del cliente no puede estar vacío.")
            .MaximumLength(100).WithMessage("El nombre del cliente no puede superar los 100 caracteres.");

        RuleFor(x => x.Cantidad)
            .InclusiveBetween(1, 100).WithMessage("La cantidad debe estar entre 1 y 100.");

        RuleFor(x => x.Sku)
            .NotEmpty().WithMessage("El SKU es obligatorio.")
            .MustAsync(async (sku, cancellation) =>
            {
                return await context.Stocks.AnyAsync(s => s.Sku == sku, cancellation);
            })
            .WithMessage(x => $"El SKU '{x.Sku}' no existe en el catálogo.");
    }
}
