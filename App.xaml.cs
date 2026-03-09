using System.Text;
using System.Windows;

namespace GrandFantasiaINIEditor
{
    public partial class App : Application
    {
        public App()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }
    }
}