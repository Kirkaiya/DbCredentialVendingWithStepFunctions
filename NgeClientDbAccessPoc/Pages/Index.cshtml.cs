using Amazon.Runtime.CredentialManagement;
using Amazon.StepFunctions;
using Amazon.StepFunctions.Model;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Data.SqlClient;
using System.Data;

namespace NgeClientDbAccessPoc.Pages
{
    public class IndexModel : PageModel
    {
        const string stateMachineArn = "arn:aws:states:us-west-2:<AWS-account-id>:stateMachine:MyStateMachine-ahn8moy3j";
		const string tempUserName = "tempuser";

		public List<SelectListItem> DbItems { get; set; } = new List<SelectListItem>();
        public string? DbHost;
        public string? SelectedDB;
        public string? NewPassword;
        public string? TempUserConnStr;
        public string? ExecutionARN;
        public bool ShowCredsDiv;
        public bool ShowTestResult;
        public bool ConnSuccess;

		private string DbListConnString;
        private readonly IConfiguration Configuration;
        static readonly Random rnd = new();

		private static List<SelectListItem> dblist;

        public IndexModel(IConfiguration configuration)
        {
            Configuration = configuration;
            DbListConnString = Configuration["NgeConnString"] ?? "";

            if (DbListConnString == null)
                throw new Exception("Database connection string is not loaded into Configuration.");

            if (dblist == null)
                dblist = GetDatabaseList();
        }

        public void OnGet()
        {
            DbItems = dblist;
        }

        public void OnPost()
		{
			DbItems = dblist;
			SelectedDB = Request.Form["CustomerDB"][0];
			NewPassword = GetRandomPassword(10);
			ShowCredsDiv = true;

			// pass db name and new password as simple json
			var sfJson = "{\"database\": \"" + SelectedDB + "\", \"password\": \"" + NewPassword + "\"}";

			var sfRequest = new StartExecutionRequest
			{
				StateMachineArn = stateMachineArn,
				Input = sfJson
			};

			// invoke the AWS Step Functions state machine and pass the database and password
			AmazonStepFunctionsClient client = GetStepFunctionClient();

			var response = client.StartExecutionAsync(sfRequest).Result;
			ExecutionARN = response.ExecutionArn;

			//show the new connection string
			TempUserConnStr = $"server={Configuration["NgeDbHost"]};uid={tempUserName};pwd={NewPassword};database={SelectedDB};Encrypt=False";
		}

		public void OnPostTest()
        {
            ShowCredsDiv = true;
            ShowTestResult = true;
			TempUserConnStr = Request.Form["TempUserConnStr"][0];

			using (var conn = new SqlConnection(TempUserConnStr)) {

                try
                {
                    conn.Open();
					ConnSuccess = true;
				} catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                } finally
                {
                    conn.Close();
                }
			}
		}

        private string GetRandomPassword(int stringLength)
        {
            const string allowedChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz0123456789!@$_-";
            char[] chars = new char[stringLength];

            for (int i = 0; i < stringLength; i++)
                chars[i] = allowedChars[rnd.Next(0, allowedChars.Length)];

            return new string(chars) + "@1";
        }

        private List<SelectListItem> GetDatabaseList()
        {
            var dbList = new List<SelectListItem>();
            var query = "SELECT name from sys.databases WHERE name NOT IN ('master','tempdb','model','msdb')";

            using (SqlConnection conn = new SqlConnection(DbListConnString))
            {
                conn.Open();

                using SqlCommand cmd = new SqlCommand(query, conn);
                using IDataReader dr = cmd.ExecuteReader();
                while (dr.Read())
                {
                    dbList.Add(
                        new SelectListItem
                        {
                            Text = dr[0].ToString(),
                            Value = dr[0].ToString()
                        }
                    );
                }
            }
            return dbList;
        }

		private static AmazonStepFunctionsClient GetStepFunctionClient()
		{
			AmazonStepFunctionsClient client;

			//if running locally, pull credentials from .aws/credentials file, otherwise assume IAM role
			var sharedFile = new SharedCredentialsFile();
			if (sharedFile.TryGetProfile("default", out var profile))
			{
				AWSCredentialsFactory.TryGetAWSCredentials(profile, sharedFile, out var credentials);
				client = new AmazonStepFunctionsClient(credentials);
			}
			else
			{
				client = new AmazonStepFunctionsClient();
			}

			return client;
		}

    }
}