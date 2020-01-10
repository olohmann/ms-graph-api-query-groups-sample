# Microsoft Graph API Example for Querying Group Information

This brief example demonstrates in a very simple and straight forward fashion how to query the Microsoft Graph API for information about a user's group membership.

In order for this example to work you need to create an Azure AD Application with the following **application permissions** (i.e. not *delegated* permissions) in place:

* Group.ReadAll
* GroupMember.ReadAll
* User.ReadAll

Make sure that you have the administrator consent granted.

Once the Azure AD Application is configured, you need to update the sources with the data from the AAD application registration.

In `Controllers/GroupMembershipController.cs`:

```cs
    // TODO - set a user ID that you want to query for
    var userId = "john@contoso.com";

    // TODO - add the app registration details
    var authProvider = new AuthenticationProvider(
            "<Application ID>", 
            "<Application Secret>", 
            new []{"https://graph.microsoft.com/.default"}, 
            "<Tenant ID>");
```

**This example is simple on purpose! Do never ever put hard-coded credentials in your source code. Use technologies like KeyVault or even better, Managed Identities to avoid hard-coded secrets in your code.**
