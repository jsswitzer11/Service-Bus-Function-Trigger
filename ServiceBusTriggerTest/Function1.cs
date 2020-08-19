using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Azure.Storage.Blobs;
using System.Diagnostics;
using Newtonsoft.Json;

namespace ServiceBusTriggerTest
{
    public static class Function1
    {
        private static string Season;
        private static string SeasonType;
        private static string Week;
        private static string GameKey;
        private static Settings settings;
        [FunctionName("Function1")]
        public static void Run([ServiceBusTrigger("specialteams", Connection = "ServiceBusConnectionString")]string message, ILogger log, ExecutionContext context)
        {
            var newGameMessage = JsonConvert.DeserializeObject<messageBody>(message);
            Season = newGameMessage.season;
            SeasonType = newGameMessage.seasontype;
            Week = newGameMessage.week;
            GameKey = newGameMessage.gamekey;
            string command = newGameMessage.ffmpegCmd;

            DefFfmpegArgs(command, log, context);
            log.LogInformation($"C# ServiceBus queue trigger function processed message: {command}");
        }

        public static void DefFfmpegArgs(string message, ILogger log, ExecutionContext context)
        {
            try
            {
                string outfile;
                string outputName;

                GetSettings(context, log);

                outfile = message.Substring(message.LastIndexOf("D:\\")).TrimEnd();
                outputName = message.Substring(message.LastIndexOf("Temp\\") + 5).TrimEnd();

                //outfile = message.Substring(message.LastIndexOf("C:\\")).TrimEnd();
                //outputName = message.Substring(message.LastIndexOf("FfmpegArgs\\") + 11).TrimEnd();

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
                    process.BeginOutputReadLine();
                }

                process.WaitForExit();
                process.Close();

                log.LogInformation("Completed ffmpeg write");

                WritePlayVideoBlob(outputName, outfile, log);

                log.LogInformation($"{outputName} has been uploaded to Blob storage.");
            }
            catch (Exception ex)
            {
                log.LogInformation(ex.InnerException.ToString());
            }
        }

        static void WritePlayVideoBlob(string filename, string filepath, ILogger log)
        {
            try
            {
                BlobServiceClient blobServiceClient = new BlobServiceClient(settings.outputStorageAccountConnStr);
                BlobContainerClient blobContainerClient = blobServiceClient.GetBlobContainerClient($"{Season}/{SeasonType}/{Week}/{GameKey}/processed/");

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
        public string sasToken { get; set; }
    }
    class messageBody
    {
        public string season { get; set; }
        public string seasontype { get; set; }
        public string week { get; set; }
        public string gamekey { get; set; }
        public string ffmpegCmd { get; set; }
    }
}
