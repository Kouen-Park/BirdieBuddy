using Microsoft.AspNetCore.Mvc;

namespace BirdieBuddy.Infrastructure;

public static class ControllerRegistration
{
    public static IMvcBuilder AddBirdieBuddyControllers(this IServiceCollection services) =>
        // AddControllers alone omits the MVC services needed to instantiate this filter.
        services.AddControllersWithViews(options => options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
}
