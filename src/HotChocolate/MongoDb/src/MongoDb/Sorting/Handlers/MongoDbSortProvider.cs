using System;
using System.Threading.Tasks;
using HotChocolate.Data.Sorting;
using HotChocolate.Language;
using HotChocolate.MongoDb.Data;
using HotChocolate.MongoDb.Data.Sorting;
using HotChocolate.MongoDb.Execution;
using HotChocolate.Resolvers;
using HotChocolate.Types;
using MongoDB.Bson;
using MongoDB.Driver;

namespace HotChocolate.MongoDb.Sorting.Convention.Extensions.Handlers
{
    public class MongoDbSortProvider
        : SortProvider<MongoDbSortVisitorContext>
    {
        public MongoDbSortProvider()
        {
        }

        public MongoDbSortProvider(
            Action<ISortProviderDescriptor<MongoDbSortVisitorContext>> configure)
            : base(configure)
        {
        }

        protected virtual SortVisitor<MongoDbSortVisitorContext, MongoDbSortDefinition>
            Visitor { get; } =
            new SortVisitor<MongoDbSortVisitorContext, MongoDbSortDefinition>();

        public override FieldMiddleware CreateExecutor<TEntityType>(NameString argumentName)
        {
            return next => context => ExecuteAsync(next, context);

            async ValueTask ExecuteAsync(
                FieldDelegate next,
                IMiddlewareContext context)
            {
                MongoDbSortVisitorContext? visitorContext = null;
                IInputField argument = context.Field.Arguments[argumentName];
                IValueNode filter = context.ArgumentLiteral<IValueNode>(argumentName);

                if (filter is not NullValueNode &&
                    argument.Type is ListType listType &&
                    listType.ElementType is SortInputType sortInputType)
                {
                    visitorContext = new MongoDbSortVisitorContext(sortInputType);

                    Visitor.Visit(filter, visitorContext);

                    if (!visitorContext.TryCreateQuery(out MongoDbSortDefinition? order) ||
                        visitorContext.Errors.Count > 0)
                    {
                        context.Result = Array.Empty<TEntityType>();
                        foreach (IError error in visitorContext.Errors)
                        {
                            context.ReportError(error.WithPath(context.Path));
                        }
                    }
                    else
                    {
                        context.LocalContextData =
                            context.LocalContextData.SetItem(
                                nameof(SortDefinition<TEntityType>),
                                order);

                        await next(context).ConfigureAwait(false);

                        if (context.Result is IMongoExecutable executable)
                        {
                            context.Result = executable.WithSorting(order);
                        }
                    }
                }
            }
        }
    }
}
