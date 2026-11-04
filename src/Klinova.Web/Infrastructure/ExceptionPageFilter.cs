using Klinova.Application.Common;
using Klinova.Domain.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace Klinova.Web.Infrastructure;

/// <summary>
/// Translates application exceptions into user-friendly responses:
/// missing records become 404, and business rule violations raised by a form
/// post are shown as an alert on the page the user came from.
/// </summary>
public sealed class ExceptionPageFilter(ITempDataDictionaryFactory tempDataFactory) : IAsyncPageFilter
{
    public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context) => Task.CompletedTask;

    public async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        var executed = await next();
        switch (executed.Exception)
        {
            case NotFoundException:
                executed.Result = new NotFoundResult();
                executed.ExceptionHandled = true;
                break;

            case DomainException domain when !HttpMethods.IsGet(context.HttpContext.Request.Method):
                var tempData = tempDataFactory.GetTempData(context.HttpContext);
                tempData[Alerts.ErrorKey] = domain.Message;
                var request = context.HttpContext.Request;
                executed.Result = new LocalRedirectResult(request.PathBase + request.Path + request.QueryString);
                executed.ExceptionHandled = true;
                break;
        }
    }
}
