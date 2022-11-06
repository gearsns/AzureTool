using CefSharp.Handler;
using CefSharp.WinForms;
using CefSharp;
using WebAuto.Handlers;

namespace AzureaTool
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();

            //
            var cef_cache_uri = new Uri($"{System.Windows.Forms.Application.UserAppDataPath}\\..");
            //
            CefSettings settings = new()
            {
                MultiThreadedMessageLoop = true,
                ExternalMessagePump = false,
                AcceptLanguageList = "ja-JP",
                CachePath = $"{cef_cache_uri.AbsolutePath}\\cache"
            };

            settings.RegisterScheme(new CefCustomScheme
            {
                SchemeName = "https",
                SchemeHandlerFactory = new VirtualHostSchemeHandlerFactory(),
                IsSecure = true
            });
            settings.RegisterScheme(new CefCustomScheme
            {
                SchemeName = "http",
                SchemeHandlerFactory = new VirtualHostSchemeHandlerFactory()
            });
            CefSharp.Cef.Initialize(settings, performDependencyCheck: false);
            Application.Run(new Form1());
        }
    }
}