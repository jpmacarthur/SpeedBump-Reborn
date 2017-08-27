using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace SpeedBump
{
    public class MainWindowEventHandling : MainWindow
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public void Row_StatusUpdated(object sender, NewReportEventArgs e)
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
        public void Row_StartTask(object sender, EventArgs e)
        {
            runAllProjects.IsEnabled = false;
        }
        public void Row_EndTask(object sender, EventArgs e)
        {
            runAllProjects.IsEnabled = true;
        }
    }
}
