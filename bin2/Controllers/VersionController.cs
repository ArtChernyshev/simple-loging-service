using System;
using System.Reflection;
using System.Web.Http;
using ServiceStack.Mvc;

namespace TileServer.Controllers
{
	public class VersionController : ServiceStackController
	{
		// GET api/version
		public Version Get()
		{
			return Assembly.GetExecutingAssembly().GetName().Version;
		}
	}
}
