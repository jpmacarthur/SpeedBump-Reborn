using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.ComponentModel;
using SpeedBump.Versioning;
using log4net;
using SpeedBump.Deployment;
using System.Threading.Tasks;

namespace SpeedBump
{
    /// <summary>
    /// Interaction logic for ProjectControl.xaml
    /// </summary>
    /// 

    public partial class ProjectControl : UserControl
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private ProjectControlSource source;
        private ProjectControlSourceItem item;
        private VersionManager ver = new VersionManager();
        public ProjectControl()
        {
            InitializeComponent();
            DataContext = this;
        }

        public void Reload(ProjectControlSourceItem item, ProjectControlSource source)
        {
            this.source = source;
            this.item = item;
            projectLabel.Content = item.Project;
            Timestamp = item.Timestamp;
            UpdateVersion();

            
        }

        public void UpdateVersion()
        {
            MyFile assembly = ver.OpenAssemblyInfo(source.BaseDir + item.BaseDir +@"\" + item.StageDir);
            Versioning.Version itemVersion = ver.getchildVersion(assembly);
            Version = itemVersion.getVersion();

        }

        private void run_BT_Click(object sender, RoutedEventArgs e)
        {
            log.Debug("[User Action] " + sender.ToString());
            DeploymentManager bumper = new DeploymentManager(source, item);
            string actionChoice = "";
            if ((actionDropdown.SelectedItem as ComboBoxItem).Content != null)
            {
                 actionChoice = (actionDropdown.SelectedItem as ComboBoxItem).Content.ToString();
            }
            switch (actionChoice)
            {
                case "Prepare":
                    bumper.Prepare();
                    break;
                case "Clean":
                    bumper.Clean();
                    break;
                case "Bump":
                    string bumpChoice = "unknown";
                    if(majorBump_RB.IsChecked == true) { bumpChoice = "Major"; }
                    else if (minorBump_RB.IsChecked == true) { bumpChoice = "Minor"; }
                    else if (trivialBump_RB.IsChecked == true) { bumpChoice = "Trivial"; }
                    Versioning.Version temp = bumper.Bump(bumpChoice);
                    Version = temp.getVersion();
                    Timestamp = DateTime.UtcNow;
                    item.Timestamp = Timestamp;
                    source.Save();
                    break;
                case "Build":
                    bumper.Build();
                    break;
                case "Deploy":
                    bumper.Deploy();
                    break;
                default:
                    break;
            }

        }

        private void runAll_BT_Click(object sender, RoutedEventArgs e)
        {
            log.Debug("[User Action] " + sender.ToString());

            string bumpChoice = "";
            DeploymentManager bumper = new DeploymentManager(source, item);
            bumper.Prepare();
            bumper.Clean();
            if (majorBump_RB.IsChecked == true) { bumpChoice = "Major"; }
            else if (minorBump_RB.IsChecked == true) { bumpChoice = "Minor"; }
            else if (trivialBump_RB.IsChecked == true) { bumpChoice = "Trivial"; }
            bumper.Bump(bumpChoice);
            Task<string> t1 = Task.Factory.StartNew(() =>
            {
                bumper.Bump(bumpChoice);
                MyFile assembly = ver.OpenAssemblyInfo(source.BaseDir + item.BaseDir + @"\" + item.StageDir);
                Versioning.Version itemVersion = ver.getchildVersion(assembly);
                return itemVersion.getVersion();
            });
            Task t1_cont = t1.ContinueWith((antecedent) => {
                Version = antecedent.Result;

                }, TaskScheduler.FromCurrentSynchronizationContext()); 
            try
            {

                bumper.Build();
            }
            catch(Exception ex) { MessageBox.Show(ex.ToString()); }
            bumper.Deploy();
        }
    }
}
