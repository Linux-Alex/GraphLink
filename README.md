# 📬 GraphLink

**GraphLink** is a lightweight .NET Core Web API that connects to Microsoft 365 email accounts using the Microsoft Graph API.

> 🔐 Authenticated via API key  
> 💌 Supports sending & reading emails  
> 📎 Attachments supported  
> 🛡️ Recipient filtering included  
> 🚀 Easily configurable via `appsettings.json`

---

## 🌐 Endpoints

### ✅ `GET /api/emails/{userEmail}`  
Read incoming emails from the user's inbox (with optional filters).

| Query Param | Type     | Description |
|-------------|----------|-------------|
| `top`       | `int`    | Number of messages (default: 10) |
| `folder`    | `string` | Folder name (default: "Inbox") |
| `fromDate`  | `string` | ISO 8601 date to filter from |

### ✅ `POST /api/emails/{userEmail}`  
Send an email from a configured M365 account.

**Body:**
```json
{
  "To": "someone@example.com",
  "Cc": "cc@example.com",
  "Bcc": "bcc@example.com",
  "Subject": "Hello World!",
  "Body": "This is the email content.",
  "Attachments": [
    {
      "Name": "report.pdf",
      "ContentType": "application/pdf",
      "Base64Content": "JVBERi0xLjcKJc..."
    }
  ]
}
```

## 🔑 API Key
All endpoints require an API key in the header:

```makefile
X-API-KEY: supersecureapikey123!
```

> 🔐 The key is configured in `appsettings.json` (change it in production).


## ⚙️ Configuration

You need to register a Microsoft 365 app and the GraphLink with it:

### 🛠️ Microsoft 365 App Registration Guide

1. Create App Registration in Azure AD
    1. Go to [Azure Portal](https://portal.azure.com/) and login
    2. Search for **App registrations** and click on **+ New registration**
    3. Enter details:
        - Name: `GraphLink Service`
        - Supported account types: **Accounts in this organizational directory only (#### only - Single Tenant)**
        - Redirect URI: `http://localhost` (Web platform)
    4. Submit with a click on the button **Register**
2. Configure API Permissions
    1. In your new app registration, go to **Manage** > **API permissions**
    2. Click on **Add a permission**, choose **Microsoft Graph** and **Application permissions**
    3. Search and add these permissions:
        - `Mail.Read`
        - `Mail.ReadWrite`
        - `Mail.Send`
    4. Submit with a click on the button **Add permissions**
    5. In the **Configure permissions** region, click on the **✓ Grand admin consent for ####** and confirm to add the admin permissions for the task
3. Create Client Secret
    1. In the registrated app, go to **Manage** > **Certificates & secrets**
    2. Click on **New client secret**
    3. Add a description (like "GraphLink Secret"), expiration and click on **Add**
    4. **Copy the secret value**, cause you won't see it again
4. Restrict Access to Specific Users (using Powershell):
    1. Install Microsoft Graph PowerShell SDK:
        ```ps
        Install-Module Microsoft.Graph -Scope CurrentUser
        ```
        If it prompts about an untrusted repository, choose **A** (Yes to All).
    2. Connect to Microsoft Graph:
        ```ps
        Connect-MgGraph -Scopes "User.Read.All", "Application.Read.All", "AppRoleAssignment.ReadWrite.All", "Directory.ReadWrite.All"
        ```
    3. Get your app's service principal:
        ```ps
        $appName = "GraphLink Service"
        $sp = Get-MgServicePrincipal -Filter "displayName eq '$appName'"
        ```

        If your not sure about the correct name, you can check the found app using:
        ```ps
        echo $sp
        ```
        and you should see something like this, if there is an app found (or nothing if not):
        ```
        DisplayName       Id                                   AppId                                SignInAudience ServicePrincipalType
        -----------       --                                   -----                                -------------- --------------------
        GraphLink Service b9eaaa12-####-####-####-############ 9dfa2cc9-####-####-####-############ AzureADMyOrg   Application
        ```
    4. Get user to restrict access to:
        ```ps
        $user = Get-MgUser -UserId "allowed.user@yourdomain.com"
        ```
    5. Assign app to specific user only:
        ```ps
        New-MgServicePrincipalAppRoleAssignment `
            -ServicePrincipalId $sp.Id `
            -PrincipalId $user.Id `
            -ResourceId $sp.Id `
            -AppRoleId "00000000-0000-0000-0000-000000000000" |
            Format-List
        ```
    6. Verify the assignment:
        ```ps
        Get-MgServicePrincipalAppRoleAssignedTo -ServicePrincipalId $sp.Id |
            Where-Object { $_.PrincipalId -eq $user.Id } |
            Select-Object Id, AppRoleId, PrincipalDisplayName, ResourceDisplayName
        ```
        You should see something like this:
        ```
        Id                                          AppRoleId                            PrincipalDisplayName ResourceDisplayName
        --                                          ---------                            -------------------- -------------------
        BZyVP_2wPUy#############-################## 00000000-0000-0000-0000-000000000000 Allowed User         GraphLink Service
        ```

### 🛠 Complete Configuration Guide

1. Create your own appsettings.json using the provided template:

    Copy the template (Linux/macOS):
    ```bash
    cp appsettings.template.json appsettings.json
    ```

    Windows alternative:
    ```cmd
    copy appsettings.template.json appsettings.json
    ```

2. Edit the file with these exact values from you Azure AD app registration:
    ```json
    {
        "ApiKey": "supersecureapikey123!", // Change the key
        "AzureAD": {
            "ClientId": "########-####-####-####-############", // From Azure AD > App Registration > Overview
            "TenantId": "########-####-####-####-############", // From Azure AD > Overview
            "ClientSecret": "########################################", // From Azure AD > Certificates & Secrets > Secret value
            "RedirectUri": "http://localhost",
            "AllowedAccounts": [
                {
                    "Email": "allowed.user@yourdomain.com", // Exact user principal name
                    "Password": "allowed_user_password", 
                    "DisplayName": "Allowe User Service Account",
                    "AllowedRecivers": [ 
                        "*@yourdomain.com", // Allow sending to entire domain
                        "specific.partner@external.com" // Specific allowed addresses
                    ]
                },
                // Add more allowed accounts if needed
            ]
        }
    }
    ```
    
## 🧪 Development
### Run Locally

```bash
dotnet build
dotnet run
```

### Swagger UI
Open: [https://localhost:5001/swagger](https://localhost:5001/swagger)
Use your API key to test endpoints.

## 🤝 Contributing
PRs and suggestions are welcome! Just fork, code, and submit a pull request 🚀

## 🛡 License
Licensed under [MIT](./LICENSE).