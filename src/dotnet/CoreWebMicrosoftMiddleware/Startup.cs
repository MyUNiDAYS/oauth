using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;

namespace CoreWebMicrosoftMiddleware
{
	public sealed class Startup
	{
		public IConfiguration Configuration { get; }

		public Startup(IConfiguration configuration)
		{
			Configuration = configuration;
		}

		// This method gets called by the runtime. Use this method to add services to the container.
		public void ConfigureServices(IServiceCollection services)
		{
			services.Configure<CookiePolicyOptions>(options =>
			{
				// This lambda determines whether user consent for non-essential cookies is needed for a given request.
				options.CheckConsentNeeded = context => true;
				options.MinimumSameSitePolicy = SameSiteMode.None;
			});

			services.Configure<IISServerOptions>(options =>
			{
				options.AutomaticAuthentication = false;
			});

			services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_3_0);

			services.AddAuthentication(options =>
				{
					options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
					options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
					options.DefaultChallengeScheme = "UNiDAYS";
				})
				.AddCookie()
				.AddOAuth("UNiDAYS", options =>
				{
					options.ClientId = Configuration["UNiDAYS:ClientId"];
					options.ClientSecret = Configuration["UNiDAYS:ClientSecret"];
					options.CallbackPath = new PathString(Configuration["UNiDAYS:ReturnUrl"]);

					options.AuthorizationEndpoint = $"{Configuration["UNiDAYS:OpenIdServer"]}/oauth/authorize";
					options.TokenEndpoint = $"{Configuration["UNiDAYS:OpenIdServer"]}/oauth/token";
					options.UserInformationEndpoint = $"{Configuration["UNiDAYS:OpenIdServer"]}/oauth/userinfo";

					options.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "sub");
					options.Scope.Add("openid");
					options.Scope.Add("name");
					options.Scope.Add("email");
					options.Scope.Add("verification");

					options.Events = new OAuthEvents
					{
						OnCreatingTicket = async context =>
						{
							var request = new HttpRequestMessage(HttpMethod.Get, context.Options.UserInformationEndpoint);
							request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
							request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", context.AccessToken);

							var response = await context.Backchannel.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, context.HttpContext.RequestAborted);
							response.EnsureSuccessStatusCode();

							var user = JsonDocument.Parse(await response.Content.ReadAsStringAsync()); //Do something with the scope information here

							context.RunClaimActions(user.RootElement);
						}
					};
				});
		}

		// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
		public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
		{
			app.UseDeveloperExceptionPage();

			app.UseAuthentication();

			app.UseCookiePolicy();

			app.UseStaticFiles();
			app.UseStaticFiles(new StaticFileOptions(new Microsoft.AspNetCore.StaticFiles.Infrastructure.SharedOptions() {
				FileProvider = new PhysicalFileProvider(Path.Combine(Directory.GetCurrentDirectory(), "Images"))
			})
			{				
				RequestPath = "/Images"
			});

			app.UseAuthentication();
			app.UseRouting();

			app.UseEndpoints(endpoints =>
			{
				endpoints.MapRazorPages();
				endpoints.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
			});

		}
	}
}