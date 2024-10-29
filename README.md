# Project

## How to deploy

### Register front-end and back-end applications
For API permissions, the solution requires you to register the front-end and back-end applications in Entra ID.

#### Back-end
The back-end app registration should expose an API with custom permissions, to do this, go to the app registration -> expose an API. Here you can configure a custom scope for the back-end API (e.g. "chat").
Save the value of the scope for later use.

#### Front-end
The front-end app registration should have permissions to consume the above API. In API permissions, add the following:
- offline_access
- openid
- User.Read
- The custom API you exposed above.
The first three are for logging in, the last one is for consuming the backend api.

### Deploy infra
To deploy the infra, create a resource group in Azure. In this repository, open the infra folder and run the below command:

```
az deployment group create --template-file main.bicep -g "YOUR-RESOURCE-GROUP-NAME"
```

This will ask you to provide three values, which are all used by the back-end app service to secure the exposed APIs:

|Input| Description |
|---|---|
|azureAdInstance| https://login.microsoftonline.com/ |
|azureAdClientId| Client id of the back-end app registration. |
|azureAdTenantId|  Tenant id of the Entra ID tenant. |

### Setting up end-user auth
The next step is to allow users to sign in to the front end app. For that, the redirect URI needs to be known, so this can only be done after deploying the infra. To do that, add the following in the front end app registration:
- Add a redirect URI for a single page application.
- As URI, put the base URL of the front-end app (e.g. https://frontend-{someid}.azurewebsites.net/)
- Enable access token and ID token flows.

### Deploy the apps
After deploying the infra, you can deploy the backend and frontend by going to the deploy directory and running the below command.

```
.\deploy.ps1
```

This will ask for the following variables:

|Input| Description |
|---|---|
|rgName| Name of the resource group |
|frontEndAppServiceName| Name of the front end app, this will be outputted by the bicep. |
|backEndAppServiceName|  Name of the back end app, this will be outputted by the bicep. |
|backendUrl|  Url of the back end app, this will be outputted by the bicep. |
|backendApiScope|  Scope for the back end API that you created in step 1. |
|publicAppId|  Id of the front end app registration. |
|publicAuthorityUrl|  Authority URL of the front end app registration (e.g. https://login.microsoftonline.com/{tenant-id}) |

After deploying the apps, you can access the front-end app to start chatting on your documents.


## Contributing

This project welcomes contributions and suggestions.  Most contributions require you to agree to a
Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us
the rights to use your contribution. For details, visit https://cla.opensource.microsoft.com.

When you submit a pull request, a CLA bot will automatically determine whether you need to provide
a CLA and decorate the PR appropriately (e.g., status check, comment). Simply follow the instructions
provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.

## Trademarks

This project may contain trademarks or logos for projects, products, or services. Authorized use of Microsoft 
trademarks or logos is subject to and must follow 
[Microsoft's Trademark & Brand Guidelines](https://www.microsoft.com/en-us/legal/intellectualproperty/trademarks/usage/general).
Use of Microsoft trademarks or logos in modified versions of this project must not cause confusion or imply Microsoft sponsorship.
Any use of third-party trademarks or logos are subject to those third-party's policies.
