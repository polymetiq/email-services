using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace Polymetiq.Rendering.Services;

public class ViewRenderService : IViewRenderService
{
    private readonly ICompositeViewEngine _viewEngine;
    private readonly ITempDataProvider _tempDataProvider;
    private readonly IServiceProvider _services;
    private readonly ILogger<ViewRenderService> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ViewRenderService(ICompositeViewEngine viewEngine,
        ITempDataProvider tempDataProvider,
        IServiceProvider services,
        ILogger<ViewRenderService> logger,
        IHttpContextAccessor httpContextAccessor)
    {
        _viewEngine = viewEngine;
        _tempDataProvider = tempDataProvider;
        _services = services;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<string> RenderToStringAsync<TModel>(string viewName,
        TModel model,
        bool partial = false,
        ActionContext? actionContext = null,
        ViewDataDictionary? viewDictionary = null,
        ITempDataDictionary? tempDictionary = null)
    {
        var httpContext = _httpContextAccessor.HttpContext ?? new DefaultHttpContext { RequestServices = _services };
        actionContext ??= new ActionContext(httpContext, new RouteData(), new ActionDescriptor());

        if (string.IsNullOrEmpty(viewName))
        {
            viewName = actionContext.ActionDescriptor.DisplayName!;
        }

        using var sw = new StringWriter();
        var viewResult = _viewEngine.FindView(actionContext, viewName, !partial);

        if (viewResult.Success is not true)
        {
            const string key = nameof(viewResult.SearchedLocations);
            var exn = new InvalidOperationException($"Failed to find view '{viewName}'. See Exception.Data for '{key}'.");
            exn.Data.Add(key, viewResult.SearchedLocations.ToArray());
            throw exn;
        }

        viewDictionary ??= new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary());
        viewDictionary.Model = model;

        tempDictionary ??= new TempDataDictionary(actionContext.HttpContext, _tempDataProvider);

        var viewContext = new ViewContext(
            actionContext,
            viewResult.View,
            viewDictionary,
            tempDictionary,
            sw,
            new HtmlHelperOptions()
        );

        await viewResult.View.RenderAsync(viewContext);
        return sw.GetStringBuilder().ToString();
    }
}
