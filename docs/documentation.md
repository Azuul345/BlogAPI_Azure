# Azure App Service Deployment - Assignment Documentation

**Student:** Diego Oviedo  
**Course:** Cloud Native Development  
**Date:** May 8, 2026  

---

## Phase 0: Prerequisites & Project Preparation

### Objective
Prepare the .NET Web API project and development environment for Azure deployment using CLI scripts. Ensure all Azure integrations (Key Vault, Application Insights, Blob Storage) are configured but only activate when running on Azure.

---

### Step 0.1: Verify and Install Required NuGet Packages

**Command to check installed packages:**
```bash
dotnet list package
```

**Required packages:**
- `Azure.Extensions.AspNetCore.Configuration.Secrets` - For Key Vault integration
- `Azure.Identity` - For Managed Identity authentication
- `Azure.Storage.Blobs` - For Azure Blob Storage operations
- `Microsoft.ApplicationInsights.AspNetCore` - For Application Insights monitoring
- `Microsoft.EntityFrameworkCore.SqlServer` - For SQL Server database connectivity

**Installation commands (if any are missing):**
```bash
dotnet add package Azure.Extensions.AspNetCore.Configuration.Secrets
dotnet add package Azure.Identity
dotnet add package Azure.Storage.Blobs
dotnet add package Microsoft.ApplicationInsights.AspNetCore
dotnet add package Microsoft.EntityFrameworkCore.SqlServer
```

**Why these packages?**
- **Azure.Identity**: Enables Managed Identity so the App Service can authenticate to Key Vault without hardcoded credentials
- **Azure.Extensions.AspNetCore.Configuration.Secrets**: Allows the app to read secrets from Key Vault and inject them into IConfiguration
- **Azure.Storage.Blobs**: Required for storing files (images, logs) in Azure Blob Storage
- **Microsoft.ApplicationInsights.AspNetCore**: Collects telemetry, logs, and performance data for monitoring

---

### Step 0.2: Configure appsettings Files (Remove Hardcoded Secrets)

**Modified `appsettings.Development.json`:**
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

**Content of `appsettings.json`:**
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

**Why?**
- Connection strings and secrets must never be committed to source control
- All sensitive data will be stored in Azure Key Vault and accessed via Managed Identity
- These files now contain only non-sensitive configuration

---

### Step 0.3: Create BlobStorageService.cs

Created `BlobStorageService.cs` in the project root to handle all Azure Blob Storage operations.

**Key Design Decisions:**
- The `_container` field is nullable (`?`) because locally there is no storage connection string
- Null checks in each method prevent crashes when running locally
- Storage features only activate when deployed to Azure

**File: `BlobStorageService.cs`**
```csharp
using Azure.Storage.Blobs;

namespace BlogAPI
{
    public class BlobStorageService
    {
        private readonly BlobContainerClient? _container;

        public BlobStorageService(IConfiguration configuration)
        {
            var connectionString = configuration["StorageConnectionString"];
            if (!string.IsNullOrEmpty(connectionString))
            {
                var serviceClient = new BlobServiceClient(connectionString);
                _container = serviceClient.GetBlobContainerClient("uploads");
            }
        }

        public async Task<string> UploadFileAsync(Stream fileStream, string fileName)
        {
            if (_container == null)
                throw new InvalidOperationException("Storage is not configured.");
            var blobClient = _container.GetBlobClient(fileName);
            await blobClient.UploadAsync(fileStream, overwrite: true);
            return blobClient.Uri.ToString();
        }

        public async Task<IEnumerable<string>> ListFilesAsync()
        {
            if (_container == null) return Enumerable.Empty<string>();
            var files = new List<string>();
            await foreach (var blob in _container.GetBlobsAsync())
                files.Add(blob.Name);
            return files;
        }

        public async Task DeleteFileAsync(string fileName)
        {
            if (_container == null) return;
            var blobClient = _container.GetBlobClient(fileName);
            await blobClient.DeleteIfExistsAsync();
        }
    }
}
```

**How it works:**
- **Constructor**: Reads `StorageConnectionString` from configuration (will come from Key Vault on Azure)
- **UploadFileAsync**: Uploads a file to the "uploads" container and returns its public URL
- **ListFilesAsync**: Lists all files in the container
- **DeleteFileAsync**: Deletes a specific file by name

---

### Step 0.4: Configure Program.cs

Updated `Program.cs` to integrate Key Vault, Application Insights, and Blob Storage with conditional activation (only runs on Azure).

