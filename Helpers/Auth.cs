using System.Linq;
using System.Web;
using System.Web.Helpers;
using System.Web.Mvc;
using System.Web.Routing;
using DatBanQuanAn.Models;

namespace DatBanQuanAn.Helpers
{
    public class RoleAttribute : AuthorizeAttribute
    {
        readonly string[] roles;
        public RoleAttribute(params string[] roles) { this.roles = roles; }
        protected override bool AuthorizeCore(HttpContextBase c)
        {
            var r = c.Session["Role"] as string;
            return r != null && roles.Contains(r);
        }
        protected override void HandleUnauthorizedRequest(AuthorizationContext f)
        {
            f.Result = new RedirectToRouteResult(new RouteValueDictionary(
                new { controller = "Account", action = "Login", returnUrl = f.HttpContext.Request.RawUrl }));
        }
    }

    public class CsrfFilter : IAuthorizationFilter
    {
        public void OnAuthorization(AuthorizationContext c)
        {
            if (c.HttpContext.Request.HttpMethod == "POST") AntiForgery.Validate();
        }
    }

    public static class Cur
    {
        public static int? UserId { get { var o = HttpContext.Current.Session["UserId"]; return o == null ? (int?)null : (int)o; } }
        public static string Role { get { return HttpContext.Current.Session["Role"] as string; } }
        public static bool IsStaff { get { return Role == "PhucVu" || Role == "QuanLy"; } }
    }

    public static class Cfg
    {
        public static int Int(string key, int def)
        {
            using (var db = new DatBanQuanAnEntities())
            {
                var s = db.CauHinhs.Where(c => c.Khoa == key).Select(c => c.GiaTri).FirstOrDefault();
                int v;
                return s != null && int.TryParse(s, out v) ? v : def;
            }
        }
    }
}