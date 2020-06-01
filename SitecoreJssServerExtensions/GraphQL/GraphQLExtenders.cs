using GraphQL;
using GraphQL.Types;
using Newtonsoft.Json;
using Sitecore;
using Sitecore.Data;
using Sitecore.Data.Fields;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.LayoutService.Serialization.FieldSerializers;
using Sitecore.LayoutService.Serialization.ItemSerializers;
using Sitecore.LayoutService.Serialization.Pipelines.GetFieldSerializer;
using Sitecore.Pipelines.ParseDataSource;
using Sitecore.Services.GraphQL.Content;
using Sitecore.Services.GraphQL.Content.GraphTypes;
using Sitecore.Services.GraphQL.Content.GraphTypes.FieldTypes;
using Sitecore.Services.GraphQL.GraphTypes;
using Sitecore.Services.GraphQL.Schemas;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using FieldType = GraphQL.Types.FieldType;

namespace Altola.JssBootcamp.SitecoreJssServerExtensions.GraphQL
{
    public class GraphQLExtenders : SchemaExtender
    {
        public GraphQLExtenders()
        {
            GraphQLExtenders extender = this;

            ExtendTypes((Action<ItemFieldGraphType>)(type => type.Field<JsonGraphType>("stats", "field stats", null, context => extender.GetFieldStats(context.Source), null)));

            ExtendTypes((Action<ItemFieldInterfaceGraphType>)(type => type.Field<JsonGraphType>("stats", "field stats", null, null, null)));
        }

        public override void ExtendQueries(IList<FieldType> queries)
        {
            queries.Add(new FeaturedArticlesQuery());
            queries.Add(new FeaturedArticleQuery());
        }

        protected virtual string GetFieldStats(
          Field field)
        {

            dynamic stats = new ExpandoObject();
            stats.sentiment = 0.4;

            string result = JsonConvert.SerializeObject(stats);
            return result.Substring(result.IndexOf('{'));
        }

        protected class FeaturedArticleQuery : RootFieldType<ItemInterfaceGraphType, Item>
        {
            public FeaturedArticleQuery()
              : base("article", "returns current featured article based on persona.")
            {
                QueryArgument[] queryArgumentArray = new QueryArgument[1];
                var queryArgument = new QueryArgument<NonNullGraphType<StringGraphType>>
                {
                    Name = "persona",
                    Description = "The name of the persona"
                };
                queryArgumentArray[0] = queryArgument;
                Arguments = new QueryArguments(queryArgumentArray);
            }

            protected override Item Resolve(ResolveFieldContext context)
            {
                Database database = this.GetDatabase(context);
                if (database == null)
                {
                    context.Errors.Add(new ExecutionError("Unable to resolve context database. Perhaps no content schema provider registered on this endpoint?"));
                    return null;
                }
                var persona = context.GetArgument<string>("persona", null);
                string path = $"/sitecore/content/Home/Articles/{persona}";
                return string.IsNullOrWhiteSpace(persona) ? null : database.GetItem(path)?.Children.FirstOrDefault();
            }

            protected virtual Database GetDatabase(ResolveFieldContext context)
            {
                if (!(context.UserContext is GraphQLUserContext userContext))
                    return null;
                return userContext.GetContext<ContentSchemaContext>("ContentSchemaProvider", true)?.ContextDatabase;
            }
        }

        protected class FeaturedArticlesQuery : RootFieldType<ListGraphType<ItemInterfaceGraphType>, IEnumerable<Item>>
        {
            public FeaturedArticlesQuery()
              : base("articles", "Featured articles.")
            {
                QueryArgument[] queryArgumentArray = new QueryArgument[1];
                var queryArgument = new QueryArgument<NonNullGraphType<StringGraphType>>
                {
                    Name = "persona",
                    Description = "The name of the persona"
                };
                queryArgumentArray[0] = queryArgument;
                Arguments = new QueryArguments(queryArgumentArray);
            }

            protected override IEnumerable<Item> Resolve(ResolveFieldContext context)
            {
                Database database = this.GetDatabase(context);
                if (database == null)
                {
                    context.Errors.Add(new ExecutionError("Unable to resolve context database. Perhaps no content schema provider registered on this endpoint?"));
                    return null;
                }
                var persona = context.GetArgument<string>("persona", null);
                string path = $"/sitecore/content/Home/Articles/{persona}";
                return string.IsNullOrWhiteSpace(persona) ? null : database.GetItem(path)?.Children;
            }

            protected virtual Database GetDatabase(ResolveFieldContext context)
            {
                if (!(context.UserContext is GraphQLUserContext userContext))
                    return null;
                return userContext.GetContext<ContentSchemaContext>("ContentSchemaProvider", true)?.ContextDatabase;
            }
        }
    }
}