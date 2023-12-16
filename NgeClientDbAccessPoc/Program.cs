using Amazon;
using Amazon.Extensions.NETCore.Setup;
using Amazon.Runtime.CredentialManagement;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using System.Text.Json.Nodes;

namespace NgeClientDbAccessPoc
{
    public class Program
    {
        static string secretName = "prod-sqlserver-nge";

        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            var dbConnTuple = GetConnectionString();

			//store conn string in config
			builder.Configuration["NgeConnString"] = dbConnTuple.Item1;
            builder.Configuration["NgeDbHost"] = dbConnTuple.Item2;

            // Add services to the container.
            builder.Services.AddRazorPages();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
            }
            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthorization();

            app.MapRazorPages();

            app.Run();
        }

        static Tuple<string, string> GetConnectionString()
        {
			var client = GetSmClient();

			// Pull database credentials from secrets manager
			var request = new GetSecretValueRequest { SecretId = secretName };
			var connStringObj = JsonNode.Parse(client.GetSecretValueAsync(request).Result.SecretString)?.AsObject();

			if (connStringObj == null)
				throw new Exception("Could not parse GetSecretValueResponse.SecretString as JsonNode.");

			if (!connStringObj.ContainsKey("host"))
				throw new Exception("Could not find host in GetSecretValueResponse.SecretString.");

			if (!connStringObj.ContainsKey("username"))
				throw new Exception("Could not find username in GetSecretValueResponse.SecretString.");

			if (!connStringObj.ContainsKey("password"))
				throw new Exception("Could not find password in GetSecretValueResponse.SecretString.");

			if (!connStringObj.ContainsKey("dbname"))
				throw new Exception("Could not find dbname in GetSecretValueResponse.SecretString.");

			Console.WriteLine("Successfully retrieved secret from AWS Secrets Manager.");

			return new Tuple<string, string>(string.Format("server={0};uid={1};pwd={2};database={3};Encrypt=False",
				connStringObj["host"],
				connStringObj["username"],
				connStringObj["password"],
				connStringObj["dbname"]
				),
				(string)connStringObj["host"]);
		}

		static IAmazonSecretsManager GetSmClient()
		{
			// Note - this is only for demo purposes where I want to use AWS credentials in my local cred file - for production, 
			//	we only need to create a new SecretsManagerClient and it will pick up creds from container/instance IAM role
			var options = new AWSOptions { };
			var sharedFile = new SharedCredentialsFile();

			if (sharedFile.TryGetProfile("default", out var profile))
			{
				AWSCredentialsFactory.TryGetAWSCredentials(profile, sharedFile, out var credentials);
				options = new AWSOptions { Profile = "default", Credentials = credentials, Region = RegionEndpoint.USWest2 };
				secretName = "dev-sqlserver-nge";  //only for the PoC demo, if I need to run this app locally (and access db via public endpoint)
			};

			return options.CreateServiceClient<IAmazonSecretsManager>();
		}
	}
}