using GraphQL;
using GraphQL.Language.AST;
using Sitecore;
using Sitecore.Abstractions;
using Sitecore.Diagnostics;
using Sitecore.JavaScriptServices.Configuration;
using Sitecore.JavaScriptServices.GraphQL.Helpers;
using Sitecore.JavaScriptServices.ViewEngine.Pipelines.GetRenderingEngineViewBag;
using Sitecore.LayoutService.Configuration;
using Sitecore.LayoutService.ItemRendering;
using Sitecore.Services.GraphQL.Abstractions;
using Sitecore.Services.GraphQL.Hosting;
using Sitecore.Services.GraphQL.Hosting.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Altola.JssBootcamp.SitecoreJssServerExtensions.NavBar.Pipelines.GetRenderingEngineViewBag
{
    public class AddNavBarContent : IGetRenderingEngineViewBagProcessor
    {
        private readonly IPlaceholderRenderingService _placeholderService;
        private readonly IConfiguration _layoutServiceConfiguration;
        private readonly IConfigurationResolver _configurationResolver;
        private readonly BaseLog _log;
        private readonly Dictionary<string, IGraphQLEndpoint> _graphQLEndpoints;
        private readonly IAsyncHelpers _asyncHelpers;

        /// <summary>
        /// The item ID (or path) to statically render into JSS SSR ViewBag
        /// </summary>
        public string Item { get; set; } = "{8A7F47E6-564F-488C-91E6-9E11E2C1EE6A}";

        /// <summary>
        /// The database name to resolve the item from. Defaults to the context database.
        /// </summary>
        public string Database { get; set; }

        public AddNavBarContent(IConfigurationResolver configurationResolver, IGraphQLEndpointManager graphQLEndpointManager, IPlaceholderRenderingService placeholderService, IConfiguration layoutServiceConfiguration, BaseLog log, IAsyncHelpers asyncHelpers)
        {
            Assert.ArgumentNotNull(asyncHelpers, nameof(asyncHelpers));

            _configurationResolver = configurationResolver;
            _placeholderService = placeholderService;
            _layoutServiceConfiguration = layoutServiceConfiguration;
            _log = log;
            _asyncHelpers = asyncHelpers;

            _graphQLEndpoints = graphQLEndpointManager
                .GetEndpoints()
                .ToDictionary(
                    endpoint => endpoint.Url,
                    endpoint => endpoint,
                    StringComparer.OrdinalIgnoreCase
                );
        }

        public void Process(GetRenderingEngineViewBagArgs args)
        {
            Assert.ArgumentNotNull(args, nameof(args));
            Assert.IsNotNull(args.Item, "args.Item is null");

            ProcessGraphQL(args);
        }

        protected void ProcessGraphQL(GetRenderingEngineViewBagArgs args)
        {
            var db = args.Item.Database;

            var item = db.GetItem(Item);
            if (item == null)
            {
                _log.Warn($"Static item {Item} did not exist or no read access in {db.Name} database", this);
                return;
            }

            var query = item["GraphQL Query"];

            if (string.IsNullOrWhiteSpace(query))
            {
                _log.Debug($"No GraphQL query specified in item '{item.ID}', '{item.Paths.FullPath}");
                return;
            }

            var jssConfig = _configurationResolver.ResolveForItem(item);

            if (jssConfig == null)
            {
                _log.Warn($"[JSS] - Item {item.Paths.FullPath} defined a GraphQL query to resolve its data, but it was not within a known JSS app path. The GraphQL query will not be used.", this);
                return;
            }

            if (string.IsNullOrWhiteSpace(jssConfig.GraphQLEndpoint))
            {
                _log.Error($"[JSS] - The JSS app {jssConfig.Name} did not have a graphQLEndpoint set, but item {item.Paths.FullPath} defined a GraphQL query to resolve its data. The GraphQL query will not be used until an endpoint is defined on the app config.", this);
                return;
            }

            if (!_graphQLEndpoints.TryGetValue(jssConfig.GraphQLEndpoint, out var graphQLEndpoint))
            {
                _log.Error($"[JSS] - The JSS app {jssConfig.Name} is set to use GraphQL endpoint {jssConfig.GraphQLEndpoint}, but no GraphQL endpoint was registered with this URL. GraphQL resolution will not be used.", this);
                return;
            }

            var request = new LocalGraphQLRequest { Query = query };
            
            request.LocalVariables.Add("contextItem", Context.Item.ID.Guid.ToString());
            request.LocalVariables.Add("datasource", item.ID.Guid.ToString());

            var executor = graphQLEndpoint.CreateDocumentExecutor();

            // note: executor handles its own error responses internally
            var options = graphQLEndpoint.CreateExecutionOptions(request, !HttpContext.Current.IsCustomErrorEnabled);

            if (options == null) throw new ArgumentException("Endpoint returned null options.");

            var transformResult = graphQLEndpoint.SchemaInfo.QueryTransformer.Transform(request);
            if (transformResult.Errors != null)
            {
                var result = new ExecutionResult
                {
                    Errors = transformResult.Errors
                };
                args.ViewBag.navbar = result.Data;
                return;
            }

            options.Query = transformResult.Document.OriginalQuery;
            options.Document = transformResult.Document;

            if (options.Document.Operations.Any(op => op.OperationType != OperationType.Query))
            {
                throw new InvalidOperationException("Cannot use mutations or subscriptions in a datasource query. Use queries only.");
            }

            using (var tracker = graphQLEndpoint.Performance.TrackQuery(request, options))
            {
                var result = _asyncHelpers.RunSyncWithThreadContext(() => executor.ExecuteAsync(options));

                graphQLEndpoint.Performance.CollectMetrics(graphQLEndpoint.SchemaInfo.Schema, options.Document.Operations, result);

                new QueryErrorLog(new BaseLogAdapter(_log)).RecordQueryErrors(result);
                tracker.Result = result;

                args.ViewBag.navbar = result.Data;
            }
        }

        protected class LocalGraphQLRequest : GraphQLRequest
        {
            public Inputs LocalVariables { get; } = new Inputs();

            public override Inputs GetVariables()
            {
                return LocalVariables;
            }
        }
    }
}