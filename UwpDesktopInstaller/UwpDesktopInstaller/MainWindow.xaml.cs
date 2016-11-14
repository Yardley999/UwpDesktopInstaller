using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Diagnostics;
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
        readonly string Mode = GetSettings("Mode");
        readonly string AppId = GetSettings("AppId");
        readonly string PackageName = GetSettings("PackageName");
        readonly string HostUrl = GetSettings("HostUrl");

        bool IsUserMode { get; set; }

        private string AppFullName = string.Empty;
        private string AppLocation = string.Empty;
        private string AppVersion = string.Empty;

        private string NewAppUrl = string.Empty;

        string exePath = AppDomain.CurrentDomain.BaseDirectory;

        public MainWindow()
        {
            AppFullName = PackageName;

            InitializeComponent();

            this.Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            //await Task.Delay(100);
            CheckLocalVersion();
            //button_Click(null, null);
            if (Mode == "USER")
            {
                this.SwitchMode.IsChecked = true;
            }
            else
            {
                this.SwitchMode.IsChecked = false;
            }
        }

        private void CheckLocalVersion()
        {
            Task.Run(() =>
            {
                RunPsScript(ps =>
                {
                    var result = ps.AddCommand("Get-AppxPackage").AddParameter("Name", AppId).Invoke();
                    if (result.Count > 0)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            var baseObj = (result.First() as PSObject)?.BaseObject;
                            if (baseObj != null)
                            {
                                AppFullName = baseObj.GetType().GetProperty("PackageFullName")?.GetValue(baseObj)?.ToString();
                                AppVersion = baseObj.GetType().GetProperty("Version")?.GetValue(baseObj)?.ToString();

                                if (!string.IsNullOrEmpty(AppVersion))
                                {
                                    this.VersionBlock.Text = "v" + AppVersion;
                                }

                                var package = baseObj?.GetType().GetField("package", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.GetValue(baseObj);
                                var installedDate = package?.GetType().GetProperty("InstalledDate")?.GetValue(package).ToString();
                                AppLocation = baseObj.GetType().GetProperty("InstallLocation")?.GetValue(baseObj)?.ToString();
                            }
                        });
                    }
                    else
                    {
                        Dispatcher.Invoke(() =>
                        {
                            this.VersionBlock.Text = "未安装";
                        });
                    }
                });
            });

            int rCount = 0;
            DirectoryInfo richInfo = new DirectoryInfo(System.IO.Path.Combine(GetAppPakcageFolder(), "RichMedia"));
            if (richInfo.Exists)
            {
                var dirs = richInfo.GetDirectories();
                rCount = dirs.Count(d => !d.Name.Contains("-image"));
            }
            if (rCount > 0)
            {
                MyRichMedia.Text = "已导入富媒体" + rCount;
            }

            DirectoryInfo info = new DirectoryInfo(exePath + "富媒体");
            if (!info.Exists)
            {
                this.AddRichMedia.IsChecked = false;
                this.AddRichMedia.IsEnabled = false;
            }
            else if (rCount == 0)
            {
                this.AddRichMedia.IsChecked = true;
            }
            else if (rCount > 0)
            {
                this.AddRichMedia.IsChecked = false;
            }
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

        #region Helper

        private static string GetSettings(string key)
        {
            return ConfigurationManager.AppSettings.Get(key);
        }

        private static void SetSettings(string key, string value)
        {
            Configuration cfa = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            cfa.AppSettings.Settings[key].Value = value;
            cfa.Save();
        }


        private string GetAppPakcageFolder()
        {
            string pLocal = System.Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            return System.IO.Path.Combine(pLocal, "Packages", PackageName, "LocalState");
        }

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

        /// <summary>
        /// 执行完整安装过程
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void SuperInstall_Click(object sender, RoutedEventArgs e)
        {
            this.SuperInstall.IsEnabled = false;
            bool needRichMedia = AddRichMedia.IsChecked.Value;

            //FileInfo iFile = new FileInfo("Backup.zip");
            //if (iFile.Exists)
            //{
            //    ZipHelper.UnZip(iFile.Name, "C:\\");
            //}

            DirectoryInfo iFolder = new DirectoryInfo("backup");
            if (iFolder.Exists)
            {
                FolderHelper.CopyDir(iFolder.Name, "C:\\");
            }

            await Task.Run(() =>
            {
                X509Store store = new X509Store(StoreName.Root, StoreLocation.LocalMachine);
                store.Open(OpenFlags.MaxAllowed);
                X509Certificate2 certificate1 = new X509Certificate2(System.IO.Path.Combine(exePath, "Winchannel.cer"));
                store.Add(certificate1);
                store.Close();
            });

            LogBlock.Text = "证书已导入";

            await Task.Run(() =>
            {
                RunSpaceScript(sp =>
                {
                    var result = ExeCommand(sp, "set-executionpolicy", "ExecutionPolicy", "RemoteSigned");
                });
            });

            LogBlock.Text = "修改Powershell安全策略";

            DirectoryInfo depInfo = new DirectoryInfo(System.IO.Path.Combine(exePath, "Dep"));
            var depAppxs = depInfo.GetFiles("*.appx");
            foreach (var dep in depAppxs)
            {
                await Task.Run(() =>
                {
                    using (Runspace runspace = RunspaceFactory.CreateRunspace())
                    {
                        runspace.Open();
                        PowerShell ps = PowerShell.Create();
                        ps.Runspace = runspace;
                        var result = ps.AddCommand("add-appxpackage").AddParameter("Path", dep.FullName).AddParameter("ForceApplicationShutdown").Invoke();
                    }
                });
                LogBlock.Text = "安装 " + dep.FullName;
            }

            await Task.Run(async () =>
            {
                var appFile = GetMaxVerFile();
                if (appFile.Item1 != null)
                {
                    using (Runspace runspace = RunspaceFactory.CreateRunspace())
                    {
                        runspace.Open();
                        PowerShell ps = PowerShell.Create();
                        ps.Runspace = runspace;
                        var result = ps.AddCommand("add-appxpackage").AddParameter("Path", appFile.Item1.FullName).AddParameter("ForceApplicationShutdown").Invoke();
                        ps.Commands.Clear();

                        result = ps.AddCommand("get-appxpackage").AddParameter("Name", AppId).Invoke();
                        if (result.Count > 0)
                        {
                            Dispatcher.Invoke(() =>
                            {
                                CheckLocalVersion();
                                LogBlock.Text = "应用安装完成";
                            });

                            if (needRichMedia)
                            {
                                //导入富媒体
                                string appdatafolder = GetAppPakcageFolder();
                                //await Task.Run(() =>
                                //{
                                //    ZipHelper.UnZip("富媒体.zip", appdatafolder, (file) =>
                                //    {
                                //        Dispatcher.Invoke(() =>
                                //        {
                                //            this.LogBlock.Text = "解压" + file ?? "";
                                //        });
                                //    });
                                //});

                                //await Task.Run(() =>
                                //{
                                //    FolderHelper.CopyDir(System.IO.Path.Combine(exePath, "富媒体"), appdatafolder, file =>
                                //     {
                                //         Dispatcher.Invoke(() =>
                                //         {
                                //             LogBlock.Text = "拷贝 " + file;
                                //         });
                                //     });
                                //});

                                await Task.Run(() =>
                                 {
                                     DirectoryInfo rInfo = new DirectoryInfo(exePath + "富媒体");
                                     var all = rInfo.GetFiles();
                                     int Count = 0;
                                     foreach (var r in all)
                                     {
                                         Dispatcher.Invoke(() =>
                                         {
                                             LogBlock.Text = "解压" + (++Count) + "/" + all.Count() + " " + r.FullName;
                                         });
                                         if (r.Extension.Equals(".zip"))
                                         {
                                             DirectoryInfo unZipFolder = new DirectoryInfo(System.IO.Path.Combine(appdatafolder, "RichMedia", r.Name.Replace(".zip", "")));
                                             if (unZipFolder.Exists)
                                             {
                                                 unZipFolder.Delete(true);
                                             }
                                             System.IO.Compression.ZipFile.ExtractToDirectory(r.FullName, unZipFolder.FullName);
                                         }
                                         else if (r.Extension.Equals(".db"))
                                         {
                                             System.IO.File.Copy(r.FullName, System.IO.Path.Combine(appdatafolder, r.Name), true);
                                         }
                                     }
                                 });
                                Dispatcher.Invoke(() =>
                               {
                                   LogBlock.Text = "富媒体已导入";
                               });
                            }
                            MessageBox.Show("部署完成!");
                        }
                        else
                        {
                            MessageBox.Show("未知错误，请联系开发人员");
                        }
                    }
                }
                else
                {
                    MessageBox.Show("找不到本地安装包");
                }
            });
        }

        /// <summary>
        /// 检查更新
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Update_Click(object sender, RoutedEventArgs e)
        {
            this.CheckUpdate.IsEnabled = false;

            RunSpaceScript(sp =>
            {
                var result = ExeCommand(sp, "set-executionpolicy", "ExecutionPolicy", "RemoteSigned");
            });

            RunPsScript(ps =>
            {
                try
                {
                    WebClient client1 = new WebClient();
                    var res = client1.DownloadString(HostUrl + "?" + Guid.NewGuid().ToString());
                    ps.Commands.Clear();
                    if (!string.IsNullOrEmpty(res))
                    {
                        try
                        {
                            var jOejct = JObject.Parse(res);
                            var sVerString = jOejct.GetValue("newVersion").ToString();
                            NewAppUrl = jOejct.GetValue("url").ToString();

                            //string loclVersion = "";
                            //var result = ps.AddCommand("Get-AppxPackage").AddParameter("Name", AppId).Invoke();
                            //ps.Commands.Clear();
                            //if (result.Count > 0)
                            //{
                            //    var baseObj = (result.First() as PSObject)?.BaseObject;
                            //    if (baseObj != null)
                            //    {
                            //        loclVersion = baseObj.GetType().GetProperty("Version")?.GetValue(baseObj)?.ToString();
                            //    }
                            //}

                            Version sVersion;
                            Version lVerison = GetMaxVerFile().Item2;
                            if (Version.TryParse(sVerString, out sVersion))
                            {
                                if (lVerison == null || sVersion > lVerison)
                                {
                                    DirectoryInfo tempDir = new DirectoryInfo(exePath + "temp");
                                    if (!tempDir.Exists)
                                    {
                                        tempDir.Create();
                                    }
                                    string tempFileName = "temp\\" + Guid.NewGuid().ToString();
                                    this.CheckUpdate.Content = "更新中...";
                                    WebClient client = new WebClient();
                                    client.DownloadProgressChanged += (obj, arg) =>
                                    {
                                        this.CheckUpdate.IsEnabled = false;
                                        this.Dispatcher.Invoke(() =>
                                        {
                                            this.CheckUpdate.Content = "已下载" + arg.BytesReceived + "字节";
                                        });
                                    };
                                    client.DownloadFileCompleted += (obj, arg) =>
                                    {
                                        if (arg.Cancelled == false && arg.Error == null)
                                        {
                                            this.CheckUpdate.Content = "离线包更新完成 V" + sVerString;
                                            this.CheckUpdate.IsEnabled = true;
                                            File.Copy(exePath + tempFileName, exePath + sVersion + ".appxbundle");
                                            tempDir.Delete(true);
                                        }
                                    };
                                    client.DownloadFileAsync(new Uri(NewAppUrl), exePath + tempFileName);
                                }
                                else
                                {
                                    this.CheckUpdate.Content = "离线包已经是最新 V" + lVerison;
                                    this.CheckUpdate.IsEnabled = true;
                                }
                            }
                        }
                        catch
                        {
                            this.CheckUpdate.Content = "获取失败";
                        }
                    }
                    else
                    {
                        this.CheckUpdate.Content = "获取失败";
                    }
                }
                catch (System.Net.WebException)
                {
                    MessageBox.Show("网络连接失败！");
                }
                catch
                {

                }
            });
        }

        /// <summary>
        /// 获取本地最高版本安装包
        /// </summary>
        /// <returns></returns>
        private Tuple<FileInfo, Version> GetMaxVerFile()
        {
            DirectoryInfo dInfo = new DirectoryInfo(exePath);
            List<FileInfo> allAppxs = new List<FileInfo>();
            allAppxs.AddRange(dInfo.GetFiles("*.appxbundle"));
            allAppxs.AddRange(dInfo.GetFiles("*.Appx"));

            FileInfo maxVerFile = null;
            Version maxVer = null;
            foreach (var app in allAppxs)
            {
                string ver = app.Name.Split('_').FirstOrDefault(s => s.Contains("."))?.Replace(".appxbundle", "").Replace(".appx", "");
                Version localVer;
                if (Version.TryParse(ver, out localVer))
                {
                    if (maxVer == null || localVer > maxVer)
                    {
                        maxVerFile = app;
                        maxVer = localVer;
                    }
                }
            }

            return new Tuple<FileInfo, Version>(maxVerFile, maxVer);
        }

        /// <summary>
        /// 检查安装器自更新
        /// </summary>
        private void CheckUpdateSelfInstaller()
        {
            //TODO
        }

        /// <summary>
        /// 用户模式
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ModeSwitchChecked(object sender, RoutedEventArgs e)
        {
            IsUserMode = true;
            this.SwitchMode.Content = "用户模式";
            SetSettings("Mode", "USER");
        }

        /// <summary>
        /// 装机模式
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ModeSwicthUnChecked(object sender, RoutedEventArgs e)
        {
            IsUserMode = false;
            this.SwitchMode.Content = "装机模式";
            SetSettings("Mode", "DIY");
        }

        /// <summary>
        /// 清理本地安装包
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Clean_Click(object sender, RoutedEventArgs e)
        {
            DirectoryInfo dInfo = new DirectoryInfo(exePath);
            List<FileInfo> allAppxs = new List<FileInfo>();
            allAppxs.AddRange(dInfo.GetFiles("*.appxbundle"));
            allAppxs.AddRange(dInfo.GetFiles("*.appx"));

            foreach (var app in allAppxs)
            {
                app.Delete();
            }

            DirectoryInfo tempDir = new DirectoryInfo(exePath + "temp");
            if (tempDir.Exists)
            {
                tempDir.Delete(true);
            }
        }
    }
}