**File: `Program.cs`**
```csharp
using AutoMapper;
using BlogAPI.Data;
using BlogAPI.Profiles;
using BlogAPI.Repositories.Implementations;
using BlogAPI.Repositories.Interfaces;
using BlogAPI.Services.Implementations;
using BlogAPI.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Azure.Identity;

namespace BlogAPI
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Key Vault (only on Azure)
            var keyVaultUri = builder.Configuration["KeyVaultUri"];
            if (!string.IsNullOrEmpty(keyVaultUri))
            {
                builder.Configuration.AddAzureKeyVault(
                    new Uri(keyVaultUri),
                    new DefaultAzureCredential()
                );
            }

            // Database
            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
            builder.Services.AddDbContext<AppDbContext>(options =>
                options.UseSqlServer(connectionString));

            // Application Insights (only on Azure)
            var appInsightsConnection = builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
            if (!string.IsNullOrEmpty(appInsightsConnection))
            {
                builder.Services.AddApplicationInsightsTelemetry();
            }

            // Blob Storage
            builder.Services.AddSingleton<BlobStorageService>();

            // AutoMapper
            builder.Services.AddAutoMapper(typeof(Program));

            // Repositories
            builder.Services.AddScoped<IPostRepository, PostRepository>();
            builder.Services.AddScoped<ICommentRepository, CommentRepository>();
            builder.Services.AddScoped<IUserRepository, UserRepository>();

            // Services
            builder.Services.AddScoped<IPostService, PostService>();
            builder.Services.AddScoped<ICommentService, CommentService>();
            builder.Services.AddScoped<IUserService, UserService>();

            // API & Swagger
            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            // CORS
            builder.Services.AddCors();

            var app = builder.Build();

            app.UseSwagger();
            app.UseSwaggerUI();

            app.UseHttpsRedirection();

            app.UseCors(policy => policy
                .AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader());

            app.UseAuthorization();
            app.MapControllers();
            app.Run();
        }
    }
}
```

**Key Configuration Points:**

1. **Key Vault Integration**:
   - Only loads when `KeyVaultUri` app setting exists (set via CLI on Azure)
   - Uses `DefaultAzureCredential` for Managed Identity authentication

2. **Application Insights**:
   - Only activates when `APPLICATIONINSIGHTS_CONNECTION_STRING` exists (set via CLI)
   - Without the null check, the app would crash locally

3. **Database Connection**:
   - On Azure: reads `DefaultConnection` from Key Vault
   - Locally: would need User Secrets 

4. **Blob Storage**:
   - Registered as singleton
   - Handles null check internally in `BlobStorageService.cs`

---

### Step 0.5: Verify Azure CLI Installation

**Verified Azure CLI:**


---


## Phase 1: Create Azure Resources

In this phase we create all the Azure resources needed to host, secure, and monitor the BlogAPI.
All commands are run in PowerShell inside Visual Studio (or Windows Terminal).

---

### Step 1.1: Login to Azure CLI

This connects your local terminal to your Azure account. A browser window will open automatically — sign in with your school Microsoft account.

**Command:**
```powershell
az login
```

**What to look for:** After signing in, your terminal will show a list of subscriptions. This means the login was successful.

---

### Step 1.2: Set the Correct Subscription

This makes sure all commands go to the correct school Azure subscription.

**Command:**
```powershell
az account set --subscription "SUB-Utbildning-DotNetCloudDeveloper-2026-VT-Mars-Goteborg"
```

**Verify it worked:**
```powershell
az account show
```

**What to look for:** You should see `"name": "SUB-Utbildning-DotNetCloudDeveloper-2026-VT-Mars-Goteborg"` and `"isDefault": true` in the output.

---

### Step 1.3: Set Default Resource Group & Location

This saves you from typing `--resource-group` and `--location` in every future command.

**Commands:**
```powershell
az configure --defaults group="RG-Diego-Oviedo-245b81-DotNetCloudDeveloper-VT-Mars-Goteborg"
```
```powershell
az configure --defaults location="swedencentral"
```

**Verify it worked:**
```powershell
az configure --list-defaults
```

**What to look for:** You should see both `group` and `location` listed with the correct values.

---

### Step 1.4: Create Azure SQL Server

Creates the SQL Server instance that will host the database. Think of this as the "container" — the actual database comes in the next step.

**Command:**
```powershell
az sql server create --name "blogapi-sql-server-diego" --admin-user "sqladmin" --admin-password "BlogAPI_SQL_P@ssw0rd2026"
```

**What to look for:** A JSON response with `"state": "Ready"` confirms the server was created successfully.

> ⚠️ **Write down the admin password** — `BlogAPI_SQL_P@ssw0rd2026`. It will be stored securely in Key Vault in Phase 2.

---

### Step 1.5: Create Azure SQL Database

Creates the actual database inside the SQL Server. Basic tier with 5 DTUs is the minimum required and keeps costs low.

