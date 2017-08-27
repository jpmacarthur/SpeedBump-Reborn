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

        public ProjectControlSource source;
        public ProjectControlSourceItem item;
        private VersionManager ver = new VersionManager();
        public delegate void NewReportEventHandler(object sender, NewReportEventArgs args);
        public event NewReportEventHandler StatusUpdated;
        public event EventHandler StartTask;
        public event EventHandler EndTask;
        public event EventHandler UpdateUI;

        public Task runAll;
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
            RunAllButton.IsEnabled = false;
            RunButton.IsEnabled = false;
            if (this.StartTask != null)
            {
                this.StartTask(this, new EventArgs());
            }
            DeploymentManager bumper = new DeploymentManager(source, item);
            string actionChoice = "";
            if ((actionDropdown.SelectedItem as ComboBoxItem).Content != null)
            {
                 actionChoice = (actionDropdown.SelectedItem as ComboBoxItem).Content.ToString();
            }
            switch (actionChoice)
            {
                case "Prepare":
                    Task prepare = Task.Factory.StartNew(() => {
                        bumper.Prepare();
                    });
                    Task prepare_ui = prepare.ContinueWith((antecedent) =>
                    {
                        RunAllButton.IsEnabled = true;
                        RunButton.IsEnabled = true;
                        if (this.EndTask != null)
                        {
                            this.EndTask(this, new EventArgs());
                        }
                    },TaskScheduler.FromCurrentSynchronizationContext());
                    break;
                case "Clean":
                    Task clean = Task.Factory.StartNew(() => {
                        bumper.Clean();
                    });
                    Task clean_ui = clean.ContinueWith((antecedent) =>
                    {
                        RunAllButton.IsEnabled = true;
                        RunButton.IsEnabled = true;
                        if (this.EndTask != null)
                        {
                            this.EndTask(this, new EventArgs());
                        }
                    }, TaskScheduler.FromCurrentSynchronizationContext());
                    break;
                case "Bump":
                    string bumpChoice = "unknown";
                    if(majorBump_RB.IsChecked == true) { bumpChoice = "Major"; }
                    else if (minorBump_RB.IsChecked == true) { bumpChoice = "Minor"; }
                    else if (trivialBump_RB.IsChecked == true) { bumpChoice = "Trivial"; }
                    Task<Versioning.Version> bump = Task.Factory.StartNew(() => {
                        return bumper.Bump(bumpChoice);
                    });
                    Task bump_ui = bump.ContinueWith((antecedent) =>
                    {
                        Version = antecedent.Result.getVersion();
                        Timestamp = DateTime.UtcNow;
                        item.Timestamp = Timestamp;
                        source.Save();
                        RunAllButton.IsEnabled = true;
                        RunButton.IsEnabled = true;
                        if (this.EndTask != null)
                        {
                            this.EndTask(this, new EventArgs());
                        }
                    }, TaskScheduler.FromCurrentSynchronizationContext());
                    break;
                case "Build":
                    Task build = Task.Factory.StartNew(() => {
                        Report = bumper.Build();

                    });
                    Task build_ui = build.ContinueWith((antecedent) =>
                    {
                        if (this.StatusUpdated != null)
                        {
                            this.StatusUpdated(this, new NewReportEventArgs(Report, projectLabel.Content.ToString()));
                        }
                        if (this.EndTask != null)
                        {
                            this.EndTask(this, new EventArgs());
                        }
                        RunAllButton.IsEnabled = true;
                        RunButton.IsEnabled = true;
                    }, TaskScheduler.FromCurrentSynchronizationContext());
                    break;
                case "Deploy":
                    Task deploy = Task.Factory.StartNew(() => {
                        bumper.Deploy();
                    });
                    Task deploy_ui = deploy.ContinueWith((antecedent) =>
                    {
                        if (this.EndTask != null)
                        {
                            this.EndTask(this, new EventArgs());
                        }
                        RunAllButton.IsEnabled = true;
                        RunButton.IsEnabled = true;
                    }, TaskScheduler.FromCurrentSynchronizationContext());
                    break;
                default:
                    break;
            }

        }

        private void runAll_BT_Click(object sender, RoutedEventArgs e)
        {
            if (this.UpdateUI != null)
            {
                this.UpdateUI(this, new EventArgs());
            }
            RunAllButton.IsEnabled = false;
            RunButton.IsEnabled = false;
            string bumpChoice = "";
            string pjContent = projectLabel.Content.ToString();
            if (majorBump_RB.IsChecked == true) { bumpChoice = "Major"; }
            else if (minorBump_RB.IsChecked == true) { bumpChoice = "Minor"; }
            else if (trivialBump_RB.IsChecked == true) { bumpChoice = "Trivial"; }
            Task runAll = Task.Factory.StartNew(() =>
            {
                log.Debug("[User Action] " + sender.ToString());
                DeploymentManager bumper = new DeploymentManager(source, item);
                bumper.Prepare();
                bumper.Clean();
                Versioning.Version temp = bumper.Bump(bumpChoice);
                Version = temp.getVersion();
                Timestamp = DateTime.UtcNow;
                item.Timestamp = Timestamp;
                try
                {
                    Report = bumper.Build();
                  /*  if (this.StatusUpdated != null)
                    {
                        this.StatusUpdated(this, new NewReportEventArgs(Report, pjContent));
                    }*/
                }
                catch (Exception ex) { MessageBox.Show(ex.ToString()); }
                bumper.Deploy();
            });
            Task runAll_cont = runAll.ContinueWith((antecedent) =>
            {
                RunAllButton.IsEnabled = true;
                RunButton.IsEnabled = true;
                if (this.UpdateUI != null)
                {
                    this.UpdateUI(this, new EventArgs());
                }
                if (this.StatusUpdated != null)
                {
                    this.StatusUpdated(this, new NewReportEventArgs(Report, pjContent));
                }
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }
    }
}
