using System;
using Microsoft.AspNet.Builder;

namespace HelloAgain
{
    public class Startup
    {
        public void Configure(IBuilder app)
        {
            // For more information on how to configure your application, visit http://go.microsoft.com/fwlink/?LinkID=398940
            app.Run(async context =>
            {
                context.Response.ContentType = "text/plain";
                context.Response.ContentLength = "Hello Firefly!".Length;
                await context.Response.WriteAsync("Hello Firefly!");
            });
        }
    }
}
