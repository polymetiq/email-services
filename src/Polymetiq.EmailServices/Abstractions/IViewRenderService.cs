using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;

namespace Polymetiq.Rendering;

public interface IViewRenderService
{
    public Task<string> RenderToStringAsync<TModel>(
        string viewName,
        TModel model,
        bool partial = false,
        ActionContext? actionContext = null,
        ViewDataDictionary? viewDictionary = null,
        ITempDataDictionary? tempDictionary = null);
}

public static class ViewRenderServiceExtensions
{
    extension(IViewRenderService viewRenderService)
    {
        /// <summary>
        /// Render a view that copies the context and dictionaries from the current page model, but renders a view using a custom model.
        /// </summary>
        /// <typeparam name="TModel">The type of the custom model.</typeparam>
        /// <param name="viewRenderService">The view rendering service.</param>
        /// <param name="pageModel">The RazorPages page model.</param>
        /// <param name="viewName">The name of the Razor view.</param>
        /// <param name="model">The custom model for the view.</param>
        /// <param name="partial">If <c>true</c>, includes Razor Partials in the search.</param>
        /// <returns></returns>
        public async Task<string> RenderToStringAsync<TModel>(PageModel pageModel, string viewName, TModel model, bool partial)
        {
            var services = pageModel.PageContext.HttpContext!.RequestServices;
            var metadataProvider = services.GetRequiredService<IModelMetadataProvider>() ?? new EmptyModelMetadataProvider();

            // Build a fresh ViewDataDictionary<TModel> so metadata.Type == typeof(TModel)
            var viewData = new ViewDataDictionary<TModel>(metadataProvider, pageModel.ModelState)
            {
                Model = model
            };

            foreach (var key in pageModel.ViewData.Keys)
            {
                // `RenderToStringAsync` implementation is expected to already override the `Model` key.
                if (string.Equals(key, nameof(ViewDataDictionary.Model), StringComparison.OrdinalIgnoreCase))
                    continue;

                viewData[key] = pageModel.ViewData[key];
            }

            return await viewRenderService.RenderToStringAsync(viewName, model, partial, pageModel.PageContext, viewData, pageModel.TempData);
        }

        /// <inheritdoc cref="RenderToStringAsync{TModel}(IViewRenderService, PageModel, string, TModel, bool)"/>
        public Task<string> RenderToStringAsync<TModel>(PageModel pageModel, string viewName, TModel model)
            => RenderToStringAsync(viewRenderService, pageModel, viewName, model, false);

        /// <summary>
        /// Render a view that implicitly uses the current page model.
        /// </summary>
        /// <typeparam name="TModel"></typeparam>
        /// <param name="viewRenderService">The view rendering service.</param>
        /// <param name="pageModel">The RazorPages page model.</param>
        /// <param name="viewName">The name of the Razor view.</param>
        /// <param name="partial">If <c>true</c>, includes Razor Partials in the search.</param>
        /// <returns></returns>
        public async Task<string> RenderToStringAsync(PageModel pageModel, string viewName, bool partial)
        {
            return await viewRenderService.RenderToStringAsync(viewName, pageModel, partial, pageModel.PageContext, pageModel.ViewData, pageModel.TempData);
        }

        /// <inheritdoc cref="RenderToStringAsync(IViewRenderService, PageModel, string, bool)"/>
        public Task<string> RenderToStringAsync(PageModel pageModel, string viewName)
            => RenderToStringAsync(viewRenderService, pageModel, viewName, false);
    }
}
