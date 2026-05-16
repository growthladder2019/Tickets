# Ticket Management System CI/CD Guide (Azure + GitHub)

## 1) Overview

This repository uses a split deployment model:

- Frontend deploys to Azure Static Web Apps.
- API deploys to Azure App Service (Linux Web App).
- Both pipelines run from GitHub Actions.

Current workflow files:

- .github/workflows/frontend-static-web-app.yml
- .github/workflows/api-webapp-ticket.yml

## 2) Deployment Architecture

Frontend

- Source path: Tickets
- Build output: dist
- Target: Azure Static Web Apps

Backend API

- Project path: TicketAPI/TicketAPI/TicketAPI.csproj
- Publish target: linux-x64, framework-dependent
- Target: Azure App Service

## 3) Trigger Rules

Frontend workflow (Frontend Static Web App)

- Runs on push to main when:
  - Tickets/** changes, or
  - .github/workflows/frontend-static-web-app.yml changes
- Runs on pull requests to main for preview environments.

API workflow (API Web App Ticket)

- Runs on push to main when:
  - TicketAPI/** changes, or
  - SQL/** changes, or
  - .github/workflows/api-webapp-ticket.yml changes
- Also supports manual run by workflow_dispatch.

## 4) Required Azure Resources

Create or verify these resources:

- Azure App Service for API (Linux)
- Azure Static Web App for frontend
- Azure SQL Database
- Azure Storage Account + Blob container

## 5) Required GitHub Secrets

Add these in repository settings:

Settings -> Secrets and variables -> Actions

API pipeline secrets:

- AZURE_CREDENTIALS
- AZURE_WEBAPP_NAME
- API_APPSETTINGS_PRODUCTION_JSON

Frontend pipeline secrets:

- AZURE_STATIC_WEB_APPS_API_TOKEN
- Optional fallback token:
  - AZURE_STATIC_WEB_APPS_API_TOKEN_VICTORIOUS_SEA_0B0A48A00

## 6) How to Get Secret Values

### 6.1 AZURE_WEBAPP_NAME

From Azure Portal:

- Open App Services
- Open your API app
- Copy Overview -> Name

Example:

- mindticketmanagement-fna0gsfmfjecfrbe

### 6.2 AZURE_CREDENTIALS

Use Azure Cloud Shell or local Azure CLI.

1. Collect values from your App Service Overview:

- subscription id
- resource group
- app service name

2. Run:

    az ad sp create-for-rbac \
      --name tickets-gha-deploy \
      --role contributor \
      --scopes /subscriptions/<SUBSCRIPTION_ID>/resourceGroups/<RESOURCE_GROUP>/providers/Microsoft.Web/sites/<APP_SERVICE_NAME> \
      --sdk-auth

3. Copy the full JSON output and save it as AZURE_CREDENTIALS.

### 6.3 API_APPSETTINGS_PRODUCTION_JSON

Use this template and replace placeholders:

{
  "ConnectionStrings": {
    "TicketSystem": "Server=tcp:<SQL_SERVER>.database.windows.net,1433;Initial Catalog=<SQL_DATABASE>;Persist Security Info=False;User ID=<SQL_USER>;Password=<SQL_PASSWORD>;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
  },
  "Jwt": {
    "Issuer": "TicketSystem",
    "Audience": "TicketSystem.Client",
    "SigningKey": "<JWT_SIGNING_KEY_64_PLUS_CHARS>",
    "AccessTokenMinutes": 30,
    "RefreshTokenDays": 7
  },
  "BlobStorage": {
    "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=<STORAGE_ACCOUNT_NAME>;AccountKey=<STORAGE_ACCOUNT_KEY>;EndpointSuffix=core.windows.net",
    "ContainerName": "ticket-system"
  },
  "SuperAdmin": {
    "AppCode": "SUPERADMIN"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}

Generate a strong JWT signing key:

    openssl rand -base64 64

### 6.4 AZURE_STATIC_WEB_APPS_API_TOKEN

From Azure Portal:

- Open Static Web App
- Open Deployment token (Manage deployment token)
- Copy token to secret

## 7) API Workflow Execution Details

Workflow file: .github/workflows/api-webapp-ticket.yml

Main steps:

1. Restore .NET dependencies
2. Publish API for Linux
3. Validate required secrets
4. Write appsettings.Production.json from API_APPSETTINGS_PRODUCTION_JSON
5. Login to Azure using AZURE_CREDENTIALS
6. Deploy package to App Service using AZURE_WEBAPP_NAME

## 8) Frontend Workflow Execution Details

Workflow file: .github/workflows/frontend-static-web-app.yml

Main steps:

1. Validate Static Web App token exists
2. Build and deploy app from Tickets to dist
3. On PR close, close preview environment

## 9) First-Time Setup Checklist

- Push repository to GitHub
- Add all required secrets
- Verify Azure resources exist and are reachable
- Run API workflow manually once
- Run frontend workflow manually once
- Validate production URLs

## 10) Post-Deployment Validation

API checks:

- Swagger endpoint opens
- Login endpoint returns token
- Authenticated ticket endpoints respond

Frontend checks:

- Login works for user and super-admin
- Ticket list loads
- Status badges and summary band render correctly
- Ticket create/update/assign flow works

## 11) Troubleshooting Guide

### Error: Missing required GitHub secret

Cause:

- One or more required secrets are not set.

Fix:

- Add the missing secret in GitHub repository settings.

### Error: Deployment failed in webapps deploy

Cause:

- Invalid AZURE_CREDENTIALS, wrong app name, or insufficient role assignment.

Fix:

- Regenerate AZURE_CREDENTIALS using correct scope.
- Confirm AZURE_WEBAPP_NAME matches App Service Name exactly.
- Ensure service principal role has at least contributor on target App Service scope.

### Error: Frontend workflow runs but deploy does not update site

Cause:

- Wrong app_location or output_location.

Fix:

- Ensure app_location is Tickets and output_location is dist.

### Error: Duplicate or strange workflow names in Actions

Cause:

- Hidden AppleDouble files committed, such as files starting with ._.

Fix:

- Remove ._ files from repository and push cleanup commit.

## 12) Security Best Practices

- Never commit production credentials to repository files.
- Keep secrets only in GitHub Actions secrets.
- Rotate these regularly:
  - SQL user password
  - Storage account key
  - Service principal secret
- Keep publish output folders untracked.
- Use least privilege scopes for service principal.

## 13) Recommended Operational Process

For each release:

1. Create feature branch
2. Open pull request
3. Merge to main after checks pass
4. Monitor workflow logs
5. Run smoke tests

For hotfix:

1. Patch branch from main
2. Merge quickly
3. Manually trigger API workflow if needed
4. Verify production immediately

## 14) Current Workflow File References

- .github/workflows/api-webapp-ticket.yml
- .github/workflows/frontend-static-web-app.yml

This document reflects the current repository setup and can be updated as pipeline strategy evolves.
