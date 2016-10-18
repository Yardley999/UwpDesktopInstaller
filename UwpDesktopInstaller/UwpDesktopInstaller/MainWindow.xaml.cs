using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

        string exePath = AppDomain.CurrentDomain.BaseDirectory;

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
            RunPsScript(ps =>
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
            RunPsScript(ps =>
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
            RunPsScript(ps =>
            {
                var result = ps.AddCommand("Remove-AppxPackage").AddParameter("Package", AppFullName).Invoke();
            });
        }

        /// <summary>
        /// 启动Uwp
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button_Click_3(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start("mobilechef://");
        }

        /// <summary>
        /// 测试安装
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button_Click_4(object sender, RoutedEventArgs e)
        {

            FolderHelper.CopyDir("d:\\tool", "d:\\d");

            RunPsScript(ps =>
            {
                var result = ps.AddScript("$PSVersionTable").Invoke();
                if (result.Count > 0)
                {
                    Version anniversaryVersion = new Version(10, 0, 14393);
                    var osVersion = (result.First().BaseObject as System.Collections.Hashtable)["BuildVersion"] as System.Version;
                    if (osVersion < anniversaryVersion)
                    {
                        //旧版本不支持appx安装，需要执行ps脚本&打开开发者模式

                    }
                    else
                    {

                    }
                }
            });


            RunSpaceScript(sp =>
            {
                var result = ExeCommand(sp, "add-appxpackage", "Path", @"C:\Users\Winchannel10\Documents\Visual Studio 2015\Projects\App12\App12\AppPackages\App12_1.0.0.0_Debug_Test\App12_1.0.0.0_x86_Debug.appxbundle");
            });

            RunPipeScript(pl =>
            {
                var cmd = new Command("add-appxpackage");
                cmd.Parameters.Add("Path", @"C:\Users\Winchannel10\Documents\Visual Studio 2015\Projects\App12\App12\AppPackages\App12_1.0.0.0_Debug_Test\App12_1.0.0.0_x86_Debug.appxbundle");
                pl.Commands.Add(cmd);
                pl.Invoke();
            });

            RunPsScript(ps =>
            {
                try
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
                    X509Certificate2 certificate1 = new X509Certificate2(@"C:\Users\Winchannel10\Documents\Visual Studio 2015\Projects\App12\App12\AppPackages\App12_1.0.0.0_Debug_Test\App12_1.0.0.0_x86_Debug.cer");
                    store.Add(certificate1);
                    store.Close();

                    result = ps.AddCommand("add-appxpackage").AddParameter("Path", @"C:\Users\Winchannel10\Documents\Visual Studio 2015\Projects\App12\App12\AppPackages\App12_1.0.0.0_Debug_Test\App12_1.0.0.0_x86_Debug.appxbundle").AddParameter("ForceApplicationShutdown").Invoke();
                    ps.Commands.Clear();

                    result = ps.AddCommand("get-appxpackage").AddParameter("Name", "acde0ea3-f00f-4b4f-80e6-9fa7c5a152da").Invoke();
                    if (result.Count > 0)
                    {
                        MessageBox.Show("安装成功!");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            });
        }

        #region Helper

        private FileInfo[] FindFiles(string path, string suffix)
        {
            DirectoryInfo dInfo = new DirectoryInfo(path);
            if (dInfo.Exists)
            {
                return dInfo.GetFiles("*." + suffix);
            }

            return null;
        }

        private void RunPsScript(Action<PowerShell> psAction)
        {
            using (Runspace runspace = RunspaceFactory.CreateRunspace())
            {
                runspace.Open();
                PowerShell ps = PowerShell.Create();
                ps.Runspace = runspace;


                psAction?.Invoke(ps);
            }

        }

        private void RunPipeScript(Action<Pipeline> psAction)
        {
            using (Runspace runspace = RunspaceFactory.CreateRunspace())
            {
                runspace.Open();

                Pipeline pipe = runspace.CreatePipeline();

                psAction?.Invoke(pipe);
            }
        }

        private void RunSpaceScript(Action<Runspace> spAcvtion)
        {
            using (Runspace runspace = RunspaceFactory.CreateRunspace())
            {
                runspace.Open();

                spAcvtion?.Invoke(runspace);
            }
        }

        private Tuple<Collection<PSObject>, Collection<object>> ExeCommand(Runspace space, string cmd, string p1 = null, string v1 = null, string p2 = null, string v2 = null)
        {
            Pipeline line = space.CreatePipeline();
            Command command = new Command(cmd);
            if (p1 != null)
            {
                command.Parameters.Add(p1, v1);
            }
            if (p2 != null)
            {
                command.Parameters.Add(p2, v2);
            }
            line.Commands.Add(command);
            var result = line.Invoke();
            Collection<object> error = null;
            if (line.Error.Count > 0)
            {
                error = line.Error.ReadToEnd();
            }

            return new Tuple<Collection<PSObject>, Collection<object>>(result, error);
        }

        #endregion

        private void ImportCer_Click(object sender, RoutedEventArgs e)
        {
            X509Store store = new X509Store(StoreName.Root, StoreLocation.LocalMachine);
            store.Open(OpenFlags.MaxAllowed);
            X509Certificate2 certificate1 = new X509Certificate2(System.IO.Path.Combine(exePath, "Winchannel.cer"));
            store.Add(certificate1);
            store.Close();
            MessageBox.Show("已导入根证书");
        }

        private void OfflineInstall_Click(object sender, RoutedEventArgs e)
        {
            X509Store store = new X509Store(StoreName.Root, StoreLocation.LocalMachine);
            store.Open(OpenFlags.MaxAllowed);
            X509Certificate2 certificate1 = new X509Certificate2(System.IO.Path.Combine(exePath, "Winchannel.cer"));
            store.Add(certificate1);
            store.Close();

            RunSpaceScript(sp =>
            {
                var result = ExeCommand(sp, "set-executionpolicy", "ExecutionPolicy", "RemoteSigned");
            });

            DirectoryInfo depInfo = new DirectoryInfo(System.IO.Path.Combine(exePath, "Dep"));
            var depAppxs = depInfo.GetFiles("*.appx");
            foreach (var dep in depAppxs)
            {
                using (Runspace runspace = RunspaceFactory.CreateRunspace())
                {
                    runspace.Open();
                    PowerShell ps = PowerShell.Create();
                    ps.Runspace = runspace;
                    var result = ps.AddCommand("add-appxpackage").AddParameter("Path", dep.FullName).AddParameter("ForceApplicationShutdown").Invoke();
                }
            }

            DirectoryInfo dInfo = new DirectoryInfo(exePath);
            var files = dInfo.GetFiles("*.AppxBundle");
            if (files.Length == 0)
            {
                files = dInfo.GetFiles("*.Appx");
            }
            if (files.Length > 0)
            {
                using (Runspace runspace = RunspaceFactory.CreateRunspace())
                {
                    runspace.Open();
                    PowerShell ps = PowerShell.Create();
                    ps.Runspace = runspace;
                    var result = ps.AddCommand("add-appxpackage").AddParameter("Path", files.First().FullName).AddParameter("ForceApplicationShutdown").Invoke();
                    MessageBox.Show("已安装!");
                }
            }
            else
            {
                MessageBox.Show("找不到本地安装包");
            }
        }
    }
}
//X509Store store = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
//store.Open(OpenFlags.MaxAllowed); 
//X509Certificate2 certificate1 = new X509Certificate2("INGS.cer"); store.Add(certificate1); 
//store.Close();