**Command:**
```powershell
az sql db create --name "blogapi-db" --server "blogapi-sql-server-diego" --tier "Basic" --capacity 5
```

**What to look for:** A JSON response with `"status": "Online"` confirms the database was created successfully.

---

### Step 1.6: Create Azure Storage Account

Creates the Storage Account where the API will store uploaded files. Storage account names must be all lowercase with no dashes.

**Command:**
```powershell
az storage account create --name "blogapistoratediego" --sku "Standard_LRS" --kind "StorageV2"
```

**What to look for:** A JSON response with `"provisioningState": "Succeeded"` confirms the storage account was created successfully.

---

### Step 1.7: Create Blob Container

Creates the `uploads` container inside the Storage Account. This is the actual folder where files will be stored. This name must match what is set in `BlobStorageService.cs`.

**First, retrieve your storage account key:**
```powershell
az storage account keys list --account-name "blogapistoratediego" --query "[0].value" -o tsv
```

**What to look for:** A long string ending in `==`. Copy the entire string including the `==`.

**Then create the container** (replace `<YOUR-KEY>` with the key you just copied):
```powershell
az storage container create --name "uploads" --account-name "blogapistoratediego" --account-key "<YOUR-KEY>"
```

**What to look for:** `{ "created": true }` confirms the container was created successfully.

---

### Step 1.8: Create Azure Key Vault

Creates the Key Vault where all secrets (passwords, connection strings) will be stored securely. The app retrieves these automatically via Managed Identity — no hardcoded secrets needed.

**Command:**
```powershell
az keyvault create --name "blogapi-keyvault-diego2" --sku "standard"
```

**What to look for:** A JSON response with `"provisioningState": "Succeeded"` confirms the Key Vault was created successfully.

> **Write down the `vaultUri` value** from the output — it will look like:
> `"vaultUri": "https://blogapi-keyvault-diego.vault.azure.net/"`
> This is needed when configuring the App Service in Phase 2.

Note: If the Key Vault name blogapi-keyvault-diego is already taken or still in soft-delete, you may need to adjust the name, for example to blogapi-keyvault-diego2. Make sure to use the same name in all later commands (for example when storing secrets and when setting KeyVaultUri on the App Service)

!!blogapi-keyvault-diego2 taken for test, next is 3.!!


---

### Step 1.9: Create App Service Plan

Creates the App Service Plan — this is the underlying server/hardware that your web app will run on. B1 (Basic) tier is required.

**Command:**
```powershell
az appservice plan create --name "blogapi-plan-diego" --sku "B1" --is-linux
```

**What to look for:** A JSON response with `"provisioningState": "Succeeded"` confirms the plan was created successfully.

---

### Step 1.10: Create App Service (Web App)

Creates the actual Web App that will host and run the BlogAPI. Note: Azure uses `DOTNETCORE` (not `DOTNET`) for the runtime name, and single quotes are required in PowerShell to avoid the `|` symbol being misread.

**Command:**
```powershell
az webapp create --name "blogapi-webapp-diego" --plan "blogapi-plan-diego" --runtime 'DOTNETCORE:10.0'
```

**What to look for:** A JSON response containing `"defaultHostName": "blogapi-webapp-diego.azurewebsites.net"` confirms the web app was created successfully.

> **Write down the `defaultHostName` URL** — `blogapi-webapp-diego.azurewebsites.net`. This is the public address of your API once deployed.

---

### Step 1.11: Create Application Insights

Creates the Application Insights resource that monitors your app — tracking requests, response times, errors, and logs.

**Command:**
```powershell
az monitor app-insights component create --app "blogapi-insights-diego" --kind "web" --application-type "web"
```

**What to look for:** A JSON response with `"provisioningState": "Succeeded"` confirms Application Insights was created successfully.

> **Write down the `connectionString` value** from the output — it starts with `InstrumentationKey=...`. It will be stored in Key Vault in Phase 2.

---

## Phase 2: Configure Secrets & Managed Identity

In this phase we securely connect all Azure resources together. All sensitive values
are stored in Key Vault and the App Service is given permission to read them
automatically using Managed Identity — no passwords in the code!

---

### Step 2.1: Grant Yourself Permission to Write to Key Vault

By default, even the creator of a Key Vault cannot write secrets to it. We need to
explicitly grant our own user account permission first.

**Get your own Object ID:**
```powershell
az ad signed-in-user show --query id -o tsv
```

**What to look for:** A string of letters and numbers — copy the entire string.
This is your personal Azure user ID.

