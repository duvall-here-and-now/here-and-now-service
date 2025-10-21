# Swagger/OpenAPI Documentation Setup

## Overview
This service now includes Swagger/OpenAPI documentation for easy API testing and exploration. The Swagger UI is accessible locally and when deployed to Azure App Service, with IP-based access restrictions managed at the Azure level.

## Local Development Access

### Accessing Swagger UI Locally
1. Start the service locally (default port: 3001)
2. Open your browser and navigate to: `http://localhost:3001/swagger`
3. You'll see the Swagger UI with all available endpoints

### Testing Authenticated Endpoints
1. In Swagger UI, click the **"Authorize"** button (lock icon)
2. Enter your Auth0 JWT token (just the token value, without the `Bearer ` prefix)
3. Click **"Authorize"** and then **"Close"**
4. You can now test protected endpoints like `/api/messages/protected` and `/api/messages/admin`

### Getting an Auth0 JWT Token
You can obtain a JWT token by:
- Authenticating through your frontend SPA
- Using Auth0's test token endpoint
- Using a tool like Postman to authenticate with Auth0

## Azure App Service Deployment

### IP Restriction Configuration (Azure Portal)

To secure Swagger UI access when deployed to Azure App Service, configure IP restrictions:

#### Step 1: Find Your Public IP Address
1. Visit https://whatismyipaddress.com/ or run `curl ifconfig.me` in your terminal
2. Note your public IPv4 address (e.g., `203.0.113.42`)

#### Step 2: Configure IP Restrictions in Azure Portal
1. Navigate to your App Service in the Azure Portal
2. In the left menu, select **"Networking"**
3. Under **"Inbound Traffic"**, click **"Access restriction"**
4. Click **"+ Add rule"**

#### Step 3: Add IP Restriction Rule for Swagger
Create a rule with the following settings:

**Rule 1: Allow Your Local Machine**
- **Name**: `Allow-MyLocalMachine-Swagger`
- **Action**: Allow
- **Priority**: 100
- **Type**: IPv4
- **IP Address Block**: `YOUR_PUBLIC_IP/32` (e.g., `203.0.113.42/32`)
- **Description**: Allow Swagger access from my local machine

**Important**: Click **"Add path"** or **"Restrict access to specific paths"**
- **Path**: `/swagger*`
- This ensures the rule ONLY applies to Swagger endpoints

#### Step 4: Add IP Restriction Rule for OpenAPI JSON
Create another rule for the OpenAPI specification endpoint:

**Rule 2: Allow OpenAPI Spec Access**
- **Name**: `Allow-MyLocalMachine-OpenAPI`
- **Action**: Allow
- **Priority**: 101
- **Type**: IPv4
- **IP Address Block**: `YOUR_PUBLIC_IP/32` (same as above)
- **Path**: `/swagger.json`
- **Description**: Allow OpenAPI spec access from my local machine

#### Step 5: Verify Default Rule
- Ensure there's a default **"Deny all"** rule with the lowest priority (e.g., 2147483647)
- This blocks all other IPs from accessing Swagger
- Your API endpoints at `/api/*` should NOT have IP restrictions (they're protected by Auth0)

### Alternative: Using Azure CLI

You can also configure IP restrictions using Azure CLI:

```bash
# Replace these variables with your values
RESOURCE_GROUP="your-resource-group"
APP_NAME="your-app-service-name"
YOUR_IP="203.0.113.42"

# Add IP restriction for Swagger UI
az webapp config access-restriction add \
  --resource-group $RESOURCE_GROUP \
  --name $APP_NAME \
  --rule-name "Allow-MyLocalMachine-Swagger" \
  --action Allow \
  --ip-address "${YOUR_IP}/32" \
  --priority 100 \
  --scm-site false

# Add path-specific restriction (requires Azure CLI 2.40+)
az webapp config access-restriction add \
  --resource-group $RESOURCE_GROUP \
  --name $APP_NAME \
  --rule-name "Allow-MyLocalMachine-Swagger" \
  --action Allow \
  --ip-address "${YOUR_IP}/32" \
  --priority 100 \
  --scm-site false \
  --http-headers x-azure-fdid= \
  --service-tag ""
```

**Note**: Path-based restrictions through Azure CLI can be complex. Using the Azure Portal is recommended for path-specific rules.

## Accessing Swagger in Azure

### Once Deployed
1. Navigate to your Azure App Service URL: `https://your-app-service.azurewebsites.net/swagger`
2. If you're on your allowed IP, you'll see the Swagger UI
3. If you're on a different IP, you'll receive a `403 Forbidden` error

### Testing from Your Local Machine
```bash
# Verify you can access Swagger (should return HTML)
curl -I https://your-app-service.azurewebsites.net/swagger

# Verify your API endpoints still work (should return JSON)
curl https://your-app-service.azurewebsites.net/api/messages/public
```

## Important Notes

### API Endpoints Remain Accessible
- The IP restrictions **ONLY** apply to `/swagger*` paths
- Your REST API endpoints at `/api/*` are NOT affected by IP restrictions
- Your SPA can continue to call the API normally
- Auth0 JWT authentication is still required for protected endpoints

### Dynamic IP Addresses
- Most residential internet connections have **dynamic IPs** that can change
- If your IP changes, you'll need to update the Azure IP restriction rules
- Consider these alternatives:
  - Use a VPN with a static IP
  - Set up a bastion/jump host in Azure
  - Use Azure App Service Authentication/AAD for additional protection

### Multiple Developer Access
To allow multiple developers to access Swagger:
1. Add multiple IP restriction rules (one per developer)
2. Use different priorities (100, 101, 102, etc.)
3. Each rule should allow access to `/swagger*` paths

### IPv6 Considerations
If you have an IPv6 address:
1. Get your IPv6 address: `curl -6 ifconfig.me`
2. Add a separate IPv6 rule in Azure
3. Use `/128` suffix for single IPv6 addresses

## Security Best Practices

1. **Never commit IP addresses to source control** - Configure them in Azure Portal
2. **Regularly review IP restriction rules** - Remove old/unused rules
3. **Use Azure App Service Authentication** for additional protection in production
4. **Monitor access logs** for unauthorized access attempts
5. **Consider disabling Swagger in production** using environment variables:

```csharp
// In Program.cs, wrap Swagger configuration:
if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("ENABLE_SWAGGER"))
{
    app.UseSwagger();
    app.UseSwaggerUI(options => { /* ... */ });
}
```

## Troubleshooting

### Can't Access Swagger in Azure
1. Verify your current public IP: `curl ifconfig.me`
2. Check Azure IP restriction rules match your current IP
3. Ensure rules are applied to the main site (not SCM site)
4. Check path restrictions include `/swagger*`

### API Not Working After IP Restrictions
1. Verify IP restrictions are ONLY applied to `/swagger*` paths
2. Ensure `/api/*` paths are NOT restricted
3. Check that Auth0 authentication is still working
4. Review Azure App Service logs for errors

### 403 Forbidden on Swagger
- Your IP has likely changed
- Update the Azure IP restriction rules with your current IP
- Verify you're not behind a VPN/proxy that masks your IP

## Support
For issues related to:
- **Swagger UI**: Check Swashbuckle.AspNetCore documentation
- **Azure IP Restrictions**: Review Azure App Service networking documentation
- **Auth0**: Consult Auth0 documentation for JWT token issues
