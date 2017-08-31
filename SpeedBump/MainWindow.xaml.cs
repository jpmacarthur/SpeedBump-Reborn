﻿using System;
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
using System.Windows.Controls.Primitives;

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
        public event NewReportEventHandler StatusUpdated;
        public event EventHandler StartTask;
        ProjectControlSource source;
        int checkcount;
        public Dictionary<string, string> reportsHolder = new Dictionary<string, string>();
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public MainWindow()
        {
            Logger.Setup();

            InitializeComponent();
            DataContext = this;
           // this.status_BT.ButtonClicked += Status_BT_ButtonClicked;
            Reload();
            //sv.ReportViewerToggle += Sv_ReportViewerToggle;

        }

        private void Sv_ReportViewerToggle(object sender, EventArgs e)
        {
            if (reportsToggle == true)
            {
                foreach (ProjectControl row in projectRowsPanel.Children)
                {
                    row.innerPR.Visibility = Visibility.Collapsed;
                    row.WarningStatus.Visibility = Visibility.Collapsed;
                }
                topBar.Visibility = Visibility.Collapsed;
                reportsToggle = !reportsToggle;
            }
            else
            {
                foreach (ProjectControl row in projectRowsPanel.Children)
                {
                    row.innerPR.Visibility = Visibility.Visible;
                    row.WarningStatus.Visibility = Visibility.Visible;
                }
                topBar.Visibility = Visibility.Visible;
                reportsToggle = !reportsToggle;
            }
        }

        /* private void Status_BT_ButtonClicked(object sender, EventArgs e)
         {
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

                     //TODO Add logic for running all 
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
         }*/

        public void Reload()
        {

            log.Debug("Reload called");
            checkcount = 0;
            projectRowsPanel.Children.Clear();
            source = PersistableJson.Load<ProjectControlSource>();
            log.Debug(source.Items + "items are in the project control source");
            foreach (ProjectControlSourceItem item in source.Items)
            {

                ProjectControl row = new ProjectControl();
                row.StatusUpdated += Row_StatusUpdated;
                row.StartTask += Row_StartTask;
                row.EndTask += Row_EndTask;
               // row.SendReportButtonClick += Row_ToggleVis;
                row.Reload(item, source);
                row.trivialBump_RB.GroupName = "bumpGroup" + item.Project;
                row.minorBump_RB.GroupName = "bumpGroup" + item.Project;
                row.majorBump_RB.GroupName = "bumpGroup" + item.Project;

                projectRowsPanel.Children.Add(row);

            }
            foreach (FTPHost host in source.FTPHosts)
            {
                CheckBox ftpcheck = new CheckBox { Content = host.IPAddress, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
                ftpcheck.DataContext = host;
                ftpcheck.SetBinding(ToggleButton.IsCheckedProperty, "Checked");
                ftpcheck.Checked += Ftpcheck_Checked;
                ftpcheck.Unchecked += Ftpcheck_Unchecked;
                if(host.Checked == true)
                {
                    checkcount++;
                }
                ServerChoices.Children.Add(ftpcheck);
            }
        }

        private void Ftpcheck_Unchecked(object sender, RoutedEventArgs e)
        {
            checkcount--;
            if(checkcount == 0)
            {
                foreach(ProjectControl pc in projectRowsPanel.Children)
                {
                    pc.DeployButton.IsEnabled = false;
                }
            }
        }

        private void Ftpcheck_Checked(object sender, RoutedEventArgs e)
        {
            checkcount++;
            if (checkcount > 0)
            {
                foreach (ProjectControl pc in projectRowsPanel.Children)
                {
                    pc.DeployButton.IsEnabled = true;
                }
            }
        }

        /*  private void Row_ToggleVis(object sender, NewReportEventArgs e)
          {
              sv.scroll.Content = new TextBlock { Text = e.Report };
              sv.Visibility = Visibility.Visible;
              sv.HorizontalAlignment = HorizontalAlignment.Stretch;
              sv.VerticalAlignment = VerticalAlignment.Stretch;
              if (reportsToggle == true)
              {
                  foreach(ProjectControl row in projectRowsPanel.Children)
                  {
                      row.innerPR.Visibility = Visibility.Collapsed;
                      row.WarningStatus.Visibility = Visibility.Collapsed;
                  }
                  topBar.Visibility = Visibility.Collapsed;
                  reportsToggle = !reportsToggle;
              }
              else
              {
                  foreach (ProjectControl row in projectRowsPanel.Children)
                  {
                      row.innerPR.Visibility = Visibility.Visible;
                      row.WarningStatus.Visibility = Visibility.Visible;
                  }
                  topBar.Visibility = Visibility.Visible;
                  reportsToggle = !reportsToggle;
              }
          }*/

        private void Row_StatusUpdated(object sender, NewReportEventArgs e)
        {
            Report = e.Report;
        }
        private void Row_StartTask(object sender, EventArgs e)
        {
           // runAllProjects.IsEnabled = false;
        }
        private void Row_EndTask(object sender, EventArgs e)
        {
           // runAllProjects.IsEnabled = true;
        }

        private void runAllProjects_Click(object sender, RoutedEventArgs e)
        {
            log.Debug("[USER ACTION] Run all projects called");
            List<Task> TaskList = new List<Task>();
            //runAllProjects.IsEnabled = false;
            List<string> checkedboxes = new List<string>();
            foreach (CheckBox cb in ServerChoices.Children)
            {
                if (cb.IsChecked == true)
                {
                    checkedboxes.Add(cb.Content.ToString());
                }
            }
            foreach (ProjectControl child in projectRowsPanel.Children)
            {
                bool success = true;
                child.runAll_BT.IsEnabled = false;
                child.run_BT.IsEnabled = false;
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
                            if (child.Report.Contains("Build FAILED") || child.Report.Contains("MSBUILD : error"))
                            {
                                success = false;
                            }
                            /*  if (this.StatusUpdated != null)
                              {
                                  this.StatusUpdated(this, new NewReportEventArgs(Report, pjContent));
                              }*/
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(ex.ToString());
                            success = false;
                        }
                        if (success == true)
                        {
                            try
                            {
                                foreach (string address in checkedboxes)
                                {
                                    bumper.Deploy(address);
                                }
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show(ex.ToString());
                                success = false;
                            }
                        }
                    });
                Task runAll_cont = runAll.ContinueWith((antecedent) =>
                {
                    child.setStatus(success);
                }, TaskScheduler.FromCurrentSynchronizationContext()); 
                TaskList.Add(runAll_cont);
                } }
            if (TaskList.Count > 0)
            {
                Task.Factory.ContinueWhenAll(TaskList.ToArray(), (antecedent) =>
                {
                    Task.WaitAll(TaskList.ToArray());
                    StatusCheck check = new StatusCheck(reportsHolder);
                    //updateStatus(check);
                    foreach (ProjectControl child in projectRowsPanel.Children)
                    {
                        child.runAll_BT.IsEnabled = true;
                        child.run_BT.IsEnabled = true;
                    }
                    //runAllProjects.IsEnabled = true;
                }, new System.Threading.CancellationToken(), TaskContinuationOptions.None, TaskScheduler.FromCurrentSynchronizationContext());
            }
            else
            {
                foreach (ProjectControl child in projectRowsPanel.Children)
                {
                    child.runAll_BT.IsEnabled = true;
                    child.run_BT.IsEnabled = true;
                }
                //runAllProjects.IsEnabled = true;
            }
         }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            source.Save();
        }
    }
 }

