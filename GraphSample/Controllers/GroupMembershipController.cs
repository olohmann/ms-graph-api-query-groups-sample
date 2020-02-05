using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Mime;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Graph;
using Microsoft.Identity.Client;

namespace GraphSample.Controllers
{
    public class Group
    {
        public string GroupId { get; set; }
        public string DisplayName { get; set; }
    }

    public class UserInformation
    {
        public string Id { get; set; }
        public string GivenName { get; set; }
        public string Surname { get; set; }
        public string[] BusinessPhones { get; set; }
        public string MobilePhone { get; set; }
        public string Mail { get; set; }
        
        public Group[] GroupMemberships { get; set; }
    }

    /// <summary>
    /// A helper component that provides the GraphClient with a proper Bearer token for
    /// sending out requests to the Graph API.
    /// </summary>
    public class AuthenticationProvider : IAuthenticationProvider
    {
        private readonly string _clientId;
        private readonly string _clientSecret;
        private readonly string[] _appScopes;
        private readonly string _tenantId;

        public AuthenticationProvider(string clientId, string clientSecret, string[] appScopes, string tenantId)
        {
            _clientId = clientId;
            _clientSecret = clientSecret;
            _appScopes = appScopes;
            _tenantId = tenantId;
        }

        public async Task AuthenticateRequestAsync(HttpRequestMessage request)
        {
            var clientApplication = ConfidentialClientApplicationBuilder.Create(_clientId)
                .WithClientSecret(_clientSecret)
                .WithClientId(_clientId)
                .WithTenantId(_tenantId)
                .Build();
    
            var result = await clientApplication.AcquireTokenForClient(_appScopes).ExecuteAsync();

            request.Headers.Add("Authorization", result.CreateAuthorizationHeader());
        }
    }

    [ApiController]
    [Route("[controller]")]
    public class GroupMembershipController : ControllerBase
    {
        [HttpGet]
        [Consumes(MediaTypeNames.Application.Json)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<UserInformation>>> Get([FromQuery] string userPrincipalName,
            [FromQuery] string givenName, [FromQuery] string surname)
        {
            // Instead of hard-coding the values here, get them from the ASP.NET (Core) configuration
            // provider system. Avoid any means of hard-coded secrets. Best approach is injection via
            // the configuration system. This also supports getting the secret from a KeyVault.

            // TODO
            var authProvider = new AuthenticationProvider(
                "<Application ID>", 
                "<Application Secret>", 
                new []{"https://graph.microsoft.com/.default"}, 
                "<Tenant ID>");

            // Create a new instance of the GraphServiceClient. It needs a properly configured
            // authProvider instance in order to issue the requests to the Graph API without
            // any user dependencies (-> NO DELEGATION).
            var graphClient = new GraphServiceClient(authProvider);

            // Add filters based on existence of query string information.
            var filterExpressions = new List<string>();
            if (!string.IsNullOrWhiteSpace(userPrincipalName))
            {
                filterExpressions.Add($"startsWith({nameof(userPrincipalName)}, '{userPrincipalName}')");
            }

            if (!string.IsNullOrWhiteSpace(givenName))
            {
                filterExpressions.Add($"startsWith({nameof(givenName)}, '{givenName}')");
            }

            if (!string.IsNullOrWhiteSpace(surname))
            {
                filterExpressions.Add($"startsWith({nameof(surname)}, '{surname}')");
            }

            var filterExpression = string.Join(" and ", filterExpressions);

            var userQuery = graphClient.Users.Request().Filter(filterExpression);
            var result = new List<UserInformation>();
            
            // TODO: Handle paging properly!
            //       That is, you should not fetch all user results, but page them and
            //       let the user trigger the next page fetch! You can treat the nextPageRequest
            //       as an anchor. Without paging, you would potentially load the whole directory
            //       into memory. Not good!
            var getUsersPagedResults = await userQuery.GetAsync();
            foreach (var user in getUsersPagedResults)
            {
                // Issue a query for the current user to identify the group memberships. The result is a list of
                // group IDs.
                var getMemberGroupsPagedResults =
                    await graphClient.Users[user.Id].GetMemberGroups(false).Request().PostAsync();
                
                 // Also group REQ results are always paged. If we want to collect all results, all pages must be retrieved.
                 // If you look for a specific membership, it might make sense, to use it as a filter instead
                 // of loading potentially 100s of membership entries.
                 // Potentially, you should cap at a certain group count size.
                 var userMemberGroupIDs = getMemberGroupsPagedResults.ToList();
                 PageIterator<string>.CreatePageIterator(graphClient, getMemberGroupsPagedResults, groupId =>
                 {
                     userMemberGroupIDs.Add(groupId);
     
                     // Take all results.
                     return true;
                 });
     
                 // Optional: Get DisplayNames and additional information about the groups where
                 //           the user is a member of. Caching the results for a SHORT period
                 //           (minutes, not days or months) would make sense.
                 var groupMemberships = new List<Group>();
                 foreach (var userMemberGroupGuid in userMemberGroupIDs)
                 {
                     var groupReqResp = await graphClient.Groups[userMemberGroupGuid].Request().GetAsync();
                     groupMemberships.Add(
                         new Group
                         {
                             GroupId = groupReqResp.Id,
                             DisplayName = groupReqResp.DisplayName
                         });
                 }

                 var userInformation = new UserInformation()
                 {
                    Id = user.Id,
                    GivenName = user.GivenName,
                    Surname = user.Surname,
                    Mail = user.Mail,
                    BusinessPhones = user.BusinessPhones.ToArray(),
                    MobilePhone = user.MobilePhone,
                    GroupMemberships = groupMemberships.ToArray()
                 };
                 
                 result.Add(userInformation);
            }

            return Ok(result);
        }
    }
}
