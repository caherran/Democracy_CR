using Microsoft.Owin;
using Owin;

[assembly: OwinStartupAttribute(typeof(Democracy_CR.Startup))]
namespace Democracy_CR
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            ConfigureAuth(app);
        }
    }
}
