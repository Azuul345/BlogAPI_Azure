
Before deploying to Azure, the project needs three additions:
- A package for reading secrets from **Azure Key Vault**
- A package for sending telemetry to **Application Insights**
- A package for uploading files to **Azure Blob Storage**

All three are only active when running on Azure. When running locally, they are automatically skipped because their configuration values don't exist until the CLI script (Phase 2) creates them.

---

## Step 1 — Install NuGet Packages

Open a terminal inside Visual Studio by going to **View → Terminal**. Make sure the terminal is inside the `BlogAPI` project folder (where the `.csproj` file is). Run the following commands one at a time:

```bash
# For reading secrets from Azure Key Vault
dotnet add package Azure.Identity
dotnet add package Azure.Extensions.AspNetCore.Configuration.Secrets

# For sending logs and metrics to Application Insights
dotnet add package Microsoft.ApplicationInsights.AspNetCore

# For uploading and managing files in Azure Blob Storage
dotnet add package Azure.Storage.Blobs
```

**How to verify:** Each command should end with `Successfully installed ...`. Open the `.csproj` file — you should see four new `<PackageReference>` entries. Build the project (`Ctrl+Shift+B`) — it should build with no errors.

---

## Step 2 — Create BlobStorageService.cs

Create a new file in the `BlogAPI` project folder called `BlobStorageService.cs`. This class handles all communication with Azure Blob Storage.

> **Important:** The `_container` field is marked with `?` (nullable) because locally there is no storage connection string. The null checks in each method ensure the app does not crash when running locally — the storage features simply do nothing until deployed to Azure.

```csharp
using Azure.Storage.Blobs;

namespace BlogAPI
{
    public class BlobStorageService
    {
        private readonly BlobContainerClient? _container;

        public BlobStorageService(IConfiguration configuration)
        {
            // StorageConnectionString only exists on Azure (stored in Key Vault via CLI script).
            // Locally this value is null, so the container is not initialized.
            var connectionString = configuration["StorageConnectionString"];
            if (!string.IsNullOrEmpty(connectionString))
            {
                var serviceClient = new BlobServiceClient(connectionString);
                _container = serviceClient.GetBlobContainerClient("uploads");
            }
        }

        /// Upload a file to the "uploads" container and return its public URL
        public async Task<string> UploadFileAsync(Stream fileStream, string fileName)
        {
            if (_container == null)
                throw new InvalidOperationException("Storage is not configured. Deploy to Azure first.");
            var blobClient = _container.GetBlobClient(fileName);
            await blobClient.UploadAsync(fileStream, overwrite: true);
            return blobClient.Uri.ToString();
        }

        /// List all file names in the "uploads" container
        public async Task<IEnumerable<string>> ListFilesAsync()
        {
            if (_container == null) return Enumerable.Empty<string>();
            var files = new List<string>();
            await foreach (var blob in _container.GetBlobsAsync())
                files.Add(blob.Name);
            return files;
        }

        /// Delete a file by name
        public async Task DeleteFileAsync(string fileName)
        {
            if (_container == null) return;
            var blobClient = _container.GetBlobClient(fileName);
            await blobClient.DeleteIfExistsAsync();
        }
    }
}
```

---

## Step 3 — Update Program.cs

Open `Program.cs` and update it to include Key Vault, Application Insights, and BlobStorageService. Each Azure service is wrapped in a null check so it is only activated when running on Azure — locally these blocks are skipped automatically.

