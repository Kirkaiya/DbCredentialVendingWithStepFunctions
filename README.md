# DbCredentialVendingWithStepFunctions
Proof of concept solution to allow creating/revoking SQL Server database credentials from ASP.NET Core using Step Functions state machine to orchesrate two Lambda functions that do the actual create/drop SQL user and login.


![Architecture diagram](https://raw.githubusercontent.com/Kirkaiya/DbCredentialVendingWithStepFunctions/main/arch-diagram.png)