**Then grant yourself the Key Vault Secrets Officer role.**
Replace `<YOUR-OBJECT-ID>` with the string you just copied (no angle brackets):
```powershell
az role assignment create --role "Key Vault Secrets Officer" --assignee "<YOUR-OBJECT-ID>" --scope "/subscriptions/457c50ad-2cb0-4bed-9fea-fbdf6eed15bf/resourcegroups/RG-Diego-Oviedo-245b81-DotNetCloudDeveloper-VT-Mars-Goteborg/providers/Microsoft.KeyVault/vaults/blogapi-keyvault-diego2"
```

!! blogapi-keyvault-diego2 used for test!! 


**What to look for:** A JSON response with `"principalType": "User"` confirms the permission was granted successfully.

> ⚠️ **Wait 30–60 seconds** before moving to the next step — Azure needs time to apply the new permissions!

---

### Step 2.2: Store SQL Connection String in Key Vault

This stores the database connection string securely in Key Vault so the app can
connect to the SQL Database without any passwords in the code.

⚠️ **Important naming rule:** .NET reads the connection string using
`ConnectionStrings:DefaultConnection`. Azure Key Vault uses `--` to represent
the `:` separator in nested config names. So the secret MUST be named
`ConnectionStrings--DefaultConnection` (with double dashes) — not just
`DefaultConnection`.

**Run this command:**
```powershell
az keyvault secret set --vault-name "blogapi-keyvault-diego2" --name "ConnectionStrings--DefaultConnection" --value "Server=tcp:blogapi-sql-server-diego.database.windows.net,1433;Initial Catalog=blogapi-db;Persist Security Info=False;User ID=sqladmin;Password=BlogAPI_SQL_P@ssw0rd2026;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
```
!! "blogapi-keyvault-diego2" used for test !!

**What to look for:** A JSON response with `"name": "ConnectionStrings--DefaultConnection"` confirms the secret was stored successfully. ✅

---

### Step 2.3: Store Application Insights Connection String in Key Vault

This stores the Application Insights connection string so the app can send
monitoring data to Azure.

**Run this command:**
```powershell
az keyvault secret set --vault-name "blogapi-keyvault-diego2" --name "APPLICATIONINSIGHTS-CONNECTION-STRING" --value "InstrumentationKey=5e338946-820d-4010-a8ce-282652942e3d;IngestionEndpoint=https://swedencentral-0.in.applicationinsights.azure.com/;LiveEndpoint=https://swedencentral.livediagnostics.monitor.azure.com/;ApplicationId=0761f09b-cac5-4ed4-9388-f9e78633d8be"
```

**What to look for:** A JSON response with `"name": "APPLICATIONINSIGHTS-CONNECTION-STRING"` confirms the secret was stored successfully.

---

### Step 2.4: Get the Storage Connection String

This retrieves the full Storage Account connection string generated by Azure.

**Run this command:**
```powershell
az storage account show-connection-string --name "blogapistoratediego" -o tsv
```

**What to look for:** A long string starting with `DefaultEndpointsProtocol=https;...` — copy the entire string including the end.

---

### Step 2.5: Store Storage Connection String in Key Vault

This stores the Storage connection string so the app can upload and retrieve files.
Replace `<YOUR-STORAGE-CONNECTION-STRING>` with the string copied in the previous step (no angle brackets):

**Run this command:**
```powershell
az keyvault secret set --vault-name "blogapi-keyvault-diego2" --name "StorageConnectionString" --value "<YOUR-STORAGE-CONNECTION-STRING>"
```

**What to look for:** A JSON response with `"name": "StorageConnectionString"` confirms the secret was stored successfully.

---

### Step 2.6: Enable Managed Identity on the App Service

This gives the App Service its own Azure identity so it can securely access
Key Vault without any hardcoded credentials.

**Run this command:**
```powershell
az webapp identity assign --name "blogapi-webapp-diego"
```

**What to look for:** A JSON response with `"type": "SystemAssigned"` confirms Managed Identity is enabled.

> 📝 **Write down the `principalId` value** from the output — you will need it in the next step.

---

### Step 2.7: Grant the App Service Access to Key Vault

This gives the App Service permission to read secrets from Key Vault.
Replace `<APP-SERVICE-PRINCIPAL-ID>` with the `principalId` value from the previous step (no angle brackets):

**Run this command:**
```powershell
az role assignment create --role "Key Vault Secrets User" --assignee "<APP-SERVICE-PRINCIPAL-ID>" --scope "/subscriptions/457c50ad-2cb0-4bed-9fea-fbdf6eed15bf/resourcegroups/RG-Diego-Oviedo-245b81-DotNetCloudDeveloper-VT-Mars-Goteborg/providers/Microsoft.KeyVault/vaults/blogapi-keyvault-diego2"
```

**What to look for:** A JSON response with `"principalType": "ServicePrincipal"` confirms the access was granted successfully.

---

### Step 2.8: Set the Key Vault URI on the App Service

This tells the App Service where Key Vault is located so it can fetch secrets
automatically when it starts up.

