using Democracy_CR.Models;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web;
using System.Web.Http;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;

namespace Democracy_CR
{
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            Database.SetInitializer(new MigrateDatabaseToLatestVersion<Models.DemocracyContext, Migrations.Configuration>());
            //Database.SetInitializer(new DropCreateDatabaseAlways<Models.DemocracyContext>());
            this.CheckSuperUser(); //Garantizar que siempre esten los dos roles
            AreaRegistration.RegisterAllAreas();
            GlobalConfiguration.Configure(WebApiConfig.Register);
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);
        }

        private void CheckSuperUser()
        {
            //var user = new User
            //{
            //    Address = userView.Address,
            //    FirstName = userView.FirstName,
            //    LastName = userView.LastName,
            //    Grade = userView.Grade,
            //    Group = userView.Group,
            //    Phone = userView.Phone,
            //    Photo = pic == string.Empty ? string.Empty : string.Format("~/Content/Photos/{0}", pic),
            //    UserName = userView.UserName
            //};
            var userContext = new ApplicationDbContext();
            var userManager = new UserManager<ApplicationUser>(new UserStore<ApplicationUser>(userContext));
            var db = new DemocracyContext();

            this.CheckRole("Admin", userContext);
            this.CheckRole("User", userContext);

            var email = "carlosherran@gmail.com";

            var user = db.Users.Where(u => u.UserName.ToLower().Equals(email)).FirstOrDefault();

            if(user == null )
            {
                user = new User
                {
                    Address = "Calle 1 # 1-1",
                    FirstName = "Carlos",
                    LastName = "Herran",
                    Phone = "388 520 11 22",
                    UserName = email,
                    Photo = "~/Content/Photos/CC_94487627_2.jpg",
                };
                db.Users.Add(user);
                db.SaveChanges();
            }

            var userASP = userManager.FindByName(user.UserName);

            if(userASP == null)
            {
                userASP = new ApplicationUser
                {
                    UserName = user.UserName,
                    Email = user.UserName,
                    PhoneNumber = user.Phone,
                };
                userManager.Create(userASP, "Admin123%");
            }


            userManager.AddToRole(userASP.Id, "Admin");
            userManager.AddToRole(userASP.Id, "User");
        }

        private void CheckRole(string roleName, ApplicationDbContext userContext)
        {
            var roleManager = new RoleManager<IdentityRole>(new RoleStore<IdentityRole>(userContext));

            //Check to see if Role Exists, if not create it
            if (!roleManager.RoleExists(roleName))
            {
                roleManager.Create(new IdentityRole(roleName));
            }
        }
    }
}
