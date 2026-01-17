# API Key Configuration Guide

## Overview

This project requires API keys to access LLM providers (Anthropic, OpenRouter, etc.). To prevent accidentally committing sensitive API keys to version control, we use multiple secure configuration strategies.

## ✅ Recommended Approach: appsettings.Development.json

The **best and easiest** way to configure your API keys locally is using `appsettings.Development.json`:

### Setup Steps:

1. **Create the file** (if it doesn't already exist):
   ```bash
   # Navigate to the CLI project directory
   cd samples/DetectiveAgent.Cli
   
   # Create appsettings.Development.json
   # (You can copy from appsettings.json as a template)
   ```

2. **Add your API key** to `appsettings.Development.json`:
   ```json
   {
     "Providers": {
       "Anthropic": {
         "ApiKey": "sk-ant-api03-your-actual-key-here"
       }
     }
   }
   ```

3. **How it works:**
   - In Development environment, .NET automatically merges `appsettings.Development.json` with `appsettings.json`
   - Values in `appsettings.Development.json` override those in `appsettings.json`
   - The `.gitignore` file ensures `appsettings.Development.json` is **never committed**
   - The base `appsettings.json` stays in source control with placeholder values

4. **Run the application:**
   ```bash
   dotnet run --project samples/DetectiveAgent.Cli
   ```

### ✅ Why this approach is best:

- ✅ **No environment variables to manage** - just edit a file
- ✅ **Automatically ignored by git** - can't accidentally commit
- ✅ **Standard .NET pattern** - works with all .NET configuration systems
- ✅ **IDE-friendly** - Visual Studio and VS Code understand this pattern
- ✅ **Easy to maintain** - one file with all your local secrets

## Alternative Approach: Environment Variables

If you prefer environment variables (e.g., for CI/CD or production deployments):

### Windows (PowerShell):
```powershell
$env:ANTHROPIC_API_KEY = "sk-ant-api03-your-actual-key-here"
dotnet run --project samples/DetectiveAgent.Cli
```

### Windows (Command Prompt):
```cmd
set ANTHROPIC_API_KEY=sk-ant-api03-your-actual-key-here
dotnet run --project samples/DetectiveAgent.Cli
```

### Linux/macOS:
```bash
export ANTHROPIC_API_KEY="sk-ant-api03-your-actual-key-here"
dotnet run --project samples/DetectiveAgent.Cli
```

### Using .env file (optional):
Create a `.env` file in the project root:
```env
ANTHROPIC_API_KEY=sk-ant-api03-your-actual-key-here
```

**Note:** The `.env` file is already in `.gitignore`, so it won't be committed.

## Alternative Approach: User Secrets (Development only)

For sensitive data during development, you can use .NET User Secrets:

```bash
cd samples/DetectiveAgent.Cli
dotnet user-secrets init
dotnet user-secrets set "Providers:Anthropic:ApiKey" "sk-ant-api03-your-actual-key-here"
```

User secrets are stored outside the project directory and never committed to source control.

## Security Checklist

Before committing code, verify:

- [ ] `appsettings.json` contains **only placeholder values** (e.g., "your-api-key-here")
- [ ] Your actual API key is in `appsettings.Development.json` (which is gitignored)
- [ ] Run `git status` and confirm `appsettings.Development.json` is **not** listed
- [ ] Check `.gitignore` includes `appsettings.Development.json`

## What's Protected by .gitignore

The following files are automatically ignored and safe for storing secrets:

```
appsettings.Development.json    ✅ Ignored (recommended)
appsettings.*.json              ✅ Ignored (except appsettings.json)
.env                            ✅ Ignored
.env.local                      ✅ Ignored
```

## Testing Your Configuration

To verify your API key is loaded correctly:

```bash
cd samples/DetectiveAgent.Cli
dotnet run
```

If configured correctly, the agent should start and connect to the LLM provider without errors.

## Production Deployment

For production deployments, **never use appsettings files for secrets**. Instead use:

- **Azure Key Vault** (for Azure deployments)
- **AWS Secrets Manager** (for AWS deployments)
- **Environment variables** (set by your hosting platform)
- **Kubernetes Secrets** (for K8s deployments)

## Getting API Keys

### Anthropic Claude:
1. Visit https://console.anthropic.com/
2. Sign up or log in
3. Navigate to API Keys section
4. Create a new API key
5. Copy the key (starts with `sk-ant-api03-`)

### OpenRouter:
1. Visit https://openrouter.ai/
2. Sign up or log in
3. Go to Keys section
4. Create a new API key
5. Copy the key

## Troubleshooting

### "API key not found" error:
- Verify `appsettings.Development.json` exists in `samples/DetectiveAgent.Cli/`
- Confirm the JSON is valid (use a JSON validator)
- Check that the key path matches: `Providers:Anthropic:ApiKey`

### Changes not taking effect:
- Restart the application after modifying configuration files
- Verify you're running from the correct directory
- Check that `DOTNET_ENVIRONMENT` is not set to something other than `Development`

### Key still being committed:
- Run `git status` to check
- Verify `.gitignore` includes the pattern
- If already committed, remove from history:
  ```bash
  git rm --cached samples/DetectiveAgent.Cli/appsettings.Development.json
  git commit -m "Remove accidentally committed secrets"
  ```

## Support

For questions or issues with API key configuration, please open an issue in the repository.
