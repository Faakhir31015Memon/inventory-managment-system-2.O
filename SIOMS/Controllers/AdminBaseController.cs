using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Http;

namespace SIOMS.Controllers
{
    public class AdminBaseController : Controller
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            // Check if user is logged in (for production)
            var user = context.HttpContext.Session.GetString("AdminUser");
            if (string.IsNullOrEmpty(user))
            {
                // Redirect to login if not authenticated
                context.Result = new RedirectToActionResult("Login", "Admin", null);
            }

            base.OnActionExecuting(context);
        }
    }
}