> **Important:** Application Insights must also be wrapped in a null check. Without it, the app crashes locally because the `APPLICATIONINSIGHTS_CONNECTION_STRING` value does not exist until it is set by the CLI script on Azure.

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
        // Program.cs is the entry point of the application.
        // Here it configures dependency injection, EF Core, AutoMapper, CORS, Swagger
        // and sets up the HTTP request pipeline.

        public static void Main(string[] args)
        {
            // Create the builder which holds configuration, logging and DI container.
            var builder = WebApplication.CreateBuilder(args);

            // ── Key Vault ────────────────────────────────────────────────────────────
            // Only runs on Azure. The CLI script sets KeyVaultUri as an app setting.
            // Locally, KeyVaultUri is not set, so this block is skipped entirely.
            // Once loaded, all Key Vault secrets are available via builder.Configuration.
            var keyVaultUri = builder.Configuration["KeyVaultUri"];
            if (!string.IsNullOrEmpty(keyVaultUri))
            {
                builder.Configuration.AddAzureKeyVault(
                    new Uri(keyVaultUri),
                    new DefaultAzureCredential()
                );
            }

            // ── Database ─────────────────────────────────────────────────────────────
            // Locally: reads DefaultConnection from appsettings.Development.json.
            // On Azure: reads DefaultConnection from Key Vault (stored there by the CLI script).
            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
            builder.Services.AddDbContext<AppDbContext>(options =>
                options.UseSqlServer(connectionString));

            // ── Application Insights ─────────────────────────────────────────────────
            // Only activates on Azure. The CLI script sets APPLICATIONINSIGHTS_CONNECTION_STRING.
            // Without this null check, the app crashes locally with a missing connection string error.
            var appInsightsConnection = builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
            if (!string.IsNullOrEmpty(appInsightsConnection))
            {
                builder.Services.AddApplicationInsightsTelemetry();
            }

            // ── Blob Storage ──────────────────────────────────────────────────────────
            // BlobStorageService handles the null check internally (see BlobStorageService.cs).
            // Locally it does nothing. On Azure it reads StorageConnectionString from Key Vault.
            builder.Services.AddSingleton<BlobStorageService>();

            // ── AutoMapper ────────────────────────────────────────────────────────────
            // Scans the assembly for Profile classes (e.g. BlogProfile).
            builder.Services.AddAutoMapper(typeof(Program));

            // ── Repositories (data access layer) ──────────────────────────────────────
            // Controllers and services depend only on interfaces, not concrete classes.
            builder.Services.AddScoped<IPostRepository, PostRepository>();
            builder.Services.AddScoped<ICommentRepository, CommentRepository>();
            builder.Services.AddScoped<IUserRepository, UserRepository>();

            // ── Services (business logic layer) ────────────────────────────────────────
            builder.Services.AddScoped<IPostService, PostService>();
            builder.Services.AddScoped<ICommentService, CommentService>();
            builder.Services.AddScoped<IUserService, UserService>();

            // ── API & Swagger ──────────────────────────────────────────────────────────
            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            // ── CORS ───────────────────────────────────────────────────────────────────
            builder.Services.AddCors();

            // Build the WebApplication (creates the DI container and middleware pipeline).
            var app = builder.Build();

            // In development, expose Swagger UI to document and test the API.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            // Redirect HTTP requests to HTTPS.
            app.UseHttpsRedirection();

            // Enable CORS so any frontend can call this API during development.
            // In production this should be restricted to known origins.
            app.UseCors(policy => policy
                .AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader());

            // Add authorization middleware (required for [Authorize] attributes).
            app.UseAuthorization();

            // Map attribute-routed controllers (e.g. [Route("api/[controller]")]).
            app.MapControllers();

            // Start the application and begin listening for HTTP requests.
            app.Run();
        }
    }
}
```

---

## Step 4 — Update appsettings Files

The connection string must **only** live in `appsettings.Development.json`. The main `appsettings.json` must have no connection string — otherwise the app will try to use the local SQL Server when deployed to Azure.

### `appsettings.json` — no connection string:

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

### `appsettings.Development.json` — local connection string only:

> **Important:** Use exactly two backslashes (`\\`) for the SQL Server instance name. Four backslashes (`\\\\`) is incorrect and will cause the connection to fail.

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=JEGO\\SQLEXPRESS;Initial Catalog=BlogAPI;Integrated Security=True;Connect Timeout=30;Encrypt=True;Trust Server Certificate=True;Application Intent=ReadWrite"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

---

## Step 5 — Verify the Project Runs Locally

Before pushing to GitHub, confirm the project works locally:

1. Press `Ctrl+Shift+B` to build — should show **No issues found**
2. Press `F5` to run — the browser should open Swagger UI at `https://localhost:7162/swagger`
3. Test one of the existing API endpoints (e.g. `GET /api/posts`) — it should return data from the local SQL database

**Expected behaviour locally:**
- Key Vault block → skipped (no `KeyVaultUri` set)
- Application Insights → skipped (no connection string set)
- BlobStorageService → starts but does nothing (no storage connection string)
- Database → connects to local SQL Express ✅

---

## Step 6 — Push the Project to GitHub

The automated deployment in Phase 6 requires the code to be in a GitHub repository.

Go to [github.com](https://github.com) and create a new **private** repository. Do **not** initialize it with a README. Then open a terminal in Visual Studio and run:

```bash
git add .
git commit -m "Phase 1 complete - added Key Vault, App Insights, Blob Storage"
git push
```

**How to verify:** Go to your GitHub repository in the browser — you should see all project files including the new `BlobStorageService.cs`.

---

## Summary of What Was Changed in Phase 1

| File | What was changed |
|---|---|
| `BlogAPI.csproj` | Added 4 NuGet packages: Azure.Identity, Azure.Extensions.AspNetCore.Configuration.Secrets, Microsoft.ApplicationInsights.AspNetCore, Azure.Storage.Blobs |
| `BlobStorageService.cs` | New file — handles file uploads, listing, and deletion. Null-safe for local development |
| `Program.cs` | Added Key Vault loader, Application Insights registration, BlobStorageService registration — all wrapped in null checks so they only activate on Azure |
| `appsettings.json` | Removed connection string (Azure will use Key Vault instead) |
| `appsettings.Development.json` | Local SQL Server connection string stored here only |
'''


