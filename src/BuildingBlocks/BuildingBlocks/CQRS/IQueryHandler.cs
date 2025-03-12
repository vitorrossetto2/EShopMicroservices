using MediatR;

namespace BuildingBlocks.CQRS;

public interface IQueryHandler<in Query, TResponse>
	: IRequestHandler<Query, TResponse>
	where Query : IQuery<TResponse>
	where TResponse : notnull
{
}

