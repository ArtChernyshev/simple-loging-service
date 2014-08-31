using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Web;
using System.Web.Mvc;
using DataAccess;
using Funq;
using ServiceStack;
using ServiceStack.Mvc;
using MvcEmptyWebApp1.ServiceInterface;


namespace MvcEmptyWebApp1
{
	public class AppHost : AppHostBase
	{
		/// <summary>
		/// Default constructor.
		/// Base constructor requires a name and assembly to locate web service classes. 
		/// </summary>
		public AppHost()
			: base("MvcEmptyWebApp1", typeof(MyServices).Assembly)
		{
			// System.Data.SQLite.dll сама может определить нативную сборку какой разрядности ей подгрузить для использования
			// из папки x64 или x86. Но так как ASP.NET-сайты при выполнении грузят каждую сборку в отдельную временную папку, то 
			// рядом с нашей сборкой DataAccess.dll не оказывается нативных SQLite сборок. Для разрешения таких ситуаций можно установить
			// переменную среды PreLoadSQLite_BaseDirectory и указать где нужно искать нативные сборки.
			// http://system.data.sqlite.org/index.html/artifact/0c51bf87d4a40dc4ea37d1fbee71daa13d9788df
			string path = Assembly.GetAssembly(typeof(SqliteTileStorage)).CodeBase.Substring(8); // путь до изначального расположения сборок сервиса без префикса file:////
			Environment.SetEnvironmentVariable("PreLoadSQLite_BaseDirectory", Path.GetDirectoryName(path));
		}

		/// <summary>
		/// Application specific configuration
		/// This method should initialize any IoC resources utilized by your web service classes.
		/// </summary>
		/// <param name="container"></param>
		public override void Configure(Container container)
		{
			SetConfig(new HostConfig
			{
				HandlerFactoryPath = "api",
			});
			//Config examples
			//this.AddPlugin(new PostmanFeature());
			//this.AddPlugin(new CorsFeature());

			//Set MVC to use the same Funq IOC as ServiceStack
			ControllerBuilder.Current.SetControllerFactory(new FunqControllerFactory(container));
		}
	}
}