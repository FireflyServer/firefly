using System;
using Nancy;

namespace Sandbox
{
    public class MainModule : NancyModule
    {
        public MainModule()
        {
            Get["/"] = o => { return View["hello"]; };
            Get["/posting"] =
                o =>
                {
                    return
                        "<form method='post'><p><input type='textbox' name='Hello' value='world'/><br/><input type='submit' value='go'/></p></form>";
                };
            Post["/posting"] = o =>
            {
                //var data = new byte[1024];
                //var count = Request.Body.Read(data, 0, data.Length);
                return string.Format("Hello {0}!", Request.Form.Hello);
            };

            Get["websockets"] = o => { return View["websockets", Request.Url]; };
        }
    }


    public class AspNetRootSourceProvider : IRootPathProvider
    {
        public string GetRootPath()
        {
            return Environment.CurrentDirectory;
        }
    }
}
