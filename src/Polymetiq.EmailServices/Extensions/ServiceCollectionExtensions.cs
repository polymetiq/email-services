using Microsoft.Extensions.DependencyInjection;
using Polymetiq.EmailServices.Services;
using Polymetiq.Rendering;
using Polymetiq.Rendering.Services;

namespace Polymetiq.EmailServices;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddEmailServices()
        {
            services.AddTransient<IViewRenderService, ViewRenderService>();
            services.AddTransient<IEmailAddressValidator, EmailAddressValidator>();
            services.AddTransient<IMailMessageValidator, MailMessageValidator>();
            services.AddLocalization();
            services.AddTransient<IEmailService, SmtpEmailService>();
            services.AddOptions<SmtpOptions>().BindConfiguration(nameof(SmtpOptions)).Validate(static options => options.ConfigurationExists()).ValidateOnStart();
            return services;
        }
    }
}

