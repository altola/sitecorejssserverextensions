using Sitecore.JavaScriptServices.Configuration;
using Sitecore.JavaScriptServices.ViewEngine.LayoutService.Pipelines.GetLayoutServiceContext;
using Sitecore.LayoutService.ItemRendering.Pipelines.GetLayoutServiceContext;

namespace Altola.JssBootcamp.SitecoreJssServerExtensions.Pipelines.GetLayoutServiceContext
{
    public class MembershipProcessor : JssGetLayoutServiceContextProcessor
    {
        public MembershipProcessor(IConfigurationResolver configurationResolver) : base(configurationResolver)
        {
        }

        protected override void DoProcess(GetLayoutServiceContextArgs args, AppConfiguration application)
        {
            args.ContextData.Add("membership", new
            {
                level = "Silver",
                authenticated = Sitecore.Context.IsLoggedIn
            });
        }
    }
}