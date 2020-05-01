using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.ServiceBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Azure.Storage.Blobs;
using System.Diagnostics;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace ServiceBusTriggerTest
{
    public static class Function1
    {
        private static Settings settings;
        [FunctionName("Function1")]
        public static void Run([ServiceBusTrigger("defense", Connection = "ServiceBusConnectionString", IsSessionsEnabled =true)]string message, ILogger log, ExecutionContext context)
        {
            DefFfmpegArgs(message, log, context);
            log.LogInformation($"C# ServiceBus queue trigger function processed message: {message}");
        }

        public static Task<string> DefFfmpegArgs(string message, ILogger log, ExecutionContext context)
        {
            try
            {
                string outfile;
                string outputName;

                GetSettings(context, log);

                //var lines = await GetDefArgs("ffmpegargs", log);
                //List<string> args = new List<string>(lines.Split('\n'));

                //outfile = message.Substring(message.LastIndexOf("D:\\")).TrimEnd();
                //outputName = message.Substring(message.LastIndexOf("Temp\\") + 5).TrimEnd();

                outfile = message.Substring(message.LastIndexOf("C:\\")).TrimEnd();
                outputName = message.Substring(message.LastIndexOf("FfmpegArgs\\") + 11).TrimEnd();

                log.LogInformation("Play file location: " + outfile);
                log.LogInformation("Play file name: " + outputName);

                var psi = new ProcessStartInfo();
                psi.FileName = settings.ffmpegPath;

                psi.Arguments = message.TrimEnd();

                psi.UseShellExecute = false;

                if (settings.verboseFFMPEGLogging)
                {
                    psi.RedirectStandardError = true;
                    psi.RedirectStandardOutput = true;
                }

                log.LogInformation($"Args: {psi.Arguments}");
                log.LogInformation("Start exe");

                var process = Process.Start(psi);

                if (settings.verboseFFMPEGLogging)
                {
                    log.LogInformation(process.StandardError.ReadToEnd());
                    //log.LogInformation(process.StandardOutput.ReadToEnd());
                    process.BeginOutputReadLine();
                }

                process.WaitForExit();
                process.Close();

                log.LogInformation("Completed ffmpeg write");

                WritePlayVideoBlob(outputName, outfile, log);
            }
            catch (Exception ex)
            {
                log.LogInformation(ex.InnerException.ToString());
            }

            log.LogInformation($"{message} has been executed.");
            //return $"Successfully Completed";
            return Task.Delay(1000).ContinueWith(t => "Successfully Completed");
        }

        static void WritePlayVideoBlob(string filename, string filepath, ILogger log)
        {
            try
            {
                BlobServiceClient blobServiceClient = new BlobServiceClient(settings.outputStorageAccountConnStr);
                BlobContainerClient blobContainerClient = blobServiceClient.GetBlobContainerClient(settings.blobContainerName);

                BlobClient blobClient = blobContainerClient.GetBlobClient(filename);

                FileStream uploadFileStream = File.OpenRead(filepath);
                blobClient.Upload(uploadFileStream);
                uploadFileStream.Close();
            }
            catch (Exception ex)
            {
                log.LogInformation(ex.Message);
            }
        }

        static void GetSettings(ExecutionContext context, ILogger log)
        {
            try
            {
                var config = new ConfigurationBuilder()
                    .SetBasePath(context.FunctionAppDirectory)
                    .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
                    .AddEnvironmentVariables()
                    .Build();

                settings = new Settings();

                if (String.IsNullOrEmpty(config["ffmpegPath"]))
                {
                    settings.ffmpegPath = Path.Combine(@"D:\\home\site\wwwroot", "ffmpeg.exe");
                }
                else
                {
                    settings.ffmpegPath = config["ffmpegPath"];
                }

                if (String.IsNullOrEmpty(config["outputPath"]))
                {
                    settings.outputPath = Path.GetTempPath();
                }
                else
                {
                    settings.outputPath = config["outputPath"];
                }

                //Assume verbose logging is off unless set to true in config
                settings.verboseFFMPEGLogging = false;
                if (!String.IsNullOrEmpty(config["verboseFFMPEGLogging"]))
                {
                    if (config["verboseFFMPEGLogging"] == "true")
                    {
                        settings.verboseFFMPEGLogging = true;
                    }
                }

                settings.outputStorageAccountConnStr = config["VikingsStorageAccount"];
                settings.storageAccountName = config["storageAccountName"];
                settings.sasToken = config["sasToken"];
                settings.blobContainerName = "plays";
                if (!String.IsNullOrEmpty(config["blobContainerName"]))
                {
                    settings.blobContainerName = config["blobContainerName"];
                }
            }
            catch (Exception ex)
            {
                log.LogInformation(ex.Message);
            }
        }
    }
    class Settings
    {
        public string ffmpegPath { get; set; }
        public string outputPath { get; set; }
        public bool verboseFFMPEGLogging { get; set; }
        public string outputStorageAccountConnStr { get; set; }
        public string storageAccountName { get; set; }
        public string blobContainerName { get; set; }
        public string sasToken { get; set; }
    }
}
