using System;
using Microsoft.AspNet.Builder;
using System.Text;

namespace HelloAgain
{
    public class Startup
    {
        public void Configure(IApplicationBuilder app)
        {
            // For more information on how to configure your application, visit http://go.microsoft.com/fwlink/?LinkID=398940
            app.Run(async context =>
            {
                context.Response.ContentType = "text/plain";
                var buffer = Encoding.UTF8.GetBytes("Hello Firefly!");
                context.Response.ContentLength = buffer.Length;                
                await context.Response.Body.WriteAsync(buffer, 0, buffer.Length);
            });
        }
    }
}
