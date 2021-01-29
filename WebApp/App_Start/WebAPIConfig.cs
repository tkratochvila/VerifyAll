using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Http;

namespace webApp.App_Start
{
    public static class WebAPIConfig
    {
        public static void register(HttpConfiguration config)
        {
            config.Routes.MapHttpRoute("defaultapi", "api/{controller}/{id}", new { id = RouteParameter.Optional });
        }
    }
}