using System.Configuration;
using System.Data;
using System.Windows;
using SpaceEditor.Data;

namespace SpaceEditor
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private void OnExit(object sender, ExitEventArgs e)
        {
            Settings.Default.Save();
        }
    }

}
