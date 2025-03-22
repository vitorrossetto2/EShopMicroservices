using Discount.Grpc;

namespace Basket.API.Basket.StoreBasket;

public record StoreBasketCommand(ShoppingCart Cart) : ICommand<StoreBasketResult>;
public record StoreBasketResult(string UserName);

public class StoreBasketCommandValidator : AbstractValidator<StoreBasketCommand>
{
    public StoreBasketCommandValidator()
    {
        RuleFor(x => x.Cart).NotNull().WithMessage("Cart can not be null");
        RuleFor(x => x.Cart.UserName).NotEmpty().WithMessage("UserName is required");
    }
}

public class StoreBasketCommandHandler
    (IBasketRepository repository, DiscountProtoService.DiscountProtoServiceClient client)
    : ICommandHandler<StoreBasketCommand, StoreBasketResult>
{
    public async Task<StoreBasketResult> Handle(StoreBasketCommand command, CancellationToken cancellationToken)
	{
		await ApplyDiscounts(command);

		//var discount = Discount
		await repository.StoreBasket(command.Cart, cancellationToken);

		return new StoreBasketResult(command.Cart.UserName);
	}

	private async Task ApplyDiscounts(StoreBasketCommand command)
	{
		var requestDiscounts = new ListDiscountsRequest();

		requestDiscounts.ProductNames.AddRange(command.Cart.Items.Select(x => x.ProductName));
		var coupons = await client.ListDiscountsAsync(requestDiscounts);

		foreach (var item in command.Cart.Items)
		{
			var coupon = coupons.Coupons.FirstOrDefault(x => x.ProductName == item.ProductName);

			if (coupon != null)
			{
				item.Price -= coupon.Amount;
			}
		}
	}
}
