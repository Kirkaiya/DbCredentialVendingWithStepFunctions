using Amazon.Lambda.Core;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Microsoft.Data.SqlClient;
using Microsoft.IdentityModel.Tokens;
using System.Text.Json.Nodes;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace NgeCreateSqlLoginLambda;

public class Function
{
	const string secretName = "prod-sqlserver-nge";
	const string tempUserName = "tempuser";
	private static string connString = "";

	/// <summary>
	/// Creates a database login and user associated with specified database access
	/// </summary>
	/// <param name="input">JSON bag from Step Functions state machine having dbname and password</param>
	/// <param name="context"></param>
	public void FunctionHandler(StateMachineEvent input, ILambdaContext context)
    {
        var dbName = input.database;
        var passwd = input.password;

        var createLoginSql = $"CREATE LOGIN {tempUserName} WITH PASSWORD = '{passwd}', DEFAULT_DATABASE = {dbName}";
        var createUserSql = $"CREATE USER {tempUserName} FOR LOGIN tempuser";

		// fetch db credentials from Secrets Manager if we don't already have them
		SetDbConnectionString();

		using (var conn = new SqlConnection(string.Format(connString, dbName)))
        {
            conn.Open();

            // create login
            var cmd = new SqlCommand(createLoginSql, conn);
            cmd.ExecuteNonQuery();

            // create db user
            cmd = new SqlCommand(createUserSql, conn);
            cmd.ExecuteNonQuery();

			// ********************************************************
			// Add any custom application-credential vending logic here
			// ********************************************************

			LambdaLogger.Log($"User {tempUserName} created in {dbName}");
        }
    }

	private static void SetDbConnectionString()
	{
		if (string.IsNullOrWhiteSpace(connString))
		{
			var client = new AmazonSecretsManagerClient();
			var request = new GetSecretValueRequest { SecretId = secretName };
			var connStringObj = JsonNode.Parse(client.GetSecretValueAsync(request).Result.SecretString);

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
