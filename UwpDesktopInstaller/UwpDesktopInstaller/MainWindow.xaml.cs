using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml.Linq;

namespace UwpDesktopInstaller
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        readonly string AppId = "2223d805-63e2-4ded-87a7-ee0b94c17a56";

        private string AppFullName = string.Empty;
        private string AppLocation = string.Empty;
        private string NewAppUrl = string.Empty;

        public MainWindow()
        {
            InitializeComponent();
            this.Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await Task.Delay(100);
            CheckLocalVersion();
            button_Click(null, null);
        }

        private void CheckLocalVersion()
        {
            ExecuteScript(ps =>
            {
                var result = ps.AddCommand("Get-AppxPackage").AddParameter("Name", AppId).Invoke();
                if (result.Count > 0)
                {
                    var baseObj = (result.First() as PSObject)?.BaseObject;
                    if (baseObj != null)
                    {
                        string fullName = baseObj.GetType().GetProperty("PackageFullName")?.GetValue(baseObj)?.ToString();
                        if (!string.IsNullOrEmpty(fullName))
                        {
                            AppFullName = fullName;
                        }

                        string version = baseObj.GetType().GetProperty("Version")?.GetValue(baseObj)?.ToString();
                        if (!string.IsNullOrEmpty(version))
                        {
                            this.VersionBlock.Text = "v" + version;
                        }

                        var package = baseObj?.GetType().GetField("package", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.GetValue(baseObj);
                        var installedDate = package?.GetType().GetProperty("InstalledDate")?.GetValue(package).ToString();
                        if (!string.IsNullOrEmpty(installedDate))
                        {
                            this.InstalledDate.Text = installedDate.Replace("+08:00", "");
                        }

                        AppLocation = baseObj.GetType().GetProperty("InstallLocation")?.GetValue(baseObj)?.ToString();
                        this.UninstallButon.IsEnabled = true;
                    }
                }
                else
                {
                    this.VersionBlock.Text = "未安装";
                }
            });
        }

        /// <summary>
        /// 获取新版本
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void button_Click(object sender, RoutedEventArgs e)
        {
            this.NewVersionBlock.Text = "....";
            await Task.Delay(100);
            ExecuteScript(ps =>
            {
                var result = ps.AddCommand("Invoke-WebRequest").AddParameter("Uri", "ftp://hu.youstandby.me/appVersion.json").Invoke();
                if (result.Count > 0)
                {
                    try
                    {
                        var jOejct = JObject.Parse(result.First().ToString());
                        var newVersion = "v" + jOejct.GetValue("newVersion").ToString();
                        NewAppUrl = jOejct.GetValue("url").ToString();
                        this.NewVersionBlock.Text = newVersion;
                        this.InstallButton.IsEnabled = true;
                        this.InstallButton.Content = "安装" + newVersion;
                    }
                    catch
                    {
                        this.NewVersionBlock.Text = "获取失败";
                    }
                }
                else
                {
                    this.NewVersionBlock.Text = "获取失败";
                }
            });
            this.CheckNewVersion.IsEnabled = true;
        }

        /// <summary>
        /// 安装
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            var exePath = AppDomain.CurrentDomain.BaseDirectory;

            WebClient client = new WebClient();
            client.DownloadProgressChanged += (obj, arg) =>
            {
                this.InstallButton.IsEnabled = false;
                this.InstallButton.Content = "已下载" + arg.BytesReceived + "字节";
            };
            client.DownloadFileCompleted += (obj, arg) =>
            {
                this.InstallButton.Content = "正在安装...";
                var unZiped = ZipHelper.UnZip(exePath + "new.zip", exePath + "new");
                if (unZiped)
                {
                    DirectoryInfo dInfo = new DirectoryInfo(exePath + "new");
                    var files = dInfo.GetFiles("*.AppxBundle");
                    if (files.Length > 0)
                    {
                        using (Runspace runspace = RunspaceFactory.CreateRunspace())
                        {
                            runspace.Open();
                            PowerShell ps = PowerShell.Create();
                            ps.Runspace = runspace;
                            var result = ps.AddCommand("add-appxpackage").AddParameter("Path", files.First().FullName).AddParameter("ForceApplicationShutdown").Invoke();
                            this.InstallButton.Content = "已安装";
                        }
                    }
                }
            };
            client.DownloadFileAsync(new Uri(NewAppUrl), exePath + "new.zip");
        }

        /// <summary>
        /// 卸载
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            ExecuteScript(ps =>
            {
                var result = ps.AddCommand("Remove-AppxPackage").AddParameter("Package", AppFullName).Invoke();
            });
        }

        private void ExecuteScript(Action<PowerShell> psAction)
        {
            using (Runspace runspace = RunspaceFactory.CreateRunspace())
            {
                runspace.Open();
                PowerShell ps = PowerShell.Create();
                ps.Runspace = runspace;

                psAction?.Invoke(ps);
            }
        }

        private void button_Click_3(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start("mobilechef://");
        }

        private void button_Click_4(object sender, RoutedEventArgs e)
        {
            ExecuteScript(ps =>
            {
                var result = ps.AddCommand("Get-ExecutionPolicy").Invoke();
                var policy = result.FirstOrDefault().ToString();
                ps.Commands.Clear();
                if ("Restricted".Equals(policy))
                {
                    result = ps.AddCommand("set-executionpolicy").AddParameter("ExecutionPolicy", "RemoteSigned").Invoke();
                    ps.Commands.Clear();
                }

                X509Store store = new X509Store(StoreName.Root, StoreLocation.LocalMachine);
                store.Open(OpenFlags.MaxAllowed);
                X509Certificate2 certificate1 = new X509Certificate2(@"C:\Users\Yardley\Documents\Visual Studio 2015\Projects\App11\App11\AppPackages\App11_1.0.0.0_x86_Debug_Test\App11_1.0.0.0_x86_Debug.cer");
                store.Add(certificate1);
                store.Close();

                result = ps.AddCommand("add-appxpackage").AddParameter("Path", @"C:\Users\Yardley\Documents\Visual Studio 2015\Projects\App11\App11\AppPackages\App11_1.0.0.0_x86_Debug_Test\App11_1.0.0.0_x86_Debug.appx").AddParameter("ForceApplicationShutdown").Invoke();
            });
        }
    }
}
//X509Store store = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
//store.Open(OpenFlags.MaxAllowed); 
//X509Certificate2 certificate1 = new X509Certificate2("INGS.cer"); store.Add(certificate1); 
//store.Close();