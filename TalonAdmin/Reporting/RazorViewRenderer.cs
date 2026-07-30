using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;

namespace TalonAdmin.Reporting
{
    /// <summary>
    /// Renders a Razor view to a string. This replaces RazorEngine, which has no supported
    /// build for current .NET; the report templates are ordinary Razor views now, so the
    /// framework's own view engine can compile them.
    /// </summary>
    public class RazorViewRenderer
    {
        private readonly IRazorViewEngine _viewEngine;
        private readonly ITempDataProvider _tempDataProvider;
        private readonly IServiceProvider _serviceProvider;

        public RazorViewRenderer(
            IRazorViewEngine viewEngine,
            ITempDataProvider tempDataProvider,
            IServiceProvider serviceProvider)
        {
            _viewEngine = viewEngine;
            _tempDataProvider = tempDataProvider;
            _serviceProvider = serviceProvider;
        }

        public async Task<string> RenderAsync<TModel>(string viewPath, TModel model)
        {
            var actionContext = new ActionContext(
                new DefaultHttpContext { RequestServices = _serviceProvider },
                new RouteData(),
                new ActionDescriptor());

            var result = _viewEngine.GetView(executingFilePath: null, viewPath: viewPath, isMainPage: true);
            if (!result.Success)
            {
                var searched = string.Join(Environment.NewLine, result.SearchedLocations);
                throw new InvalidOperationException(
                    $"Report view '{viewPath}' was not found. Searched:{Environment.NewLine}{searched}");
            }

            await using var writer = new StringWriter();

            var viewData = new ViewDataDictionary<TModel>(
                new EmptyModelMetadataProvider(),
                new ModelStateDictionary())
            {
                Model = model
            };

            var viewContext = new ViewContext(
                actionContext,
                result.View,
                viewData,
                new TempDataDictionary(actionContext.HttpContext, _tempDataProvider),
                writer,
                new HtmlHelperOptions());

            await result.View.RenderAsync(viewContext);

            return writer.ToString();
        }
    }
}
