# Secrets Management Guide

This guide explains how to securely manage API keys and sensitive configuration in the AI Agent Suite.

## ?? Security Architecture

The application uses a **layered configuration approach** that prioritizes security:

1. **Local Development**: User Secrets (encrypted, per-user)
2. **Production**: Azure Key Vault (centralized, audit-logged)
3. **Configuration Files**: `appsettings.json` (NO secrets here!)

---

## ?? Quick Start: Local Development

### Step 1: Initialize User Secrets

Run this command in the `src/Agent.Presentation.Cli` directory:

```bash
dotnet user-secrets init
```

This creates a `.gitignore`-protected secrets storage on your machine.

### Step 2: Store Your API Keys

```bash
# SerpApi key
dotnet user-secrets set "SerpApi:ApiKey" "your_actual_api_key_here"

# LLM API key (if needed)
dotnet user-secrets set "LLM:ApiKey" "your_llm_key"

# Embeddings API key (if needed)
dotnet user-secrets set "Embeddings:ApiKey" "your_embeddings_key"
```

### Step 3: Verify Secrets Are Set

```bash
dotnet user-secrets list
```

### Step 4: Run the Application

```bash
dotnet run --project src/Agent.Presentation.Cli
```

? Your secrets are now securely loaded without being in code!

---

## ?? User Secrets Storage Location

User secrets are stored **locally and encrypted** at:

- **Windows**: `%APPDATA%\Microsoft\UserSecrets\agensuit-cli-secrets\secrets.json`
- **macOS/Linux**: `~/.microsoft/usersecrets/agensuit-cli-secrets/secrets.json`

?? **Never commit secrets to git** - User Secrets are automatically excluded.

---

## ?? Production: Azure Key Vault Setup

### Prerequisites

- Azure subscription
- Azure CLI (`az login`)
- Key Vault created in Azure

### Step 1: Create Azure Key Vault

```bash
az keyvault create \
  --resource-group MyResourceGroup \
  --name agent-suite-kv \
  --location eastus
```

### Step 2: Add Secrets to Key Vault

```bash
az keyvault secret set \
  --vault-name agent-suite-kv \
  --name "SerpApi--ApiKey" \
  --value "your_actual_api_key"

az keyvault secret set \
  --vault-name agent-suite-kv \
  --name "LLM--ApiKey" \
  --value "your_llm_key"
```

### Step 3: Enable Key Vault in Configuration

Update `appsettings.Production.json`:

```json
{
  "KeyVault": {
    "Enabled": true,
    "VaultUrl": "https://agent-suite-kv.vault.azure.net/"
  }
}
```

### Step 4: Deploy with Managed Identity

When deploying to Azure App Service:

1. Enable Managed Identity for your app
2. Grant Key Vault access:

```bash
az keyvault set-policy \
  --name agent-suite-kv \
  --object-id <your-managed-identity-id> \
  --secret-permissions get list
```

---

## ?? Configuration Precedence (Highest to Lowest)

1. **Azure Key Vault** (if enabled in production)
2. **Environment Variables**
3. **User Secrets** (local dev)
4. **appsettings.{Environment}.json**
5. **appsettings.json** (defaults only)

---

## ? Best Practices

### DO ?

- Store ALL API keys in User Secrets (dev) or Key Vault (prod)
- Rotate API keys regularly
- Use strong, unique keys for each service
- Enable Key Vault audit logging
- Restrict Key Vault access to specific managed identities
- Use environment-specific appsettings files

### DON'T ?

- ? Hardcode API keys in source code
- ? Commit secrets to git (even in a "private" branch)
- ? Share API keys via chat, email, or issue trackers
- ? Use the same key across multiple environments
- ? Log sensitive values in application logs
- ? Store secrets in comments

---

## ?? Common Tasks

### List All Secrets (Local Dev)

```bash
dotnet user-secrets list
```

### Clear All Secrets (Local Dev)

```bash
dotnet user-secrets clear
```

### Update a Secret

```bash
dotnet user-secrets set "SerpApi:ApiKey" "new_key_value"
```

### View Raw Secrets File

```bash
# Windows
notepad "%APPDATA%\Microsoft\UserSecrets\agensuit-cli-secrets\secrets.json"

# macOS/Linux
nano ~/.microsoft/usersecrets/agensuit-cli-secrets/secrets.json
```

---

## ?? Incident Response: Leaked API Key

If an API key is accidentally committed:

1. **Immediately rotate** the API key in the service dashboard
2. **Remove** the key from git history:
   ```bash
   git filter-branch --tree-filter 'find . -name "*.cs" -o -name "*.json" | xargs sed -i "s/leaked_key//g"'
   ```
3. **Force push** (?? use with caution):
   ```bash
   git push --force-with-lease origin main
   ```
4. **Notify** your team
5. **Audit logs** to check if the key was misused

---

## ?? References

- [Microsoft: User Secrets](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets)
- [Microsoft: Azure Key Vault](https://learn.microsoft.com/en-us/azure/key-vault/)
- [Azure Identity Documentation](https://learn.microsoft.com/en-us/dotnet/api/overview/azure/identity-readme)

---

## ?? Questions?

If you have questions about secrets management, consult this guide or reach out to your team lead.
