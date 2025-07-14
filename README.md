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

> 🔐 The key is configured in Program.cs (change it in production).

## ⚙️ Configuration
1. Create your own appsettings.json using the provided template:

```bash
cp appsettings.template.json appsettings.json
```

2. Replace placeholder values:

    - ClientId, TenantId, ClientSecret

    - Add your own allowed accounts & filters

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
Licensed under MIT.