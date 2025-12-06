using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Polymetiq.EmailServices;

public static class EmailResultExtensions
{
    extension(EmailResult result)
    {
        public void AddErrorsToModelState(ModelStateDictionary modelState)
        {
            if (result.Succeeded || result.Errors is null)
                return;
            foreach (var kvp in result.Errors)
            {
                var key = kvp.Key;
                var localizedErrors = kvp.Value;

                foreach (var error in localizedErrors)
                {
                    modelState.AddModelError(key, error.Value);
                }
            }
        }
    }
}
