using Funq;
using ServiceStack;
using LogingService.ServiceInterface;
using ServiceStack.Auth;
using ServiceStack.Caching;

namespace LogingService
{
	public class AppHost : AppHostBase
	{
		/// <summary>
		/// Default constructor.
		/// Base constructor requires a name and assembly to locate web service classes. 
		/// </summary>
		public AppHost()
			: base("LogingService", typeof(MyServices).Assembly)
		{

		}

		/// <summary>
		/// Application specific configuration
		/// This method should initialize any IoC resources utilized by your web service classes.
		/// </summary>
		/// <param name="container"></param>
		public override void Configure(Container container)
		{
			
			AddPlugin(new AuthFeature(() => new AuthUserSession(),
				new IAuthProvider[] {new BasicAuthProvider(), new CredentialsAuthProvider()}));

			Plugins.Add(new RegistrationFeature());
			container.Register<ICacheClient>(new MemoryCacheClient());
			var userRep = new InMemoryAuthRepository();
			container.Register<IUserAuthRepository>(userRep);
		}
	}
}