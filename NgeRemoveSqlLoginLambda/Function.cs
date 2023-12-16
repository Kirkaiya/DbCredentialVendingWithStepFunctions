using Amazon.Lambda.Core;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Microsoft.Data.SqlClient;
using Microsoft.IdentityModel.Tokens;
using System.Text.Json.Nodes;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace NgeRemoveSqlLoginLambda;

public class Function
{
	const string secretName = "prod-sqlserver-nge";
	const string tempUserName = "tempuser";
	private static string connString = "";

	/// <summary>
	/// Removes a database login and user
	/// </summary>
	/// <param name="input">JSON bag from Step Functions state machine having dbname and password</param>
	/// <param name="context"></param>
	public void FunctionHandler(StateMachineEvent input, ILambdaContext context)
	{
		const string killSql = $"SELECT session_id FROM sys.dm_exec_sessions WHERE login_name = '{tempUserName}' AND is_user_process = 1";
		const string dropUserSql = $"DROP USER IF EXISTS {tempUserName}";
		const string dropLoginSql = $"DROP LOGIN {tempUserName}";

		var dbName = input.database;

		LambdaLogger.Log("Fetching credentials from Secrets Manager");

		// fetch db credentials from Secrets Manager if we don't already have them
		SetDbConnectionString();

		LambdaLogger.Log("Connecting to database to kill any existing connection");
		// kill active connection if user is still connected
		using (var conn = new SqlConnection(string.Format(connString, "master")))
		{
			conn.Open();

			var cmd = new SqlCommand(killSql, conn);
			var pid = cmd.ExecuteScalar();
			if (pid != null)
			{
				cmd = new SqlCommand("KILL " + pid.ToString(), conn);
				cmd.ExecuteNonQuery();
			}
		}

		LambdaLogger.Log("Connecting to database to remove user and login");
		// drop the tempuser user and login
		using (var conn = new SqlConnection(string.Format(connString, dbName)))
		{
			conn.Open();

			// Drop database user
			var cmd = new SqlCommand(dropUserSql, conn);
			try
			{
				cmd.ExecuteNonQuery();
				LambdaLogger.Log($"Dropped SQL User {tempUserName}");
			}
			catch { }
			

			// Drop database login
			try
			{
				cmd = new SqlCommand(dropLoginSql, conn);
				cmd.ExecuteNonQuery();
				LambdaLogger.Log($"Dropped SQL Login {tempUserName}");
			}
			catch { }
		}

		// ***********************************************************
		// Add any custom application-credential revocation logic here
		// ***********************************************************
	}

	private static void SetDbConnectionString()
	{
		if (string.IsNullOrWhiteSpace(connString))
		{
			var client = new AmazonSecretsManagerClient();
			var request = new GetSecretValueRequest { SecretId = secretName };
			var connStringObj = JsonNode.Parse(client.GetSecretValueAsync(request).Result.SecretString)?.AsObject();

			if (connStringObj == null)
				throw new Exception("Could not parse GetSecretValueResponse.SecretString as JsonNode.");

			connString = "server=" + (string?)connStringObj["host"] + "; uid=" + (string?)connStringObj["username"] +
				"; pwd=" + (string?)connStringObj["password"] + "; database={0};Encrypt=False";
		}
	}
}

public class StateMachineEvent
{
    public string? database { get; set; }
    public string? password { get; set; }
}

