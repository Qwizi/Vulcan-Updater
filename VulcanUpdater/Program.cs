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

namespace VulcanUpdater
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Vulcan Updater");
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("config.json");
            
            var config = builder.Build();
            var adress = config.GetSection("adress").Value;
            
            var uri = new Uri(adress);
            var socket = new SocketIO(uri);
            
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
            
            var currentDir = Directory.GetCurrentDirectory();
            string clientFilename = $"{currentDir}/client/VulcanClient2.exe";
            Process process = new Process();
            process.StartInfo.FileName = clientFilename;
            
            var ghClient = new GitHubClient(ProductHeaderValue.Parse("Vulcan"));
            
            if (File.Exists(clientFilename))
            {
                process.Start();
                string currentClientVersion = process.MainModule.FileVersionInfo.ProductVersion;
                var releases = await ghClient.Repository.Release.GetAll("Qwizi", "Vulcan-Client-2");
                var latest = releases[0];
                if (latest.TagName != currentClientVersion)
                {
                    Console.WriteLine("Pobieramy najnowsza wersje Vulcana");
                    process.Kill();
                    var assets = latest.Assets;
                    foreach (var asset in assets) 
                    {
                        if (asset.Name == "binaries.zip")
                        {
                            string zipName = asset.Name;
                            string BrowserDownloadUrl = asset.BrowserDownloadUrl;
                            using (HttpClient client = new HttpClient())
                            {
                                client.DefaultRequestHeaders.Add("User-Agent", "Vulcan");
                                using (HttpResponseMessage res = await client.GetAsync(BrowserDownloadUrl,
                                    HttpCompletionOption.ResponseHeadersRead))
                                {
                                    res.EnsureSuccessStatusCode();
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
                                    process.Start();
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                Console.WriteLine("Pobieramy najnowsza wersje Vulcana");
                
                var releases = await ghClient.Repository.Release.GetAll("Qwizi", "Vulcan-Client-2");
                var latest = releases[0];
                var assets = latest.Assets;
                
                foreach (var asset in assets) 
                {
                    if (asset.Name == "binaries.zip")
                    {
                        string zipName = asset.Name;
                        string BrowserDownloadUrl = asset.BrowserDownloadUrl;
                        using (HttpClient client = new HttpClient())
                        {
                            client.DefaultRequestHeaders.Add("User-Agent", "Vulcan");
                            using (HttpResponseMessage res = await client.GetAsync(BrowserDownloadUrl,
                                HttpCompletionOption.ResponseHeadersRead))
                            {
                                res.EnsureSuccessStatusCode();
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
                                process.Start();
                            }
                        }
                    }
                }
            }

            Console.ReadLine();
        }

        private static void Socket_OnConnected(object sender, EventArgs e)
        {
            var socket = sender as SocketIO;
            Console.WriteLine("Updater polaczony " + socket.Id);
        }

        private static void Socket_onDisconnected(object sender, string e)
        {
            Console.WriteLine("Updater rozlaczony");
        }
    }
}