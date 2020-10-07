using System;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using SocketIOClient;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using System.Diagnostics;
using Octokit;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using System.Runtime.InteropServices;
using Serilog;
using Serilog.Events;

namespace VulcanUpdater
{
    class Program
    {
        private static GitHubClient GhClient { get; set; }
        private static HttpClient HClient { get; set; }
        private static string CurrentDir { get; set; }
        
        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        const int SW_HIDE = 0;
        const int SW_SHOW = 5;
        
        static async Task Main(string[] args)
        {
            var handle = GetConsoleWindow();
            // Chowamy okno konsoli
            ShowWindow(handle, SW_HIDE);
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .WriteTo.File(Directory.GetCurrentDirectory() + "/logs/LogFile.txt")
                .CreateLogger();
            try
            {
                Log.Information("Vulcan Updater 1.0.3");
                var builder = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("config.json");
            
                var config = builder.Build();
                var adress = config.GetSection("Adress").Value;
                Console.WriteLine(adress);
                var uri = new Uri(adress +"updater");
                var socket = new SocketIO(uri);
            
                CurrentDir = Directory.GetCurrentDirectory();
                string clientFilename = $"{CurrentDir}/client/VulcanClient2.exe";
            
                Process process = new Process();
                process.StartInfo.FileName = clientFilename;
                process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.UseShellExecute = false;
            
            
                GhClient = new GitHubClient(ProductHeaderValue.Parse("Vulcan"));
                HClient = new HttpClient();
                HClient.DefaultRequestHeaders.Add("User-Agent", "Vulcan");
            
                socket.OnConnected += Socket_OnConnected;
                socket.OnDisconnected += Socket_onDisconnected;
                
                socket.On("download_new_version", async response =>
                {
                    Console.WriteLine(response);
                    JObject release = response.GetValue(0).Value<JObject>("release");
                    var assets = release.GetValue("assets");
                    
                    foreach (JObject asset in assets)
                    {
                        Console.WriteLine(asset);
                        var assetName = asset.GetValue("name").ToString();
                        if (assetName == "binaries.zip")
                        {
                            string zipName = asset.GetValue("name").ToString();
                            string BrowserDownloadUrl = asset.GetValue("browser_download_url").ToString();
                            using (HttpClient client = new HttpClient())
                            {
                                client.DefaultRequestHeaders.Add("User-Agent", "Vulcan");
                                using (HttpResponseMessage res = await client.GetAsync(BrowserDownloadUrl,
                                    HttpCompletionOption.ResponseHeadersRead))
                                {
                                    res.EnsureSuccessStatusCode();
                                    var currentDir = Directory.GetCurrentDirectory();
                                    string filePath = $"{currentDir}/{zipName}";
                                    var extractPath = $"{currentDir}/client/";
                                    
                                    using (Stream stream = await res.Content.ReadAsStreamAsync())
                                    {
                                        using (Stream streamToWrite = File.Open(filePath, System.IO.FileMode.Create))
                                        {
                                            await stream.CopyToAsync(streamToWrite);
                                        }
                                    }

                                    await Task.Run(() => ZipFile.ExtractToDirectory(filePath, extractPath, true));
                                    await Task.Run(() => File.Delete(filePath));
                                }
                            }
                        }
                    }
                });
                
                await socket.ConnectAsync();

                if (File.Exists(clientFilename))
                {
                    await DownloadNewVersionOrRunProcess(process, clientFilename);
                }
                else
                {
                    await DownloadVLatestVulcanVersion(process);
                }
                
                await process.WaitForExitAsync();
            
                Console.ReadLine();
            }
            catch (Exception e)
            {
                Log.Fatal(e, "Wystapil problem z updaterem");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        private static void Socket_OnConnected(object sender, EventArgs e)
        {
            var socket = sender as SocketIO;
            Log.Information("Updater polaczony " + socket.Id);
        }

        private static void Socket_onDisconnected(object sender, string e)
        {
            Log.Information("Updater rozlaczony");
        }

        private static async Task DownloadVLatestVulcanVersion(Process process)
        {
            Log.Information("Pobieramy najnowsza wersje Vulcana");
                
            var releases = await GhClient.Repository.Release.GetAll("Qwizi", "Vulcan-Client-2");
            var latest = releases[0];
            var assets = latest.Assets;
                
            foreach (var asset in assets) 
            {
                if (asset.Name == "binaries.zip")
                {
                    string zipName = asset.Name;
                    string BrowserDownloadUrl = asset.BrowserDownloadUrl;

                    using (HttpResponseMessage res = await HClient.GetAsync(BrowserDownloadUrl,
                        HttpCompletionOption.ResponseHeadersRead))
                    {
                        res.EnsureSuccessStatusCode();
                        string filePath = $"{CurrentDir}/{zipName}";
                        var extractPath = $"{CurrentDir}/client/";
                        Log.Information("Pobieramy klienta");
                        using (Stream stream = await res.Content.ReadAsStreamAsync())
                        {
                            using (Stream streamToWrite = File.Open(filePath, System.IO.FileMode.Create))
                            {
                                await stream.CopyToAsync(streamToWrite);
                            }
                        }

                        await Task.Run(() =>
                        {
                            Log.Information("Rozpakowujemy klienta");
                            ZipFile.ExtractToDirectory(filePath, extractPath, true);
                            Log.Information("Klient rozpakowany");
                        });
                        await Task.Run(() =>
                        {
                            Log.Information("Usuwamy zipa");
                            File.Delete(filePath);
                            Log.Information("Zip usuniety");
                        });
                        Log.Information("Uruchamiany klienta");
                        process.Start();
                        Log.Information("Klient uruchomiony");
                    }
                }
            }
        }

        private static async Task DownloadNewVersionOrRunProcess(Process process, string clientFilename)
        {
            var clientVersion = FileVersionInfo.GetVersionInfo(clientFilename);
            string currentClientVersion = clientVersion.ProductVersion;
            var releases = await GhClient.Repository.Release.GetAll("Qwizi", "Vulcan-Client-2");
            var latest = releases[0];
            Log.Information("Uruchamiany klienta");
            process.Start();
            Log.Information("Klient uruchomiony");
            if (latest.TagName != currentClientVersion)
            {
                Log.Information("Pobieramy najnowsza wersje Vulcana");
                process.Kill();
                Log.Debug("Process zamkniety");
                var assets = latest.Assets;
                foreach (var asset in assets)
                {
                    if (asset.Name == "binaries.zip")
                    {
                        string zipName = asset.Name;
                        string BrowserDownloadUrl = asset.BrowserDownloadUrl;
                        
                        using (HttpResponseMessage res = await HClient.GetAsync(BrowserDownloadUrl,
                            HttpCompletionOption.ResponseHeadersRead))
                        {
                            res.EnsureSuccessStatusCode();
                            string filePath = $"{CurrentDir}/{zipName}";
                            var extractPath = $"{CurrentDir}/client/";
                            Log.Information("Pobieramy klienta");
                            using (Stream stream = await res.Content.ReadAsStreamAsync())
                            {
                                using (Stream streamToWrite = File.Open(filePath, System.IO.FileMode.Create))
                                {
                                    await stream.CopyToAsync(streamToWrite);
                                    Log.Information("Klient pobrany");
                                }
                            }

                            await Task.Run(() =>
                            {
                                Log.Information("Rozpakowujemy klienta");
                                ZipFile.ExtractToDirectory(filePath, extractPath, true);
                                Log.Information("Klient rozpakowany");
                            });
                            await Task.Run(() =>
                            {
                                Log.Information("Usuwamy zipa");
                                File.Delete(filePath);
                                Log.Information("Zip usuniety");
                            });
                            Log.Information("Uruchamiany klienta");
                            process.Start();
                            Log.Information("Klient uruchomiony");
                        }
                    }
                }
            }
        }
    }
}