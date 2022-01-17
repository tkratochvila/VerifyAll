using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Optimization;
using System.Web.Routing;
using System.Web.Security;
using System.Web.SessionState;
using System.Web.Http;
using System.Configuration;
using InterLayerLib;

namespace webApp
{
	public class Global : HttpApplication
	{
		void Application_Start(object sender, EventArgs e)
		{	
			var appDataDirectory = ConfigurationManager.AppSettings["workingDirectory"]; ;
			
			if(!System.IO.Directory.Exists(appDataDirectory))
            {
				System.IO.Directory.CreateDirectory(appDataDirectory);
            }

			System.IO.Directory.SetCurrentDirectory(appDataDirectory);
			WebVerifyServiceManager.init();

			// Code that runs on application startup
			RouteConfig.RegisterRoutes(RouteTable.Routes);
			BundleConfig.RegisterBundles(BundleTable.Bundles);
			webApp.App_Start.WebAPIConfig.register(GlobalConfiguration.Configuration);
		}

		void Session_Start(object sender, EventArgs e)
		{
			//Checker checker = new Checker();
			//Session["checker"] = checker;
		}


	}
}