**Run this command:**
```powershell
az webapp config appsettings set --name "blogapi-webapp-diego" --settings 'KeyVaultUri=https://blogapi-keyvault-diego2.vault.azure.net/'
```

**Verify it was saved correctly:**
```powershell
az webapp config appsettings list --name "blogapi-webapp-diego"
```

**What to look for:** `"name": "KeyVaultUri"` with `"value": "https://blogapi-keyvault-diego3.vault.azure.net/"` confirms the setting was saved correctly.

> ⚠️ **Note:** The `set` command may display `"value": null` in its output — this is a known display bug in Azure CLI. Always use the `list` command above to verify the value was actually saved correctly.

### Step 2.9: Set Application Insights Connection String as App Setting

Application Insights requires its connection string to be set directly as an
App Service app setting — not just in Key Vault — to fully activate the SDK.

**Run this command:**
```powershell
az webapp config appsettings set --name "blogapi-webapp-diego" --settings "APPLICATIONINSIGHTS_CONNECTION_STRING=<Instrumentation-Key>"
```

**What to look for:** A JSON response showing `"name": "APPLICATIONINSIGHTS_CONNECTION_STRING"` confirms it was set successfully. ✅

---

## Phase 3: Configure Security

In this phase we secure the App Service by forcing HTTPS, restricting access to
specific IP addresses, and setting up automatic daily backups.

---

### Step 3.1: Enable HTTPS-Only

This forces all traffic to use HTTPS. Any HTTP request is automatically redirected
to HTTPS so all communication is always encrypted.

**Run this command:**
```powershell
az webapp update --name "blogapi-webapp-diego" --https-only true
```

**What to look for:** `"httpsOnly": true` in the JSON response confirms HTTPS-only is enabled. ✅

---

### Step 3.2: Find Your Current Public IP Address

We need your current public IP address to create an access rule that allows you
into the API while blocking everyone else.

**Run this command:**
```powershell
Invoke-RestMethod -Uri "https://api.ipify.org"
```

**What to look for:** A simple IP address like `213.64.158.99` — copy it.

> ⚠️ **Note:** Your IP address may change if you switch networks (e.g. home to school).
> If you cannot access the API later, run this command again to get your new IP and
> add a new rule in Step 3.3.

---

### Step 3.3: Configure IP Restrictions

This adds an allow rule for your IP address and automatically blocks all other traffic.
Replace `<YOUR-IP>` with the IP address from the previous step (no angle brackets):

**Run this command:**
```powershell
az webapp config access-restriction add --name "blogapi-webapp-diego" --rule-name "AllowMyIP" --action Allow --ip-address "<YOUR-IP>/32" --priority 100
```

**What to look for:** The output should show two rules:
- `"name": "AllowMyIP"`, `"action": "Allow"` — your IP is allowed ✅
- `"name": "Deny all"`, `"action": "Deny"` — everything else is blocked ✅

---

### Step 3.4: Create a Backup Container

This creates a separate container in the existing Storage Account specifically
for storing App Service backup files.

**Run this command:**
```powershell
az storage container create --name "backups" --account-name "blogapistoratediego" --account-key "<ACCOUNT-KEY>"
```

**What to look for:** `{ "created": true }` confirms the container was created successfully. ✅

---

### Step 3.5: Generate a SAS Token for the Backup Container

A SAS token is a temporary access pass that gives the App Service permission to
write backups into the container. It expires on January 1st 2027.

**Run this command:**
```powershell
az storage container generate-sas --name "backups" --account-name "blogapistoratediego" --account-key "<ACCOUNT-KEY>" --permissions dlrw --expiry "2027-01-01" --output tsv
```

**What to look for:** A long string starting with `se=...` — copy the entire string.

> 📝 **Write down the SAS token** — you will need it combined with the container URL in the next step.

---

### Step 3.6: Configure Daily Backup Schedule

This sets up automatic daily backups of the App Service using the backup container
and SAS token from the previous steps.

The full container URL is built by combining:
- Base URL: `https://blogapistoratediego.blob.core.windows.net/backups?`
- SAS token: the string copied from the previous step

**Run this command:**
```powershell
az webapp config backup create --webapp-name "blogapi-webapp-diego" --container-url "https://blogapistoratediego.blob.core.windows.net/backups?<SAS-TOKEN>" --frequency 1d --retain-one true
```

**What to look for:**
- `"status": "Created"` — backup schedule was created successfully ✅
- `"storageAccountUrl"` present — correctly pointing to the backups container ✅

> ⚠️ **Note:** After this command runs, PowerShell may show errors like `'sp' is not recognized`
> and `'sv' is not recognized`. This is a known PowerShell display issue with `&` symbols in the
> URL and is completely harmless. Always check the JSON response above the errors to confirm success.

