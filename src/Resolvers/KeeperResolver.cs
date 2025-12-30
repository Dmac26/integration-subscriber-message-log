using System;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Serilog;
using SecretsManager;
using System.Collections.Generic;

namespace Wachter.IntegrationSubscriberMessageLog.Resolvers
{
    public static class KeeperResolver
    {
        private static SecretsManagerOptions? _options;

        public static void Initialize()
        {
            //var configPath = ConfigurationManager.AppSettings["Keeper.Secret.Config.Location"];
            var configPath = "C:/ProgramData/KSM/config.json";
            if (string.IsNullOrWhiteSpace(configPath))
            {
                Log.Fatal("Keeper config path not set in AppSettings");
                return;
            }

            if (!File.Exists(configPath))
            {
                Log.Fatal("Keeper config file not found at {Path}", configPath);
                return;
            }

            try
            {
                var storage = new LocalConfigStorage(configPath);
                _options = new SecretsManagerOptions(storage);
                Log.Information("Keeper Secrets Manager initialized with config: {Path}", configPath);
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Failed to initialize Keeper Secrets Manager");
            }
        }

        public static async Task<string> GetPassphraseAsync(string secretTitle)
        {
            if (_options == null)
            {
                Log.Error("Keeper not initialized - cannot fetch secret {Title}", secretTitle);
                return string.Empty;
            }

            try
            {
                var records = await SecretsManagerClient.GetSecretsByTitle(_options, secretTitle);
                Log.Information("Records: {records}", records);
                var secret = records.LastOrDefault();

                if (secret == null)
                {
                    Log.Warning("No secret found for title {Title}", secretTitle);
                    return string.Empty;
                }

                var passphrase = secret.FieldValue("password")?.ToString() ?? string.Empty;
                if (string.IsNullOrEmpty(passphrase))
                {
                    Log.Warning("Passphrase field empty for title {Title}", secretTitle);
                }

                Log.Information("Successfully fetched passphrase for {Title}", secretTitle);
                return passphrase;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Keeper secret retrieval failed for title {Title}", secretTitle);
                return string.Empty;
            }
        }

        public static async Task<List<KeeperRecord>> GetSecretsAsync(string secretTitle)
        {
            if (_options == null)
            {
                Log.Error("Keeper not initialized");
                return new List<KeeperRecord>();
            }

            try
            {
                var records = await SecretsManagerClient.GetSecretsByTitle(_options, secretTitle);
                return records.ToList();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Keeper fetch failed for {Title}", secretTitle);
                return new List<KeeperRecord>();
            }
        }

        public static async Task<(string username, string password)> GetRabbitCredentialsAsync(string secretTitle)
        {
            if (_options == null)
            {
                Log.Error("Keeper not initialized");
                return (string.Empty, string.Empty);
            }

            try
            {
                var records = await SecretsManagerClient.GetSecretsByTitle(_options, secretTitle);
                var secret = records.FirstOrDefault();

                if (secret == null)
                {
                    Log.Warning("No secret found for title {Title}", secretTitle);
                    return (string.Empty, string.Empty);
                }

                // Adjust field names if your record uses different ones (common: "login", "user", "username")
                var username = secret.FieldValue("login")?.ToString()
                            ?? secret.FieldValue("username")?.ToString()
                            ?? secret.FieldValue("user")?.ToString()
                            ?? "guest";

                var password = secret.FieldValue("password")?.ToString() ?? string.Empty;

                Log.Information("Fetched Rabbit credentials from Keeper for {Title}", secretTitle);
                return (username, password);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Keeper Rabbit credentials fetch failed for {Title}", secretTitle);
                return (string.Empty, string.Empty);
            }
        }
    }
}