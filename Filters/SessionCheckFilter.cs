using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using StudentPortal.Models;

namespace StudentPortal.Filters
{
    public class SessionCheckFilter : IActionFilter
    {
        private readonly StudentLoginContext _context;

        // The DI system will provide the DB context here
        public SessionCheckFilter(StudentLoginContext context)
        {
            _context = context;
        }

        public void OnActionExecuting(ActionExecutingContext context)
        {
            var session = context.HttpContext.Session;
            string controller = context.RouteData.Values["controller"]?.ToString();
            string action = context.RouteData.Values["action"]?.ToString();

            if (action == "Login" || action == "Register" || (controller == "Candidate" && action == "HandleAction"))
            {
                return;
            }
            int? userId = session.GetInt32("UserId");
            string sessionGuid = session.GetString("UserSessionGuid");
            string storedUserAgent = session.GetString("UserAgent");
            string currentUserAgent = context.HttpContext.Request.Headers["User-Agent"].ToString();
            if (userId == null || string.IsNullOrEmpty(sessionGuid))
            {
                RedirectToLogin(context);
                return;
            }
            if (storedUserAgent != currentUserAgent)
            {
                session.Clear();
                RedirectToLogin(context);
                return;
            }
            var user = _context.Candidates
                .Where(u => u.CandidateId == userId)
                .Select(u => new { u.CurrentSessionId })
                .FirstOrDefault();
            if (user == null || user.CurrentSessionId != sessionGuid)
            {
                session.Clear();
                RedirectToLogin(context);
            }
        }

        private void RedirectToLogin(ActionExecutingContext context)
        {
            if (context.HttpContext.Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                context.Result = new JsonResult(new { success = false, message = "Session expired", redirectUrl = "/Candidate/Login" });
            }
            else
            {
                context.Result = new RedirectToActionResult("Login", "Candidate", null);
            }
        }

        public void OnActionExecuted(ActionExecutedContext context) { }
    }
}