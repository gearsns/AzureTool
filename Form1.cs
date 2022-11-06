using CefSharp;
using CefSharp.JavascriptBinding;
using System.Reflection;

namespace AzureaTool
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();

            chromiumWebBrowser1.IsBrowserInitializedChanged += ChromiumWebBrowser1_IsBrowserInitializedChanged;
            chromiumWebBrowser1.KeyboardHandler = new KeyboardHandler();
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        }
        class KeyboardHandler : IKeyboardHandler
        {
            bool IKeyboardHandler.OnKeyEvent(IWebBrowser chromiumWebBrowser, IBrowser browser, KeyType type, int windowsKeyCode, int nativeKeyCode, CefEventFlags modifiers, bool isSystemKey)
            {
                return false;
            }

            bool IKeyboardHandler.OnPreKeyEvent(IWebBrowser chromiumWebBrowser, IBrowser browser, KeyType type, int windowsKeyCode, int nativeKeyCode, CefEventFlags modifiers, bool isSystemKey, ref bool isKeyboardShortcut)
            {
                if (type == KeyType.RawKeyDown)
                {
                    // VK_F12キー押下
                    if (windowsKeyCode == (int)Keys.F12 && modifiers == CefEventFlags.None)
                    {
                        // 開発者ツールを表示する
                        browser.ShowDevTools();
                        return true;
                    }
                }
                return false;
            }
        }
        public class AzureJavascriptNameConverter : IJavascriptNameConverter
        {
            string IJavascriptNameConverter.ConvertReturnedObjectPropertyAndFieldToNameJavascript(MemberInfo memberInfo)
            {
                return memberInfo.Name;
            }

            string IJavascriptNameConverter.ConvertToJavascript(MemberInfo memberInfo)
            {
                return memberInfo.Name;
            }
        }
        private void ChromiumWebBrowser1_IsBrowserInitializedChanged(object? sender, EventArgs e)
        {
            var eventObject = new ScriptedMethodsBoundObject();
            eventObject.AddLog += AddLog;
            chromiumWebBrowser1.JavascriptObjectRepository.NameConverter = new AzureJavascriptNameConverter();
            chromiumWebBrowser1.JavascriptObjectRepository.Register("AzureApi", eventObject, BindingOptions.DefaultBinder);
            chromiumWebBrowser1.LoadUrl("https://gears.azure.app");
        }

        private void AddLog(string mes)
        {
            Invoke(() =>
            {
                textBoxLog.Text += mes;
                textBoxLog.Text += "\r\n";
            });
        }
    }
}