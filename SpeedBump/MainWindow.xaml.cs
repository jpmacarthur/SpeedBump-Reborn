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
using LCP.Common.Json;
using LCP.Common.Logging;
using log4net;
using System.Windows.Automation.Peers;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using SpeedBump.Deployment;
using SpeedBump.Versioning;
using System.Threading;

namespace SpeedBump
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private VersionManager ver = new VersionManager();
        public bool reportsToggle = true;
        public delegate void NewReportEventHandler(object sender, NewReportEventArgs args);
        public delegate void StartTaskEventHandler(object sender, EventArgs args);
        public Dictionary<string, string> reportsHolder = new Dictionary<string, string>();
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public MainWindow()
        {
            Logger.Setup();
            InitializeComponent();
            DataContext = this;
            this.status_BT.ButtonClicked += Status_BT_ButtonClicked;
            Reload();
        }
        private void Status_BT_ButtonClicked(object sender, EventArgs e)
        {
            log.Debug("[USER ACTION] Status Button Clicked");
            status_BT.reports_TC.Items.Clear();
            foreach (ProjectControl child in projectRowsPanel.Children)
            {
                if (child.Report != null)
                {
                    ScrollViewer scroll = new ScrollViewer();
                    scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Visible;
                    TabItem tab = new TabItem();
                    TextBlock reporttext = new TextBlock();
                    reporttext.Text = child.Report;
                    scroll.Content = reporttext;
                    tab.Header = child.projectLabel.Content;
                    tab.Content = scroll;
                    status_BT.reports_TC.Items.Add(tab);
                    status_BT.reports_TC.SelectedIndex = 0; 
                }
            }
            status_BT.reports_TC.Height = this.ActualHeight;
            status_BT.reports_TC.Width = this.ActualWidth;
            if (reportsToggle == true)
            {
                overall.Visibility = Visibility.Collapsed;
                status_BT.reports_TC.Visibility = Visibility.Visible;
                reportsToggle = !reportsToggle;
            }
            else
            {
                status_BT.reports_TC.Visibility = Visibility.Collapsed;
                overall.Visibility = Visibility.Visible;
                reportsToggle = !reportsToggle;
            }
        }
        public void Reload()
        {
            log.Debug("Reload called");
            projectRowsPanel.Children.Clear();
            ProjectControlSource source = PersistableJson.Load<ProjectControlSource>();
            log.Debug(source.Items + "items are in the project control source");
            foreach (ProjectControlSourceItem item in source.Items)
            {

                ProjectControl row = new ProjectControl();
                row.StatusUpdated += Row_StatusUpdated;
                row.StartTask += Row_StartTask;
                row.EndTask += Row_EndTask;
                row.UpdateUI += Row_UpdateUI;
                row.Reload(item, source);
                row.trivialBump_RB.GroupName = "bumpGroup" + item.Project;
                row.minorBump_RB.GroupName = "bumpGroup" + item.Project;
                row.majorBump_RB.GroupName = "bumpGroup" + item.Project;

                projectRowsPanel.Children.Add(row);

            }
            foreach (string host in source.FTPHosts)
            {
                ftp_Combobox.Items.Add(host);
            }
        }

        private void Row_UpdateUI(object sender, EventArgs e)
        {
            runAllProjects.IsEnabled = !runAllProjects.IsEnabled;
        }

        private void Row_StatusUpdated(object sender, NewReportEventArgs e)
        {
            log.Debug("[Event Raised] Status Updated");
            string pattern = "[1-9]+?[0-9]?[ ][W][a][r]";
            Regex warningCheck = new Regex(pattern);
            if (e.Report.Contains("Build FAILED") || e.Report.Contains("MSBUILD : error"))
            {
                status_BT.Status = new BitmapImage(new Uri("Images\\Error Circle.png", UriKind.Relative));
                status_BT.Status.Freeze();
            }
            else if (warningCheck.IsMatch(e.Report))
            {
                status_BT.Status = new BitmapImage(new Uri("Images\\Warning Circle.png", UriKind.Relative));
                status_BT.Status.Freeze();
            }
            else if (e.Report.Contains("Build succeeded"))
            {
                status_BT.Status = new BitmapImage(new Uri("Images\\Good Circle.png", UriKind.Relative));
                status_BT.Status.Freeze();
            }
            if (reportsHolder.ContainsKey(e.Name))
            {
                reportsHolder[e.Name] = e.Report;
            }
            else reportsHolder.Add(e.Name, e.Report);
        }
        private void Row_StartTask(object sender, EventArgs e)
        {
            runAllProjects.IsEnabled = false;
        }
        private void Row_EndTask(object sender, EventArgs e)
        {
            runAllProjects.IsEnabled = true;
        }
        private void runAllProjects_Click(object sender, RoutedEventArgs e)
        {
            log.Debug("[USER ACTION] Run all projects called");
            List<Task> TaskList = new List<Task>();
            runAllProjects.IsEnabled = false;
            foreach (ProjectControl child in projectRowsPanel.Children)
            {
                child.RunAllButton.IsEnabled = false;
                child.RunButton.IsEnabled = false;
                if (child.runAll_CB.IsChecked == true)
                {
                    string bumpChoice = "";
                    string pjContent = child.projectLabel.Content.ToString();
                    if (child.majorBump_RB.IsChecked == true) { bumpChoice = "Major"; }
                    else if (child.minorBump_RB.IsChecked == true) { bumpChoice = "Minor"; }
                    else if (child.trivialBump_RB.IsChecked == true) { bumpChoice = "Trivial"; }

                    Task runAll = Task.Factory.StartNew(() =>
                    {
                        log.Debug("[User Action] " + sender.ToString());
                        DeploymentManager bumper = new DeploymentManager(child.source, child.item);
                        bumper.Prepare();
                        bumper.Clean();
                        Versioning.Version temp = bumper.Bump(bumpChoice);
                        child.Version = temp.getVersion();
                        child.Timestamp = DateTime.UtcNow;
                        child.item.Timestamp = child.Timestamp;
                        try
                        {
                            child.Report = bumper.Build();
                        }
                        catch (Exception ex) { MessageBox.Show(ex.ToString()); }
                        bumper.Deploy();
                    });

                    Task runAllCont = runAll.ContinueWith((antecedent) =>
                    {
                        if (reportsHolder.ContainsKey(child.projectLabel.ToString()))
                            {
                                reportsHolder[child.projectLabel.ToString()] = child.Report;
                            }
                            else reportsHolder.Add(child.projectLabel.ToString(), child.Report);
                    }, TaskScheduler.FromCurrentSynchronizationContext()); 
                     TaskList.Add(runAllCont);
                } }
            if (TaskList.Count > 0)
            {
                Task.Factory.ContinueWhenAll(TaskList.ToArray(), (antecedent) =>
                {
                    Task.WaitAll(TaskList.ToArray());
                    StatusCheck check = new StatusCheck(reportsHolder);
                    updateStatus(check);
                    runAllProjects.IsEnabled = true;
                    foreach (ProjectControl child in projectRowsPanel.Children)
                    {
                        child.RunAllButton.IsEnabled = true;
                        child.RunButton.IsEnabled = true;
                    }
                }, new System.Threading.CancellationToken(), TaskContinuationOptions.None, TaskScheduler.FromCurrentSynchronizationContext());
            }
            else
            {
                runAllProjects.IsEnabled = false;
                foreach (ProjectControl child in projectRowsPanel.Children)
                {
                    child.RunAllButton.IsEnabled = true;
                    child.RunButton.IsEnabled = true;
                }
            }
         }
     }
 }

