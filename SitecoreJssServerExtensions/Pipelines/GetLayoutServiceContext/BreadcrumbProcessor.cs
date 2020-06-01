using Sitecore;
using Sitecore.Data;
using Sitecore.Data.Items;
using Sitecore.JavaScriptServices.Configuration;
using Sitecore.JavaScriptServices.ViewEngine.LayoutService.Pipelines.GetLayoutServiceContext;
using Sitecore.LayoutService.ItemRendering.Pipelines.GetLayoutServiceContext;
using Sitecore.Links;
using System.Collections.Generic;
using System.Dynamic;

namespace Altola.JssBootcamp.SitecoreJssServerExtensions.Pipelines.GetLayoutServiceContext
{
    public class BreadcrumbProcessor : JssGetLayoutServiceContextProcessor
    {
        public BreadcrumbProcessor(IConfigurationResolver configurationResolver) : base(configurationResolver)
        {
            ExcludeTemplates = new List<string>();
        }

        public List<string> ExcludeTemplates { get; }

        public string LabelField { get; set; }

        protected override void DoProcess(GetLayoutServiceContextArgs args, AppConfiguration application)
        {
            args.ContextData.Add("breadcrumb", GetBreadcrumb(args.RenderedItem, Context.Site));
        }

        public object[] GetBreadcrumb(Item current, Sitecore.Sites.SiteContext site)
        {
            var breadcrumbs = new List<object>();

            while (current != null)
            {
                if (current.Paths.FullPath.TrimEnd('/').Equals(site.RootPath.TrimEnd('/'), System.StringComparison.InvariantCultureIgnoreCase))
                    break;

                if (!ExcludeTemplates.Contains(current.TemplateID.ToString()))
                {
                    var label = ID.TryParse(LabelField, out ID labelFieldId) && current.Fields.Contains(labelFieldId) && !string.IsNullOrWhiteSpace(current[labelFieldId]) ?
                                    current[labelFieldId]
                                    : current.DisplayName;

                    dynamic breadcrumb = new ExpandoObject();
                    breadcrumb.label = label;
                    breadcrumb.url = GetItemUrl(current);
                    breadcrumbs.Add(breadcrumb);
                }

                current = current.Parent;
            }

            breadcrumbs.Reverse();

            return breadcrumbs.ToArray();
        }

        private string GetItemUrl(Item item)
        {
            var options = UrlOptions.DefaultOptions;
            options.SiteResolving = Sitecore.Configuration.Settings.Rendering.SiteResolving;
            options.Site = Context.Site;
            options.AlwaysIncludeServerUrl = false;
            options.LanguageEmbedding = LanguageEmbedding.Always;
            options.Language = Context.Language;
            options.LowercaseUrls = true;
            return LinkManager.GetItemUrl(item, options);
        }
    }
}