---


## Phase 4: Deploy Application

In this phase we set up an automated deployment pipeline using GitHub Actions.
Every time code is pushed to the main branch, GitHub automatically builds and
deploys the latest version to Azure App Service.

---

### Step 4.1: Create a Service Principal for GitHub Actions

A Service Principal is a special account that GitHub Actions uses to authenticate
to Azure and deploy the application.

**Command:**
```powershell
az ad sp create-for-rbac --name "blogapi-github-actions" --role contributor --scopes "/subscriptions/457c50ad-2cb0-4bed-9fea-fbdf6eed15bf/resourcegroups/RG-Diego-Oviedo-245b81-DotNetCloudDeveloper-VT-Mars-Goteborg" --sdk-auth
```

**Why:** Creates a robot account with Contributor access to the resource group,
and outputs credentials in the exact JSON format GitHub Actions expects.

> 📝 Copy the entire JSON block from the output — it is needed in the next step.

---

### Step 4.2: Install GitHub CLI

GitHub CLI is needed to store the Service Principal credentials as a GitHub secret
directly from the terminal.

**Command:**
```powershell
winget install --id GitHub.cli
```

**Why:** Allows us to manage GitHub repositories and secrets from the terminal
without using the browser.

> ⚠️ After installation, close and reopen Visual Studio entirely before continuing.
> The terminal will not recognize `gh` until it is restarted.

**Verify the install:**
```powershell
gh --version
```

**What to look for:** `gh version 2.x.x` confirms the install was successful.

---

### Step 4.3: Authenticate GitHub CLI

**Command:**
```powershell
gh auth login
```

Follow the prompts:
- Account: `GitHub.com`
- Protocol: `HTTPS`
- Authenticate Git: `Yes`
- Method: `Login with a web browser`

Enter the one-time code shown in the terminal into the browser when prompted.

**What to look for:** `✓ Logged in as [your username]` confirms authentication was successful.

---

### Step 4.4: Add Service Principal as a GitHub Secret


This stores the Azure credentials securely in GitHub so the workflow can use them.

**Step 1 — Save the JSON to a temp file.** Copy the entire block below, replacing the values with the ones from Step 4.1, then paste it into PowerShell and press `Enter`:

```powershell
@'
{
  "clientId": "YOUR-CLIENT-ID",
  "clientSecret": "YOUR-CLIENT-SECRET",
  "subscriptionId": "YOUR-SUBSCRIPTION-ID",
  "tenantId": "YOUR-TENANT-ID",
  "activeDirectoryEndpointUrl": "https://login.microsoftonline.com",
  "resourceManagerEndpointUrl": "https://management.azure.com/",
  "activeDirectoryGraphResourceId": "https://graph.windows.net/",
  "sqlManagementEndpointUrl": "https://management.core.windows.net:8443/",
  "galleryEndpointUrl": "https://gallery.azure.com/",
  "managementEndpointUrl": "https://management.core.windows.net/"
}
'@ | Out-File -FilePath "$env:TEMP\creds.json" -Encoding utf8
```

**What to look for:** No output and no errors — the file was saved silently.

**Step 2 — Upload the secret to GitHub:**

```powershell
Get-Content "$env:TEMP\creds.json" | gh secret set AZURE_CREDENTIALS --repo Azuul345/BlogAPI_Azure
```

**What to look for:** `✓ Set Actions secret AZURE_CREDENTIALS for Azuul345/BlogAPI_Azure` ✅

**Step 3 — Delete the temp file:**

```powershell
Remove-Item "$env:TEMP\creds.json"
```

**What to look for:** No output and no errors — the file was deleted successfully.

**Step 4 — Verify the secret exists in GitHub:**

```powershell
gh secret list --repo Azuul345/BlogAPI_Azure
```

**What to look for:** `AZURE_CREDENTIALS` appears in the list with a recent timestamp. GitHub never shows secret values for security reasons, but seeing the name confirms it was stored successfully. ✅
---

### Step 4.5: Create the GitHub Actions Workflow File

First, navigate to the repository root and create the required folder structure:

```powershell
cd C:\Users\diego\Documents\ITHS-DM\Labb1\BlogAPI_Azure
```
```powershell
New-Item -ItemType Directory -Path .github/workflows -Force
```

Then create the workflow file:

```powershell
@"
name: Deploy BlogAPI to Azure

on:
  push:
    branches:
      - main

jobs:
  build-and-deploy:
    runs-on: ubuntu-latest

    steps:
      - name: Checkout code
        uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Build
        run: dotnet build --configuration Release

      - name: Publish
        run: dotnet publish --configuration Release --output ./publish

      - name: Login to Azure
        uses: azure/login@v2
        with:
          creds: `${{ secrets.AZURE_CREDENTIALS }}

      - name: Deploy to Azure App Service
        uses: azure/webapps-deploy@v3
        with:
          app-name: blogapi-webapp-diego
          package: ./publish
"@ | Out-File -FilePath .github/workflows/deploy.yml -Encoding UTF8
```

**Why:** This file tells GitHub Actions to automatically build and deploy the API
to Azure every time code is pushed to the main branch.

---


### Step 4.6: Push Code and Trigger Deployment

```powershell
git add .
git commit -m "Add GitHub Actions deployment workflow"
git push origin main
```

**Why:** Pushing to main automatically triggers the GitHub Actions workflow,
which builds and deploys the app to Azure App Service.

**Verify the deployment succeeded:**
```powershell
gh run list --repo Azuul345/BlogAPI_Azure
```

**What to look for:** `completed` + `success` ✅

---

### Step 4.8: Run Database Migrations

This creates all the database tables in Azure SQL so the API can store and
retrieve data.

**First, allow your local IP to connect to the SQL Server:**
```powershell
az sql server firewall-rule create --server "blogapi-sql-server-diego" --name "AllowMyIP" --start-ip-address "213.64.158.99" --end-ip-address "213.64.158.99"
```

**Then run the migrations** (from the `BlogAPI` folder):
```powershell
cd BlogAPI
```
```powershell
dotnet ef database update --connection "Server=tcp:blogapi-sql-server-diego.database.windows.net,1433;Initial Catalog=blogapi-db;Persist Security Info=False;User ID=sqladmin;Password=BlogAPI_SQL_P@ssw0rd2026;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
```

**What to look for:** `Done.` at the end of the output confirms all tables were
created successfully. ✅

### Step 4.9: Allow Azure Services to Access SQL Server

By default Azure SQL blocks all incoming connections — including from other
Azure services like App Service. This special firewall rule allows all Azure
services to connect.

**Run this command:**
```powershell
az sql server firewall-rule create --server "blogapi-sql-server-diego" --name "AllowAzureServices" --start-ip-address "0.0.0.0" --end-ip-address "0.0.0.0"
```

**What to look for:** A JSON response with `"name": "AllowAzureServices"` confirms the rule was created. ✅
***

## Phase 5: Verify & Test

In this phase we verify that everything is working end-to-end on Azure —
the API, the database, and Application Insights monitoring.

---

### Step 5.1: Enable Verbose Application Logging

This turns on detailed logging so we can see exactly what happens inside
the app when requests come in.

**Run this command:**
```powershell
az webapp log config --name "blogapi-webapp-diego" --application-logging filesystem --level verbose
```

**What to look for:** No error = success ✅

---

### Step 5.2: Test the API is Responding

This verifies the API is running and connected to the database.

**Run this command:**
```powershell
Invoke-RestMethod -Uri "https://blogapi-webapp-diego.azurewebsites.net/api/posts" -Method GET
```

**What to look for:**
- An empty array `[]` or blank response = ✅ API is running, database is connected
- A JSON object with post data = ✅ API is working perfectly

---

### Step 5.3: Create a Test User

Before creating a post, a user must exist in the database.

**Run this command:**
```powershell
Invoke-RestMethod -Uri "https://blogapi-webapp-diego.azurewebsites.net/api/Users/register" -Method POST -ContentType "application/json" -Body '{"userName":"testuser","email":"test@test.com","password":"Test123!"}'
```

**What to look for:** A JSON response with an `id` field confirms the user was created. ✅

📝 **Note the `id` value** — you need it in Step 5.5.

---

### Step 5.4: Create a Test Category

Posts require a category. Create one before creating a post.

**Run this command:**
```powershell
Invoke-RestMethod -Uri "https://blogapi-webapp-diego.azurewebsites.net/api/Categories" -Method POST -ContentType "application/json" -Body '{"name":"General"}'
```

**What to look for:** A JSON response with an `id` field confirms the category was created. ✅

📝 **Note the `id` value** — you need it in Step 5.5.

---

### Step 5.5: Create a Test Post

This verifies the full write cycle — API receives the request, writes to
Azure SQL, and returns the saved data.

**Run this command** (replace `1` with the actual user and category IDs from the previous steps if different):
```powershell
Invoke-RestMethod -Uri "https://blogapi-webapp-diego.azurewebsites.net/api/Posts" -Method POST -ContentType "application/json" -Body '{"title":"Test Post","text":"This is a test post from Azure!","userId":1,"categoryId":1}'
```

**What to look for:** A JSON response like this confirms everything works: ✅
```json
{
  "id": 1,
  "title": "Test Post",
  "text": "This is a test post from Azure!",
  "userName": "testuser",
  "categoryName": "General"
}
```

---

### Step 5.6: Verify the Post is Retrievable

This confirms the full read cycle — the data we just wrote can be read back.

**Run this command:**
```powershell
Invoke-RestMethod -Uri "https://blogapi-webapp-diego.azurewebsites.net/api/posts" -Method GET
```

**What to look for:** The test post we created in Step 5.5 appears in the response. ✅

---

### Step 5.7: Verify Application Insights in Azure Portal

1. Go to [https://portal.azure.com](https://portal.azure.com)
2. Search for **Application Insights** in the top search bar
3. Click on **`blogapi-insights-diego`**
4. Click **"Search"**
5. Click **"Refresh"**

**What to look for:**
- Traces showing `Executed DbCommand` with SELECT queries ✅
- A spike in the chart matching the time of your test requests ✅
- 0 failed requests on the overview dashboard ✅

> ⚠️ Application Insights has a 2–3 minute delay before data appears.
> If you see nothing, wait a few minutes and click Refresh again.

***

### Final Cleanup Before Demonstration (Delete Existing Resources)

Before demonstrating the full recreation of the solution, all Azure resources and the existing GitHub Actions workflow were cleaned up so that the environment is in a fresh state.

#### Step X.1 Delete BlogAPI Azure Resources (Safe Resources Only)

First, list all resources in the assignment resource group to identify what belongs to BlogAPI and what must be kept:

```powershell
az resource list --resource-group RG-Diego-Oviedo-245b81-DotNetCloudDeveloper-VT-Mars-Goteborg -o table
```

From this list, only the following types of resources were deleted:

- All resources with names starting with `blogapi-` (BlogAPI assignment)
- All resources with names starting with `diegoblog` or `sql-diegoblog` (old test resources no longer used)

The following resources were **not** deleted because they belong to other projects:

- `ITHS-RG-Guard-DontTouch`
- All resources with names starting with `azultracker-` (AzulTracker application)

Delete the BlogAPI web app and App Service plan:

```powershell
az webapp delete --name blogapi-webapp-diego
az appservice plan delete --name blogapi-plan-diego --yes
```

Delete the Application Insights instance for BlogAPI:

```powershell
az resource delete --name blogapi-insights-diego --resource-group RG-Diego-Oviedo-245b81-DotNetCloudDeveloper-VT-Mars-Goteborg --resource-type "Microsoft.Insights/components"
```

Delete Storage Accounts used for BlogAPI and older test resources:

```powershell
az storage account delete --name blogapistoratediego --yes
az storage account delete --name diegoblogstorage245b --yes
```

Delete older Key Vaults and SQL Servers that were only used for BlogAPI tests:

```powershell
az keyvault delete --name diegoblogkv245b
az sql server delete --name sql-diegoblogapi245b --yes
```

Delete the final Key Vault and SQL Server used in the documented BlogAPI solution:

```powershell
az keyvault delete --name blogapi-keyvault-diego
az sql server delete --name blogapi-sql-server-diego --yes
```

After deleting these resources, verify that only the guard and AzulTracker resources remain:

```powershell
az resource list --resource-group RG-Diego-Oviedo-245b81-DotNetCloudDeveloper-VT-Mars-Goteborg -o table
```



#### Step X.2 Notes About Key Vault Soft-Delete

When deleting Key Vaults, Azure may show a warning about soft-delete. This means the Key Vault is deleted from the resource group but the name may be reserved for some time. Because of this, when recreating the solution, the Key Vault name may need to be adjusted.

> Note: If the Key Vault name `blogapi-keyvault-diego` is already taken or still in soft-delete, you may need to adjust the name, for example to `blogapi-keyvault-diego2`. Make sure to use the same name in all later commands (for example when storing secrets and when setting `KeyVaultUri` on the App Service).

#### Step X.3 Remove Existing GitHub Actions Workflow Files

To fully match the documentation and recreate the CI/CD pipeline from scratch during the demonstration, the existing GitHub Actions workflow file was removed from the repository.

From the repository root:

```powershell
cd C:\DM1\Azure    # adjust to actual repo root

# Remove the workflow file
Remove-Item .github\workflows\deploy.yml

# Optionally remove the workflows folder if it is now empty
Remove-Item .github\workflows -Force

# Optionally remove the .github folder if it is only used for this workflow
Remove-Item .github -Force
```

These changes can be committed so that the GitHub repository no longer contains the workflow until it is recreated in Phase 4:

```powershell
git add .
git commit -m "Cleanup: remove BlogAPI Azure resources and GitHub Actions workflow before full recreation"
git push origin main
```

During the presentation, all resources and the GitHub Actions workflow will then be recreated step by step by following this documentation from Phase 1 to Phase 5.